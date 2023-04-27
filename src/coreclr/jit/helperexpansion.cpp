// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

// Obtain constant pointer from a tree
static void* GetConstantPointer(Compiler* comp, GenTree* tree)
{
    void* cns = nullptr;
    if (tree->gtEffectiveVal()->IsCnsIntOrI())
    {
        cns = (void*)tree->gtEffectiveVal()->AsIntCon()->IconValue();
    }
    else if (comp->vnStore->IsVNConstant(tree->gtVNPair.GetLiberal()))
    {
        cns = (void*)comp->vnStore->CoercedConstantValue<ssize_t>(tree->gtVNPair.GetLiberal());
    }
    return cns;
}

// Save expression to a local and append it as the last statement in exprBlock
static GenTree* SpillExpression(Compiler* comp, GenTree* expr, BasicBlock* exprBlock, DebugInfo& debugInfo)
{
    unsigned const tmpNum  = comp->lvaGrabTemp(true DEBUGARG("spilling expr"));
    Statement*     asgStmt = comp->fgNewStmtAtEnd(exprBlock, comp->gtNewTempAssign(tmpNum, expr), debugInfo);
    comp->gtSetStmtInfo(asgStmt);
    comp->fgSetStmtSeq(asgStmt);
    return comp->gtNewLclvNode(tmpNum, genActualType(expr));
};

//------------------------------------------------------------------------------
// gtNewRuntimeLookupHelperCallNode : Helper to create a runtime lookup call helper node.
//
// Arguments:
//    helper    - Call helper
//    type      - Type of the node
//    args      - Call args
//
// Return Value:
//    New CT_HELPER node
//
GenTreeCall* Compiler::gtNewRuntimeLookupHelperCallNode(CORINFO_RUNTIME_LOOKUP* pRuntimeLookup,
                                                        GenTree*                ctxTree,
                                                        void*                   compileTimeHandle)
{
    // Call the helper
    // - Setup argNode with the pointer to the signature returned by the lookup
    GenTree* argNode = gtNewIconEmbHndNode(pRuntimeLookup->signature, nullptr, GTF_ICON_GLOBAL_PTR, compileTimeHandle);
    GenTreeCall* helperCall = gtNewHelperCallNode(pRuntimeLookup->helper, TYP_I_IMPL, ctxTree, argNode);

    // No need to perform CSE/hoisting for signature node - it is expected to end up in a rarely-taken block after
    // "Expand runtime lookups" phase.
    argNode->gtFlags |= GTF_DONT_CSE;

    // Leave a note that this method has runtime lookups we might want to expand (nullchecks, size checks) later.
    // We can also consider marking current block as a runtime lookup holder to improve TP for Tier0
    impInlineRoot()->setMethodHasExpRuntimeLookup();
    helperCall->SetExpRuntimeLookup();
    if (!impInlineRoot()->GetSignatureToLookupInfoMap()->Lookup(pRuntimeLookup->signature))
    {
        JITDUMP("Registering %p in SignatureToLookupInfoMap\n", pRuntimeLookup->signature)
        impInlineRoot()->GetSignatureToLookupInfoMap()->Set(pRuntimeLookup->signature, *pRuntimeLookup);
    }
    return helperCall;
}

//------------------------------------------------------------------------------
// fgExpandRuntimeLookups : partially expand runtime lookups helper calls
//                          to add a nullcheck [+ size check] and a fast path
// Returns:
//    PhaseStatus indicating what, if anything, was changed.
//
// Notes:
//    The runtime lookup itself is needed to access a handle in code shared between
//    generic instantiations. The lookup depends on the typeContext which is only available at
//    runtime, and not at compile - time. See ASCII block diagrams in comments below for
//    better understanding how this phase expands runtime lookups.
//
PhaseStatus Compiler::fgExpandRuntimeLookups()
{
    PhaseStatus result = PhaseStatus::MODIFIED_NOTHING;

    if (!doesMethodHaveExpRuntimeLookup())
    {
        // The method being compiled doesn't have expandable runtime lookups. If it does
        // and doesMethodHaveExpRuntimeLookup() still returns false we'll assert in LowerCall
        return result;
    }

    return fgExpandHelper<&Compiler::fgExpandRuntimeLookupsForCall>(false);
}

