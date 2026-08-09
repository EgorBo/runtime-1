// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace System.Runtime.InteropServices.Java
{
    /// <summary>
    /// Provides helpers to create and manage GC handles used for tracking references
    /// between the managed runtime and a Java VM.
    /// </summary>
    /// <remarks>
    /// The APIs provided by this type allow managed objects to be referenced from native
    /// Java code so the runtime can participate in cross-reference processing and
    /// correctly control object lifetime across the managed/native boundary.
    /// </remarks>
    [CLSCompliant(false)]
    [SupportedOSPlatform("android")]
    public static partial class JavaMarshal
    {
        /// <summary>
        /// Initializes the Java marshal subsystem with a callback used when the runtime
        /// needs to mark managed objects that are referenced from Java during cross-
        /// reference processing.
        /// </summary>
        /// <param name="markCrossReferences">A pointer to an unmanaged callback that
        /// will be invoked to enumerate or mark managed objects referenced from Java
        /// during a cross-reference sweep. The callback is expected to accept a
        /// <see cref="MarkCrossReferencesArgs"/> pointer describing the objects to mark.</param>
        /// <exception cref="ArgumentNullException"><paramref name="markCrossReferences"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The subsystem cannot be initialized or is reinitialized.</exception>
        /// <exception cref="PlatformNotSupportedException">The runtime or platform does not support Java cross-reference marshalling.</exception>
        /// <remarks>
        /// Only a single initialization is supported for the process. The runtime
        /// stores the provided function pointer and will invoke it from internal
        /// runtime code when cross-reference marking is required.
        /// Additionally, this callback must be implemented in unmanaged code.
        /// </remarks>
        public static unsafe void Initialize(delegate* unmanaged<MarkCrossReferencesArgs*, void> markCrossReferences)
        {
            ArgumentNullException.ThrowIfNull(markCrossReferences);

            if (!InitializeInternal((IntPtr)markCrossReferences))
            {
                throw new InvalidOperationException(SR.InvalidOperation_ReinitializeJavaMarshal);
            }
        }

        /// <summary>
        /// Completes processing of cross references after the runtime has invoked the
        /// callback provided to <see cref="Initialize" />. This notifies the runtime of
        /// handles that are no longer reachable from native Java code so the runtime
        /// can release or update them accordingly.
        /// </summary>
        /// <param name="crossReferences">A pointer to the structure containing cross-reference information produced during marking.</param>
        /// <param name="unreachableObjectHandles">A span of <see cref="GCHandle"/> values that were determined to be unreachable from the native side.</param>
        /// <exception cref="PlatformNotSupportedException">The runtime or platform does not support Java cross-reference marshalling.</exception>
        public static unsafe void FinishCrossReferenceProcessing(
            MarkCrossReferencesArgs* crossReferences,
            ReadOnlySpan<GCHandle> unreachableObjectHandles)
        {
            fixed (GCHandle* pHandles = unreachableObjectHandles)
            {
                FinishCrossReferenceProcessingCore(
                    crossReferences,
                    (nuint)unreachableObjectHandles.Length,
                    pHandles);
            }
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "JavaMarshal_Initialize")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool InitializeInternal(IntPtr markCrossReferences);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "JavaMarshal_FinishCrossReferenceProcessing")]
        private static unsafe partial void FinishCrossReferenceProcessingCore(
            MarkCrossReferencesArgs* crossReferences,
            nuint numHandles,
            GCHandle* unreachableObjectHandles);
    }
}
