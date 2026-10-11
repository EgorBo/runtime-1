// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_134067;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using TestLibrary;
using Xunit;

public class Runtime_134067
{
    [InlineArray(40000)]
    private struct Pad
    {
        private byte _e;
    }

    private struct Payload
    {
        public long A, B, C, D, E, F, G, H;
    }

    private class C
    {
        public Pad P;
        public Payload T;
    }

    private static volatile bool s_stop;

    // The large field offset requires materializing the destination address in a temp register.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(C c, int n)
    {
        int bad = 0;
        for (int i = 0; i < n; i++)
        {
            Volatile.Write(ref c.T.H, 1);
            c.T = default;
            if (Volatile.Read(ref c.T.H) != 0)
            {
                bad++;
            }
        }
        return bad;
    }

    [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsMultithreadingSupported))]
    public static void TestEntryPoint()
    {
        s_stop = false;
        Thread gcThread = new Thread(() =>
        {
            while (!s_stop)
            {
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }
        });
        Thread allocThread = new Thread(() =>
        {
            Random rand = new Random(42);
            while (!s_stop)
            {
                GC.KeepAlive(new byte[rand.Next(1000, 20000)]);
            }
        });
        gcThread.Start();
        allocThread.Start();

        int bad = 0;
        Stopwatch sw = Stopwatch.StartNew();
        while ((sw.ElapsedMilliseconds < 1000) && (bad == 0))
        {
            bad += Test(new C(), 10000);
        }

        s_stop = true;
        gcThread.Join();
        allocThread.Join();

        Assert.Equal(0, bad);
    }
}
