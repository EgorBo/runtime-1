// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// KnownBits: a bit-level integer lattice (LLVM-style known zeros/ones) and its transfer
// functions, plus an analysis (KnownBits::Compute) that derives the known bits of a
// value number from its value-number structure and the incoming assertions. This is the
// bit-level analog of the range analysis in rangecheck.{h,cpp}.
//

#pragma once

#include "compiler.h"

//------------------------------------------------------------------------
// KnownBits: an LLVM-style "known bits" lattice for an integral value, tracked over a 64-bit
//    container. For bit i: set in knownZero => definitely 0; set in knownOne => definitely 1;
//    set in neither => unknown. Invariant: (knownZero & knownOne) == 0. For a value narrower
//    than 64 bits (e.g. TYP_INT has width 32), only the low "width" bits carry meaning; bits
//    >= width are kept unknown so we never assert anything about sign/zero extension.
//
struct KnownBits
{
    uint64_t knownZero;
    uint64_t knownOne;

    KnownBits()
        : knownZero(0)
        , knownOne(0)
    {
    }

    KnownBits(uint64_t knownZero, uint64_t knownOne)
        : knownZero(knownZero)
        , knownOne(knownOne)
    {
        assert((knownZero & knownOne) == 0);
    }

    // Mask of the low "width" bits (width must be 32 or 64).
    static uint64_t WidthMask(unsigned width)
    {
        assert((width == 32) || (width == 64));
        return (width == 64) ? UINT64_MAX : 0xFFFFFFFFull;
    }

    // Mask with the low "n" bits set (n in [0, 64]).
    static uint64_t LowMask(unsigned n)
    {
        assert(n <= 64);
        return (n == 0) ? 0 : (UINT64_MAX >> (64 - n));
    }

    bool IsUnknown() const
    {
        return (knownZero == 0) && (knownOne == 0);
    }

    // True if every bit within "width" is known.
    bool IsConstant(unsigned width) const
    {
        const uint64_t mask = WidthMask(width);
        return ((knownZero | knownOne) & mask) == mask;
    }

    // The constant value (valid only when IsConstant(width) is true).
    uint64_t GetConstant(unsigned width) const
    {
        assert(IsConstant(width));
        return knownOne & WidthMask(width);
    }

    // Is bit "pos" known to be 0?
    bool IsBitZero(unsigned pos) const
    {
        return (knownZero & (1ull << pos)) != 0;
    }

    // Drop any knowledge about bits at or above "width".
    KnownBits Truncate(unsigned width) const
    {
        const uint64_t mask = WidthMask(width);
        return KnownBits(knownZero & mask, knownOne & mask);
    }

    // Fully-known KnownBits for the constant "value" interpreted in "width" bits.
    static KnownBits FromConstant(uint64_t value, unsigned width)
    {
        const uint64_t mask = WidthMask(width);
        value &= mask;
        return KnownBits(~value & mask, value);
    }

    // KnownBits derived purely from an unsigned upper bound: if the value is known to be
    // unsigned and <= maxVal, then all bits above the most-significant set bit of maxVal
    // are known to be 0.
    static KnownBits FromUnsignedUpperBound(uint64_t maxVal, unsigned width)
    {
        const uint64_t mask = WidthMask(width);
        if (maxVal >= mask)
        {
            return KnownBits();
        }

        // "bitLen" = number of bits needed to represent maxVal (0 when maxVal == 0); every bit
        // at or above it is known 0. LeadingZeroCount(0) == 64, so this also handles maxVal == 0.
        const unsigned bitLen = 64 - (unsigned)BitOperations::LeadingZeroCount(maxVal);
        return KnownBits(~LowMask(bitLen) & mask, 0);
    }

    // Combine two facts about the *same* value (assertion refinement): a bit is known if it
    // is known in either input. Conflicting bits (one says 0, the other says 1) indicate a
    // dead code path; we conservatively drop them to "unknown" so we never assert a false fact.
    static KnownBits Intersect(const KnownBits& a, const KnownBits& b)
    {
        const uint64_t z        = a.knownZero | b.knownZero;
        const uint64_t o        = a.knownOne | b.knownOne;
        const uint64_t conflict = z & o;
        return KnownBits(z & ~conflict, o & ~conflict);
    }

