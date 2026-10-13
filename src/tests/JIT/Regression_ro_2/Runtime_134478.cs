// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134478
{
    private static byte s_x;

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.False(IsGreater(ref s_x));
        Assert.False(IsLess(ref s_x));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsGreater(ref byte r) => Unsafe.IsAddressGreaterThan(ref r, ref s_x);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsLess(ref byte r) => Unsafe.IsAddressLessThan(ref r, ref s_x);
}
