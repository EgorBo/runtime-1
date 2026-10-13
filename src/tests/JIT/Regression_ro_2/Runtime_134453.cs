// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134453
{
    [Fact]
    public static void TestEntryPoint()
    {
        Assert.True(Test(new int[10], -5, -1, false));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool Test(Span<int> s, int i, int n, bool c)
    {
        if (c)
            return s.Slice(0, n)[0] == 42;
        if ((uint)i < (uint)n)
            return i < 0;
        return false;
    }
}
