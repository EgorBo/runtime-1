// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134481
{
    private struct S
    {
        public long A, B;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        try
        {
            Test(new int[1], 0);
        }
        catch (OverflowException)
        {
            return;
        }

        Assert.Fail("Expected OverflowException");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static S Callee(int a, int b, int c, int d, int e, int f, int g, int h) => new S { A = a, B = h };

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static S Test(int[] arr, int i)
    {
        int x = int.MaxValue;
        return Callee(arr[i] + checked(x + 1), 0, 0, 0, 0, 0, 0, 0);
    }
}
