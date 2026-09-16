// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

// The relop simplification in optRedundantDominatingBranch used to strengthen the compare
// in Test's shared block even though that block is also entered from the loop back edge,
// where the dominating predicate does not hold.
public class Runtime_133980
{
    private static int s_counter;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SideEffect() => s_counter++;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int x, int y)
    {
        if (x != y)
        {
            goto B;
        }
    X:
        SideEffect();
        if (s_counter > 1000)
        {
            return -1;
        }
    B:
        if (x <= y)
        {
            return 1;
        }
        goto X;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        s_counter = 0;
        Assert.Equal(1, Test(5, 5));
        s_counter = 0;
        Assert.Equal(1, Test(3, 7));
        s_counter = 0;
        Assert.Equal(-1, Test(9, 2));
    }
}
