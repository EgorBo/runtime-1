// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;
using Xunit;

public class Runtime_134893
{
    [StructLayout(LayoutKind.Explicit)]
    private struct S
    {
        [FieldOffset(0)] public Vector256<byte> V;
        [FieldOffset(16)] public long L;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<int> WithElement1(Vector128<int> v, int idx) => v.WithElement(idx, Interlocked.Exchange(ref idx, 100));

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<int> WithElement2(Vector128<int> v, int idx) => v.WithElement(idx, Interlocked.Exchange(ref idx, 0));

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector256<byte> Shuffle(Vector256<byte> v)
    {
        S s = default;
        s.V = v;
        return Vector256.Shuffle(s.V, Vector256.Create((byte)Interlocked.Exchange(ref s.L, -1)));
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(Vector128.Create(0, 1, 0, 0), WithElement1(Vector128<int>.Zero, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WithElement2(Vector128<int>.Zero, 6));
        Assert.Equal(Vector256.Create((byte)16), Shuffle(Vector256<byte>.Indices));
    }
}
