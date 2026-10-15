// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Xunit;

public class Runtime_135815
{
    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(3, Test(5, 1));
        Assert.Equal(21, Test(5, 7));
        Assert.Equal(2, Test(5, -1));
        Assert.Equal(2, Test(5, int.MinValue));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int a, int x)
    {
        Vector128<int> v = Vector128.Create(a, a + 1, a + 2, a + 3);
        int s = v.GetElement(1) + v.GetElement(2) + v.GetElement(3);
        if (x >= 4)
            return s;
        if (x < 0 || v.GetElement(x) < 0)
            return 2;
        return 3;
    }
}
