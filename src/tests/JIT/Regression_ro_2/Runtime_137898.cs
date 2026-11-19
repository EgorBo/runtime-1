// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Xunit;

namespace Runtime_137898;

public class Runtime_137898
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EqualsLowerIsZero(Vector512<long> a, Vector512<long> b)
        => Vector512.Equals(a, b).GetLower() == Vector256<long>.Zero;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EqualsLowerLowerIsZero(Vector512<long> a, Vector512<long> b)
        => Vector512.Equals(a, b).GetLower().GetLower() == Vector128<long>.Zero;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EqualsLowerIsNotZero(Vector512<long> a, Vector512<long> b)
        => Vector512.Equals(a, b).GetLower() != Vector256<long>.Zero;

    [Fact]
    public static void CompareLowerOfMask()
    {
        Vector512<long> a = Vector512.Create(0L, 1, 2, 3, 4, 5, 6, 7);
        Vector512<long> b = Vector512.Create(9L, 9, 9, 9, 4, 5, 6, 7);

        Assert.True(EqualsLowerIsZero(a, b));
        Assert.True(EqualsLowerLowerIsZero(a, b));
        Assert.False(EqualsLowerIsNotZero(a, b));
    }

    // https://github.com/dotnet/runtime/issues/137899
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Vector256<int> NotTernaryLogicLower(Vector512<int> a, Vector512<int> b, Vector512<int> c)
    {
        Vector256<int> v = ~Avx512F.TernaryLogic(a, b, c, 0x96).GetLower();
        Consume(v);
        return v;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(Vector256<int> v) { }

    [ConditionalFact(typeof(Avx512F), nameof(Avx512F.IsSupported))]
    public static void NotOfTernaryLogicLower()
    {
        Assert.Equal(Vector256.Create(-8), NotTernaryLogicLower(Vector512.Create(1), Vector512.Create(2), Vector512.Create(4)));
    }
}
