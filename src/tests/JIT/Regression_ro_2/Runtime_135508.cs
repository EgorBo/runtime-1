// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_135508;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using Xunit;

public class Runtime_135508
{
    [Fact]
    public static void TestEntryPoint()
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            return;
        }

        byte[] a = new byte[256];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (byte)i;
        }

        Vector128<byte> def = Vector128.Create((byte)0xAA);
        Vector128<byte> idx = Vector128.Create((byte)5);
        Assert.Equal(14034272802312799429UL, Test3(default, def, idx, a));
        Assert.Equal(14034272802312799429UL, Test4(default, def, idx, a));
    }

    private static Vector128<byte> L(byte[] a, int i) => Vector128.Create(a, i * 16);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static ulong Test3(Vector128<byte> unused, Vector128<byte> def, Vector128<byte> idx, byte[] a)
    {
        Vector128<byte> l0 = L(a, 0), l1 = L(a, 1), l2 = L(a, 2), l3 = L(a, 3), l4 = L(a, 4), l5 = L(a, 5), l6 = L(a, 6), l7 = L(a, 7);
        Vector128<byte> l8 = L(a, 8), l9 = L(a, 9), l10 = L(a, 10), l11 = L(a, 11), l12 = L(a, 12), l13 = L(a, 13), l14 = L(a, 14);
        Vector128<byte> r = AdvSimd.Arm64.VectorTableLookupExtension(def, (l3, l7, l1), idx & Vector128.Create((byte)63));
        return (r ^ l0 ^ l1 ^ l2 ^ l3 ^ l4 ^ l5 ^ l6 ^ l7 ^ l8 ^ l9 ^ l10 ^ l11 ^ l12 ^ l13 ^ l14).AsUInt64().GetElement(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static ulong Test4(Vector128<byte> unused, Vector128<byte> def, Vector128<byte> idx, byte[] a)
    {
        Vector128<byte> l0 = L(a, 0), l1 = L(a, 1), l2 = L(a, 2), l3 = L(a, 3), l4 = L(a, 4), l5 = L(a, 5), l6 = L(a, 6), l7 = L(a, 7);
        Vector128<byte> l8 = L(a, 8), l9 = L(a, 9), l10 = L(a, 10), l11 = L(a, 11), l12 = L(a, 12), l13 = L(a, 13), l14 = L(a, 14);
        Vector128<byte> r = AdvSimd.Arm64.VectorTableLookupExtension(def, (l3, l7, l1, l2), idx & Vector128.Create((byte)63));
        return (r ^ l0 ^ l1 ^ l2 ^ l3 ^ l4 ^ l5 ^ l6 ^ l7 ^ l8 ^ l9 ^ l10 ^ l11 ^ l12 ^ l13 ^ l14).AsUInt64().GetElement(0);
    }
}