    // Merge facts across two possible values (e.g. phi inputs): a bit is known in the result
    // only if it is known and equal in both inputs.
    static KnownBits Union(const KnownBits& a, const KnownBits& b)
    {
        return KnownBits(a.knownZero & b.knownZero, a.knownOne & b.knownOne);
    }

    // Sign-extend the low "width" bits of "value" to a 64-bit signed integer.
    static int64_t SignExtend(uint64_t value, unsigned width)
    {
        if (width == 64)
        {
            return (int64_t)value;
        }
        const uint64_t mask    = WidthMask(width);
        const uint64_t signBit = 1ull << (width - 1);
        value &= mask;
        if ((value & signBit) != 0)
        {
            value |= ~mask;
        }
        return (int64_t)value;
    }

    // Try to express this KnownBits as a signed [lo, hi] range of width "width". Succeeds only
    // when the sign bit is known (otherwise the range would straddle 0 and need two intervals).
    bool TryGetSignedRange(unsigned width, int64_t* lo, int64_t* hi) const
    {
        const uint64_t mask    = WidthMask(width);
        const uint64_t signBit = 1ull << (width - 1);

        const uint64_t minBits = knownOne & mask;   // unknown bits taken as 0
        const uint64_t maxBits = ~knownZero & mask; // unknown bits taken as 1 (== MaybeOne)

        if ((knownZero & signBit) != 0)
        {
            // Sign bit known 0 => value is non-negative.
            *lo = (int64_t)minBits;
            *hi = (int64_t)maxBits;
            return true;
        }
        if ((knownOne & signBit) != 0)
        {
            // Sign bit known 1 => value is negative; sign-extend both bounds.
            *lo = SignExtend(minBits, width);
            *hi = SignExtend(maxBits, width);
            return true;
        }
        return false;
    }

    // Unsigned minimum/maximum value possible given these known bits (within "width").
    uint64_t GetUMin(unsigned width) const
    {
        return knownOne & WidthMask(width);
    }
    uint64_t GetUMax(unsigned width) const
    {
        return ~knownZero & WidthMask(width);
    }

    // Signed minimum/maximum value possible given these known bits (sign-extended to 64 bits).
    int64_t GetSMin(unsigned width) const
    {
        const uint64_t signBit = 1ull << (width - 1);
        uint64_t       v       = knownOne & WidthMask(width);
        if ((knownZero & signBit) == 0)
        {
            // Sign bit could be 1 => most-negative candidate sets it.
            v |= signBit;
        }
        return SignExtend(v, width);
    }
    int64_t GetSMax(unsigned width) const
    {
        const uint64_t signBit = 1ull << (width - 1);
        uint64_t       v       = ~knownZero & WidthMask(width);
        if ((knownOne & signBit) == 0)
        {
            // Sign bit could be 0 => most-positive candidate clears it.
            v &= ~signBit;
        }
        return SignExtend(v, width);
    }

    // Number of known-zero low bits, i.e. trailing bits known to be 0 (LLVM countMinTrailingZeros).
    unsigned CountMinTrailingZeros() const
    {
        return (unsigned)BitOperations::TrailingZeroCount(~knownZero);
    }
    // Number of known low bits (each known 0 or 1) starting from bit 0.
    unsigned CountMinTrailingKnown() const
    {
        return (unsigned)BitOperations::TrailingZeroCount(~(knownZero | knownOne));
    }
    // Number of known-zero high bits within "width" (LLVM countMinLeadingZeros).
    unsigned CountMinLeadingZeros(unsigned width) const
    {
        const uint64_t top = (knownZero & WidthMask(width)) << (64 - width);
        return (unsigned)BitOperations::LeadingZeroCount(~top);
    }

    // Bit-level analog of RangeCheck::GetRangeFromAssertions: computes which bits of "num" are
    // known to be 0 or 1, based purely on its value-number structure and the incoming assertions.
    // Supports both 32- and 64-bit integral values.
    static KnownBits Compute(Compiler* comp, ValueNum num, ASSERT_VALARG_TP assertions, int budget = 10);
};

//------------------------------------------------------------------------
// KnownBitsOps: transfer functions that compute the KnownBits of the result of an
//    operation from the KnownBits of its operands. Every function maintains the
//    "bits at or above width are unknown (0/0)" invariant of KnownBits.
//
struct KnownBitsOps
{
    static KnownBits And(const KnownBits& a, const KnownBits& b)
    {
        // Result bit is 0 if either operand bit is 0; 1 only if both are 1.
        return KnownBits(a.knownZero | b.knownZero, a.knownOne & b.knownOne);
    }

