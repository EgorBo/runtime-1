// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133760
{
    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(1408, Simplification(4));
        Assert.Equal(536870913, Multiplication(3));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Multiplication(int n)
    {
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            int product = i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            product *= i;
            sum += product;
        }
        return sum;
    }

    [Fact]
    public static void TestRecursiveAddition()
    {
        Assert.Equal(-1, RecursiveAddition(3));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Simplification(int n)
    {
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            int a0 = i;
            int b0 = n;
            int a1 = a0 + b0; int b1 = a0 - b0;
            int a2 = a1 + b1; int b2 = a1 - b1;
            int a3 = a2 + b2; int b3 = a2 - b2;
            int a4 = a3 + b3; int b4 = a3 - b3;
            int a5 = a4 + b4; int b5 = a4 - b4;
            int a6 = a5 + b5; int b6 = a5 - b5;
            int a7 = a6 + b6; int b7 = a6 - b6;
            int a8 = a7 + b7; int b8 = a7 - b7;
            int a9 = a8 + b8; int b9 = a8 - b8;
            int a10 = a9 + b9; int b10 = a9 - b9;
            int a11 = a10 + b10; int b11 = a10 - b10;
            int a12 = a11 + b11; int b12 = a11 - b11;
            sum += a12 + b12;
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int RecursiveAddition(int n)
    {
        int x = 0;
        for (int i = 0; i < n; i++)
        {
            int a0 = x;
            int b0 = x + 1;
            int c0 = x + 2;
            int a1 = a0 + b0; int b1 = b0 + c0; int c1 = c0 + a0;
            int a2 = a1 + b1; int b2 = b1 + c1; int c2 = c1 + a1;
            int a3 = a2 + b2; int b3 = b2 + c2; int c3 = c2 + a2;
            int a4 = a3 + b3; int b4 = b3 + c3; int c4 = c3 + a3;
            int a5 = a4 + b4; int b5 = b4 + c4; int c5 = c4 + a4;
            int a6 = a5 + b5; int b6 = b5 + c5; int c6 = c5 + a5;
            int a7 = a6 + b6; int b7 = b6 + c6; int c7 = c6 + a6;
            int a8 = a7 + b7; int b8 = b7 + c7; int c8 = c7 + a7;
            int a9 = a8 + b8; int b9 = b8 + c8; int c9 = c8 + a8;
            int a10 = a9 + b9; int b10 = b9 + c9; int c10 = c9 + a9;
            int a11 = a10 + b10; int b11 = b10 + c10; int c11 = c10 + a10;
            int a12 = a11 + b11; int b12 = b11 + c11; int c12 = c11 + a11;
            int a13 = a12 + b12; int b13 = b12 + c12; int c13 = c12 + a12;
            int a14 = a13 + b13; int b14 = b13 + c13; int c14 = c13 + a13;
            int a15 = a14 + b14; int b15 = b14 + c14; int c15 = c14 + a14;
            int a16 = a15 + b15; int b16 = b15 + c15; int c16 = c15 + a15;
            int a17 = a16 + b16; int b17 = b16 + c16; int c17 = c16 + a16;
            int a18 = a17 + b17; int b18 = b17 + c17; int c18 = c17 + a17;
            int a19 = a18 + b18; int b19 = b18 + c18; int c19 = c18 + a18;
            int a20 = a19 + b19; int b20 = b19 + c19; int c20 = c19 + a19;
            int a21 = a20 + b20; int b21 = b20 + c20; int c21 = c20 + a20;
            int a22 = a21 + b21; int b22 = b21 + c21; int c22 = c21 + a21;
            int a23 = a22 + b22; int b23 = b22 + c22; int c23 = c22 + a22;
            int a24 = a23 + b23; int b24 = b23 + c23; int c24 = c23 + a23;
            int a25 = a24 + b24; int b25 = b24 + c24; int c25 = c24 + a24;
            int a26 = a25 + b25; int b26 = b25 + c25; int c26 = c25 + a25;
            int a27 = a26 + b26; int b27 = b26 + c26; int c27 = c26 + a26;
            int a28 = a27 + b27; int b28 = b27 + c27; int c28 = c27 + a27;
            int a29 = a28 + b28; int b29 = b28 + c28;
            x = a29 + b29;
        }
        return x;
    }
}
