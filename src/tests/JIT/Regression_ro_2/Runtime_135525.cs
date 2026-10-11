// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135525;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

// Without LSE atomics, CompareExchange is emitted as an ldaxr/stlxr retry loop
// whose inputs must stay reported as GC refs across the loop labels.
public class Runtime_135525
{
    private class Node
    {
        public object Item;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(Node n, object[] objs, int iters)
    {
        int failures = 0;
        for (int i = 0; i < iters; i++)
        {
            object o = objs[i & 7];
            n.Item = o;
            Interlocked.CompareExchange(ref n.Item, null, o);
            if (n.Item != null)
            {
                failures++;
            }
        }
        return failures;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        int failures = 0;
        for (int k = 0; k < 20; k++)
        {
            object[] objs = new object[8];
            for (int i = 0; i < objs.Length; i++)
            {
                GC.KeepAlive(new byte[64]);
                objs[i] = new object();
            }
            failures += Test(new Node(), objs, 100);
        }
        Assert.Equal(0, failures);
    }
}