//------------------------------------------------------------------------------
// fgExpandRuntimeLookupsForCall : partially expand runtime lookups helper calls
//    to add a nullcheck [+ size check] and a fast path
//
// Arguments:
//    pBlock - Block containing the helper call to expand. If expansion is performed,
//             this is updated to the new block that was an outcome of block splitting.
//    stmt   - Statement containing the helper call
//    call   - The helper call
//
// Returns:
//    true if a runtime lookup was found and expanded.
//
// Notes:
//    The runtime lookup itself is needed to access a handle in code shared between
//    generic instantiations. The lookup depends on the typeContext which is only available at
//    runtime, and not at compile - time. See ASCII block diagrams in comments below for
//    better understanding how this phase expands runtime lookups.
//
bool Compiler::fgExpandRuntimeLookupsForCall(BasicBlock** pBlock, Statement* stmt, GenTreeCall* call)
{
    BasicBlock* block = *pBlock;
    assert(call->IsHelperCall());

    if (!call->IsExpRuntimeLookup())
    {
        return false;
    }

    // Clear ExpRuntimeLookup flag so we won't miss any runtime lookup that needs partial expansion
    call->ClearExpRuntimeLookup();

    if (call->IsTailCall())
    {
        // It is very unlikely to happen and is impossible to represent in C#
        return false;
    }

    assert(call->gtArgs.CountArgs() == 2);
    // The call has the following signature:
    //
    //   type = call(genericCtx, signatureCns);
    //
    void* signature = GetConstantPointer(this, call->gtArgs.GetArgByIndex(1)->GetNode());
    if (signature == nullptr)
    {
        // Technically, it is possible (e.g. it was CSE'd and then VN was erased), but for Debug mode we
        // want to catch such cases as we really don't want to emit just a fallback call - it's too slow
        assert(!"can't restore signature argument value");
        return false;
    }

    JITDUMP("Expanding runtime lookup for [%06d] in " FMT_BB ":\n", dspTreeID(call), block->bbNum)
    DISPTREE(call)
    JITDUMP("\n")

    // Restore runtimeLookup using signature argument via a global dictionary
    CORINFO_RUNTIME_LOOKUP runtimeLookup = {};
    const bool             lookupFound   = GetSignatureToLookupInfoMap()->Lookup(signature, &runtimeLookup);
    assert(lookupFound);

    const bool needsSizeCheck = runtimeLookup.sizeOffset != CORINFO_NO_SIZE_CHECK;
    if (needsSizeCheck)
    {
        JITDUMP("dynamic expansion, needs size check.\n")
    }

    DebugInfo debugInfo = stmt->GetDebugInfo();

    assert(runtimeLookup.indirections != 0);
    assert(runtimeLookup.testForNull);

    // Split block right before the call tree
    BasicBlock* prevBb       = block;
    GenTree**   callUse      = nullptr;
    Statement*  newFirstStmt = nullptr;
    block                    = fgSplitBlockBeforeTree(block, stmt, call, &newFirstStmt, &callUse);
    *pBlock                  = block;
    assert(prevBb != nullptr && block != nullptr);

    // Block ops inserted by the split need to be morphed here since we are after morph.
    // We cannot morph stmt yet as we may modify it further below, and the morphing
    // could invalidate callUse.
    while ((newFirstStmt != nullptr) && (newFirstStmt != stmt))
    {
        fgMorphStmtBlockOps(block, newFirstStmt);
        newFirstStmt = newFirstStmt->GetNextStmt();
    }

    GenTreeLclVar* rtLookupLcl = nullptr;

    // Mostly for Tier0: if the current statement is ASG(LCL, RuntimeLookup)
    // we can drop it and use that LCL as the destination
    if (stmt->GetRootNode()->OperIs(GT_ASG))
    {
        GenTree* lhs = stmt->GetRootNode()->gtGetOp1();
        GenTree* rhs = stmt->GetRootNode()->gtGetOp2();
        if (lhs->OperIs(GT_LCL_VAR) && rhs == *callUse)
        {
            rtLookupLcl = gtClone(lhs)->AsLclVar();
            fgRemoveStmt(block, stmt);
        }
    }

    // Grab a temp to store result (it's assigned from either fastPathBb or fallbackBb)
    if (rtLookupLcl == nullptr)
    {
        // Define a local for the result
        unsigned rtLookupLclNum         = lvaGrabTemp(true DEBUGARG("runtime lookup"));
        lvaTable[rtLookupLclNum].lvType = TYP_I_IMPL;
        rtLookupLcl                     = gtNewLclvNode(rtLookupLclNum, call->TypeGet());

        *callUse = gtClone(rtLookupLcl);

        fgMorphStmtBlockOps(block, stmt);
        gtUpdateStmtSideEffects(stmt);
    }

    GenTree* ctxTree = call->gtArgs.GetArgByIndex(0)->GetNode();
    GenTree* sigNode = call->gtArgs.GetArgByIndex(1)->GetNode();

    // Prepare slotPtr tree (TODO: consider sharing this part with impRuntimeLookup)
    GenTree* slotPtrTree   = gtCloneExpr(ctxTree);
    GenTree* indOffTree    = nullptr;
    GenTree* lastIndOfTree = nullptr;
    for (WORD i = 0; i < runtimeLookup.indirections; i++)
    {
        if ((i == 1 && runtimeLookup.indirectFirstOffset) || (i == 2 && runtimeLookup.indirectSecondOffset))
        {
            indOffTree  = SpillExpression(this, slotPtrTree, prevBb, debugInfo);
            slotPtrTree = gtCloneExpr(indOffTree);
        }

        // The last indirection could be subject to a size check (dynamic dictionary expansion)
        const bool isLastIndirectionWithSizeCheck = (i == runtimeLookup.indirections - 1) && needsSizeCheck;
        if (i != 0)
        {
            GenTreeFlags indirFlags = GTF_IND_NONFAULTING;
            if (!isLastIndirectionWithSizeCheck)
            {
                indirFlags |= GTF_IND_INVARIANT;
            }
            slotPtrTree = gtNewIndir(TYP_I_IMPL, slotPtrTree, indirFlags);
        }

        if ((i == 1 && runtimeLookup.indirectFirstOffset) || (i == 2 && runtimeLookup.indirectSecondOffset))
        {
            slotPtrTree = gtNewOperNode(GT_ADD, TYP_I_IMPL, indOffTree, slotPtrTree);
        }
        if (runtimeLookup.offsets[i] != 0)
        {
            if (isLastIndirectionWithSizeCheck)
            {
                lastIndOfTree = SpillExpression(this, slotPtrTree, prevBb, debugInfo);
                slotPtrTree   = gtCloneExpr(lastIndOfTree);
            }
            slotPtrTree =
                gtNewOperNode(GT_ADD, TYP_I_IMPL, slotPtrTree, gtNewIconNode(runtimeLookup.offsets[i], TYP_I_IMPL));
        }
    }

    // Non-dynamic expansion case (no size check):
    //
    // prevBb(BBJ_NONE):                    [weight: 1.0]
    //     ...
    //
    // nullcheckBb(BBJ_COND):               [weight: 1.0]
    //     if (*fastPathValue == null)
    //         goto fallbackBb;
    //
    // fastPathBb(BBJ_ALWAYS):              [weight: 0.8]
    //     rtLookupLcl = *fastPathValue;
    //     goto block;
    //
    // fallbackBb(BBJ_NONE):                [weight: 0.2]
    //     rtLookupLcl = HelperCall();
    //
    // block(...):                          [weight: 1.0]
    //     use(rtLookupLcl);
    //

    // null-check basic block
    GenTree* fastPathValue = gtNewIndir(TYP_I_IMPL, gtCloneExpr(slotPtrTree), GTF_IND_NONFAULTING);
    // Save dictionary slot to a local (to be used by fast path)
    GenTree* fastPathValueClone =
        opts.OptimizationEnabled() ? fgMakeMultiUse(&fastPathValue) : gtCloneExpr(fastPathValue);
    GenTree* nullcheckOp = gtNewOperNode(GT_EQ, TYP_INT, fastPathValue, gtNewIconNode(0, TYP_I_IMPL));
    nullcheckOp->gtFlags |= GTF_RELOP_JMP_USED;
    BasicBlock* nullcheckBb =
        fgNewBBFromTreeAfter(BBJ_COND, prevBb, gtNewOperNode(GT_JTRUE, TYP_VOID, nullcheckOp), debugInfo);

    // Fallback basic block
    GenTree*    asgFallbackValue = gtNewAssignNode(gtClone(rtLookupLcl), call);
    BasicBlock* fallbackBb       = fgNewBBFromTreeAfter(BBJ_NONE, nullcheckBb, asgFallbackValue, debugInfo, true);

    // Fast-path basic block
    GenTree*    asgFastpathValue = gtNewAssignNode(gtClone(rtLookupLcl), fastPathValueClone);
    BasicBlock* fastPathBb       = fgNewBBFromTreeAfter(BBJ_ALWAYS, nullcheckBb, asgFastpathValue, debugInfo);

    BasicBlock* sizeCheckBb = nullptr;
    if (needsSizeCheck)
    {
        // Dynamic expansion case (sizeCheckBb is added and some preds are changed):
        //
        // prevBb(BBJ_NONE):                    [weight: 1.0]
        //
        // sizeCheckBb(BBJ_COND):               [weight: 1.0]
        //     if (sizeValue <= offsetValue)
        //         goto fallbackBb;
        //     ...
        //
        // nullcheckBb(BBJ_COND):               [weight: 0.8]
        //     if (*fastPathValue == null)
        //         goto fallbackBb;
        //
        // fastPathBb(BBJ_ALWAYS):              [weight: 0.64]
        //     rtLookupLcl = *fastPathValue;
        //     goto block;
        //
        // fallbackBb(BBJ_NONE):                [weight: 0.36]
        //     rtLookupLcl = HelperCall();
        //
        // block(...):                          [weight: 1.0]
        //     use(rtLookupLcl);
        //

        // sizeValue = dictionary[pRuntimeLookup->sizeOffset]
        GenTreeIntCon* sizeOffset = gtNewIconNode(runtimeLookup.sizeOffset, TYP_I_IMPL);
        assert(lastIndOfTree != nullptr);
        GenTree* sizeValueOffset = gtNewOperNode(GT_ADD, TYP_I_IMPL, lastIndOfTree, sizeOffset);
        GenTree* sizeValue       = gtNewIndir(TYP_I_IMPL, sizeValueOffset, GTF_IND_NONFAULTING);

        // sizeCheck fails if sizeValue <= pRuntimeLookup->offsets[i]
        GenTree* offsetValue = gtNewIconNode(runtimeLookup.offsets[runtimeLookup.indirections - 1], TYP_I_IMPL);
        GenTree* sizeCheck   = gtNewOperNode(GT_LE, TYP_INT, sizeValue, offsetValue);
        sizeCheck->gtFlags |= GTF_RELOP_JMP_USED;

        GenTree* jtrue = gtNewOperNode(GT_JTRUE, TYP_VOID, sizeCheck);
        sizeCheckBb    = fgNewBBFromTreeAfter(BBJ_COND, prevBb, jtrue, debugInfo);
    }

    //
    // Update preds in all new blocks
    //
    fgRemoveRefPred(block, prevBb);
    fgAddRefPred(block, fastPathBb);
    fgAddRefPred(block, fallbackBb);
    nullcheckBb->bbJumpDest = fallbackBb;
    fastPathBb->bbJumpDest  = block;

    if (needsSizeCheck)
    {
        // sizeCheckBb is the first block after prevBb
        fgAddRefPred(sizeCheckBb, prevBb);
        // sizeCheckBb flows into nullcheckBb in case if the size check passes
        fgAddRefPred(nullcheckBb, sizeCheckBb);
        // fallbackBb is reachable from both nullcheckBb and sizeCheckBb
        fgAddRefPred(fallbackBb, nullcheckBb);
        fgAddRefPred(fallbackBb, sizeCheckBb);
        // fastPathBb is only reachable from successful nullcheckBb
        fgAddRefPred(fastPathBb, nullcheckBb);
        // sizeCheckBb fails - jump to fallbackBb
        sizeCheckBb->bbJumpDest = fallbackBb;
    }
    else
    {
        // nullcheckBb is the first block after prevBb
        fgAddRefPred(nullcheckBb, prevBb);
        // No size check, nullcheckBb jumps to fast path
        fgAddRefPred(fastPathBb, nullcheckBb);
        // fallbackBb is only reachable from nullcheckBb (jump destination)
        fgAddRefPred(fallbackBb, nullcheckBb);
    }

    //
    // Re-distribute weights (see '[weight: X]' on the diagrams above)
    // TODO: consider marking fallbackBb as rarely-taken
    //
    block->inheritWeight(prevBb);
    if (needsSizeCheck)
    {
        sizeCheckBb->inheritWeight(prevBb);
        // 80% chance we pass nullcheck
        nullcheckBb->inheritWeightPercentage(sizeCheckBb, 80);
        // 64% (0.8 * 0.8) chance we pass both nullcheck and sizecheck
        fastPathBb->inheritWeightPercentage(nullcheckBb, 80);
        // 100-64=36% chance we fail either nullcheck or sizecheck
        fallbackBb->inheritWeightPercentage(sizeCheckBb, 36);
    }
    else
    {
        nullcheckBb->inheritWeight(prevBb);
        // 80% chance we pass nullcheck
        fastPathBb->inheritWeightPercentage(nullcheckBb, 80);
        // 20% chance we fail nullcheck (TODO: Consider making it cold (0%))
        fallbackBb->inheritWeightPercentage(nullcheckBb, 20);
    }

    //
    // Update loop info
    //
    nullcheckBb->bbNatLoopNum = prevBb->bbNatLoopNum;
    fastPathBb->bbNatLoopNum  = prevBb->bbNatLoopNum;
    fallbackBb->bbNatLoopNum  = prevBb->bbNatLoopNum;
    if (needsSizeCheck)
    {
        sizeCheckBb->bbNatLoopNum = prevBb->bbNatLoopNum;
    }

    // All blocks are expected to be in the same EH region
    assert(BasicBlock::sameEHRegion(prevBb, block));
    assert(BasicBlock::sameEHRegion(prevBb, nullcheckBb));
    assert(BasicBlock::sameEHRegion(prevBb, fastPathBb));
    if (needsSizeCheck)
    {
        assert(BasicBlock::sameEHRegion(prevBb, sizeCheckBb));
    }
    return true;
}

