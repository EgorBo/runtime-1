// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TestLibrary;
using Xunit;

public class Runtime_134489
{
    private struct S
    {
        public int A, B, C, D, E;
    }

    private class Holder
    {
        public S F;
    }

    [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsMultithreadingSupported))]
    public static void TestEntryPoint()
    {
        Holder h = new Holder();
        int[] arr = new int[10];
        bool stop = false;
        Thread writer = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                Volatile.Write(ref h.F.A, 0);
                Volatile.Write(ref h.F.A, 100_000_000);
            }
        });
        writer.Start();
        try
        {
            for (int i = 0; i < 100_000; i++)
            {
                try
                {
                    Test(h, arr, true);
                }
                catch (IndexOutOfRangeException)
                {
                }
            }
        }
        finally
        {
            Volatile.Write(ref stop, true);
            writer.Join();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Use(S s) => 0;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(Holder h, int[] arr, bool c)
    {
        S s = h.F;
        if (c)
            s = h.F;
        if ((uint)s.A < (uint)arr.Length)
        {
            S t = h.F;
            if (c)
                t = h.F;
            // t.A is a separate read of h.F.A, so arr[t.A] must keep its bounds check.
            return Use(s) + Use(t) + arr[t.A] + s.B;
        }
        return -1;
    }
}
