// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

internal static class Program
{
    private static void Main() => Console.WriteLine(Test(Guid.NewGuid()));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string Test(Guid value) => value.ToString();
}