//------------------------------------------------------------------------------
// fgVNBasedIntrinsicExpansion: Expand specific calls marked as intrinsics using VN.
//
// Returns:
//    PhaseStatus indicating what, if anything, was changed.
//
PhaseStatus Compiler::fgVNBasedIntrinsicExpansion()
{
    PhaseStatus result = PhaseStatus::MODIFIED_NOTHING;

    if (!doesMethodHasSpecialIntrinsics() || opts.OptimizationDisabled())
    {
        return result;
    }

    // TODO: Replace with opts.compCodeOpt once it's fixed
    const bool preferSize = opts.jitFlags->IsSet(JitFlags::JIT_FLAG_SIZE_OPT);
    if (preferSize)
    {
        // The optimization comes with a codegen size increase
        JITDUMP("Optimized for size - bail out.\n")
        return result;
    }
    return fgExpandHelper<&Compiler::fgVNBasedIntrinsicExpansionForCall>(true, true);
}

//------------------------------------------------------------------------------
// fgVNBasedIntrinsicExpansionForCall : Expand specific calls marked as intrinsics using VN.
//
// Arguments:
//    block - Block containing the intrinsic call to expand
//    stmt  - Statement containing the call
//    call  - The intrinsic call
//
// Returns:
//    True if expanded, false otherwise.
//
bool Compiler::fgVNBasedIntrinsicExpansionForCall(BasicBlock** pBlock, Statement* stmt, GenTreeCall* call)
{
    assert(call->gtCallMoreFlags & GTF_CALL_M_SPECIAL_INTRINSIC);
    NamedIntrinsic ni = lookupNamedIntrinsic(call->gtCallMethHnd);
    if (ni == NI_System_Text_UTF8Encoding_UTF8EncodingSealed_GetUtf8Bytes)
    {
        return fgVNBasedIntrinsicExpansionForCall_GetUtf8Bytes(pBlock, stmt, call);
    }

    // TODO: Expand IsKnownConstant here
    // Also, move various unrollings here

    return false;
}

