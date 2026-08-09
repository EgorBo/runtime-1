// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.InteropServices.Java
{
    public static partial class JavaMarshal
    {
        /// <summary>
        /// Creates a GC handle that native Java code can hold to reference a managed
        /// object. The handle prevents the object from being reclaimed while the
        /// native side holds the reference, and an opaque <paramref name="context"/>
        /// value can be associated with the handle for later retrieval.
        /// </summary>
        /// <param name="obj">The managed object to be referenced from native code.</param>
        /// <param name="context">An opaque pointer-sized value that will be associated
        /// with the handle and can be retrieved by the runtime via <see cref="GetContext(GCHandle)"/>.
        /// Callers can use this to store native-side state or identifiers alongside
        /// the handle.</param>
        /// <returns>A <see cref="GCHandle"/> that represents the allocated reference-tracking handle.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is null.</exception>
        /// <exception cref="PlatformNotSupportedException">The runtime or platform does not support Java cross-reference marshalling.</exception>
        public static unsafe GCHandle CreateReferenceTrackingHandle(object obj, void* context)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return GCHandle.FromIntPtr(RuntimeImports.RhHandleAllocCrossReference(obj, (IntPtr)context));
        }

        /// <summary>
        /// Retrieves the opaque context pointer associated with a reference-tracking
        /// GC handle previously created using <see cref="CreateReferenceTrackingHandle(object, void*)"/>.
        /// </summary>
        /// <param name="obj">The <see cref="GCHandle"/> whose context should be returned.</param>
        /// <returns>The opaque context pointer associated with the handle.</returns>
        /// <exception cref="InvalidOperationException">The provided handle is null or does not represent a reference-tracking handle.</exception>
        /// <exception cref="PlatformNotSupportedException">The runtime or platform does not support Java cross-reference marshalling.</exception>
        /// <remarks>
        /// The returned pointer is the exact value that was originally provided as
        /// the context parameter when the handle was created.
        /// </remarks>
        public static unsafe void* GetContext(GCHandle obj)
        {
            IntPtr handle = GCHandle.ToIntPtr(obj);
            if (handle == IntPtr.Zero
                || !RuntimeImports.RhHandleTryGetCrossReferenceContext(handle, out nint context))
            {
                throw new InvalidOperationException(SR.InvalidOperation_IncorrectGCHandleType);
            }

            return (void*)context;
        }
    }
}
