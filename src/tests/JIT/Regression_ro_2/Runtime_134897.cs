// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_134897
{
    public struct S
    {
        public int A;
        public int B;
        public S(int a, int b) { A = a; B = b; }
    }

    public class Holder
    {
        public static S Value = new S(10, 20);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(bool[] cond)
    {
        int sum = 0;
        for (int i = 0; i < cond.Length; i++)
        {
            if (cond[i])
            {
                sum++;
            }
            S s = Holder.Value;
            sum += s.A + s.B;
        }
        return sum;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(92, Test(new[] { false, true, true }));
    }
}