//------------------------------------------------------------------------------
// fgVNBasedIntrinsicExpansionForCall_GetUtf8Bytes : Expand NI_System_Text_UTF8Encoding_UTF8EncodingSealed_GetUtf8Bytes
//    when src data is a string literal (UTF16) tha can be narrowed to ASCII (UTF8), e.g.:
//
//      string str = "Hello, world!";
//      int bytesWritten = GetUtf8Bytes(ref str[0], str.Length, buffer, buffer.Length);
//
//    becomes:
//
//      bytesWritten = 0; // default value
//      if (buffer.Length >= str.Length) // *might* be folded if buffer.Length is a constant
//      {
//          memcpy(buffer, "Hello, world!"u8, str.Length); // note the u8 suffix
//          bytesWritten = str.Length;
//      }
//
// Arguments:
//    block - Block containing the intrinsic call to expand
//    stmt  - Statement containing the call
//    call  - The intrinsic call
//
// Returns:
//    True if expanded, false otherwise.
//
bool Compiler::fgVNBasedIntrinsicExpansionForCall_GetUtf8Bytes(BasicBlock** pBlock, Statement* stmt, GenTreeCall* call)
{
    BasicBlock* block = *pBlock;

    // NI_System_Text_UTF8Encoding_UTF8EncodingSealed_GetUtf8Bytes
    assert(call->gtArgs.CountUserArgs() == 5);

    // First, list all arguments of the intrinsic call, the signature is:
    //
    //   int GetUtf8Bytes(ref char srcPtr, int srcLen, ref byte dstPtr, int dstLen)
    //
    GenTree* thisArg = call->gtArgs.GetUserArgByIndex(0)->GetNode();
    GenTree* srcPtr  = call->gtArgs.GetUserArgByIndex(1)->GetNode();
    GenTree* srcLen  = call->gtArgs.GetUserArgByIndex(2)->GetNode();
    GenTree* dstPtr  = call->gtArgs.GetUserArgByIndex(3)->GetNode();
    GenTree* dstLen  = call->gtArgs.GetUserArgByIndex(4)->GetNode();

    // We're interested in a case when srcPtr is a string literal and srcLen is a constant

    ssize_t               strObjOffset = 0;
    CORINFO_OBJECT_HANDLE strObj       = nullptr;
    if (!GetObjectHandleAndOffset(srcPtr, &strObjOffset, &strObj) || (strObjOffset != OFFSETOF__CORINFO_String__chars))
    {
        // TODO: Consider supporting any offset if that is a common pattern, also
        // static readonly fields (RVA ROS<char> or non-frozen string objects)
        JITDUMP("GetUtf8Bytes: srcPtr is not a string literal\n")
        return false;
    }

    if (info.compCompHnd->getObjectType(strObj) != impGetStringClass())
    {
        JITDUMP("GetUtf8Bytes: srcPtr is not a string object\n")
        return false;
    }

    if (!srcLen->gtVNPair.BothEqual() || !vnStore->IsVNInt32Constant(srcLen->gtVNPair.GetLiberal()))
    {
        JITDUMP("GetUtf8Bytes: srcLen is not constant\n")
        return false;
    }

    // Do we need to care if srcLenCns is larger than the actual string literal length? It's a faulty case anyway.

    const int      MaxPossibleUnrollThreshold = 256;
    const unsigned unrollThreshold            = min(getUnrollThreshold(UnrollKind::Memcpy), MaxPossibleUnrollThreshold);
    const unsigned srcLenCns                  = (unsigned)vnStore->GetConstantInt32(srcLen->gtVNPair.GetLiberal());
    if (srcLenCns == 0 || srcLenCns > unrollThreshold)
    {
        // TODO: handle srcLenCns == 0 if it's a common case
        JITDUMP("GetUtf8Bytes: srcLenCns is out of unrollable range\n")
        return false;
    }

    // We don't need the length condition if we know that the destination buffer is large enough
    bool noSizeCheck = false;
    if (dstLen->gtVNPair.BothEqual() && vnStore->IsVNInt32Constant(dstLen->gtVNPair.GetLiberal()))
    {
        const int dstLenCns = vnStore->GetConstantInt32(dstLen->gtVNPair.GetLiberal());
        noSizeCheck         = dstLenCns >= (int)srcLenCns;
        assert(dstLenCns > 0);
    }

    // Read the string literal (UTF16) into a local buffer (UTF8)
    assert(strObj != nullptr);
    BYTE srcUtf8cns[MaxPossibleUnrollThreshold] = {}; // same length since we're narrowing U16 to U8
    for (unsigned charIndex = 0; charIndex < srcLenCns; charIndex++)
    {
        uint16_t ch = 0;
        if (!info.compCompHnd->getStringChar(strObj, (int)charIndex, &ch))
        {
            // Something went wrong, e.g. the string is shorter than expected - bail out.
            JITDUMP("GetUtf8Bytes: getStringChar(strObj, %d, &ch) returned false.\n", charIndex)
            return false;
        }
        if (ch > 127)
        {
            // Only ASCII is supported.
            JITDUMP("GetUtf8Bytes: %dth char is not ASCII.\n", charIndex)
            return false;
        }
        // Narrow U16 to U8
        srcUtf8cns[charIndex] = (BYTE)ch;
    }

    DebugInfo debugInfo = stmt->GetDebugInfo();

    // Split block right before the call tree (this is a standard pattern we use in helperexpansion.cpp)
    BasicBlock* prevBb       = block;
    GenTree**   callUse      = nullptr;
    Statement*  newFirstStmt = nullptr;
    block                    = fgSplitBlockBeforeTree(block, stmt, call, &newFirstStmt, &callUse);
    assert(prevBb != nullptr && block != nullptr);
    *pBlock = block;

    // Block ops inserted by the split need to be morphed here since we are after morph.
    // We cannot morph stmt yet as we may modify it further below, and the morphing
    // could invalidate callUse
    while ((newFirstStmt != nullptr) && (newFirstStmt != stmt))
    {
        fgMorphStmtBlockOps(block, newFirstStmt);
        newFirstStmt = newFirstStmt->GetNextStmt();
    }

    // We don't need this flag anymore.
    call->gtCallMoreFlags &= ~GTF_CALL_M_SPECIAL_INTRINSIC;

    GenTreeLclVar* resultLcl = nullptr;

    // Grab a temp to store the result.
    // The result corresponds the number of bytes written to dstPtr (int32).
    assert(call->TypeIs(TYP_INT));
    const unsigned resultLclNum   = lvaGrabTemp(true DEBUGARG("local for result"));
    lvaTable[resultLclNum].lvType = TYP_INT;
    resultLcl                     = gtNewLclvNode(resultLclNum, TYP_INT);
    *callUse                      = resultLcl;
    fgMorphStmtBlockOps(block, stmt);
    gtUpdateStmtSideEffects(stmt);

    // srcLenCns is the length of the string literal in chars (UTF16)
    // but we're going to use the same value as the "bytesWritten" result in the fast path and in the length check.
    GenTree* srcLenCnsNode = gtNewIconNode(srcLenCns);
    fgUpdateConstTreeValueNumber(srcLenCnsNode);

    // We're going to insert the following blocks:
    //
    //  prevBb:
    //
    //  lengthCheckBb:
    //      <side-effects>
    //      bytesWritten = -1;
    //      if (dstLen <srcLen)
    //          goto block;
    //
    //  fastpathBb:
    //      bytesWritten = unrolled copy;
    //
    //  block:
    //      use(bytesWritten)
    //

    // or in case if noSizeCheck is true:

    //  prevBb:
    //
    //  lengthCheckBb:
    //      <side-effects>
    //
    //  fastpathBb:
    //      bytesWritten = unrolled copy;
    //
    //  block:
    //      use(bytesWritten)
    //

    //
    // Block 1: lengthCheckBb (we check that dstLen < srcLen)
    //  In case if destIsKnownToFit is true we'll use this block to keep side-effects of the original arguments.
    //  and it will be a fall-through block.
    //
    BasicBlock* lengthCheckBb = fgNewBBafter(noSizeCheck ? BBJ_NONE : BBJ_COND, prevBb, true);
    lengthCheckBb->bbFlags |= BBF_INTERNAL;

    // In 99% cases "this" is expected to be "static readonly UTF8EncodingSealed s_default"
    // which is a static readonly object that is never null
    const bool thisIsKnownNonNull =
        thisArg->gtVNPair.BothEqual() && vnStore->IsKnownNonNull(thisArg->gtVNPair.GetLiberal());

    // Spill all original arguments to locals in the lengthCheckBb to preserve all possible side-effects.
    thisArg = SpillExpression(this, thisArg, lengthCheckBb, debugInfo);
    srcPtr  = SpillExpression(this, srcPtr, lengthCheckBb, debugInfo);
    srcLen  = SpillExpression(this, srcLen, lengthCheckBb, debugInfo);
    dstPtr  = SpillExpression(this, dstPtr, lengthCheckBb, debugInfo);
    dstLen  = SpillExpression(this, dstLen, lengthCheckBb, debugInfo);

    if (!noSizeCheck)
    {
        // Set bytesWritten -1 by default, if the fast path is not taken we'll return it as the result.
        GenTree* bytesWrittenDefaultVal = gtNewAssignNode(gtClone(resultLcl), gtNewIconNode(-1));
        fgInsertStmtAtEnd(lengthCheckBb, fgNewStmtFromTree(bytesWrittenDefaultVal, debugInfo));
    }

    // We don't need "this" object in the fast path so insert an explicit null check here.
    // (after we evaluated all arguments)
    if (!thisIsKnownNonNull)
    {
        GenTree* thisNullcheck = gtNewNullCheck(gtClone(thisArg), lengthCheckBb);
        fgInsertStmtAtEnd(lengthCheckBb, fgNewStmtFromTree(thisNullcheck, debugInfo));
    }

    if (!noSizeCheck)
    {
        GenTree* lengthCheck = gtNewOperNode(GT_LT, TYP_INT, gtClone(dstLen), srcLenCnsNode);
        lengthCheck->gtFlags |= GTF_RELOP_JMP_USED;
        Statement* lengthCheckStmt = fgNewStmtFromTree(gtNewOperNode(GT_JTRUE, TYP_VOID, lengthCheck), debugInfo);
        fgInsertStmtAtEnd(lengthCheckBb, lengthCheckStmt);
        lengthCheckBb->bbCodeOffs    = block->bbCodeOffsEnd;
        lengthCheckBb->bbCodeOffsEnd = block->bbCodeOffsEnd;
    }

    //
    // Block 2: fastpathBb - unrolled loop that copies the UTF8 const data to the destination
    //
    // We're going to emit a series of loads and stores to copy the data.
    // In theory, we could just emit the const U8 data to the data section and use GT_BLK here
    // but that would be a bit less efficient since we would have to load the data from memory.
    //
    BasicBlock* fastpathBb = fgNewBBafter(BBJ_NONE, lengthCheckBb, true);
    fastpathBb->bbFlags |= BBF_INTERNAL;

    // The widest type we can use for loads
    const var_types maxLoadType = roundDownMaxRegSize(srcLenCns);

    // How many iterations we need to copy UTF8 const data to the destination
    unsigned iterations = srcLenCns / genTypeSize(maxLoadType);

    // Add one more iteration if we have a remainder
    iterations += srcLenCns % genTypeSize(maxLoadType) == 0 ? 0 : 1;

    for (unsigned i = 0; i < iterations; i++)
    {
        ssize_t offest = (ssize_t)i * genTypeSize(maxLoadType);

        // Last iteration: overlap with previous load if needed
        if (i == iterations - 1)
        {
            offest = (ssize_t)srcLenCns - genTypeSize(maxLoadType);
        }

        // We're going to emit the following tree:

        // -A-XG------       *  ASG       %maxLoadType% (copy)
        // D--XG--N---       +--*  IND       %maxLoadType%
        // -------N---       |  \--*  ADD       byref
        // -----------       |     +--*  LCL_VAR   byref dstPtr
        // -----------       |     \--*  CNS_INT   int   %offset%
        // -----------       \--*  CNS_VEC or CNS_INT representing UTF8 const data chunk

        GenTreeIntCon* offsetNode = gtNewIconNode(offest);
        fgUpdateConstTreeValueNumber(offsetNode);

        // Grab a chunk from srcUtf8cnsData for the given offset and width
        GenTree* utf8cnsChunkNode = gtNewCon(maxLoadType, srcUtf8cns + offest);
        fgUpdateConstTreeValueNumber(utf8cnsChunkNode);

        GenTree*   dstAddOffsetNode = gtNewOperNode(GT_ADD, TYP_BYREF, gtClone(dstPtr), offsetNode);
        GenTree*   indirNode        = gtNewIndir(maxLoadType, dstAddOffsetNode);
        GenTreeOp* storeInd         = gtNewAssignNode(indirNode, utf8cnsChunkNode);
        Statement* storeIndStmt     = fgNewStmtFromTree(storeInd, debugInfo);
        fgInsertStmtAtEnd(fastpathBb, storeIndStmt);
        gtUpdateStmtSideEffects(storeIndStmt);
    }

    // Finally, store the number of bytes written to the resultLcl local
    Statement* finalStmt =
        fgNewStmtFromTree(gtNewAssignNode(gtClone(resultLcl), gtCloneExpr(srcLenCnsNode)), debugInfo);
    fgInsertStmtAtEnd(fastpathBb, finalStmt);
    fastpathBb->bbCodeOffs    = block->bbCodeOffsEnd;
    fastpathBb->bbCodeOffsEnd = block->bbCodeOffsEnd;

    //
    // Update preds in all new blocks
    //
    // block is no longer a predecessor of prevBb
    fgRemoveRefPred(block, prevBb);
    // prevBb flows into lengthCheckBb
    fgAddRefPred(lengthCheckBb, prevBb);
    // lengthCheckBb has two successors: block and fastpathBb (if !destIsKnownToFit)
    fgAddRefPred(fastpathBb, lengthCheckBb);
    if (!noSizeCheck)
    {
        fgAddRefPred(block, lengthCheckBb);
    }
    // fastpathBb flows into block
    fgAddRefPred(block, fastpathBb);
    // lengthCheckBb jumps to block if condition is met
    lengthCheckBb->bbJumpDest = block;

    //
    // Re-distribute weights
    //
    lengthCheckBb->inheritWeight(prevBb);
    // we don't have any real world data on how often this fallback path is taken so we just assume 20% of the time
    fastpathBb->inheritWeightPercentage(lengthCheckBb, noSizeCheck ? 100 : 80);
    block->inheritWeight(prevBb);

    //
    // Update bbNatLoopNum for all new blocks
    //
    lengthCheckBb->bbNatLoopNum = prevBb->bbNatLoopNum;
    fastpathBb->bbNatLoopNum    = prevBb->bbNatLoopNum;

    // All blocks are expected to be in the same EH region
    assert(BasicBlock::sameEHRegion(prevBb, block));
    assert(BasicBlock::sameEHRegion(prevBb, lengthCheckBb));
    assert(BasicBlock::sameEHRegion(prevBb, fastpathBb));

    // Extra step: merge prevBb with lengthCheckBb if possible
    if (fgCanCompactBlocks(prevBb, lengthCheckBb))
    {
        fgCompactBlocks(prevBb, lengthCheckBb);
    }

    JITDUMP("GetUtf8Bytes: succesfully expanded!\n")
    return true;
}

