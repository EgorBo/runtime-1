// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_135810
{
    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Throws<IndexOutOfRangeException>(() => Test(false, true, 100));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(bool cond0, bool cond1, int y)
    {
        int[] arr = new int[1];
        if (cond0)
        {
            y = 0;
            goto Target;
        }

        int v = 0;
        if (!cond1)
        {
            v = y;
        }

        if (v == 0)
        {
            goto Target;
        }

        return v;

    Target:
        return arr[y];
    }
}
