// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Runtime_134068;

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134068
{
    public class Base
    {
        public virtual string M<T>(T t) => "B" + typeof(T).Name;
    }

    public class Derived : Base
    {
        public override string M<T>(T t) => "D" + typeof(T).Name;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Inlinee<T>(Base b, T t) => b.M<T>(t);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Caller<T>(Base b, T t) => Inlinee<T>(b, t);

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal("DString", Caller<string>(new Derived(), "x"));
        Assert.Equal("BObject", Caller<object>(new Base(), new object()));
    }
}
