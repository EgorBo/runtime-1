// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135528;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using Xunit;

public class Runtime_135528
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<byte> Test(Vector128<byte> a, Vector128<byte> b, Vector128<byte> idx) =>
        AdvSimd.Arm64.VectorTableLookupExtension(a, (a, b), idx);

    [Fact]
    public static void TestEntryPoint()
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            return;
        }

        Vector128<byte> a = Vector128.Create((byte)1);
        Vector128<byte> b = Vector128.Create((byte)2);
        Assert.Equal(a, Test(a, b, Vector128.Create((byte)3)));
        Assert.Equal(b, Test(a, b, Vector128.Create((byte)20)));
        Assert.Equal(a, Test(a, b, Vector128.Create((byte)40)));
    }
}
