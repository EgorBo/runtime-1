// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//==============================================================================
//
// fgPromoteFrozenAllocations
//
// In a static constructor, promote allocator helper calls whose result is
// uniquely published into a `static readonly` field so they call the
// `*_MAYBEFROZEN` allocator helpers (CORINFO_HELP_NEWFAST_MAYBEFROZEN /
// CORINFO_HELP_NEWARR_1_MAYBEFROZEN). The VM may then place the resulting
// object on the Frozen Object Heap (FOH); subsequent JITed methods can then
// VN-fold loads of those fields to a constant frozen-object handle (see
// `valuenum.cpp::fgValueNumberConstLoad` /
// `Compiler::GetImmutableDataFromAddress`).
//
// Objects on the FOH never get collected, so promoting an allocation that is
// not guaranteed to be reachable through a stable root would be a permanent
// leak. The phase is conservative on purpose; failing to promote a candidate
// is harmless, but a wrong promotion is not.
//
// Phase placement
//   This phase runs in the SSA/VN-valid window — right after
//   `PHASE_OPTIMIZE_INDEX_CHECKS` (the rangeCheckPhase) and before
//   `PHASE_VN_BASED_DEAD_STORE_REMOVAL` (which invalidates SSA). At this
//   point GT_ALLOCOBJ has already been morphed by `PHASE_ALLOCATE_OBJECTS`
//   into a GT_CALL to one of the allocator helpers, so we recognize and
//   rewrite helper calls only.
//
// Pattern recognized
//   In a leaf basic block of the cctor we expect the importer-emitted
//   pattern:
//
//       STORE_LCL_VAR(temp, <alloc-helper-call>)         // SSA def
//       [ STOREIND(temp + cns, value)            ]*      // box payload
//                                                          or array element
//                                                          initializer (any
//                                                          number, allowed)
//       STOREIND<TYP_REF>(<static-field-addr>, temp)     // publication
//
//   The publication's address must recover to a static field handle
//   (`fldSeq->GetFieldHandle()` on a `GTF_ICON_STATIC_HDL`/`GTF_ICON_CONST_PTR`
//   icon, optionally under an `ADD(icon, cns)`).
//
// Safety constraints (any failure leaves the cctor untouched)
//   1. `cctor` only, with `JIT_FLAG_FROZEN_ALLOC_ALLOWED` granted by the VM.
//   2. No EH and no backward jumps in the cctor (loops would re-execute the
//      alloc and leak the older copies).
//   3. Field is `static readonly` AND is written exactly once in the cctor.
//   4. Publication value is `LCL_VAR(temp)` (or `BOX(LCL_VAR(temp))`) where
//      `temp` is single-defined via SSA, the def block equals the publishing
//      block, and SSA reports no global / phi uses (so the temp can't escape
//      across blocks or get mixed via merges).
//   5. Every other use of the temp inside the def block must be an
//      "address-only" use — the `LCL_VAR` is the address operand of an
//      `IND/STOREIND/BLK/STOREBLK` (directly, or under `ADD(t, cns)`). This
//      rules out passing the temp as a call argument, returning it,
//      comparing it, etc.
//   6. No statement strictly between the SSA def and the publication is
//      potentially throwing (`GTF_EXCEPT`) — a throw would leave the
//      already-allocated frozen object unrooted.
//
// Helpers we rewrite
//   * Object allocators: `CORINFO_HELP_NEWSFAST` / `CORINFO_HELP_NEWFAST`
//     and `CORINFO_HELP_READYTORUN_NEW`. The R2R variant has no
//     class-handle arg (it's encoded in the entry point); we reconstruct it
//     from `compileTimeHelperArgumentHandle`, which
//     `MorphAllocObjNodeIntoHelperCall` stashes for us, and clear the entry
//     point.
//   * Array allocators: `CORINFO_HELP_NEWARR_1_{DIRECT,VC,PTR,ALIGN8}` and
//     `CORINFO_HELP_READYTORUN_NEWARR_1`. Same fixup story.
//   * Finalize / align8 specializations are intentionally not promoted —
//     `RhpGcAllocMaybeFrozen` does not implement those.
//
//==============================================================================

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

