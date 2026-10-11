// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135523;

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_135523
{
    private class C
    {
        public object F0, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12;
        public long L;
    }

    private static object s_orig;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Call(object x) => 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long H(object x) => 1;

    // A spill temp store and a GC local store get combined into an stp.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static long Test(C c, bool b, int n, int m)
    {
        object o1 = c.F1, o2 = c.F2, o3 = c.F3, o4 = c.F4, o5 = c.F5, o6 = c.F6, o7 = c.F7, o8 = c.F8, o9 = c.F9, o10 = c.F10, o11 = c.F11, o12 = c.F12;
        object o0 = c.F0;
        object v;
        long r = c.L + Call(v = o0);
        if (b)
        {
            v = o1;
        }
        for (int i = 0; i < n; i++)
        {
            r += H(o1) + H(o2) + H(o3) + H(o4) + H(o5) + H(o6);
            r += H(o7) + H(o8) + H(o9) + H(o10) + H(o11) + H(o12);
        }
        for (int k = 0; k < m; k++)
        {
            r += k;
        }
        return ReferenceEquals(v, s_orig) ? 0 : 1;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        C c = new C
        {
            F1 = new object(), F2 = new object(), F3 = new object(), F4 = new object(), F5 = new object(), F6 = new object(),
            F7 = new object(), F8 = new object(), F9 = new object(), F10 = new object(), F11 = new object(), F12 = new object(),
            L = 5
        };

        for (int i = 0; i < 100; i++)
        {
            GC.KeepAlive(new byte[100]);
            c.F0 = new object();
            s_orig = c.F0;
            Assert.Equal(0, Test(c, false, 0, 0));
        }
    }
}
