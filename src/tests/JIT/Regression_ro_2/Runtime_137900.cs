// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;
using Xunit;

namespace Runtime_137900;

// Lowering must not read a vector operand after the side effects of later operands.
public class Runtime_137900
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector256<int> WithElement256(int x, int y)
    {
        Vector256<int> v = Vector256.Create(x);
        Span<int> lanes = MemoryMarshal.Cast<Vector256<int>, int>(new Span<Vector256<int>>(ref v));
        return v.WithElement(0, Interlocked.Exchange(ref lanes[1], y) + 10);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector512<int> WithElement512(int x, int y)
    {
        Vector512<int> v = Vector512.Create(x);
        Span<int> lanes = MemoryMarshal.Cast<Vector512<int>, int>(new Span<Vector512<int>>(ref v));
        return v.WithElement(0, Interlocked.Exchange(ref lanes[1], y) + 10);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<int> ConditionalSelect(int x, int y)
    {
        Vector128<int> m = Vector128.Create(x);
        Span<int> lanes = MemoryMarshal.Cast<Vector128<int>, int>(new Span<Vector128<int>>(ref m));
        return Vector128.ConditionalSelect(m, Vector128.Create(Interlocked.Exchange(ref lanes[0], y)), Vector128<int>.AllBitsSet);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static long GetElementLong(long x, int y)
    {
        Vector128<long> v = Vector128.Create(x, 2);
        Span<int> lanes = MemoryMarshal.Cast<Vector128<long>, int>(new Span<Vector128<long>>(ref v));
        return v.GetElement(Interlocked.Exchange(ref lanes[0], y) & 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int GetElementLocal(int x, int y)
    {
        Vector128<int> v = Vector128.Create(x);
        Span<int> lanes = MemoryMarshal.Cast<Vector128<int>, int>(new Span<Vector128<int>>(ref v));
        return v.GetElement((Interlocked.Exchange(ref lanes[1], y) & 0) + 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int GetElementIndirConstIndex(Vector128<int>[] arr, int y)
    {
        Span<int> lanes = MemoryMarshal.Cast<Vector128<int>, int>(arr);
        return arr[0].GetElement(Interlocked.Exchange(ref lanes[0], y) & 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int GetElementIndir(Vector128<int>[] arr, int y, int i)
    {
        Span<int> lanes = MemoryMarshal.Cast<Vector128<int>, int>(arr);
        return arr[0].GetElement(i + (Interlocked.Exchange(ref lanes[0], y) & 0));
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(Vector256.Create(1).WithElement(0, 11), WithElement256(1, 99));
        Assert.Equal(Vector512.Create(1).WithElement(0, 11), WithElement512(1, 99));
        Assert.Equal(Vector128<int>.AllBitsSet, ConditionalSelect(0x0F0F0F0F, -1));
        Assert.Equal(5, GetElementLong(5, 7));
        Assert.Equal(5, GetElementLocal(5, 7));
        Assert.Equal(5, GetElementIndirConstIndex([Vector128.Create(5)], 7));
        Assert.Equal(5, GetElementIndir([Vector128.Create(5)], 7, 0));
    }
}
