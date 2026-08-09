// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime
{
    public partial struct DependentHandle
    {
        private static IntPtr AllocateHandle(object? target, object? dependent)
            => RuntimeImports.RhHandleAllocDependent(target, dependent);

        private static void FreeHandle(IntPtr dependentHandle)
            => RuntimeImports.RhHandleFree(dependentHandle);

        private static object? InternalGetTarget(IntPtr dependentHandle)
            => RuntimeImports.RhHandleGet(dependentHandle);

        private static object? InternalGetDependent(IntPtr dependentHandle)
        {
            RuntimeImports.RhHandleGetDependent(dependentHandle, out object? dependent);
            return dependent;
        }

        private static object? InternalGetTargetAndDependent(IntPtr dependentHandle, out object? dependent)
            => RuntimeImports.RhHandleGetDependent(dependentHandle, out dependent);

        private static void InternalSetDependent(IntPtr dependentHandle, object? dependent)
            => RuntimeImports.RhHandleSetDependentSecondary(dependentHandle, dependent);

        private static void InternalSetTargetToNull(IntPtr dependentHandle)
            => RuntimeImports.RhHandleSet(dependentHandle, null);
    }
}