namespace
{

struct StsfldStore
{
    BasicBlock*          block;
    Statement*           stmt;
    GenTreeStoreInd*     storeInd;
    CORINFO_FIELD_HANDLE field;
    GenTree*             value;
};

typedef JitHashTable<CORINFO_FIELD_HANDLE, JitPtrKeyFuncs<struct CORINFO_FIELD_STRUCT_>, int> FieldStoreCountMap;

//------------------------------------------------------------------------
// TryGetStaticFieldHandle: recover a static field handle from the address
// operand of a STSFLD-shaped indirection. We only recognize the canonical
// patterns the importer emits for plain class statics; anything else (TLS,
// shared-helper-based statics, etc.) silently returns NO_FIELD_HANDLE so we
// just skip those candidates.
//
static CORINFO_FIELD_HANDLE TryGetStaticFieldHandle(GenTree* addr)
{
    if (addr == nullptr)
    {
        return NO_FIELD_HANDLE;
    }

    // Strip a single ADD(icon, cns) used for sub-field offsets.
    GenTree* iconNode = addr;
    if (addr->OperIs(GT_ADD))
    {
        GenTree* op1 = addr->gtGetOp1();
        GenTree* op2 = addr->gtGetOp2();
        if (op1->IsCnsIntOrI() && op2->IsCnsIntOrI())
        {
            // Pick the one carrying the field sequence.
            iconNode = (op1->AsIntCon()->gtFieldSeq != nullptr) ? op1 : op2;
        }
        else if (op1->IsCnsIntOrI())
        {
            iconNode = op1;
        }
        else if (op2->IsCnsIntOrI())
        {
            iconNode = op2;
        }
        else
        {
            return NO_FIELD_HANDLE;
        }
    }

    if (!iconNode->IsCnsIntOrI())
    {
        return NO_FIELD_HANDLE;
    }
    if (!iconNode->IsIconHandle(GTF_ICON_STATIC_HDL) && !iconNode->IsIconHandle(GTF_ICON_CONST_PTR))
    {
        return NO_FIELD_HANDLE;
    }

    FieldSeq* fldSeq = iconNode->AsIntCon()->gtFieldSeq;
    return (fldSeq != nullptr) ? fldSeq->GetFieldHandle() : NO_FIELD_HANDLE;
}

//------------------------------------------------------------------------
// IsAllocCall: returns true if `tree` is a helper call that we know how to
// swap with a `*_MAYBEFROZEN` variant.
//
static bool IsAllocCall(GenTree* tree, bool* isObj, bool* isArr)
{
    *isObj = false;
    *isArr = false;
    if ((tree == nullptr) || !tree->IsCall())
    {
        return false;
    }
    switch (tree->AsCall()->GetHelperNum())
    {
        case CORINFO_HELP_NEWSFAST:
        case CORINFO_HELP_NEWFAST:
#ifdef FEATURE_READYTORUN
        case CORINFO_HELP_READYTORUN_NEW:
#endif
            *isObj = true;
            return true;

        case CORINFO_HELP_NEWARR_1_DIRECT:
        case CORINFO_HELP_NEWARR_1_PTR:
        case CORINFO_HELP_NEWARR_1_VC:
        case CORINFO_HELP_NEWARR_1_ALIGN8:
#ifdef FEATURE_READYTORUN
        case CORINFO_HELP_READYTORUN_NEWARR_1:
#endif
            *isArr = true;
            return true;

        default:
            return false;
    }
}

//------------------------------------------------------------------------
// IsAddressOnlyUseOfLcl: returns true when the GT_LCL_VAR `lclNode` is being
// consumed only as a base address for an indirection (directly, or under
// `ADD(t, cns)`). This is the only "safe" non-publication use we accept for
// an alloc temp — payload writes via the allocated pointer can't leak the
// reference.
//
static bool IsAddressOnlyUseOfLcl(GenTree* lclNode, GenTree* parent, GenTree* grandparent)
{
    if (parent == nullptr)
    {
        return false;
    }

    if (parent->OperIs(GT_STOREIND, GT_IND, GT_BLK) || parent->OperIsStoreBlk())
    {
        return parent->AsIndir()->Addr() == lclNode;
    }

    if (parent->OperIs(GT_ADD) && (grandparent != nullptr))
    {
        GenTree* sibling = (parent->gtGetOp1() == lclNode) ? parent->gtGetOp2() : parent->gtGetOp1();
        if (!sibling->IsCnsIntOrI())
        {
            return false;
        }
        if ((grandparent->OperIs(GT_STOREIND, GT_IND, GT_BLK) || grandparent->OperIsStoreBlk()) &&
            (grandparent->AsIndir()->Addr() == parent))
        {
            return true;
        }
    }

    return false;
}

//------------------------------------------------------------------------
// PromoteAllocToFrozen: rewrite an allocator helper call to its
// `*_MAYBEFROZEN` variant. Returns true on success.
//
static bool PromoteAllocToFrozen(Compiler* comp, GenTreeCall* call)
{
    bool isObj;
    bool isArr;
    if (!IsAllocCall(call, &isObj, &isArr))
    {
        return false;
    }

#ifdef FEATURE_READYTORUN
    // R2R helpers carry the class handle on the entry point rather than as
    // an arg. The MAYBEFROZEN variants are non-R2R and take an explicit
    // class arg, so reconstruct that arg from
    // `compileTimeHelperArgumentHandle` (stashed by the importer for
    // newarr, by `MorphAllocObjNodeIntoHelperCall` for newobj/box) and
    // clear the entry point.
    const bool isR2R = (call->GetHelperNum() == CORINFO_HELP_READYTORUN_NEW) ||
                       (call->GetHelperNum() == CORINFO_HELP_READYTORUN_NEWARR_1);
    if (isR2R)
    {
        CORINFO_CLASS_HANDLE cls = (CORINFO_CLASS_HANDLE)call->compileTimeHelperArgumentHandle;
        if (cls == NO_CLASS_HANDLE)
        {
            return false;
        }
        GenTree* clsHandleNode = comp->gtNewIconEmbClsHndNode(cls);
        call->gtArgs.PushFront(comp, NewCallArg::Primitive(clsHandleNode));
        call->gtFlags |= clsHandleNode->gtFlags & GTF_ALL_EFFECT;
        call->gtEntryPoint.addr       = nullptr;
        call->gtEntryPoint.accessType = IAT_VALUE;
    }
#endif

    call->gtCallMethHnd =
        comp->eeFindHelper(isObj ? CORINFO_HELP_NEWFAST_MAYBEFROZEN : CORINFO_HELP_NEWARR_1_MAYBEFROZEN);
    return true;
}

//------------------------------------------------------------------------
// IsStaticFinalRefField: returns true when the given field is a `static`
// `final` (a.k.a. `static readonly`) field.
//
static bool IsStaticFinalRefField(Compiler* comp, CORINFO_FIELD_HANDLE field)
{
    CORINFO_CLASS_HANDLE fldClass = comp->info.compCompHnd->getFieldClass(field);
    if (fldClass == NO_CLASS_HANDLE)
    {
        return false;
    }
    CORINFO_RESOLVED_TOKEN tok = {};
    tok.tokenContext           = MAKE_METHODCONTEXT(comp->info.compMethodHnd);
    tok.tokenScope             = comp->info.compScopeHnd;
    tok.tokenType              = CORINFO_TOKENKIND_Field;
    tok.hField                 = field;
    tok.hClass                 = fldClass;
    CORINFO_FIELD_INFO fi;
    comp->eeGetFieldInfo(&tok, CORINFO_ACCESS_SET, &fi);
    const unsigned required = CORINFO_FLG_FIELD_STATIC | CORINFO_FLG_FIELD_FINAL;
    return (fi.fieldFlags & required) == required;
}

//------------------------------------------------------------------------
// StsfldCollector: pre-order walker that records candidate STSFLDs and
// per-field store counts for a single block.
//
class StsfldCollector final : public GenTreeVisitor<StsfldCollector>
{
public:
    enum
    {
        DoPreOrder = true,
    };

