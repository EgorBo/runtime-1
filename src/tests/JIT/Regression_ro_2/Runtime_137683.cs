// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_137683
{
    private static int s_sum;
    private static int s_x;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MayThrow(int k)
    {
        if (k != 0) throw new Exception();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Yes(int x) => x == 0;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void TestFilter(int k)
    {
        int i = 100;
        try
        {
            try
            {
                MayThrow(k);
            }
            finally
            {
                i = 10;
            }
        }
        catch when (Yes(i = 0))
        {
            do
            {
                s_sum += i;
                i++;
            } while (i < 3);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void TestFinally(int k)
    {
        int i = 10;
        try
        {
            MayThrow(k);
            i = 0;
        }
        finally
        {
            do
            {
                s_sum += i;
                i++;
            } while (i < 3);

            // Prevents finally cloning.
            switch (k)
            {
                case 0: s_x += 1; break;
                case 1: s_x += 2; break;
                case 2: s_x += 3; break;
                case 3: s_x += 4; break;
                case 4: s_x += 5; break;
                case 5: s_x += 6; break;
            }
        }
    }

    [Fact]
    public static void TestEntryPoint()
    {
        s_sum = 0;
        TestFilter(1);
        Assert.Equal(10, s_sum);

        s_sum = 0;
        Assert.Throws<Exception>(() => TestFinally(1));
        Assert.Equal(10, s_sum);
    }
}
