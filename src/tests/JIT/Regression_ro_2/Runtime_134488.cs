// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Xunit;

public class Runtime_134488
{
    private struct S
    {
        public int A, B, C, D, E, F, G, H;
    }

    private static class Holder
    {
        public static readonly S Value = new S { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8 };
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(Vector128.Create(1, 2, 3, 4), Test(0));
        Assert.Equal(Vector128.Create(2, 3, 4, 5), Test(1));
        Assert.Equal(1, TestScalar(0));
        Assert.Equal(4, TestScalar(3));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Vector128<int> Test(int i) => Vector128.LoadUnsafe(in Holder.Value.A, (nuint)i);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TestScalar(int i) => Unsafe.Add(ref Unsafe.AsRef(in Holder.Value.A), i);
}
