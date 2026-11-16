// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

public class Runtime_137638
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int[,] a)
    {
        return a[Interlocked.Exchange(ref a, null) != null ? 0 : 1, 0];
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int TestSZ(int[] a)
    {
        return a[Interlocked.Exchange(ref a, null) != null ? 0 : 1];
    }

    [Fact]
    public static void TestEntryPoint()
    {
        var arr = new int[1, 1];
        arr[0, 0] = 42;
        Assert.Equal(42, Test(arr));
        Assert.Equal(42, TestSZ(new int[] { 42 }));
    }
}
