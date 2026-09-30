// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134891
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static nint GetHandle() => typeof(Runtime_134891).TypeHandle.Value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GreaterThan() => typeof(Runtime_134891).TypeHandle.Value > 0x1000000;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LessThan() => typeof(Runtime_134891).TypeHandle.Value < 0x1000000;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GreaterThanLong() => typeof(Runtime_134891).TypeHandle.Value > 0x100000000L;

    [Fact]
    public static void TestEntryPoint()
    {
        nint handle = GetHandle();
        Assert.Equal(handle > 0x1000000, GreaterThan());
        Assert.Equal(handle < 0x1000000, LessThan());
        Assert.Equal(handle > 0x100000000L, GreaterThanLong());
    }
}
