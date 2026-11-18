// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Runtime_137845;

public class Runtime_137845
{
    public interface I
    {
        static abstract string M();
    }

    public class Base<T> : I
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string M() => typeof(T).Name;
    }

    public class Derived<U> : Base<string>
    {
    }

    public class Derived : Base<string>
    {
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal("String", Helper<Derived<int>>());
        Assert.Equal("String", Helper<Derived>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Helper<T>() where T : I => T.M();
}
