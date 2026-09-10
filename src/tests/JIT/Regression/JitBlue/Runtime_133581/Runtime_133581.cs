// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

public class Runtime_133581
{
    [StructLayout(LayoutKind.Explicit, Size = 65600)]
    private struct Big
    {
        [FieldOffset(0)] public int Head;
        [FieldOffset(32768)] public int Middle;
        [FieldOffset(65584)] public int Tail;
        [FieldOffset(65592)] public object Reference;
    }

    private class Holder
    {
        public Big Value;
    }

    private struct Pair
    {
        public object First;
        public object Second;
    }

    [StructLayout(LayoutKind.Explicit, Size = 65600)]
    private struct BigWithPair
    {
        [FieldOffset(65528)] public Pair Pair;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        var holder = new Holder();
        CopyToField(holder);
        Assert.Equal(42, holder.Value.Head);
        Assert.Equal(123, holder.Value.Middle);
        Assert.Equal(456, holder.Value.Tail);
        Assert.Equal("payload", holder.Value.Reference);

        holder.Value = default;
        CopyToByref(ref holder.Value);
        Assert.Equal(42, holder.Value.Head);
        Assert.Equal(123, holder.Value.Middle);
        Assert.Equal(456, holder.Value.Tail);
        Assert.Equal("payload", holder.Value.Reference);

        Big stackValue = default;
        CopyToByref(ref stackValue);
        Assert.Equal(42, stackValue.Head);
        Assert.Equal(123, stackValue.Middle);
        Assert.Equal(456, stackValue.Tail);
        Assert.Equal("payload", stackValue.Reference);

        Pair pair = CopyPairFromLargeLocal();
        Assert.Equal("first", pair.First);
        Assert.Equal("second", pair.Second);

        pair = CopyPairToLargeLocal();
        Assert.Equal("first", pair.First);
        Assert.Equal("second", pair.Second);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void CopyToField(Holder holder)
    {
        Big value = default;
        value.Head = 42;
        value.Middle = 123;
        value.Tail = 456;
        value.Reference = "payload";
        holder.Value = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void CopyToByref(ref Big destination)
    {
        Big value = default;
        value.Head = 42;
        value.Middle = 123;
        value.Tail = 456;
        value.Reference = "payload";
        destination = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Pair CopyPairFromLargeLocal()
    {
        BigWithPair value = default;
        value.Pair.First = "first";
        value.Pair.Second = "second";
        Pair pair = value.Pair;
        return pair;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Pair CopyPairToLargeLocal()
    {
        Pair pair = new Pair { First = "first", Second = "second" };
        BigWithPair value = default;
        value.Pair = pair;
        return value.Pair;
    }
}
