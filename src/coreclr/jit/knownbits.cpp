// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#include "knownbits.h"

// Refine "pBits" using the assertions known about "num" (width is 32 or 64).
static void MergeKnownBitsAssertions(
    Compiler* comp, ValueNum num, ASSERT_VALARG_TP assertions, unsigned width, KnownBits* pBits)
{
    if (BitVecOps::MayBeUninit(assertions) || BitVecOps::IsEmpty(comp->apTraits, assertions) ||
        !comp->optAssertionHasAssertionsForVN(num))
    {
        return;
    }

    const uint64_t signBit = 1ull << (width - 1);

    BitVecOps::Iter iter(comp->apTraits, assertions);
    unsigned        index = 0;
    while (iter.NextElem(&index))
    {
        const Compiler::AssertionDsc& curAssertion = comp->optGetAssertion(GetAssertionIndex(index));

        // "num == const": fully determines the bits.
        if (curAssertion.KindIs(Compiler::OAK_EQUAL) && (curAssertion.GetOp1().GetVN() == num))
        {
            int64_t eqCns;
            if (comp->vnStore->IsVNIntegralConstant<int64_t>(curAssertion.GetOp2().GetVN(), &eqCns))
            {
                *pBits = KnownBits::Intersect(*pBits, KnownBits::FromConstant((uint64_t)eqCns, width));
            }
            continue;
        }

        // Relops of the form "num <relop> const".
        if (curAssertion.IsRelop() && (curAssertion.GetOp1().GetVN() == num) &&
            curAssertion.GetOp2().KindIs(Compiler::O2K_CONST_INT))
        {
            const int64_t relCns = curAssertion.GetOp2().GetIntConstant();

            if (curAssertion.KindIs(Compiler::OAK_LT_UN) && (relCns > 0))
            {
                // (uint)num < C  =>  num u<= C-1  =>  upper bits are 0.
                *pBits = KnownBits::Intersect(*pBits, KnownBits::FromUnsignedUpperBound((uint64_t)(relCns - 1), width));
            }
            else if (curAssertion.KindIs(Compiler::OAK_LE_UN) && (relCns >= 0))
            {
                // (uint)num <= C  =>  upper bits are 0.
                *pBits = KnownBits::Intersect(*pBits, KnownBits::FromUnsignedUpperBound((uint64_t)relCns, width));
            }
            else if (curAssertion.KindIs(Compiler::OAK_GE) && (relCns >= 0))
            {
                // num >= 0 (signed)  =>  sign bit is 0.
                *pBits = KnownBits::Intersect(*pBits, KnownBits(signBit, 0));
            }
            else if (curAssertion.KindIs(Compiler::OAK_GT) && (relCns >= -1))
            {
                // num > -1 (signed)  =>  num >= 0  =>  sign bit is 0.
                *pBits = KnownBits::Intersect(*pBits, KnownBits(signBit, 0));
            }
            continue;
        }

        // "(uint)num </<= (never-negative bound)" => num is non-negative => sign bit is 0.
        if (curAssertion.IsRelop() && (curAssertion.GetOp1().GetVN() == num) &&
            curAssertion.GetOp2().KindIs(Compiler::O2K_VN_ADD_CNS) && curAssertion.GetOp2().IsVNNeverNegative() &&
            curAssertion.KindIs(Compiler::OAK_LT_UN, Compiler::OAK_LE_UN))
        {
            *pBits = KnownBits::Intersect(*pBits, KnownBits(signBit, 0));
            continue;
        }
    }
}

