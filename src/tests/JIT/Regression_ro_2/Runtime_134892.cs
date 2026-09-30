// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Xunit;

public class Runtime_134892
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static T Sum64<T>(Vector64<T> v) => Vector64.Sum(v);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static T Sum128<T>(Vector128<T> v) => Vector128.Sum(v);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static T Sum256<T>(Vector256<T> v) => Vector256.Sum(v);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static T Sum512<T>(Vector512<T> v) => Vector512.Sum(v);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static T SumVector<T>(Vector<T> v) => Vector.Sum(v);

    private static void Test<T>(T negZero) where T : IFloatingPointIeee754<T>
    {
        // Accelerated and software fallback (via delegate) paths must both return -0.0
        Assert.True(T.IsNegative(Sum64(Vector64.Create(negZero))));
        Assert.True(T.IsNegative(new Func<Vector64<T>, T>(Vector64.Sum)(Vector64.Create(negZero))));
        Assert.True(T.IsNegative(Sum128(Vector128.Create(negZero))));
        Assert.True(T.IsNegative(new Func<Vector128<T>, T>(Vector128.Sum)(Vector128.Create(negZero))));
        Assert.True(T.IsNegative(Sum256(Vector256.Create(negZero))));
        Assert.True(T.IsNegative(new Func<Vector256<T>, T>(Vector256.Sum)(Vector256.Create(negZero))));
        Assert.True(T.IsNegative(Sum512(Vector512.Create(negZero))));
        Assert.True(T.IsNegative(new Func<Vector512<T>, T>(Vector512.Sum)(Vector512.Create(negZero))));
        Assert.True(T.IsNegative(SumVector(Vector.Create(negZero))));
        Assert.True(T.IsNegative(new Func<Vector<T>, T>(Vector.Sum)(Vector.Create(negZero))));
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Test(-0.0f);
        Test(-0.0);
    }
}