//------------------------------------------------------------------------------
// fgExpandThreadLocalAccess: Inline the CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
//      helper. See fgExpandThreadLocalAccessForCall for details.
//
// Returns:
//    PhaseStatus indicating what, if anything, was changed.
//
PhaseStatus Compiler::fgExpandThreadLocalAccess()
{
    PhaseStatus result = PhaseStatus::MODIFIED_NOTHING;

    if (!doesMethodHasTlsFieldAccess())
    {
        // TP: nothing to expand in the current method
        JITDUMP("Nothing to expand.\n")
        return result;
    }

    if (opts.OptimizationDisabled())
    {
        JITDUMP("Optimizations aren't allowed - bail out.\n")
        return result;
    }

    // TODO: Replace with opts.compCodeOpt once it's fixed
    const bool preferSize = opts.jitFlags->IsSet(JitFlags::JIT_FLAG_SIZE_OPT);
    if (preferSize)
    {
        // The optimization comes with a codegen size increase
        JITDUMP("Optimized for size - bail out.\n")
        return result;
    }

    return fgExpandHelper<&Compiler::fgExpandThreadLocalAccessForCall>(true);
}

//------------------------------------------------------------------------------
// fgExpandThreadLocalAccessForCall : Expand the CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
//                             that access fields marked with [ThreadLocal].
//
// Arguments:
//    pBlock - Block containing the helper call to expand. If expansion is performed,
//             this is updated to the new block that was an outcome of block splitting.
//    stmt   - Statement containing the helper call
//    call   - The helper call
//
//
// Returns:
//    PhaseStatus indicating what, if anything, was changed.
//
// Notes:
//    A cache is stored in thread local storage (TLS) of coreclr. It maps the typeIndex (embedded in
//    the code at the JIT time) to the base of static blocks. This method generates code to
//    extract the TLS, get the entry at which the cache is stored. Then it checks if the typeIndex of
//    enclosing type of current field is present in the cache and if yes, extract out that can be directly
//    accessed at the uses.
//    If the entry is not present, the helper is called, which would make an entry of current static block
//    in the cache.
//
bool Compiler::fgExpandThreadLocalAccessForCall(BasicBlock** pBlock, Statement* stmt, GenTreeCall* call)
{
    BasicBlock* block = *pBlock;
    assert(call->IsHelperCall());
    if (!call->IsExpTLSFieldAccess())
    {
        return false;
    }

#ifdef TARGET_ARM
    // On Arm, Thread execution blocks are accessed using co-processor registers and instructions such
    // as MRC and MCR are used to access them. We do not support them and so should never optimize the
    // field access using TLS.
    assert(!"Unsupported scenario of optimizing TLS access on Arm32");
#endif

    CORINFO_THREAD_STATIC_BLOCKS_INFO threadStaticBlocksInfo;
    info.compCompHnd->getThreadLocalStaticBlocksInfo(&threadStaticBlocksInfo);
    JITDUMP("getThreadLocalStaticBlocksInfo\n:");
    JITDUMP("tlsIndex= %u\n", (ssize_t)threadStaticBlocksInfo.tlsIndex.addr);
    JITDUMP("offsetOfMaxThreadStaticBlocks= %u\n", threadStaticBlocksInfo.offsetOfMaxThreadStaticBlocks);
    JITDUMP("offsetOfThreadLocalStoragePointer= %u\n", threadStaticBlocksInfo.offsetOfThreadLocalStoragePointer);
    JITDUMP("offsetOfThreadStaticBlocks= %u\n", threadStaticBlocksInfo.offsetOfThreadStaticBlocks);

    assert(threadStaticBlocksInfo.tlsIndex.accessType == IAT_VALUE);
    assert(eeGetHelperNum(call->gtCallMethHnd) == CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED);

    JITDUMP("Expanding thread static local access for [%06d] in " FMT_BB ":\n", dspTreeID(call), block->bbNum);
    DISPTREE(call);
    JITDUMP("\n");

    call->ClearExpTLSFieldAccess();
    assert(call->gtArgs.CountArgs() == 1);

    // Split block right before the call tree
    BasicBlock* prevBb       = block;
    GenTree**   callUse      = nullptr;
    Statement*  newFirstStmt = nullptr;
    DebugInfo   debugInfo    = stmt->GetDebugInfo();
    block                    = fgSplitBlockBeforeTree(block, stmt, call, &newFirstStmt, &callUse);
    *pBlock                  = block;
    assert(prevBb != nullptr && block != nullptr);

    // Block ops inserted by the split need to be morphed here since we are after morph.
    // We cannot morph stmt yet as we may modify it further below, and the morphing
    // could invalidate callUse.
    while ((newFirstStmt != nullptr) && (newFirstStmt != stmt))
    {
        fgMorphStmtBlockOps(block, newFirstStmt);
        newFirstStmt = newFirstStmt->GetNextStmt();
    }

    GenTreeLclVar* threadStaticBlockLcl = nullptr;

    // Grab a temp to store result (it's assigned from either fastPathBb or fallbackBb)
    unsigned threadStaticBlockLclNum         = lvaGrabTemp(true DEBUGARG("TLS field access"));
    lvaTable[threadStaticBlockLclNum].lvType = TYP_I_IMPL;
    threadStaticBlockLcl                     = gtNewLclvNode(threadStaticBlockLclNum, call->TypeGet());

    *callUse = gtClone(threadStaticBlockLcl);

    fgMorphStmtBlockOps(block, stmt);
    gtUpdateStmtSideEffects(stmt);

    GenTree* typeThreadStaticBlockIndexValue = call->gtArgs.GetArgByIndex(0)->GetNode();

    void** pIdAddr = nullptr;

    size_t   tlsIndexValue = (size_t)threadStaticBlocksInfo.tlsIndex.addr;
    GenTree* dllRef        = nullptr;

    if (tlsIndexValue != 0)
    {
        dllRef = gtNewIconHandleNode(tlsIndexValue * TARGET_POINTER_SIZE, GTF_ICON_TLS_HDL);
    }

    // Mark this ICON as a TLS_HDL, codegen will use FS:[cns] or GS:[cns]
    GenTree* tlsRef = gtNewIconHandleNode(threadStaticBlocksInfo.offsetOfThreadLocalStoragePointer, GTF_ICON_TLS_HDL);

    tlsRef = gtNewIndir(TYP_I_IMPL, tlsRef, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);

    if (dllRef != nullptr)
    {
        // Add the dllRef to produce thread local storage reference for coreclr
        tlsRef = gtNewOperNode(GT_ADD, TYP_I_IMPL, tlsRef, dllRef);
    }

    // Base of coreclr's thread local storage
    GenTree* tlsValue = gtNewIndir(TYP_I_IMPL, tlsRef, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);

    // Cache the tls value
    unsigned tlsLclNum         = lvaGrabTemp(true DEBUGARG("TLS access"));
    lvaTable[tlsLclNum].lvType = TYP_I_IMPL;
    GenTree* defTlsLclValue    = gtNewLclvNode(tlsLclNum, TYP_I_IMPL);
    GenTree* useTlsLclValue    = gtCloneExpr(defTlsLclValue); // Create a use for tlsLclValue
    GenTree* asgTlsValue       = gtNewAssignNode(defTlsLclValue, tlsValue);

    // Create tree for "maxThreadStaticBlocks = tls[offsetOfMaxThreadStaticBlocks]"
    GenTree* offsetOfMaxThreadStaticBlocks =
        gtNewIconNode(threadStaticBlocksInfo.offsetOfMaxThreadStaticBlocks, TYP_I_IMPL);
    GenTree* maxThreadStaticBlocksRef =
        gtNewOperNode(GT_ADD, TYP_I_IMPL, gtCloneExpr(useTlsLclValue), offsetOfMaxThreadStaticBlocks);
    GenTree* maxThreadStaticBlocksValue =
        gtNewIndir(TYP_INT, maxThreadStaticBlocksRef, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);

    // Create tree for "if (maxThreadStaticBlocks < typeIndex)"
    GenTree* maxThreadStaticBlocksCond =
        gtNewOperNode(GT_LT, TYP_INT, maxThreadStaticBlocksValue, gtCloneExpr(typeThreadStaticBlockIndexValue));
    maxThreadStaticBlocksCond = gtNewOperNode(GT_JTRUE, TYP_VOID, maxThreadStaticBlocksCond);

    // Create tree for "threadStaticBlockBase = tls[offsetOfThreadStaticBlocks]"
    GenTree* offsetOfThreadStaticBlocks = gtNewIconNode(threadStaticBlocksInfo.offsetOfThreadStaticBlocks, TYP_I_IMPL);
    GenTree* threadStaticBlocksRef =
        gtNewOperNode(GT_ADD, TYP_I_IMPL, gtCloneExpr(useTlsLclValue), offsetOfThreadStaticBlocks);
    GenTree* threadStaticBlocksValue =
        gtNewIndir(TYP_I_IMPL, threadStaticBlocksRef, GTF_IND_NONFAULTING | GTF_IND_INVARIANT);

    // Create tree to "threadStaticBlockValue = threadStaticBlockBase[typeIndex]"
    typeThreadStaticBlockIndexValue = gtNewOperNode(GT_MUL, TYP_INT, gtCloneExpr(typeThreadStaticBlockIndexValue),
                                                    gtNewIconNode(TARGET_POINTER_SIZE, TYP_INT));
    GenTree* typeThreadStaticBlockRef =
        gtNewOperNode(GT_ADD, TYP_I_IMPL, threadStaticBlocksValue, typeThreadStaticBlockIndexValue);
    GenTree* typeThreadStaticBlockValue = gtNewIndir(TYP_I_IMPL, typeThreadStaticBlockRef, GTF_IND_NONFAULTING);

    // Cache the threadStaticBlock value
    unsigned threadStaticBlockBaseLclNum         = lvaGrabTemp(true DEBUGARG("ThreadStaticBlockBase access"));
    lvaTable[threadStaticBlockBaseLclNum].lvType = TYP_I_IMPL;
    GenTree* defThreadStaticBlockBaseLclValue    = gtNewLclvNode(threadStaticBlockBaseLclNum, TYP_I_IMPL);
    GenTree* useThreadStaticBlockBaseLclValue =
        gtCloneExpr(defThreadStaticBlockBaseLclValue); // StaticBlockBaseLclValue that will be used
    GenTree* asgThreadStaticBlockBase = gtNewAssignNode(defThreadStaticBlockBaseLclValue, typeThreadStaticBlockValue);

    // Create tree for "if (threadStaticBlockValue != nullptr)"
    GenTree* threadStaticBlockNullCond =
        gtNewOperNode(GT_NE, TYP_INT, useThreadStaticBlockBaseLclValue, gtNewIconNode(0, TYP_I_IMPL));
    threadStaticBlockNullCond = gtNewOperNode(GT_JTRUE, TYP_VOID, threadStaticBlockNullCond);

    // prevBb (BBJ_NONE):                                               [weight: 1.0]
    //      ...
    //
    // maxThreadStaticBlocksCondBB (BBJ_COND):                          [weight: 1.0]
    //      asgTlsValue = tls_access_code
    //      if (maxThreadStaticBlocks < typeIndex)
    //          goto fallbackBb;
    //
    // threadStaticBlockNullCondBB (BBJ_COND):                          [weight: 1.0]
    //      fastPathValue = t_threadStaticBlocks[typeIndex]
    //      if (fastPathValue != nullptr)
    //          goto fastPathBb;
    //
    // fallbackBb (BBJ_ALWAYS):                                         [weight: 0]
    //      threadStaticBlockBase = HelperCall();
    //      goto block;
    //
    // fastPathBb(BBJ_ALWAYS):                                          [weight: 1.0]
    //      threadStaticBlockBase = fastPathValue;
    //
    // block (...):                                                     [weight: 1.0]
    //      use(threadStaticBlockBase);

    // maxThreadStaticBlocksCondBB
    BasicBlock* maxThreadStaticBlocksCondBB = fgNewBBFromTreeAfter(BBJ_COND, prevBb, asgTlsValue, debugInfo);

    fgInsertStmtAfter(maxThreadStaticBlocksCondBB, maxThreadStaticBlocksCondBB->firstStmt(),
                      fgNewStmtFromTree(maxThreadStaticBlocksCond));

    // threadStaticBlockNullCondBB
    BasicBlock* threadStaticBlockNullCondBB =
        fgNewBBFromTreeAfter(BBJ_COND, maxThreadStaticBlocksCondBB, asgThreadStaticBlockBase, debugInfo);
    fgInsertStmtAfter(threadStaticBlockNullCondBB, threadStaticBlockNullCondBB->firstStmt(),
                      fgNewStmtFromTree(threadStaticBlockNullCond));

    // fallbackBb
    GenTree*    asgFallbackValue = gtNewAssignNode(gtClone(threadStaticBlockLcl), call);
    BasicBlock* fallbackBb =
        fgNewBBFromTreeAfter(BBJ_ALWAYS, threadStaticBlockNullCondBB, asgFallbackValue, debugInfo, true);

    // fastPathBb
    GenTree* asgFastPathValue =
        gtNewAssignNode(gtClone(threadStaticBlockLcl), gtCloneExpr(useThreadStaticBlockBaseLclValue));
    BasicBlock* fastPathBb = fgNewBBFromTreeAfter(BBJ_ALWAYS, fallbackBb, asgFastPathValue, debugInfo, true);

    //
    // Update preds in all new blocks
    //
    fgRemoveRefPred(block, prevBb);
    fgAddRefPred(maxThreadStaticBlocksCondBB, prevBb);

    fgAddRefPred(threadStaticBlockNullCondBB, maxThreadStaticBlocksCondBB);
    fgAddRefPred(fallbackBb, maxThreadStaticBlocksCondBB);

    fgAddRefPred(fastPathBb, threadStaticBlockNullCondBB);
    fgAddRefPred(fallbackBb, threadStaticBlockNullCondBB);

    fgAddRefPred(block, fastPathBb);
    fgAddRefPred(block, fallbackBb);

    maxThreadStaticBlocksCondBB->bbJumpDest = fallbackBb;
    threadStaticBlockNullCondBB->bbJumpDest = fastPathBb;
    fastPathBb->bbJumpDest                  = block;
    fallbackBb->bbJumpDest                  = block;

    // Inherit the weights
    block->inheritWeight(prevBb);
    maxThreadStaticBlocksCondBB->inheritWeight(prevBb);
    threadStaticBlockNullCondBB->inheritWeight(prevBb);
    fastPathBb->inheritWeight(prevBb);

    // fallback will just execute first time
    fallbackBb->bbSetRunRarely();

    //
    // Update loop info if loop table is known to be valid
    //
    maxThreadStaticBlocksCondBB->bbNatLoopNum = prevBb->bbNatLoopNum;
    threadStaticBlockNullCondBB->bbNatLoopNum = prevBb->bbNatLoopNum;
    fastPathBb->bbNatLoopNum                  = prevBb->bbNatLoopNum;
    fallbackBb->bbNatLoopNum                  = prevBb->bbNatLoopNum;

    // All blocks are expected to be in the same EH region
    assert(BasicBlock::sameEHRegion(prevBb, block));
    assert(BasicBlock::sameEHRegion(prevBb, maxThreadStaticBlocksCondBB));
    assert(BasicBlock::sameEHRegion(prevBb, threadStaticBlockNullCondBB));
    assert(BasicBlock::sameEHRegion(prevBb, fastPathBb));

    return true;
}

