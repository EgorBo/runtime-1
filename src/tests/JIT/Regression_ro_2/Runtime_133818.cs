// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133818
{
    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Throws<IndexOutOfRangeException>(() => Test(new int[100], 8));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int[] arr, int n)
    {
        int sum = 0, x = -5, y = 0;
        for (int c = 0; c < n; c++)
        {
            if (x >= 50 || y >= 50)
                break;
            int idx = x + 100;
            if (idx >= arr.Length)
                break;
            sum += arr[idx];
            int t = x + y;
            y = x;
            x = t;
        }
        return sum;
    }
}
