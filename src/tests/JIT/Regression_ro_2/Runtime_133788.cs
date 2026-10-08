// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Xunit;

public class Runtime_133788
{
    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(7338, Test(3, 5, 300));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(int sel, int idx, int depth)
    {
        int r = 0;
        switch (sel)
        {
            case 0: { int[] a = new int[120]; a[idx] = idx; r = a[idx] + a[0]; break; }
            case 1: { int[] a = new int[120]; a[idx] = idx + 1; r = a[idx] + a[0]; break; }
            case 2: { int[] a = new int[120]; a[idx] = idx + 2; r = a[idx] + a[0]; break; }
            case 3: { int[] a = new int[120]; a[idx] = idx + 3; r = a[idx] + a[0]; break; }
            case 4: { int[] a = new int[120]; a[idx] = idx + 4; r = a[idx] + a[0]; break; }
            case 5: { int[] a = new int[120]; a[idx] = idx + 5; r = a[idx] + a[0]; break; }
            case 6: { int[] a = new int[120]; a[idx] = idx + 6; r = a[idx] + a[0]; break; }
            case 7: { int[] a = new int[120]; a[idx] = idx + 7; r = a[idx] + a[0]; break; }
            case 8: { int[] a = new int[120]; a[idx] = idx + 8; r = a[idx] + a[0]; break; }
            case 9: { int[] a = new int[120]; a[idx] = idx + 9; r = a[idx] + a[0]; break; }
            case 10: { int[] a = new int[120]; a[idx] = idx + 10; r = a[idx] + a[0]; break; }
            case 11: { int[] a = new int[120]; a[idx] = idx + 11; r = a[idx] + a[0]; break; }
            case 12: { int[] a = new int[120]; a[idx] = idx + 12; r = a[idx] + a[0]; break; }
            case 13: { int[] a = new int[120]; a[idx] = idx + 13; r = a[idx] + a[0]; break; }
            case 14: { int[] a = new int[120]; a[idx] = idx + 14; r = a[idx] + a[0]; break; }
            case 15: { int[] a = new int[120]; a[idx] = idx + 15; r = a[idx] + a[0]; break; }
            case 16: { int[] a = new int[120]; a[idx] = idx + 16; r = a[idx] + a[0]; break; }
            case 17: { int[] a = new int[120]; a[idx] = idx + 17; r = a[idx] + a[0]; break; }
            case 18: { int[] a = new int[120]; a[idx] = idx + 18; r = a[idx] + a[0]; break; }
            case 19: { int[] a = new int[120]; a[idx] = idx + 19; r = a[idx] + a[0]; break; }
            case 20: { int[] a = new int[120]; a[idx] = idx + 20; r = a[idx] + a[0]; break; }
            case 21: { int[] a = new int[120]; a[idx] = idx + 21; r = a[idx] + a[0]; break; }
            case 22: { int[] a = new int[120]; a[idx] = idx + 22; r = a[idx] + a[0]; break; }
            case 23: { int[] a = new int[120]; a[idx] = idx + 23; r = a[idx] + a[0]; break; }
            case 24: { int[] a = new int[120]; a[idx] = idx + 24; r = a[idx] + a[0]; break; }
            case 25: { int[] a = new int[120]; a[idx] = idx + 25; r = a[idx] + a[0]; break; }
            case 26: { int[] a = new int[120]; a[idx] = idx + 26; r = a[idx] + a[0]; break; }
            case 27: { int[] a = new int[120]; a[idx] = idx + 27; r = a[idx] + a[0]; break; }
            case 28: { int[] a = new int[120]; a[idx] = idx + 28; r = a[idx] + a[0]; break; }
            case 29: { int[] a = new int[120]; a[idx] = idx + 29; r = a[idx] + a[0]; break; }
            case 30: { int[] a = new int[120]; a[idx] = idx + 30; r = a[idx] + a[0]; break; }
            case 31: { int[] a = new int[120]; a[idx] = idx + 31; r = a[idx] + a[0]; break; }
            case 32: { int[] a = new int[120]; a[idx] = idx + 32; r = a[idx] + a[0]; break; }
            case 33: { int[] a = new int[120]; a[idx] = idx + 33; r = a[idx] + a[0]; break; }
            case 34: { int[] a = new int[120]; a[idx] = idx + 34; r = a[idx] + a[0]; break; }
            case 35: { int[] a = new int[120]; a[idx] = idx + 35; r = a[idx] + a[0]; break; }
            case 36: { int[] a = new int[120]; a[idx] = idx + 36; r = a[idx] + a[0]; break; }
            case 37: { int[] a = new int[120]; a[idx] = idx + 37; r = a[idx] + a[0]; break; }
            case 38: { int[] a = new int[120]; a[idx] = idx + 38; r = a[idx] + a[0]; break; }
            case 39: { int[] a = new int[120]; a[idx] = idx + 39; r = a[idx] + a[0]; break; }
        }
        if (depth > 0)
        {
            r += Test((sel + 7) % 40, idx, depth - 1);
        }
        return r;
    }
}
