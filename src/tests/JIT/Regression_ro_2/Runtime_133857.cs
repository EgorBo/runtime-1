// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133857
{
    private class A
    {
        public int X;
    }

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(0, TestSubtype(0));
        Assert.Equal(1, TestSubtype(1));
        Assert.Equal(99, TestSubtype(-1));
        Assert.Equal(0, TestNonNull(0));
        Assert.Equal(1, TestNonNull(1));
        Assert.Equal(99, TestNonNull(-1));
    }

    // Each merge has several inputs referring to the preceding PHI. Bounding only
    // recursion depth makes the reaching-assertion walks revisit it exponentially.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int TestSubtype(int x)
    {
        object o = new A();
        int acc = 0;
        switch (x & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 1; break;
            case 2: acc += 2; break;
            case 3: acc += 3; break;
            case 4: acc += 4; break;
            case 5: acc += 5; break;
            case 6: acc += 6; break;
            case 7: acc += 7; break;
        }
        switch ((x >> 3) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 2; break;
            case 2: acc += 3; break;
            case 3: acc += 4; break;
            case 4: acc += 5; break;
            case 5: acc += 6; break;
            case 6: acc += 7; break;
            case 7: acc += 8; break;
        }
        switch ((x >> 6) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 3; break;
            case 2: acc += 4; break;
            case 3: acc += 5; break;
            case 4: acc += 6; break;
            case 5: acc += 7; break;
            case 6: acc += 8; break;
            case 7: acc += 9; break;
        }
        switch ((x >> 9) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 4; break;
            case 2: acc += 5; break;
            case 3: acc += 6; break;
            case 4: acc += 7; break;
            case 5: acc += 8; break;
            case 6: acc += 9; break;
            case 7: acc += 10; break;
        }
        switch ((x >> 12) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 5; break;
            case 2: acc += 6; break;
            case 3: acc += 7; break;
            case 4: acc += 8; break;
            case 5: acc += 9; break;
            case 6: acc += 10; break;
            case 7: acc += 11; break;
        }
        switch ((x >> 15) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 6; break;
            case 2: acc += 7; break;
            case 3: acc += 8; break;
            case 4: acc += 9; break;
            case 5: acc += 10; break;
            case 6: acc += 11; break;
            case 7: acc += 12; break;
        }
        switch ((x >> 18) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 7; break;
            case 2: acc += 8; break;
            case 3: acc += 9; break;
            case 4: acc += 10; break;
            case 5: acc += 11; break;
            case 6: acc += 12; break;
            case 7: acc += 13; break;
        }
        switch ((x >> 21) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 8; break;
            case 2: acc += 9; break;
            case 3: acc += 10; break;
            case 4: acc += 11; break;
            case 5: acc += 12; break;
            case 6: acc += 13; break;
            case 7: acc += 14; break;
        }
        switch ((x >> 24) & 7)
        {
            case 0: o = new A(); break;
            case 1: acc += 9; break;
            case 2: acc += 10; break;
            case 3: acc += 11; break;
            case 4: acc += 12; break;
            case 5: acc += 13; break;
            case 6: acc += 14; break;
            case 7: acc += 15; break;
        }

        if (o is A a)
        {
            acc += a.X;
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object GetObject() => new A();

    // Separate locals keep local assertion propagation from removing the final
    // null check before the VN-based walk can see it.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int TestNonNull(int x)
    {
        object o0 = GetObject();
        if (o0 == null)
        {
            return -1;
        }

        int acc = 0;
        object o1 = o0;
        switch (x & 7)
        {
            case 0:
                o1 = GetObject();
                if (o1 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 1; break;
            case 2: acc += 2; break;
            case 3: acc += 3; break;
            case 4: acc += 4; break;
            case 5: acc += 5; break;
            case 6: acc += 6; break;
            case 7: acc += 7; break;
        }
        object o2 = o1;
        switch ((x >> 3) & 7)
        {
            case 0:
                o2 = GetObject();
                if (o2 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 2; break;
            case 2: acc += 3; break;
            case 3: acc += 4; break;
            case 4: acc += 5; break;
            case 5: acc += 6; break;
            case 6: acc += 7; break;
            case 7: acc += 8; break;
        }
        object o3 = o2;
        switch ((x >> 6) & 7)
        {
            case 0:
                o3 = GetObject();
                if (o3 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 3; break;
            case 2: acc += 4; break;
            case 3: acc += 5; break;
            case 4: acc += 6; break;
            case 5: acc += 7; break;
            case 6: acc += 8; break;
            case 7: acc += 9; break;
        }
        object o4 = o3;
        switch ((x >> 9) & 7)
        {
            case 0:
                o4 = GetObject();
                if (o4 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 4; break;
            case 2: acc += 5; break;
            case 3: acc += 6; break;
            case 4: acc += 7; break;
            case 5: acc += 8; break;
            case 6: acc += 9; break;
            case 7: acc += 10; break;
        }
        object o5 = o4;
        switch ((x >> 12) & 7)
        {
            case 0:
                o5 = GetObject();
                if (o5 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 5; break;
            case 2: acc += 6; break;
            case 3: acc += 7; break;
            case 4: acc += 8; break;
            case 5: acc += 9; break;
            case 6: acc += 10; break;
            case 7: acc += 11; break;
        }
        object o6 = o5;
        switch ((x >> 15) & 7)
        {
            case 0:
                o6 = GetObject();
                if (o6 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 6; break;
            case 2: acc += 7; break;
            case 3: acc += 8; break;
            case 4: acc += 9; break;
            case 5: acc += 10; break;
            case 6: acc += 11; break;
            case 7: acc += 12; break;
        }
        object o7 = o6;
        switch ((x >> 18) & 7)
        {
            case 0:
                o7 = GetObject();
                if (o7 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 7; break;
            case 2: acc += 8; break;
            case 3: acc += 9; break;
            case 4: acc += 10; break;
            case 5: acc += 11; break;
            case 6: acc += 12; break;
            case 7: acc += 13; break;
        }
        object o8 = o7;
        switch ((x >> 21) & 7)
        {
            case 0:
                o8 = GetObject();
                if (o8 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 8; break;
            case 2: acc += 9; break;
            case 3: acc += 10; break;
            case 4: acc += 11; break;
            case 5: acc += 12; break;
            case 6: acc += 13; break;
            case 7: acc += 14; break;
        }
        object o9 = o8;
        switch ((x >> 24) & 7)
        {
            case 0:
                o9 = GetObject();
                if (o9 == null)
                {
                    return -1;
                }
                break;
            case 1: acc += 9; break;
            case 2: acc += 10; break;
            case 3: acc += 11; break;
            case 4: acc += 12; break;
            case 5: acc += 13; break;
            case 6: acc += 14; break;
            case 7: acc += 15; break;
        }

        return o9 != null ? acc : -1;
    }
}