    static KnownBits Or(const KnownBits& a, const KnownBits& b)
    {
        // Result bit is 1 if either operand bit is 1; 0 only if both are 0.
        return KnownBits(a.knownZero & b.knownZero, a.knownOne | b.knownOne);
    }

    // Known bits of unsigned division a / b. Port of LLVM's KnownBits::udiv (leading-zeros part).
    static KnownBits UDiv(const KnownBits& a, const KnownBits& b, unsigned width)
    {
        const uint64_t mask     = KnownBits::WidthMask(width);
        const uint64_t maxNum   = ~a.knownZero & mask; // dividend's unsigned max
        const uint64_t minDenom = b.knownOne & mask;   // divisor's unsigned min
        if (maxNum == 0)
        {
            return KnownBits::FromConstant(0, width); // 0 / x == 0
        }

        // Largest possible result = maxNumerator / minDenominator.
        const uint64_t maxRes = (minDenom == 0) ? maxNum : (maxNum / minDenom);
        const unsigned bitLen = (maxRes == 0) ? 0 : (64 - (unsigned)BitOperations::LeadingZeroCount(maxRes));
        if (bitLen >= width)
        {
            return KnownBits();
        }
        const unsigned leadZ = width - bitLen;
        return KnownBits(mask & ~KnownBits::LowMask(width - leadZ), 0);
    }

    static unsigned UMin(unsigned a, unsigned b)
    {
        return (a < b) ? a : b;
    }

    // Known bits of a * b. Port of LLVM's KnownBits::mul (leading-zeros + low-bits parts).
    static KnownBits Mul(const KnownBits& a, const KnownBits& b, unsigned width)
    {
        const uint64_t mask = KnownBits::WidthMask(width);

        // High known-0 bits: multiply the unsigned max of each side; valid only if it does not
        // overflow "width" bits.
        const uint64_t aMax  = a.GetUMax(width);
        const uint64_t bMax  = b.GetUMax(width);
        unsigned       leadZ = 0;
        bool           overflow;
        uint64_t       umaxResult;
        if (width == 32)
        {
            umaxResult = aMax * bMax; // both < 2^32, so this fits in 64 bits
            overflow   = (umaxResult >> 32) != 0;
        }
        else
        {
            overflow   = (aMax != 0) && (bMax > (UINT64_MAX / aMax));
            umaxResult = aMax * bMax;
        }
        if (!overflow)
        {
            const unsigned bitLen =
                (umaxResult == 0) ? 0 : (64 - (unsigned)BitOperations::LeadingZeroCount(umaxResult));
            leadZ = width - bitLen;
        }

        // Low known bits: the bottom ResultBitsKnown bits of the product are determined by the
        // bottom known bits of each operand (see LLVM KnownBits::mul for the derivation).
        const unsigned trailBitsKnownA = a.CountMinTrailingKnown();
        const unsigned trailBitsKnownB = b.CountMinTrailingKnown();
        const unsigned trailZeroA      = a.CountMinTrailingZeros();
        const unsigned trailZeroB      = b.CountMinTrailingZeros();
        const unsigned trailZ          = UMin(trailZeroA + trailZeroB, width);
        const unsigned smallest        = UMin(trailBitsKnownA - trailZeroA, trailBitsKnownB - trailZeroB);
        const unsigned resultBitsKnown = UMin(smallest + trailZ, width);

        const uint64_t bottomKnown = (a.knownOne & mask) * (b.knownOne & mask);
        const uint64_t loMask      = KnownBits::LowMask(resultBitsKnown);

        uint64_t z = ~bottomKnown & loMask;
        if (leadZ > 0)
        {
            z |= ~KnownBits::LowMask(width - leadZ) & mask;
        }
        const uint64_t o = bottomKnown & loMask;
        return KnownBits(z, o).Truncate(width);
    }

    // Known bits of "a << shiftAmt" for a constant shift amount.
    static KnownBits ShlConst(const KnownBits& a, unsigned shiftAmt, unsigned width)
    {
        if (shiftAmt >= width)
        {
            return KnownBits::FromConstant(0, width);
        }
        const uint64_t z = (a.knownZero << shiftAmt) | KnownBits::LowMask(shiftAmt); // shifted-in low bits are 0
        const uint64_t o = (a.knownOne << shiftAmt);
        return KnownBits(z, o).Truncate(width);
    }

