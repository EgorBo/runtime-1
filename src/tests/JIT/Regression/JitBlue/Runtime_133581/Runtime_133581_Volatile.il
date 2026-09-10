// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

.assembly extern System.Runtime { .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A) }
.assembly extern System.Console { }
.assembly extern xunit.core { }
.assembly extern xunit.assert { }
.assembly Runtime_133581_Volatile { }

.class public auto ansi abstract sealed beforefieldinit Runtime_133581_Volatile
       extends [System.Runtime]System.Object
{
    .class nested private explicit ansi sealed beforefieldinit Big
           extends [System.Runtime]System.ValueType
    {
        .size 65600
        .field [0] public int32 Head
        .field [32768] public int32 Middle
        .field [65584] public int32 Tail
        .field [65592] public object Reference
    }

    .field private static valuetype Runtime_133581_Volatile/Big s_destination

    .method public static void TestEntryPoint() cil managed
    {
        .custom instance void [xunit.core]Xunit.FactAttribute::.ctor() = (01 00 00 00)
        .entrypoint
        .maxstack 2

        ldsflda valuetype Runtime_133581_Volatile/Big Runtime_133581_Volatile::s_destination
        call void Runtime_133581_Volatile::Copy(valuetype Runtime_133581_Volatile/Big&)

        ldc.i4.s 42
        ldsflda valuetype Runtime_133581_Volatile/Big Runtime_133581_Volatile::s_destination
        ldfld int32 Runtime_133581_Volatile/Big::Head
        call void [xunit.assert]Xunit.Assert::Equal<int32>(!!0, !!0)

        ldc.i4.s 123
        ldsflda valuetype Runtime_133581_Volatile/Big Runtime_133581_Volatile::s_destination
        ldfld int32 Runtime_133581_Volatile/Big::Middle
        call void [xunit.assert]Xunit.Assert::Equal<int32>(!!0, !!0)

        ldc.i4 456
        ldsflda valuetype Runtime_133581_Volatile/Big Runtime_133581_Volatile::s_destination
        ldfld int32 Runtime_133581_Volatile/Big::Tail
        call void [xunit.assert]Xunit.Assert::Equal<int32>(!!0, !!0)

        ldstr "payload"
        ldsflda valuetype Runtime_133581_Volatile/Big Runtime_133581_Volatile::s_destination
        ldfld object Runtime_133581_Volatile/Big::Reference
        call void [xunit.assert]Xunit.Assert::Same(object, object)

        ldstr "Runtime_133581_Volatile: PASS"
        call void [System.Console]System.Console::WriteLine(string)
        ret
    }

    // C# cannot express a volatile struct store.
    .method private static void Copy(valuetype Runtime_133581_Volatile/Big& destination)
            cil managed noinlining aggressiveoptimization
    {
        .maxstack 2
        .locals init (valuetype Runtime_133581_Volatile/Big V_0)

        ldloca.s V_0
        initobj Runtime_133581_Volatile/Big
        ldloca.s V_0
        ldc.i4.s 42
        stfld int32 Runtime_133581_Volatile/Big::Head
        ldloca.s V_0
        ldc.i4.s 123
        stfld int32 Runtime_133581_Volatile/Big::Middle
        ldloca.s V_0
        ldc.i4 456
        stfld int32 Runtime_133581_Volatile/Big::Tail
        ldloca.s V_0
        ldstr "payload"
        stfld object Runtime_133581_Volatile/Big::Reference

        ldarg.0
        ldloc.0
        volatile.
        stobj Runtime_133581_Volatile/Big
        ret
    }
}
