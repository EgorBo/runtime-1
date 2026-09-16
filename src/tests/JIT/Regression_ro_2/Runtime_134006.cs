// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

// Folding the tail-duplicated condition removed the last real predecessor of the finally's entry
// block. fgUpdateFlowGraph then saw bbRefs == 1 -- the handler entry's artificial reference -- and
// dereferenced the now empty predecessor list.
public class Runtime_134006
{
    private static int s_counter;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Test(int b)
    {
        int L = b;
        try
        {
            throw new Exception("boom");
        }
        finally
        {
        T1:
            if (L == 1)
            {
                goto Done;
            }
            L = 1;
            goto T1;
        Done:
            s_counter++;
        }
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Throws<Exception>(() => Test(0));
        Assert.Equal(1, s_counter);
    }
}
