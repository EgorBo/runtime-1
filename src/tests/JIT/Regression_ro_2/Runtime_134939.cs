// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134939
{
    private static int s_value;

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(81920, Test(10));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int n)
    {
        F14();
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            sum += s_value;
        }

        return sum;
    }

    private static void F0() => s_value = ~s_value;
    private static void G0() => s_value = -s_value;
    private static void F1() { F0(); G0(); }
    private static void F2() { F1(); F1(); }
    private static void F3() { F2(); F2(); }
    private static void F4() { F3(); F3(); }
    private static void F5() { F4(); F4(); }
    private static void F6() { F5(); F5(); }
    private static void F7() { F6(); F6(); }
    private static void F8() { F7(); F7(); }
    private static void F9() { F8(); F8(); }
    private static void F10() { F9(); F9(); }
    private static void F11() { F10(); F10(); }
    private static void F12() { F11(); F11(); }
    private static void F13() { F12(); F12(); }
    private static void F14() { F13(); F13(); }
}