    StsfldCollector(Compiler* compiler, FieldStoreCountMap* fieldMap, ArrayStack<StsfldStore>* candidates)
        : GenTreeVisitor<StsfldCollector>(compiler)
        , m_fieldMap(fieldMap)
        , m_candidates(candidates)
    {
    }

    void SetContext(BasicBlock* block, Statement* stmt)
    {
        m_block = block;
        m_stmt  = stmt;
    }

    Compiler::fgWalkResult PreOrderVisit(GenTree** use, GenTree* user)
    {
        GenTree* node = *use;
        if (!node->OperIs(GT_STOREIND) || !node->TypeIs(TYP_REF))
        {
            return Compiler::fgWalkResult::WALK_CONTINUE;
        }
        GenTreeStoreInd*     storeInd = node->AsStoreInd();
        CORINFO_FIELD_HANDLE field    = TryGetStaticFieldHandle(storeInd->Addr());
        if (field == NO_FIELD_HANDLE)
        {
            return Compiler::fgWalkResult::WALK_CONTINUE;
        }

        int count = 0;
        m_fieldMap->Lookup(field, &count);
        m_fieldMap->Set(field, count + 1, FieldStoreCountMap::Overwrite);

        StsfldStore c = {m_block, m_stmt, storeInd, field, storeInd->Data()};
        m_candidates->Push(c);
        return Compiler::fgWalkResult::WALK_CONTINUE;
    }

private:
    FieldStoreCountMap*      m_fieldMap;
    ArrayStack<StsfldStore>* m_candidates;
    BasicBlock*              m_block = nullptr;
    Statement*               m_stmt  = nullptr;
};

//------------------------------------------------------------------------
// TempUseScanner: for a single statement, verify that every use of
// `(lclNum, ssaNum)` is either the publishing use (`pubValue` itself, or a
// `BOX` wrapping it) or an address-only use. Sets `result.unsafeUse` if any
// non-conforming use is found, and counts the publishing use in
// `result.publishingUses`.
//
struct UseScanResult
{
    bool     unsafeUse       = false;
    unsigned publishingUses  = 0;
};

class TempUseScanner final : public GenTreeVisitor<TempUseScanner>
{
public:
    enum
    {
        DoPreOrder   = true,
        ComputeStack = true,
    };