    // Known bits of "(unsigned)a >> shiftAmt" (logical right shift) for a constant shift amount.
    static KnownBits LshrConst(const KnownBits& a, unsigned shiftAmt, unsigned width)
    {
        if (shiftAmt >= width)
        {
            return KnownBits::FromConstant(0, width);
        }
        const uint64_t mask     = KnownBits::WidthMask(width);
        const uint64_t highZero = ~KnownBits::LowMask(width - shiftAmt) & mask; // shifted-in high bits are 0
        const uint64_t z        = ((a.knownZero & mask) >> shiftAmt) | highZero;
        const uint64_t o        = ((a.knownOne & mask) >> shiftAmt);
        return KnownBits(z, o).Truncate(width);
    }

    // Known bits of "a >> shiftAmt" (arithmetic right shift) for a constant shift amount.
    static KnownBits AshrConst(const KnownBits& a, unsigned shiftAmt, unsigned width)
    {
        if (shiftAmt == 0)
        {
            return a;
        }
        const uint64_t mask    = KnownBits::WidthMask(width);
        const uint64_t signBit = 1ull << (width - 1);
        const bool     signZ   = (a.knownZero & signBit) != 0;
        const bool     signO   = (a.knownOne & signBit) != 0;

        if (shiftAmt >= width)
        {
            // Result is all copies of the sign bit.
            if (signZ)
            {
                return KnownBits::FromConstant(0, width);
            }
            if (signO)
            {
                return KnownBits::FromConstant(mask, width);
            }
            return KnownBits();
        }

        const uint64_t highMask = ~KnownBits::LowMask(width - shiftAmt) & mask; // bits [width-shiftAmt, width-1]
        uint64_t       z        = (a.knownZero & mask) >> shiftAmt;
        uint64_t       o        = (a.knownOne & mask) >> shiftAmt;
        if (signZ)
        {
            z |= highMask;
        }
        else if (signO)
        {
            o |= highMask;
        }
        return KnownBits(z, o).Truncate(width);
    }

    // Known bits of "a % b" (unsigned). Port of LLVM's KnownBits::urem (+ remGetLowBits).
    static KnownBits URem(const KnownBits& a, const KnownBits& b, unsigned width)
    {
        const uint64_t mask = KnownBits::WidthMask(width);
        KnownBits      result;

        // If the divisor is a known multiple of 2^k (its low k bits are known 0 and it is not the
        // zero constant), the remainder preserves the dividend's low k bits.
        const bool bIsZeroConst = b.IsConstant(width) && (b.GetConstant(width) == 0);
        if (!bIsZeroConst && b.IsBitZero(0))
        {
            const uint64_t lowMask = KnownBits::LowMask(b.CountMinTrailingZeros());
            result                 = KnownBits(a.knownZero & lowMask, a.knownOne & lowMask);
        }

        if (b.IsConstant(width))
        {
            const uint64_t c = b.GetConstant(width);
            if ((c != 0) && ((c & (c - 1)) == 0))
            {
                // x % 2^n: all bits at or above n are 0 (low bits already set above).
                result.knownZero |= ~(c - 1) & mask;
                return result.Truncate(width);
            }
        }

        // The remainder is <= either operand, so any leading zeros common to either operand are
        // leading zeros of the result.
        const unsigned lzA   = a.CountMinLeadingZeros(width);
        const unsigned lzB   = b.CountMinLeadingZeros(width);
        const unsigned leadZ = (lzA > lzB) ? lzA : lzB;
        if (leadZ > 0)
        {
            result.knownZero |= ~KnownBits::LowMask(width - leadZ) & mask;
        }
        return result.Truncate(width);
    }

