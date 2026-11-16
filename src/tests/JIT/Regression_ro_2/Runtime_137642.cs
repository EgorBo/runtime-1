// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Runtime_137642;

public static class Runtime_137642
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Sel() => 2;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int n)
    {
        while (true)
        {
            switch (Sel())
            {
                case 0: continue;
                case 1: return n;
                case 2: return n + 1;
                case 3: return n + 2;
                default: return 3;
            }
        }
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(6, Test(5));
    }
}
