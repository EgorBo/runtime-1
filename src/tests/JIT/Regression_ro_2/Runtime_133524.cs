// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

public class Runtime_133524
{
    [Fact]
    public static void TestEntryPoint()
    {
        int value = 0;
        int stop = 0;
        int reversed = 0;
        var publisher = new Thread(() =>
        {
            for (int i = 1; i <= 100_000_000 && Volatile.Read(ref stop) == 0; i++)
            {
                Volatile.Write(ref value, i);
            }
        });

        publisher.Start();
        try
        {
            for (int i = 0; i < 2_000_000; i++)
            {
                if (ReadArguments(ref value))
                {
                    reversed++;
                }
            }
        }
        finally
        {
            Volatile.Write(ref stop, 1);
            publisher.Join();
        }

        Assert.Equal(0, reversed);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool ReadArguments(ref int value) =>
        Reversed(Volatile.Read(ref value), Volatile.Read(ref value) + 1);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Reversed(int first, int secondPlusOne) => first > secondPlusOne - 1;
}
