// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134457
{
    private static int s_sink;

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(6, Test(5));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw() => throw new InvalidOperationException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Work(int v) => s_sink = v;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int arg)
    {
        int x = 0;
        try
        {
            Throw();
        }
        catch (ArgumentException)
        {
            try
            {
                Work(arg);
            }
            finally
            {
                s_sink = x;
            }
        }
        catch (Exception) when ((x = arg + 1) > 0)
        {
            s_sink = x;
        }

        return x;
    }
}
