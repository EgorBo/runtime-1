// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TestLibrary;
using Xunit;

// Lowering rounded a constant localloc size up to STACK_ALIGN without checking for
// overflow. A size that cannot be represented (such as the native int -1 that IL can
// push, but C#'s stackalloc cannot) wrapped to zero, so a zero byte allocation was
// emitted while a non-null pointer was still returned.
//
// Such an allocation can never be satisfied, so the correct behavior is to overflow the
// stack. That is fatal to the process, so this test only compiles the methods; executing
// them is not observable. On a checked JIT the old behavior asserts five times while
// compiling (in "Lowering nodeinfo" and "Generate code").
public unsafe class Runtime_133582
{
    private delegate byte* Alloc();

    private static void CompileLocalloc(bool initLocals, Action<ILGenerator> emitSize)
    {
        var method = new DynamicMethod("Localloc", typeof(byte*), Type.EmptyTypes);
        method.InitLocals = initLocals;

        ILGenerator il = method.GetILGenerator();
        il.DeclareLocal(typeof(IntPtr));
        emitSize(il);
        il.Emit(OpCodes.Localloc);
        il.Emit(OpCodes.Ret);

        // Only force compilation: the allocation itself must overflow the stack.
        RuntimeHelpers.PrepareDelegate(method.CreateDelegate<Alloc>());
    }

    private static void EmitNativeInt(ILGenerator il, long value)
    {
        il.Emit(OpCodes.Ldc_I8, value);
        il.Emit(OpCodes.Conv_I);
    }

    private static void EmitInt32(ILGenerator il, int value)
    {
        il.Emit(OpCodes.Ldc_I4, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int ValidLocalloc()
    {
        byte* p = stackalloc byte[64];
        p[63] = 42;
        return p[0] + p[63];
    }

    [ConditionalFact(typeof(Utilities), nameof(Utilities.IsReflectionEmitSupported))]
    public static void TestEntryPoint()
    {
        CompileLocalloc(initLocals: true, il => EmitNativeInt(il, -1));
        CompileLocalloc(initLocals: false, il => EmitNativeInt(il, -1));
        CompileLocalloc(initLocals: true, il => EmitNativeInt(il, -16));
        CompileLocalloc(initLocals: false, il => EmitNativeInt(il, -16));
        CompileLocalloc(initLocals: true, il => EmitNativeInt(il, 0x1_0000_0000));
        CompileLocalloc(initLocals: false, il => EmitNativeInt(il, 0x1_0000_0000));

        // The size may also be pushed as a plain int32, without a conv.i.
        CompileLocalloc(initLocals: true, il => EmitInt32(il, -1));
        CompileLocalloc(initLocals: false, il => EmitInt32(il, -1));

        // A satisfiable constant localloc must still be zero initialized and usable.
        Assert.Equal(42, ValidLocalloc());
    }
}
