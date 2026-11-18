// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Runtime_137842;

public class Runtime_137842
{
    [InlineArray(2)]
    public struct Arr
    {
        public object e;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Outer
    {
        [FieldOffset(0)] public object o;
        [FieldOffset(0)] public Arr arr;
    }

    [StructLayout(LayoutKind.Explicit)]
    public ref struct RefOuter
    {
        [FieldOffset(0)] public object o;
        [FieldOffset(0)] public Arr arr;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(-1, TestStruct(i));
            Assert.Equal(-1, TestRefStruct(i));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Outer Make(int i)
    {
        Outer x = default;
        x.arr[1] = new string('a', 40 + (i & 7));
        return x;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Make(ref RefOuter x, int i)
    {
        x.arr[1] = new string('a', 40 + (i & 7));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TestStruct(int i)
    {
        Outer x = Make(i);
        Clobber(i);
        return ((string)x.arr[1]).IndexOf('b');
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TestRefStruct(int i)
    {
        RefOuter x = default;
        Make(ref x, i);
        Clobber(i);
        return ((string)x.arr[1]).IndexOf('b');
    }

    private static void Clobber(int i)
    {
        GC.Collect();
        for (int k = 0; k < 100; k++)
        {
            GC.KeepAlive(new string('b', 40 + (i & 7)));
        }
    }
}