    TempUseScanner(
        Compiler* compiler, unsigned lclNum, unsigned ssaNum, GenTree* pubValue, UseScanResult* result)
        : GenTreeVisitor<TempUseScanner>(compiler)
        , m_lclNum(lclNum)
        , m_ssaNum(ssaNum)
        , m_pubValue(pubValue)
        , m_result(result)
    {
    }

    Compiler::fgWalkResult PreOrderVisit(GenTree** use, GenTree* user)
    {
        GenTree* node = *use;

        // Address-taken / field-style access on the temp is an immediate escape.
        if (node->OperIs(GT_LCL_ADDR, GT_LCL_FLD, GT_STORE_LCL_FLD) &&
            (node->AsLclVarCommon()->GetLclNum() == m_lclNum))
        {
            m_result->unsafeUse = true;
            return Compiler::fgWalkResult::WALK_ABORT;
        }
        if (!node->OperIs(GT_LCL_VAR))
        {
            return Compiler::fgWalkResult::WALK_CONTINUE;
        }
        if ((node->AsLclVarCommon()->GetLclNum() != m_lclNum) ||
            (node->AsLclVarCommon()->GetSsaNum() != m_ssaNum))
        {
            return Compiler::fgWalkResult::WALK_CONTINUE;
        }

        GenTree* parent      = m_ancestors.Height() >= 2 ? m_ancestors.Top(1) : nullptr;
        GenTree* grandparent = m_ancestors.Height() >= 3 ? m_ancestors.Top(2) : nullptr;

        // Publishing use: this LCL_VAR is the published value, or is wrapped
        // by a GT_BOX that is the published value.
        const bool isPublishingUse =
            (node == m_pubValue) || ((m_pubValue != nullptr) && m_pubValue->OperIs(GT_BOX) &&
                                     (m_pubValue->AsBox()->BoxOp() == node));
        if (isPublishingUse)
        {
            m_result->publishingUses++;
            return Compiler::fgWalkResult::WALK_CONTINUE;
        }

        if (!IsAddressOnlyUseOfLcl(node, parent, grandparent))
        {
            m_result->unsafeUse = true;
            return Compiler::fgWalkResult::WALK_ABORT;
        }
        return Compiler::fgWalkResult::WALK_CONTINUE;
    }

private:
    unsigned       m_lclNum;
    unsigned       m_ssaNum;
    GenTree*       m_pubValue;
    UseScanResult* m_result;
};

//------------------------------------------------------------------------
// TryFindAllocForCandidate: validate the SSA-driven path from the candidate
// STSFLD's `LCL_VAR(temp)` value back to its single defining alloc helper
// call. Performs all the safety checks in (4)-(6). Returns the alloc call
// on success, or nullptr on any rejection.
//
static GenTreeCall* TryFindAllocForCandidate(Compiler* comp, const StsfldStore& cand, GenTree* publishedValue)
{
    if (!publishedValue->OperIs(GT_LCL_VAR))
    {
        return nullptr;
    }

    unsigned lclNum = publishedValue->AsLclVarCommon()->GetLclNum();
    unsigned ssaNum = publishedValue->AsLclVarCommon()->GetSsaNum();
    if (ssaNum == SsaConfig::RESERVED_SSA_NUM)
    {
        return nullptr;
    }

    LclVarDsc*    dsc    = comp->lvaGetDesc(lclNum);
    LclSsaVarDsc* ssaDsc = dsc->GetPerSsaData(ssaNum);
    if (ssaDsc == nullptr)
    {
        return nullptr;
    }

    // The temp must be confined to a single block (no phi merges, no
    // out-of-block uses). Combined with the no-loop / no-EH gates this means
    // the alloc and the publication are reached by exactly one straight-line
    // path.
    if (ssaDsc->HasPhiUse() || ssaDsc->HasGlobalUse())
    {
        return nullptr;
    }

    GenTreeLclVarCommon* defNode = ssaDsc->GetDefNode();
    if ((defNode == nullptr) || !defNode->OperIs(GT_STORE_LCL_VAR))
    {
        return nullptr;
    }

    BasicBlock* defBlock = ssaDsc->GetBlock();
    if ((defBlock == nullptr) || (defBlock != cand.block))
    {
        return nullptr;
    }

    // The store's RHS must be a recognized allocator helper call.
    GenTree* defRhs = defNode->AsLclVar()->Data();
    bool     isObj;
    bool     isArr;
    if (!IsAllocCall(defRhs, &isObj, &isArr))
    {
        return nullptr;
    }

    // Locate the def statement and validate every intervening statement.
    Statement* defStmt = nullptr;
    for (Statement* const s : cand.block->Statements())
    {
        if (s == cand.stmt)
        {
            break;
        }
        if (s->GetRootNode() == defNode)
        {
            defStmt = s;
            break;
        }
    }
    if (defStmt == nullptr)
    {
        return nullptr;
    }

    // Walk statements from defStmt to cand.stmt (inclusive). Reject if any
    // intervening statement may throw, and verify the temp's only uses are
    // the publication plus optional address-only writes through `temp + cns`.
    UseScanResult scan;
    bool          sawDef = false;
    for (Statement* const s : cand.block->Statements())
    {
        if (s == defStmt)
        {
            sawDef = true;
            // Don't scan inside the def itself: its RHS is the alloc.
            continue;
        }

        if (sawDef && (s != cand.stmt))
        {
            // A throw between the alloc and the publication would leak the
            // freshly-allocated frozen object.
            GenTree* root = s->GetRootNode();
            if ((root != nullptr) && ((root->gtFlags & GTF_EXCEPT) != 0))
            {
                return nullptr;
            }
        }

        TempUseScanner scanner(comp, lclNum, ssaNum, cand.value, &scan);
        scanner.WalkTree(s->GetRootNodePointer(), nullptr);
        if (scan.unsafeUse)
        {
            return nullptr;
        }

        if (s == cand.stmt)
        {
            break;
        }
    }

    if (scan.publishingUses != 1)
    {
        // The publication must consume the temp exactly once.
        return nullptr;
    }

    return defRhs->AsCall();
}

} // anonymous namespace

