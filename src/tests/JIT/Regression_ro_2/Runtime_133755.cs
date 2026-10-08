// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133755
{
    private static int s_sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(int value) => s_sink += value;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Test(int n, int seed)
    {
        int i = seed;
        for (int c = 0; c < n; c++)
        {
            int t = i * 2;
            Consume(checked(t * 3));
            Consume(checked(t * 3));
            i++;
        }
    }

    [Fact]
    public static void TestEntryPoint()
    {
        s_sink = 0;
        Test(4, 1);
        Assert.Equal(120, s_sink);
        Assert.Throws<OverflowException>(() => Test(4, 400000000));
        Assert.Equal(120, s_sink);
    }
}
