// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// An earlier jump thread in the same RBO pass redirected an edge into the join block
// without adding a matching GT_PHI_ARG. The phi-use rewriting then only walked the phi
// args, concluded all remaining preds agreed, deleted the PHI for n, and rewrote its
// uses with the SSA def that only reaches via the other path. Test returned 101 (n == 1)
// instead of 200.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133981
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(bool q, bool r, int u, int v)
    {
        int z = 0, w = 0, m = 0, n = 0;
        if (r) { m = v; n = 1; goto T; }
        if (q) { z = u; w = 1; }
        if (z != w) return -1;
    T:
        if (m == n) return 200;
        return 100 + n;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(200, Test(false, false, 0, 0));
        Assert.Equal(200, Test(true, true, 1, 1));
        Assert.Equal(101, Test(false, true, 0, 7));
    }
}
