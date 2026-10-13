// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_135394
{
    private struct W
    {
        public short F;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(-1, Test(new W { F = -1 }));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(W w)
    {
        short s = 0;
        Unsafe.As<short, W>(ref s) = w;
        return s;
    }
}