//------------------------------------------------------------------------------
// fgExpandHelper: Expand the helper using ExpansionFunction.
//
// Returns:
//    true if there was any helper that was expanded.
//
template <bool (Compiler::*ExpansionFunction)(BasicBlock**, Statement*, GenTreeCall*)>
PhaseStatus Compiler::fgExpandHelper(bool skipRarelyRunBlocks, bool handleIntrinsics)
{
    PhaseStatus result = PhaseStatus::MODIFIED_NOTHING;
    for (BasicBlock* block = fgFirstBB; block != nullptr; block = block->bbNext)
    {
        if (skipRarelyRunBlocks && block->isRunRarely())
        {
            // It's just an optimization - don't waste time on rarely executed blocks
            continue;
        }

        // Expand and visit the last block again to find more candidates
        INDEBUG(BasicBlock* origBlock = block);
        while (fgExpandHelperForBlock<ExpansionFunction>(&block, handleIntrinsics))
        {
            result = PhaseStatus::MODIFIED_EVERYTHING;
#ifdef DEBUG
            assert(origBlock != block);
            origBlock = block;
#endif
        }
    }

    if ((result == PhaseStatus::MODIFIED_EVERYTHING) && opts.OptimizationEnabled())
    {
        fgReorderBlocks(/* useProfileData */ false);
        fgUpdateChangedFlowGraph(FlowGraphUpdates::COMPUTE_BASICS);
    }

    return result;
}