//------------------------------------------------------------------------
// Compiler::fgPromoteFrozenAllocations: see file-level comment.
//
PhaseStatus Compiler::fgPromoteFrozenAllocations()
{
    if (JitConfig.JitOptimizeStaticConstructors() == 0)
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }
    if ((info.compFlags & FLG_CCTOR) != FLG_CCTOR)
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }
    if (!opts.jitFlags->IsSet(JitFlags::JIT_FLAG_FROZEN_ALLOC_ALLOWED))
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }
    if (opts.OptimizationDisabled())
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }
    if ((optMethodFlags & (OMF_HAS_NEWOBJ | OMF_HAS_NEWARRAY)) == 0)
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }
    if (compHndBBtabCount > 0)
    {
        JITDUMP("fgPromoteFrozenAllocations: method has EH; punting\n");
        return PhaseStatus::MODIFIED_NOTHING;
    }
    for (BasicBlock* const block : Blocks())
    {
        if (block->HasFlag(BBF_BACKWARD_JUMP))
        {
            JITDUMP("fgPromoteFrozenAllocations: method has a loop; punting\n");
            return PhaseStatus::MODIFIED_NOTHING;
        }
    }

    // SSA must be valid here (the phase is scheduled inside the SSA-valid
    // window in compCompile).
    assert(fgSsaPassesCompleted > 0);

    CompAllocator           alloc(getAllocator(CMK_Generic));
    FieldStoreCountMap      fieldMap(alloc);
    ArrayStack<StsfldStore> candidates(alloc);

    // Pass 1: collect candidate STSFLDs and per-field store counts.
    StsfldCollector collector(this, &fieldMap, &candidates);
    for (BasicBlock* const block : Blocks())
    {
        for (Statement* const stmt : block->Statements())
        {
            collector.SetContext(block, stmt);
            collector.WalkTree(stmt->GetRootNodePointer(), nullptr);
        }
    }

    // Pass 2: validate each candidate via SSA def-use, then rewrite.
    bool modified = false;
    for (int i = 0; i < candidates.Height(); i++)
    {
        const StsfldStore& cand = candidates.BottomRef(i);

        int storeCount = 0;
        fieldMap.Lookup(cand.field, &storeCount);
        if (storeCount != 1)
        {
            continue;
        }
        if (!IsStaticFinalRefField(this, cand.field))
        {
            continue;
        }

        // Peel any GT_BOX wrapper the importer placed on the publishing
        // value of a boxed static.
        GenTree* publishedValue = cand.value;
        if (publishedValue->OperIs(GT_BOX))
        {
            publishedValue = publishedValue->AsBox()->BoxOp();
        }

        GenTreeCall* allocCall = nullptr;

        bool directObj;
        bool directArr;
        if (IsAllocCall(publishedValue, &directObj, &directArr))
        {
            // Rare: the alloc helper is the direct value of the stsfld
            // (no temp). No SSA traversal needed.
            allocCall = publishedValue->AsCall();
        }
        else
        {
            allocCall = TryFindAllocForCandidate(this, cand, publishedValue);
        }

        if (allocCall == nullptr)
        {
            continue;
        }

        if (PromoteAllocToFrozen(this, allocCall))
        {
            JITDUMP("fgPromoteFrozenAllocations: promoted allocation in cctor for field %s\n",
                    eeGetFieldName(cand.field, true));
            modified = true;
        }
    }

    return modified ? PhaseStatus::MODIFIED_EVERYTHING : PhaseStatus::MODIFIED_NOTHING;
}
