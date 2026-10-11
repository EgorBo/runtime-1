// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135526;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using Xunit;

public class Runtime_135526
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector64<float> Test(Vector64<float> a, float x) =>
        AdvSimd.MultiplyBySelectedScalar(a, Vector64.CreateScalarUnsafe(x), 0);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<float> Test2(Vector128<float> acc, Vector128<float> a, float x) =>
        AdvSimd.Arm64.FusedMultiplyAddBySelectedScalar(acc, a, Vector128.CreateScalarUnsafe(x), 0);

    [Fact]
    public static void TestEntryPoint()
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            return;
        }

        Assert.Equal(Vector64.Create(3f, 6f), Test(Vector64.Create(1f, 2f), 3f));
        Assert.Equal(Vector128.Create(4f, 7f, 10f, 13f), Test2(Vector128.Create(1f), Vector128.Create(1f, 2f, 3f, 4f), 3f));
    }
}