//------------------------------------------------------------------------------
// fgExpandHelperForBlock: Scans through all the statements of the `block` and
//    invoke `fgExpand` if any of the tree node was a helper call.
//
// Arguments:
//    pBlock   - Block containing the helper call to expand. If expansion is performed,
//               this is updated to the new block that was an outcome of block splitting.
//    fgExpand - function that expands the helper call
//
// Returns:
//    true if a helper was expanded
//
template <bool (Compiler::*ExpansionFunction)(BasicBlock**, Statement*, GenTreeCall*)>
bool Compiler::fgExpandHelperForBlock(BasicBlock** pBlock, bool handleIntrinsics)
{
    for (Statement* const stmt : (*pBlock)->NonPhiStatements())
    {
        if ((stmt->GetRootNode()->gtFlags & GTF_CALL) == 0)
        {
            // TP: Stmt has no calls - bail out
            continue;
        }

        for (GenTree* const tree : stmt->TreeList())
        {
            if (handleIntrinsics)
            {
                if (!tree->IsCall() || !(tree->AsCall()->gtCallMoreFlags & GTF_CALL_M_SPECIAL_INTRINSIC))
                {
                    continue;
                }
            }
            else if (!tree->IsHelperCall())
            {
                continue;
            }

            if ((this->*ExpansionFunction)(pBlock, stmt, tree->AsCall()))
            {
                return true;
            }
        }
    }
    return false;
}

//------------------------------------------------------------------------------
// fgExpandStaticInit: Partially expand static initialization calls, e.g.:
//
//    tmp = CORINFO_HELP_X_NONGCSTATIC_BASE();
//
// into:
//
//    if (isClassAlreadyInited)
//        CORINFO_HELP_X_NONGCSTATIC_BASE();
//    tmp = fastPath;
//
// Returns:
//    PhaseStatus indicating what, if anything, was changed.
//
PhaseStatus Compiler::fgExpandStaticInit()
{
    PhaseStatus result = PhaseStatus::MODIFIED_NOTHING;

    if (!doesMethodHaveStaticInit())
    {
        // TP: nothing to expand in the current method
        JITDUMP("Nothing to expand.\n")
        return result;
    }

    if (opts.OptimizationDisabled())
    {
        JITDUMP("Optimizations aren't allowed - bail out.\n")
        return result;
    }

    // TODO: Replace with opts.compCodeOpt once it's fixed
    const bool preferSize = opts.jitFlags->IsSet(JitFlags::JIT_FLAG_SIZE_OPT);
    if (preferSize)
    {
        // The optimization comes with a codegen size increase
        JITDUMP("Optimized for size - bail out.\n")
        return result;
    }

    return fgExpandHelper<&Compiler::fgExpandStaticInitForCall>(true);
}

