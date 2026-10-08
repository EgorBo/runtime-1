// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133818
{
    private static int s_sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(int value) => s_sink = value;

    [Fact]
    public static void TestEntryPoint()
    {
        int[] array = new int[100];
        Array.Fill(array, 1);
        Assert.Equal(7, Test(array, 7));
        Assert.Throws<IndexOutOfRangeException>(() => Test(array, 8));
        int[] smallArray = new int[6];
        Array.Fill(smallArray, 1);
        Assert.Equal(2, TestOffset(smallArray, 2));
        Assert.Throws<IndexOutOfRangeException>(() => TestOffset(smallArray, 3));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int[] array, int n)
    {
        int sum = 0, x = -5, y = 0;
        for (int c = 0; c < n; c++)
        {
            if (x >= 50 || y >= 50)
                break;
            int index = x + 100;
            if (index >= array.Length)
                break;
            sum += array[index];
            int t = x + y;
            y = x;
            x = t;
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int TestOffset(int[] array, int n)
    {
        int sum = 0, x = -5, z = 0;
        for (int c = 0; c < n; c++)
        {
            if (x >= 50 || z >= 50)
                break;
            int index = z + 5;
            if (index >= array.Length)
                break;
            sum += array[index];
            int y = x + 1;
            z += y;
            Consume(y);
            x++;
        }
        return sum;
    }
}
