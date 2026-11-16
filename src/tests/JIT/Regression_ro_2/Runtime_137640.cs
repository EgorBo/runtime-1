// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Xunit;

namespace Runtime_137640;

public static class Runtime_137640
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static long Test() => BinaryPrimitives.ReverseEndianness((long)RuntimeTypeHandle.ToIntPtr(typeof(Runtime_137640).TypeHandle)) + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Id(long x) => x;

    [Fact]
    public static void TestEntryPoint()
    {
        long handle = RuntimeTypeHandle.ToIntPtr(typeof(Runtime_137640).TypeHandle);
        long expected = BinaryPrimitives.ReverseEndianness(Id(handle)) + 1;
        Assert.Equal(expected, Test());
    }
}
