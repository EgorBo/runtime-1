// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading;
using TestLibrary;
using Xunit;

// Value numbering models a volatile load as mutating GcHeap/ByrefExposed, but the loop
// side effect summary used to ignore volatile loads entirely. The memory state computed
// for the loop entry therefore did not account for the acquire in the loop body, and the
// ordinary "box.Value" load below looked loop invariant and got hoisted into the preheader.
// The reader then kept returning the value it had captured before the writer published.
public class Runtime_133579
{
    private sealed class Box
    {
        public int Value;
        public int Ready;
        public int ReaderSpinning;
    }

    // "box.ReaderSpinning" is an ordinary store to an unrelated field: it only invalidates
    // that one field, so it does not by itself prevent "box.Value" from being hoisted.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int Reader(Box box)
    {
        int value = 0;
        int seen = 0;
        while (seen < 2)
        {
            value = box.Value;
            box.ReaderSpinning = 1;
            if (Volatile.Read(ref box.Ready) != 0)
            {
                seen++;
            }
        }
        return value;
    }

    [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsMultithreadingSupported))]
    public static void TestEntryPoint()
    {
        Box box = new Box();
        int result = 0;
        Thread reader = new Thread(() => result = Reader(box));
        reader.IsBackground = true;
        reader.Start();

        // Wait until the reader has run at least one iteration. With the buggy codegen the
        // hoisted load has captured the initial 0 by then, so publishing only afterwards is
        // what makes the stale value observable.
        while (Volatile.Read(ref box.ReaderSpinning) == 0)
        {
            Thread.Yield();
        }

        box.Value = 42;
        Volatile.Write(ref box.Ready, 1);

        // The reader only stops once it has observed Ready twice, so the release/acquire pair
        // guarantees its final read of box.Value sees 42.
        reader.Join();

        Assert.Equal(42, result);
    }
}
