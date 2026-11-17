// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

public class Runtime_137679
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(ref byte data, int length, int[] limitArr, int start)
    {
        Span<byte> span = MemoryMarshal.CreateSpan(ref data, length);
        int sum = 0;
        for (int i = start; i < limitArr.Length + (int.MaxValue - 2); i += 4)
        {
            sum += span[i];
        }
        return sum;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        // A span of int.MaxValue - 1 bytes whose element at "start" is "value", to avoid a ~2GB allocation.
        int start = int.MaxValue - 3;
        byte value = 0;
        bool thrown = false;
        try
        {
            Test(ref Unsafe.Subtract(ref value, start), int.MaxValue - 1, new int[1], start);
        }
        catch (IndexOutOfRangeException)
        {
            thrown = true;
        }
        Assert.True(thrown);
    }
}
