// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Xunit;

public class Runtime_134895
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<int> Test(Vector128<int>[] a, Vector128<int>[] b, int i) => -a[i] - -b[i];

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Throws<IndexOutOfRangeException>(() => Test(new Vector128<int>[1], null, 5));
        Assert.Equal(Vector128.Create(1), Test([Vector128.Create(2)], [Vector128.Create(3)], 0));
    }
}
