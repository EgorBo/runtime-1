// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133857
{
    private class A { public int X; }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(1, Test(1));
        Assert.Equal(0, Test(0));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int x)
    {
        object o = new A();
        int acc = 0;
        switch ((x >> 0) & 7) { case 0: o = new A(); break; case 1: acc += 1; break; case 2: acc += 2; break; case 3: acc += 3; break; case 4: acc += 4; break; case 5: acc += 5; break; case 6: acc += 6; break; case 7: acc += 7; break; }
        switch ((x >> 3) & 7) { case 0: o = new A(); break; case 1: acc += 2; break; case 2: acc += 3; break; case 3: acc += 4; break; case 4: acc += 5; break; case 5: acc += 6; break; case 6: acc += 7; break; case 7: acc += 8; break; }
        switch ((x >> 6) & 7) { case 0: o = new A(); break; case 1: acc += 3; break; case 2: acc += 4; break; case 3: acc += 5; break; case 4: acc += 6; break; case 5: acc += 7; break; case 6: acc += 8; break; case 7: acc += 9; break; }
        switch ((x >> 9) & 7) { case 0: o = new A(); break; case 1: acc += 4; break; case 2: acc += 5; break; case 3: acc += 6; break; case 4: acc += 7; break; case 5: acc += 8; break; case 6: acc += 9; break; case 7: acc += 10; break; }
        switch ((x >> 12) & 7) { case 0: o = new A(); break; case 1: acc += 5; break; case 2: acc += 6; break; case 3: acc += 7; break; case 4: acc += 8; break; case 5: acc += 9; break; case 6: acc += 10; break; case 7: acc += 11; break; }
        switch ((x >> 15) & 7) { case 0: o = new A(); break; case 1: acc += 6; break; case 2: acc += 7; break; case 3: acc += 8; break; case 4: acc += 9; break; case 5: acc += 10; break; case 6: acc += 11; break; case 7: acc += 12; break; }
        switch ((x >> 18) & 7) { case 0: o = new A(); break; case 1: acc += 7; break; case 2: acc += 8; break; case 3: acc += 9; break; case 4: acc += 10; break; case 5: acc += 11; break; case 6: acc += 12; break; case 7: acc += 13; break; }
        switch ((x >> 21) & 7) { case 0: o = new A(); break; case 1: acc += 8; break; case 2: acc += 9; break; case 3: acc += 10; break; case 4: acc += 11; break; case 5: acc += 12; break; case 6: acc += 13; break; case 7: acc += 14; break; }
        switch ((x >> 24) & 7) { case 0: o = new A(); break; case 1: acc += 9; break; case 2: acc += 10; break; case 3: acc += 11; break; case 4: acc += 12; break; case 5: acc += 13; break; case 6: acc += 14; break; case 7: acc += 15; break; }
        if (o is A a)
        {
            acc += a.X;
        }
        return acc;
    }
}
