// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace Runtime_137840;

public class Runtime_137840
{
    public enum E { A, B }

    [Fact]
    public static void TestEntryPoint()
    {
        Check(new int[] { 1 });
        Check((int[])(object)new E[] { E.B });
        Check((int[])(object)new uint[] { 3 });
    }

    private static void Check(int[] a)
    {
        IEnumerator expected = ViaInterface(a);
        IEnumerator actual = ViaArray(a);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Current.GetType(), actual.Current.GetType());
        Assert.Equal(expected.Current, actual.Current);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerator ViaArray(int[] a)
    {
        IEnumerator e = ((IEnumerable<int>)a).GetEnumerator();
        e.MoveNext();
        return e;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerator ViaInterface(IEnumerable<int> a)
    {
        IEnumerator e = a.GetEnumerator();
        e.MoveNext();
        return e;
    }
}
