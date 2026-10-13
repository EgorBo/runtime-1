// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

public class Runtime_134474
{
    private struct Pair
    {
        public long A, B;
    }

    private class Inner
    {
        public Pair P;
    }

    private class Outer
    {
        public Inner I;
    }

    private static volatile bool s_done;

    [Fact]
    public static void TestEntryPoint()
    {
        Inner x = new Inner { P = new Pair { A = 1, B = 1 } };
        Inner y = new Inner { P = new Pair { A = 2, B = 2 } };
        Outer o = new Outer { I = x };

        Thread writer = new Thread(() =>
        {
            while (!s_done)
            {
                o.I = y;
                o.I = x;
            }
        });
        writer.Start();

        bool torn = false;
        for (int i = 0; i < 10_000_000 && !torn; i++)
        {
            torn = IsTorn(o);
        }

        s_done = true;
        writer.Join();
        Assert.False(torn);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool IsTorn(Outer o) => Check(o.I.P);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Check(Pair p) => p.A != p.B;
}
