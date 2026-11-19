// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Xunit;

namespace Runtime_137901;

public class Runtime_137901
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<long> Test(Vector256<double> d, Vector128<long> a, Vector128<long> b, Vector128<long> c)
    {
        Vector128<long> conv = Avx.ConvertToVector128Int32WithTruncation(d).AsInt64();
        return Vector128.ConditionalSelect(Vector128.GreaterThan(a, b), conv, c);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<long> Test128(Vector128<double> d, Vector128<long> a, Vector128<long> b, Vector128<long> c)
    {
        Vector128<long> conv = Sse2.ConvertToVector128Int32WithTruncation(d).AsInt64();
        return Vector128.ConditionalSelect(Vector128.GreaterThan(a, b), conv, c);
    }

    [ConditionalFact(typeof(Avx), nameof(Avx.IsSupported))]
    public static void TestEntryPoint()
    {
        Vector128<long> b = Vector128<long>.Zero;
        Vector128<long> c = Vector128<long>.AllBitsSet;

        Assert.Equal(Vector128.Create(1, 2, 3, 4), Test(Vector256.Create(1.0, 2.0, 3.0, 4.0), Vector128.Create(1L, 1L), b, c).AsInt32());
        Assert.Equal(Vector128.Create(1, 2, -1, -1), Test128(Vector128.Create(1.0, 2.0), Vector128.Create(1L, 0L), b, c).AsInt32());
    }
}
