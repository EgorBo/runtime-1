// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_135393
{
    public struct Pair
    {
        public Vector4 A;
        public Vector4 B;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Pair p = Test(1);
        Assert.Equal(new Vector4(1), p.A);
        Assert.Equal(new Vector4(2), p.B);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Pair Make(float x) => new Pair { A = new Vector4(x), B = new Vector4(x + 1) };

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Pair Test(float x)
    {
        Pair p = default;
        Unsafe.As<Vector4, Pair>(ref p.A) = Make(x);
        return p;
    }
}
