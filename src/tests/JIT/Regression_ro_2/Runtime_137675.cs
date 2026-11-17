// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_137675
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int[][] rows, long k, int n)
    {
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            sum += rows[(int)k][i];
        }
        return sum;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        int[] row = new int[10];
        row.AsSpan().Fill(1);
        Assert.Equal(10, Test(new int[][] { row }, 1L << 32, 10));
    }
}
