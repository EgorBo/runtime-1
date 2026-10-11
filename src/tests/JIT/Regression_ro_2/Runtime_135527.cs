// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135527;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_135527
{
    private struct S
    {
        public Vector3 V;
        public int I;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Callee(int a, int b, int c, int d, int e, int f, int g, int h, S s) =>
        s.V.X + s.V.Y + s.V.Z + s.I + a + b + c + d + e + f + g + h;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static float Test() =>
        Callee(1, 2, 3, 4, 5, 6, 7, 8, new S { V = new Vector3(1, 2, 3), I = 5 });

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(47f, Test());
    }
}
