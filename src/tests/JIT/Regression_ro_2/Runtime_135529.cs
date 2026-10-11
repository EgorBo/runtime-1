// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135529;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Xunit;

public class Runtime_135529
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector64<int> GetLow(Vector128<int> v) => v.GetLower();

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<int> Id(Vector128<int> v) => v;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<int> Test(Vector128<int> v)
    {
        Vector64<int> lo = GetLow(v);
        Vector128<int> r = Id(lo.ToVector128());
        return r + Id(lo.ToVector128());
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(Vector128.Create(2, 4, 0, 0), Test(Vector128.Create(1, 2, 3, 4)));
    }
}
