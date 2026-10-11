// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135522;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

public class Runtime_135522
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct S
    {
        public long A;
        public byte B;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static long Test(Span<S> s)
    {
        if (s.Length < 4)
        {
            return 0;
        }
        long a = s[0].A;
        s = s.Slice(1);
        long b = s[1].A;
        return a ^ b;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        S[] data = new S[8];
        for (int i = 0; i < data.Length; i++)
        {
            data[i].A = i + 1;
        }
        Assert.Equal(1 ^ 3, Test(data));
    }
}
