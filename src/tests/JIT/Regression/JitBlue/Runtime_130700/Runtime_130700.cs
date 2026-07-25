// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Redundant branch opts jump threaded through the block holding the "assert"
// predicate, which routes some of that block's preds directly to its successors.
// The block therefore no longer dominated its old dominator subtree, but the
// stale dominator info was still used later in the same phase. That let RBO
// conclude that "obj is DerivedA" must be true inside Test, so the null result
// of the following "obj as DerivedA" was dereferenced and Test faulted with a
// NullReferenceException when called with a DerivedB.

namespace Runtime_130700;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

public abstract class Base
{
}

public class DerivedA : Base
{
    public int X = 11;
}

public class DerivedB : Base
{
    public int Y = 22;
}

public enum Mode
{
    Automatic,
    Manual
}

public class Holder
{
    public Mode Mode { get; set; }

    private static void Check(bool condition, string message = "", params object[] args)
    {
        if (condition)
        {
            return;
        }

        message ??= string.Empty;
        message = string.Format(message, args);
        throw new Exception(message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Test(Base obj)
    {
        Check(Mode != Mode.Automatic || (Mode == Mode.Automatic && obj is DerivedA), "boom");

        if (obj is DerivedA a)
        {
            return a.X;
        }

        if (obj is DerivedB b)
        {
            return b.Y;
        }

        return -1;
    }
}

public class Runtime_130700
{
    [Fact]
    public static int TestEntryPoint()
    {
        Holder holder = new Holder();
        holder.Mode = Mode.Manual;

        // Drive Test through Tier0 -> Instrumented Tier0 -> Tier1+PGO.
        for (int i = 0; i < 100; i++)
        {
            holder.Test(new DerivedB());
            Thread.Sleep(1);
        }

        Thread.Sleep(100);

        int failures = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (holder.Test(new DerivedB()) != 22)
            {
                failures++;
            }

            if (holder.Test(new DerivedA()) != 11)
            {
                failures++;
            }
        }

        return failures == 0 ? 100 : 1;
    }
}

