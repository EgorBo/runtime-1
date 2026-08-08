// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public static class StructArgVectorHoming
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Format(Guid value)
    {
        // X64:     movq
        // X64:     pinsrq
        // X64:     movups   xmmword ptr
        // X64-NOT: mov      qword ptr

        Span<byte> destination = stackalloc byte[36];
        return value.TryFormat(destination, out int bytesWritten) ? bytesWritten : 0;
    }

    [Fact]
    public static int TestEntryPoint()
    {
        Guid value = new("00112233-4455-6677-8899-aabbccddeeff");
        return Format(value) == 36 ? 100 : 101;
    }
}
