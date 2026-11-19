// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

namespace Runtime_137902;

public class Runtime_137902
{
    public struct S
    {
        public short A;
        public short B;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static S Make(short b) => new S { A = 1, B = b };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(S s) { }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int x, int y, short b)
    {
        S s = Make(b);
        s.A = (short)x;
        Consume(s);
        return y / s.A;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(10, Test(10, 100, 1));
    }
}
