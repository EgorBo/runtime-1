// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_137635;

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_137635
{
    private struct S
    {
        public long A, B;
    }

    private static long s_sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Use(S s) => s_sink += s.A;

    private const MethodImplOptions AI = MethodImplOptions.AggressiveInlining;

    [MethodImpl(AI)] private static void L1(S s) { Use(s); Use(s); Use(s); Use(s); }
    [MethodImpl(AI)] private static void L2(S s) { L1(s); L1(s); L1(s); L1(s); }
    [MethodImpl(AI)] private static void L3(S s) { L2(s); L2(s); L2(s); L2(s); }
    [MethodImpl(AI)] private static void L4(S s) { L3(s); L3(s); L3(s); L3(s); }
    [MethodImpl(AI)] private static void L5(S s) { L4(s); L4(s); L4(s); L4(s); }
    [MethodImpl(AI)] private static void L6(S s) { L5(s); L5(s); L5(s); L5(s); }
    [MethodImpl(AI)] private static void L7(S s) { L6(s); L6(s); L6(s); L6(s); }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Test(S s) { L7(s); L7(s); L7(s); L7(s); }

    [Fact]
    public static void TestEntryPoint()
    {
        Test(new S { A = 1, B = 2 });
        Assert.Equal(65536, s_sink);
    }
}
