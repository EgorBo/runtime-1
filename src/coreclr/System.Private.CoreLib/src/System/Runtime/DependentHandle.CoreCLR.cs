// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime
{
    public partial struct DependentHandle
    {
        private static IntPtr AllocateHandle(object? target, object? dependent)
        {
            IntPtr handle = InternalAlloc(target, dependent);
            if (handle == 0)
                handle = InternalAllocWithGCTransition(target, dependent);
            return handle;
        }

        private static void FreeHandle(IntPtr dependentHandle)
        {
            if (!InternalFree(dependentHandle))
            {
                InternalFreeWithGCTransition(dependentHandle);
            }
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern IntPtr InternalAlloc(object? target, object? dependent);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IntPtr InternalAllocWithGCTransition(object? target, object? dependent)
            => _InternalAllocWithGCTransition(ObjectHandleOnStack.Create(ref target), ObjectHandleOnStack.Create(ref dependent));

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "DependentHandle_InternalAllocWithGCTransition")]
        private static partial IntPtr _InternalAllocWithGCTransition(ObjectHandleOnStack target, ObjectHandleOnStack dependent);

#if DEBUG
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern object? InternalGetTarget(IntPtr dependentHandle);
#else
        // This optimization is the same that is used in GCHandle in RELEASE mode.
        // This is not used in DEBUG builds as the runtime performs additional checks.
        // The logic below is the inlined copy of ObjectFromHandle in the unmanaged runtime.
        private static unsafe object? InternalGetTarget(IntPtr dependentHandle) => *(object*)dependentHandle;
#endif

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern object? InternalGetDependent(IntPtr dependentHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern object? InternalGetTargetAndDependent(IntPtr dependentHandle, out object? dependent);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void InternalSetDependent(IntPtr dependentHandle, object? dependent);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void InternalSetTargetToNull(IntPtr dependentHandle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern bool InternalFree(IntPtr dependentHandle);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InternalFreeWithGCTransition(IntPtr dependentHandle)
            => _InternalFreeWithGCTransition(dependentHandle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "DependentHandle_InternalFreeWithGCTransition")]
        private static partial void _InternalFreeWithGCTransition(IntPtr dependentHandle);
    }
}