//------------------------------------------------------------------------------
// fgExpandStaticInitForCall: Partially expand given static initialization call.
//    Also, see fgExpandStaticInit's comments.
//
// Arguments:
//    pBlock - Block containing the helper call to expand. If expansion is performed,
//             this is updated to the new block that was an outcome of block splitting.
//    stmt   - Statement containing the helper call
//    call   - The helper call
//
// Returns:
//    true if a static initialization was expanded
//
bool Compiler::fgExpandStaticInitForCall(BasicBlock** pBlock, Statement* stmt, GenTreeCall* call)
{
    BasicBlock* block = *pBlock;
    assert(call->IsHelperCall());

    bool                    isGc       = false;
    StaticHelperReturnValue retValKind = {};
    if (!IsStaticHelperEligibleForExpansion(call, &isGc, &retValKind))
    {
        return false;
    }

    assert(!call->IsTailCall());

    if (call->gtInitClsHnd == NO_CLASS_HANDLE)
    {
        assert(!"helper call was created without gtInitClsHnd or already visited");
        return false;
    }

    int                  isInitOffset = 0;
    CORINFO_CONST_LOOKUP flagAddr     = {};
    if (!info.compCompHnd->getIsClassInitedFlagAddress(call->gtInitClsHnd, &flagAddr, &isInitOffset))
    {
        JITDUMP("getIsClassInitedFlagAddress returned false - bail out.\n")
        return false;
    }

    CORINFO_CONST_LOOKUP staticBaseAddr = {};
    if ((retValKind == SHRV_STATIC_BASE_PTR) &&
        !info.compCompHnd->getStaticBaseAddress(call->gtInitClsHnd, isGc, &staticBaseAddr))
    {
        JITDUMP("getStaticBaseAddress returned false - bail out.\n")
        return false;
    }

    JITDUMP("Expanding static initialization for '%s', call: [%06d] in " FMT_BB "\n",
            eeGetClassName(call->gtInitClsHnd), dspTreeID(call), block->bbNum);

    DebugInfo debugInfo = stmt->GetDebugInfo();

    // Split block right before the call tree
    BasicBlock* prevBb       = block;
    GenTree**   callUse      = nullptr;
    Statement*  newFirstStmt = nullptr;
    block                    = fgSplitBlockBeforeTree(block, stmt, call, &newFirstStmt, &callUse);
    *pBlock                  = block;
    assert(prevBb != nullptr && block != nullptr);

    // Block ops inserted by the split need to be morphed here since we are after morph.
    // We cannot morph stmt yet as we may modify it further below, and the morphing
    // could invalidate callUse.
    while ((newFirstStmt != nullptr) && (newFirstStmt != stmt))
    {
        fgMorphStmtBlockOps(block, newFirstStmt);
        newFirstStmt = newFirstStmt->GetNextStmt();
    }

    //
    // Create new blocks. Essentially, we want to transform this:
    //
    //   staticBase = helperCall();
    //
    // into:
    //
    //   if (!isInitialized)
    //   {
    //       helperCall(); // we don't use its return value
    //   }
    //   staticBase = fastPath;
    //

    // The initialization check looks like this for JIT:
    //
    // *  JTRUE     void
    // \--*  EQ        int
    //    +--*  AND       int
    //    |  +--*  IND       int
    //    |  |  \--*  CNS_INT(h) long   0x.... const ptr
    //    |  \--*  CNS_INT   int    1 (bit mask)
    //    \--*  CNS_INT   int    1
    //
    // For NativeAOT it's:
    //
    // *  JTRUE     void
    // \--*  EQ        int
    //    +--*  IND       nint
    //    |  \--*  ADD       long
    //    |     +--*  CNS_INT(h) long   0x.... const ptr
    //    |     \--*  CNS_INT   int    -8 (offset)
    //    \--*  CNS_INT   int    0
    //
    assert(flagAddr.accessType == IAT_VALUE);

    GenTree* cachedStaticBase = nullptr;
    GenTree* isInitedActualValueNode;
    GenTree* isInitedExpectedValue;
    if (IsTargetAbi(CORINFO_NATIVEAOT_ABI))
    {
        GenTree* baseAddr = gtNewIconHandleNode((size_t)flagAddr.addr, GTF_ICON_GLOBAL_PTR);

        // Save it to a temp - we'll be using its value for the replacementNode.
        // This leads to some size savings on NativeAOT
        if ((staticBaseAddr.addr == flagAddr.addr) && (staticBaseAddr.accessType == flagAddr.accessType))
        {
            cachedStaticBase = fgInsertCommaFormTemp(&baseAddr);
        }

        // Don't fold ADD(CNS1, CNS2) here since the result won't be reloc-friendly for AOT
        GenTree* offsetNode     = gtNewOperNode(GT_ADD, TYP_I_IMPL, baseAddr, gtNewIconNode(isInitOffset));
        isInitedActualValueNode = gtNewIndir(TYP_I_IMPL, offsetNode, GTF_IND_NONFAULTING);

        // 0 means "initialized" on NativeAOT
        isInitedExpectedValue = gtNewIconNode(0, TYP_I_IMPL);
    }
    else
    {
        assert(isInitOffset == 0);

        isInitedActualValueNode = gtNewIndOfIconHandleNode(TYP_INT, (size_t)flagAddr.addr, GTF_ICON_GLOBAL_PTR, false);

        // Check ClassInitFlags::INITIALIZED_FLAG bit
        isInitedActualValueNode = gtNewOperNode(GT_AND, TYP_INT, isInitedActualValueNode, gtNewIconNode(1));
        isInitedExpectedValue   = gtNewIconNode(1);
    }

    GenTree* isInitedCmp = gtNewOperNode(GT_EQ, TYP_INT, isInitedActualValueNode, isInitedExpectedValue);
    isInitedCmp->gtFlags |= GTF_RELOP_JMP_USED;
    BasicBlock* isInitedBb =
        fgNewBBFromTreeAfter(BBJ_COND, prevBb, gtNewOperNode(GT_JTRUE, TYP_VOID, isInitedCmp), debugInfo);

    // Fallback basic block
    // TODO-CQ: for JIT we can replace the original call with CORINFO_HELP_INITCLASS
    // that only accepts a single argument
    BasicBlock* helperCallBb = fgNewBBFromTreeAfter(BBJ_NONE, isInitedBb, call, debugInfo, true);

    GenTree* replacementNode = nullptr;
    if (retValKind == SHRV_STATIC_BASE_PTR)
    {
        // Replace the call with a constant pointer to the statics base
        assert(staticBaseAddr.addr != nullptr);

        // Use local if the addressed is already materialized and cached
        if (cachedStaticBase != nullptr)
        {
            assert(staticBaseAddr.accessType == IAT_VALUE);
            replacementNode = cachedStaticBase;
        }
        else if (staticBaseAddr.accessType == IAT_VALUE)
        {
            replacementNode = gtNewIconHandleNode((size_t)staticBaseAddr.addr, GTF_ICON_STATIC_HDL);
        }
        else
        {
            assert(staticBaseAddr.accessType == IAT_PVALUE);
            replacementNode =
                gtNewIndOfIconHandleNode(TYP_I_IMPL, (size_t)staticBaseAddr.addr, GTF_ICON_GLOBAL_PTR, false);
        }
    }

    if (replacementNode == nullptr)
    {
        (*callUse)->gtBashToNOP();
    }
    else
    {
        *callUse = replacementNode;
    }

    fgMorphStmtBlockOps(block, stmt);
    gtUpdateStmtSideEffects(stmt);

    // Final block layout looks like this:
    //
    // prevBb(BBJ_NONE):                    [weight: 1.0]
    //     ...
    //
    // isInitedBb(BBJ_COND):                [weight: 1.0]
    //     if (isInited)
    //         goto block;
    //
    // helperCallBb(BBJ_NONE):              [weight: 0.0]
    //     helperCall();
    //
    // block(...):                          [weight: 1.0]
    //     use(staticBase);
    //
    // Whether we use helperCall's value or not depends on the helper itself.

    //
    // Update preds in all new blocks
    //

    // Unlink block and prevBb
    fgRemoveRefPred(block, prevBb);

    // Block has two preds now: either isInitedBb or helperCallBb
    fgAddRefPred(block, isInitedBb);
    fgAddRefPred(block, helperCallBb);

    // prevBb always flow into isInitedBb
    fgAddRefPred(isInitedBb, prevBb);

    // Both fastPathBb and helperCallBb have a single common pred - isInitedBb
    fgAddRefPred(helperCallBb, isInitedBb);

    // helperCallBb unconditionally jumps to the last block (jumps over fastPathBb)
    isInitedBb->bbJumpDest = block;

    //
    // Re-distribute weights
    //

    block->inheritWeight(prevBb);
    isInitedBb->inheritWeight(prevBb);
    helperCallBb->bbSetRunRarely();

    //
    // Update loop info if loop table is known to be valid
    //

    isInitedBb->bbNatLoopNum   = prevBb->bbNatLoopNum;
    helperCallBb->bbNatLoopNum = prevBb->bbNatLoopNum;

    // All blocks are expected to be in the same EH region
    assert(BasicBlock::sameEHRegion(prevBb, block));
    assert(BasicBlock::sameEHRegion(prevBb, isInitedBb));

    // Extra step: merge prevBb with isInitedBb if possible
    if (fgCanCompactBlocks(prevBb, isInitedBb))
    {
        fgCompactBlocks(prevBb, isInitedBb);
    }

    // Clear gtInitClsHnd as a mark that we've already visited this call
    call->gtInitClsHnd = NO_CLASS_HANDLE;
    return true;
}
