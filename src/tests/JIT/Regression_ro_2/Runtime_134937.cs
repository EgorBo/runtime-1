// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134937
{
    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Throws<NullReferenceException>(() => Test(null, 1, 0, 2, 10));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(int value)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int[] array, int a, int b, int c, int n)
    {
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            sum += array.Length;
            sum += a / b + c;
            Consume(sum + i);
        }

        return sum;
    }
}
