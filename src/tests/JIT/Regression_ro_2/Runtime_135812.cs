// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_135812
{
    [Fact]
    public static void TestEntryPoint()
    {
        int[] a = new int[200];
        for (int i = 0; i < a.Length; i++)
            a[i] = i;

        Assert.Equal(2550, Test(a, 2, 5, true, 50));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int[] a, int k, int m, bool c, int n)
    {
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            int y;
            if (c) y = k; else y = m;
            if (c) sum += a[2 * i + y];
        }
        return sum;
    }
}