// Worker for KnownBits::Compute. "visited" guards against infinite recursion on phi defs.
static KnownBits ComputeWorker(
    Compiler* comp, ValueNum num, ASSERT_VALARG_TP assertions, int budget, ValueNumStore::SmallValueNumSet* visited)
{
    KnownBits result; // fully unknown

    if ((num == ValueNumStore::NoVN) || (budget <= 0))
    {
        return result;
    }

    var_types vnType = comp->vnStore->TypeOfVN(num);
    if (!varTypeIsIntegral(vnType) || varTypeIsGC(vnType))
    {
        // We only reason about (non-GC) integral values.
        return result;
    }

    const unsigned width = (genActualType(vnType) == TYP_LONG) ? 64 : 32;

    // Constants are fully known.
    int64_t cnsVal;
    if (comp->vnStore->IsVNIntegralConstant<int64_t>(num, &cnsVal))
    {
        return KnownBits::FromConstant((uint64_t)cnsVal, width);
    }

    VNFuncApp funcApp;
    if (comp->vnStore->GetVNFunc(num, &funcApp))
    {
        switch (funcApp.GetFunc())
        {
            case VNF_AND:
            case VNF_OR:
            case VNF_UDIV:
            {
                KnownBits a = ComputeWorker(comp, funcApp.GetArg(0), assertions, --budget, visited);
                KnownBits b = ComputeWorker(comp, funcApp.GetArg(1), assertions, --budget, visited);
                if (funcApp.FuncIs(VNF_AND))
                {
                    result = KnownBitsOps::And(a, b);
                }
                else if (funcApp.FuncIs(VNF_OR))
                {
                    result = KnownBitsOps::Or(a, b);
                }
                else
                {
                    assert(funcApp.FuncIs(VNF_UDIV));
                    result = KnownBitsOps::UDiv(a, b, width);
                }
                break;
            }

            case VNF_Cast:
            case VNF_CastOvf:
            {
                var_types castToType;
                bool      srcIsUnsigned;
                comp->vnStore->GetCastOperFromVN(funcApp.GetArg(1), &castToType, &srcIsUnsigned);

                const ValueNum  srcVN   = funcApp.GetArg(0);
                const var_types srcType = comp->vnStore->TypeOfVN(srcVN);
                if (varTypeIsIntegral(srcType) && !varTypeIsGC(srcType) && varTypeIsIntegral(castToType))
                {
                    const unsigned srcWidth = (genActualType(srcType) == TYP_LONG) ? 64 : 32;
                    KnownBits      a        = ComputeWorker(comp, srcVN, assertions, --budget, visited);
                    result                  = KnownBitsOps::Cast(a, srcWidth, castToType, srcIsUnsigned);
                }
                break;
            }

            case VNF_EQ:
            case VNF_NE:
            case VNF_LT:
            case VNF_LE:
            case VNF_GT:
            case VNF_GE:
            case VNF_LT_UN:
            case VNF_LE_UN:
            case VNF_GT_UN:
            case VNF_GE_UN:
                // Relops always produce 0 or 1.
                result = KnownBits::FromUnsignedUpperBound(1, width);
                break;

            case VNF_MDARR_LENGTH:
            case VNF_ARR_LENGTH:
                // Array length is in [0, CORINFO_Array_MaxLength], so its upper bits are 0.
                result = KnownBits::FromUnsignedUpperBound(CORINFO_Array_MaxLength, width);
                break;

            default:
                break;
        }
    }

    result = result.Truncate(width);

    // If it's a phi, merge the known bits of all the reaching values: a bit is known in the phi
    // result only if it is known and equal along every reaching edge.
    if (!result.IsConstant(width) && comp->vnStore->IsPhiDef(num) && visited->Add(comp, num))
    {
        KnownBits phiBits;
        bool      first = true;
        auto visitor = [comp, &phiBits, &first, &budget, visited](ValueNum reachingVN, ASSERT_TP reachingAssertions) {
            KnownBits edge = ComputeWorker(comp, reachingVN, reachingAssertions, --budget, visited);
            phiBits        = first ? edge : KnownBits::Union(phiBits, edge);
            first          = false;

            // Once nothing is known, merging more edges cannot recover any information.
            return phiBits.IsUnknown() ? Compiler::AssertVisit::Abort : Compiler::AssertVisit::Continue;
        };

        if ((comp->optVisitReachingAssertions(num, visitor) == Compiler::AssertVisit::Continue) && !first)
        {
            result = KnownBits::Intersect(result, phiBits);
        }
    }

    // Refine using assertions about this VN.
    MergeKnownBitsAssertions(comp, num, assertions, width, &result);

    return result.Truncate(width);
}

//------------------------------------------------------------------------
// KnownBits::Compute: bit-level analog of RangeCheck::GetRangeFromAssertions. Returns which
//    bits of "num" are known 0/1, from its value-number structure and the incoming assertions
//    (32- and 64-bit integral values). "budget" bounds the recursive search.
//
KnownBits KnownBits::Compute(Compiler* comp, ValueNum num, ASSERT_VALARG_TP assertions, int budget)
{
    ValueNumStore::SmallValueNumSet visited;
    return ComputeWorker(comp, num, assertions, budget, &visited);
}
