
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class Crypto
    {
        internal static unsafe bool GetRandomBytes(byte* pbBuffer, int count)
        {
            Debug.Assert(count >= 0);
            return Android_GetRandomBytes(pbBuffer, count);
        }

        [DllImport("__Internal")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern unsafe bool Android_GetRandomBytes(byte* buf, int num);
    }
}
