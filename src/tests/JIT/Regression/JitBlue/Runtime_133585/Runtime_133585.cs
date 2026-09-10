// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

// Loop hoisting used to keep its exception-ordering barrier open for trees that are loop
// invariant but not hoistable, such as the bounds check below (both the array and the index
// are invariant). The invariant division was then hoisted into the preheader, ahead of the
// bounds check that has to fault first, so the loop raised DivideByZeroException instead of
// IndexOutOfRangeException.
//
// The same ordering was also broken when a hoisting candidate was accepted by the tree visitor
// but rejected later by optHoistCandidate (register pressure heuristics); see TestPressure.
public class Runtime_133585
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int[] arr, int idx, int a, int b, int n)
    {
        int r = 0;
        for (int i = 0; i < n; i++)
        {
            r += arr[idx];
            r += a / b;
        }
        return r;
    }

    // The invariant "arr.Length" is a hoisting candidate but is too cheap to survive the
    // register pressure heuristics, while the much more expensive "a / b" is hoisted.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int TestPressure(int[] arr, int a, int b, int n)
    {
        int r = 0;
        int v0 = 1, v1 = 2, v2 = 3, v3 = 4, v4 = 5, v5 = 6, v6 = 7;
        int v7 = 8, v8 = 9, v9 = 10, v10 = 11, v11 = 12, v12 = 13, v13 = 14;
        for (int i = 0; i < n; i++)
        {
            r += arr.Length;
            r += a / b;
            v0 += i;
            v1 += v0;
            v2 += v1;
            v3 += v2;
            v4 += v3;
            v5 += v4;
            v6 += v5;
            v7 += v6;
            v8 += v7;
            v9 += v8;
            v10 += v9;
            v11 += v10;
            v12 += v11;
            v13 += v12;
        }
        return r + v0 + v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8 + v9 + v10 + v11 + v12 + v13;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        // arr has a single element and idx is 5, so the first statement of the first
        // iteration must fault before "a / b" is ever evaluated.
        Assert.Throws<IndexOutOfRangeException>(() => Test(new int[1], 5, 1, 0, 5));

        // Sanity check: with an in-range index the loop still runs and divides.
        Assert.Throws<DivideByZeroException>(() => Test(new int[1], 0, 1, 0, 5));

        // And the non-faulting case computes the expected value.
        Assert.Equal(35, Test(new int[] { 4 }, 0, 21, 7, 5));

        // "arr" is null, so "arr.Length" must fault before "a / b" is evaluated.
        Assert.Throws<NullReferenceException>(() => TestPressure(null, 1, 0, 5));

        Assert.Throws<DivideByZeroException>(() => TestPressure(new int[3], 1, 0, 5));
    }
}
