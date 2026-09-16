// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Dominator-based jump threading in redundantbranchopts used to bypass the second
// compare because its normal VN matched the dominating compare's, ignoring the fact
// that its exception set was a strict superset. That dropped the DivideByZeroException.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133982
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int a, int b, int c)
    {
        int r;
        if (a == b) { r = 1; } else { r = 2; }
        if (a == b + (a / c - a / c)) { r += 10; } else { r += 20; }
        return r;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Throws<DivideByZeroException>(() => Test(5, 5, 0));
        Assert.Equal(11, Test(5, 5, 1));
    }
}
