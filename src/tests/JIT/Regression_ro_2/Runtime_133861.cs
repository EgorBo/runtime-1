// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133861
{
    private static int s_evaluations;

    private class Recursive
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int Sum(Recursive next, int count, int sum)
        {
            if (count == 0)
            {
                return sum;
            }

            return next.Sum(next, count - 1, sum + count);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int WithArgument(Recursive next, int count)
        {
            if (count == 0)
            {
                return 123;
            }

            return next.WithArgument(next, Evaluate(count - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int CheckedSum(Recursive next, int count, int sum)
        {
            if (count == 0)
            {
                return sum;
            }

            return next.CheckedSum(next, count - 1, checked(sum + count));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Evaluate(int count)
    {
        s_evaluations++;
        return count;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int Sum(Recursive instance, Recursive next, int count, int sum) => instance.Sum(next, count, sum);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int WithArgument(Recursive instance, Recursive next, int count) => instance.WithArgument(next, count);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int CheckedSum(Recursive instance, Recursive next, int count, int sum) => instance.CheckedSum(next, count, sum);

    [Fact]
    public static void TestEntryPoint()
    {
        var instance = new Recursive();
        Assert.Equal(15, Sum(instance, instance, 5, 0));
        Assert.Equal(42, Sum(instance, null, 0, 42));
        Assert.Throws<NullReferenceException>(() => Sum(instance, null, 1, 0));
        Assert.Throws<NullReferenceException>(() => Sum(instance, null, 5, 0));

        s_evaluations = 0;
        Assert.Throws<NullReferenceException>(() => WithArgument(instance, null, 1));
        Assert.Equal(1, s_evaluations);
        Assert.Throws<OverflowException>(() => CheckedSum(instance, null, 1, int.MaxValue));
    }
}