    // KnownBits of a cast from a "srcWidth"-bit source to "castToType".
    static KnownBits Cast(const KnownBits& srcKB, unsigned srcWidth, var_types castToType, bool srcIsUnsigned)
    {
        const unsigned vb          = genTypeSize(castToType) * BITS_PER_BYTE; // value bits of the dest type
        const unsigned dstWidth    = (vb <= 32) ? 32 : 64;                    // result is normalized to int or long
        const bool     dstUnsigned = varTypeIsUnsigned(castToType);

        KnownBits result;
        if (vb <= srcWidth)
        {
            // Narrowing / same-size normalization: the low "vb" bits pass through unchanged.
            const uint64_t lowMask = KnownBits::LowMask(vb);
            result.knownOne        = srcKB.knownOne & lowMask;
            result.knownZero       = srcKB.knownZero & lowMask;

            if (vb < dstWidth)
            {
                const uint64_t extMask = KnownBits::LowMask(dstWidth) & ~lowMask; // bits [vb, dstWidth-1]
                const uint64_t signBit = 1ull << (vb - 1);
                if (dstUnsigned)
                {
                    result.knownZero |= extMask; // zero-extend
                }
                else if ((srcKB.knownOne & signBit) != 0)
                {
                    result.knownOne |= extMask; // sign known 1
                }
                else if ((srcKB.knownZero & signBit) != 0)
                {
                    result.knownZero |= extMask; // sign known 0
                }
            }
        }
        else
        {
            // Widening: srcWidth < vb (== dstWidth). The low "srcWidth" bits pass through; the
            // remaining bits are determined by zero-extension (unsigned source) or the source's sign.
            const uint64_t lowMask = KnownBits::LowMask(srcWidth);
            result.knownOne        = srcKB.knownOne & lowMask;
            result.knownZero       = srcKB.knownZero & lowMask;

            const uint64_t extMask = KnownBits::LowMask(dstWidth) & ~lowMask; // bits [srcWidth, dstWidth-1]
            const uint64_t signBit = 1ull << (srcWidth - 1);
            if (srcIsUnsigned)
            {
                result.knownZero |= extMask;
            }
            else if ((srcKB.knownOne & signBit) != 0)
            {
                result.knownOne |= extMask;
            }
            else if ((srcKB.knownZero & signBit) != 0)
            {
                result.knownZero |= extMask;
            }
        }
        return result.Truncate(dstWidth);
    }

    // Evaluate "a <oper> b" (with the given signedness) purely from known bits. Returns 1 when the
    // comparison is always true, 0 when always false, and -1 when it cannot be determined. Mirrors
    // LLVM's KnownBits::eq/ne/ult/ule/ugt/uge/slt/sle/sgt/sge (min/max based).
    static int EvalRelop(genTreeOps oper, bool isUnsigned, const KnownBits& a, const KnownBits& b, unsigned width)
    {
        const uint64_t mask = KnownBits::WidthMask(width);

        if ((oper == GT_EQ) || (oper == GT_NE))
        {
            // They must differ if some bit is known 1 in one and known 0 in the other.
            const bool mustDiffer =
                ((a.knownOne & b.knownZero & mask) != 0) || ((a.knownZero & b.knownOne & mask) != 0);
            if (mustDiffer)
            {
                return (oper == GT_EQ) ? 0 : 1;
            }
            if (a.IsConstant(width) && b.IsConstant(width))
            {
                const bool eq = (a.GetConstant(width) == b.GetConstant(width));
                return (eq == (oper == GT_EQ)) ? 1 : 0;
            }
            return -1;
        }

        if (isUnsigned)
        {
            const uint64_t aMin = a.GetUMin(width);
            const uint64_t aMax = a.GetUMax(width);
            const uint64_t bMin = b.GetUMin(width);
            const uint64_t bMax = b.GetUMax(width);
            switch (oper)
            {
                case GT_LT:
                    return (aMax < bMin) ? 1 : ((aMin >= bMax) ? 0 : -1);
                case GT_LE:
                    return (aMax <= bMin) ? 1 : ((aMin > bMax) ? 0 : -1);
                case GT_GT:
                    return (aMin > bMax) ? 1 : ((aMax <= bMin) ? 0 : -1);
                case GT_GE:
                    return (aMin >= bMax) ? 1 : ((aMax < bMin) ? 0 : -1);
                default:
                    return -1;
            }
        }

        const int64_t aMin = a.GetSMin(width);
        const int64_t aMax = a.GetSMax(width);
        const int64_t bMin = b.GetSMin(width);
        const int64_t bMax = b.GetSMax(width);
        switch (oper)
        {
            case GT_LT:
                return (aMax < bMin) ? 1 : ((aMin >= bMax) ? 0 : -1);
            case GT_LE:
                return (aMax <= bMin) ? 1 : ((aMin > bMax) ? 0 : -1);
            case GT_GT:
                return (aMin > bMax) ? 1 : ((aMax <= bMin) ? 0 : -1);
            case GT_GE:
                return (aMin >= bMax) ? 1 : ((aMax < bMin) ? 0 : -1);
            default:
                return -1;
        }
    }
};
