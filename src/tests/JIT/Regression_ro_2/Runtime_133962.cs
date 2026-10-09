// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133962
{
    private static readonly Half[] s_values =
    [
        (Half)0.0f, (Half)(-0.0f), (Half)1.0f, (Half)3.0f, (Half)0.1f, (Half)(-7.5f), (Half)1000.0f,
        (Half)40000.0f, Half.MaxValue, Half.MinValue, Half.Epsilon, Half.PositiveInfinity, Half.NegativeInfinity,
        Half.NaN, BitConverter.UInt16BitsToHalf(0x0200), BitConverter.UInt16BitsToHalf(0x0401),
    ];

    [Fact]
    public static void TestEntryPoint()
    {
        Func<Half, Half> reciprocal = Half.ReciprocalEstimate;
        Func<Half, Half> reciprocalSqrt = Half.ReciprocalSqrtEstimate;

        // Estimates may vary across hardware, but must be consistent within a process.
        foreach (Half value in s_values)
        {
            Assert.Equal(BitConverter.HalfToUInt16Bits(reciprocal(value)), BitConverter.HalfToUInt16Bits(Reciprocal(value)));
            Assert.Equal(BitConverter.HalfToUInt16Bits(reciprocalSqrt(value)), BitConverter.HalfToUInt16Bits(ReciprocalSqrt(value)));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Half Reciprocal(Half x) => Half.ReciprocalEstimate(x);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Half ReciprocalSqrt(Half x) => Half.ReciprocalSqrtEstimate(x);
}
