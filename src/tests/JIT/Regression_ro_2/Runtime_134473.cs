// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134473
{
    [InlineArray(100000)]
    private struct Buf
    {
        private byte _b;
    }

    private struct Big
    {
        public Buf Pad;
        public int Val;
    }

    private class C
    {
        public int Small;
        public Big B;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(1, Test(new C(), 1));
        Assert.Throws<NullReferenceException>(() => Test(null, 1));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Foo(int a, int b) => a + b;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(C o, int x) => Foo(o.Small, o.B.Val * 3 + x);
}
