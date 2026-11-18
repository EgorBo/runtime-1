// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Runtime_137846;

public class Runtime_137846
{
    public interface IBase
    {
        static abstract string M();
    }

    public interface IDerived<T> : IBase
    {
        static string IBase.M() => typeof(T).Name;
    }

    public struct S<T> : IDerived<T[]>
    {
    }

    public class Base<T> : IBase
    {
        public static string M() => typeof(T).Name;
    }

    public class Derived<U> : Base<U[]>
    {
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal("String[]", Call<S<string>>());
        Assert.Equal("String[]", GetFunc<S<string>>()());
        Assert.Equal("String[]", CallDerived<string>());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Call<T>() where T : IBase => T.M();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<string> GetFunc<T>() where T : IBase => T.M;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string CallDerived<T>() => CallInlined<Derived<T>>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CallInlined<T>() where T : IBase => T.M();
}
