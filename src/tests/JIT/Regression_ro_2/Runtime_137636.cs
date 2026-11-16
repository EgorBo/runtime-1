// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_137636;

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_137636
{
    private struct OneLong
    {
        public long A;
    }

    private struct TwoFloats
    {
        public float X, Y;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OneLong GetOneLong() => new OneLong { A = 0x4000000040800000 };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TwoFloats Test() => Unsafe.BitCast<OneLong, TwoFloats>(GetOneLong());

    [Fact]
    public static void TestEntryPoint()
    {
        TwoFloats result = Test();
        Assert.Equal(4.0f, result.X);
        Assert.Equal(2.0f, result.Y);
    }
}
