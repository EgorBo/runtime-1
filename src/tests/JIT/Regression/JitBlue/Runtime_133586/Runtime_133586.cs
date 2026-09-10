// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

// The store below writes B through a byref derived from A, so value numbering correctly
// treats it as a store that does not fit in A and invalidates the whole heap. The loop
// side effect summary, however, only looked at the field handle and recorded A as the
// modified field, so the "box.B" load still looked loop invariant and was hoisted into
// the preheader, summing the stale initial value on every iteration.
public class Runtime_133586
{
    [StructLayout(LayoutKind.Sequential)]
    private sealed class Box
    {
        public int A;
        public int B;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Test(Box box, int n)
    {
        int sum = 0;
        for (int i = 1; i <= n; i++)
        {
            sum += box.B;
            Unsafe.Add(ref box.A, 1) = i; // writes B
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetN() => 3;

    [Fact]
    public static void TestEntryPoint()
    {
        Box box = new Box();

        // The store only reaches B if B directly follows A; skip otherwise.
        if (Unsafe.ByteOffset(ref box.A, ref box.B) != (nint)Unsafe.SizeOf<int>())
        {
            return;
        }

        // B is 0, 1, 2 when it is read, so the reads have to sum to 3.
        Assert.Equal(3, Test(box, GetN()));
        Assert.Equal(0, box.A);
        Assert.Equal(3, box.B);
    }
}
