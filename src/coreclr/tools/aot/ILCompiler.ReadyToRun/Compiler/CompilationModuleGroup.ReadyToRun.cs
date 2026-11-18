// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis.ReadyToRun;
using Internal.ReadyToRunConstants;
using Internal.TypeSystem.Ecma;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ILCompiler
{
    partial class CompilationModuleGroup
    {
        /// <summary>
        /// If true, the type is fully contained in the current compilation group.
        /// </summary>
        public virtual bool ContainsTypeLayout(TypeDesc type) => ContainsType(type);

        /// <summary>
        /// Returns true when a given type belongs to the same version bubble as the compilation module group.
        /// By default return the same outcome as ContainsType.
        /// </summary>
        /// <param name="typeDesc">Type to check</param>
        /// <returns>True if the given type versions with the current compilation module group</returns>
        public virtual bool VersionsWithType(TypeDesc typeDesc) => ContainsType(typeDesc);

        /// <summary>
        /// Returns true when all of the tokens necessary to refer to a given type belong to the same version
        /// bubble as the compilation module group. By default return the same outcome as VersionsWithType.
        /// </summary>
        /// <param name="typeDesc">Type to check</param>
        /// <returns>True if the given type can safely be referred to within the current compilation module group</returns>
        public virtual bool VersionsWithTypeReference(TypeDesc typeDesc) => VersionsWithType(typeDesc);

        /// <summary>
        /// Returns true when a given method belongs to the same version bubble as the compilation module group.
        /// By default return the same outcome as ContainsMethodBody.
        /// </summary>
        /// <param name="methodDesc">Method to check</param>
        /// <returns>True if the given method versions with the current compilation module group</returns>
        public virtual bool VersionsWithMethodBody(MethodDesc methodDesc) => ContainsMethodBody(methodDesc, unboxingStub: false);

        /// <summary>
        /// Returns true when a given method does not belong to the same version bubble as the compilation module group,
        /// but we can support inlining it into other modules.
        /// </summary>
        /// <param name="methodDesc">Method to check</param>
        /// <returns>True if the given method versions with the current compilation module group</returns>
        public virtual bool CrossModuleInlineable(MethodDesc methodDesc) => false;


        /// <summary>
        /// Returns true when a given method does not belong to the same version bubble as the compilation module group,
        /// but we can support compiling it into the modules being compiled
        /// </summary>
        /// <param name="methodDesc">Method to check</param>
        /// <returns>True if the given method versions with the current compilation module group</returns>
        public virtual bool CrossModuleCompileable(MethodDesc methodDesc) => false;

        /// <summary>
        /// Returns true when a given module belongs to the same version bubble as the compilation module group.
        /// </summary>
        /// <param name="module">Module to check</param>
        /// <returns>True if the given module versions with the current compilation module group</returns>
        public abstract bool VersionsWithModule(ModuleDesc module);

        /// <summary>
        /// Checks if the given PInvoke method can produce a PInvoke stub in the current compilation, depending on the method's
        /// signature and the compilation policy.
        /// </summary>
        /// <param name="method">PInvoke method to check</param>
        /// <returns>Returns true if the given PInvoke method can produce a PInvoke stub in the current compilation</returns>
        public abstract bool GeneratesPInvoke(MethodDesc method);

        /// <summary>
        /// Retrieve the module-based token for a type that is not part of the version bubble of the current compilation.
        /// </summary>
        /// <param name="type">Type to get a module token for</param>
        /// <param name="token">Module-based token for the type</param>
        /// <returns>Returns true when the type was referenced by any of the input modules in the current compilation</returns>
        public abstract bool TryGetModuleTokenForExternalType(TypeDesc type, out ModuleToken token);

        /// <summary>
        /// Gets the flags to be stored in the generated ReadyToRun module header.
        /// </summary>
        public abstract ReadyToRunFlags GetReadyToRunFlags();

        /// <summary>
        /// When set to true, unconditionally add module overrides to all signatures. This is needed in composite
        /// build mode so that import cells and instance entry point table are caller module-agnostic.
        /// </summary>
        public bool EnforceOwningType(IEcmaModule module)
        {
            Debug.Assert(VersionsWithModule((ModuleDesc)module) || module is MutableModule);
            return IsCompositeBuildMode || module != CompilationModuleSet.Single();
        }

        /// <summary>
        /// Returns true when the compiler is running in composite build mode i.e. building an arbitrary number of
        /// input MSIL assemblies into a single output R2R binary.
        /// </summary>
        public abstract bool IsCompositeBuildMode { get; }

        /// <summary>
        /// Returns true when the compiler is running in large version bubble mode
        /// </summary>
        public abstract bool IsInputBubble { get; }

        /// <summary>
        /// Returns true when the base type and derived type don't reside in the same version bubble
        /// in which case the runtime aligns the field base offset.
        /// </summary>
        public abstract bool NeedsAlignmentBetweenBaseTypeAndDerived(MetadataType baseType, MetadataType derivedType);

        /// <summary>
        /// List of input modules to use for the compilation.
        /// </summary>
        public abstract IEnumerable<EcmaModule> CompilationModuleSet { get; }

        /// <summary>
        /// Returns true if a cast from an object of exact type <paramref name="fromType"/> to
        /// <paramref name="toType"/> that fails now also fails in every later version of its dependencies.
        /// </summary>
        public bool IsNegativeCastResultVersionResilient(TypeDesc fromType, TypeDesc toType)
        {
            if (fromType.HasTypeEquivalence || toType.HasTypeEquivalence)
                return false;

            // A value type can gain interfaces, but its other cast targets are fixed.
            if (fromType.IsValueType && !toType.IsInterface)
                return true;

            // Otherwise every type that decides the cast must version with this code. Array and variant casts
            // also depend on element types and generic arguments, and COM objects can be cast to interfaces
            // through QueryInterface.
            if (fromType is not DefType || toType is not DefType || toType.HasVariance)
                return false;

            if (!VersionsWithType(toType))
                return false;

            for (TypeDesc type = fromType; type is not null; type = type.BaseType)
            {
                if (type.IsComImport || !VersionsWithType(type))
                    return false;
            }

            foreach (DefType iface in fromType.RuntimeInterfaces)
            {
                if (!VersionsWithType(iface))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if a call to a method on the variant interface <paramref name="declInterface"/> on an object
        /// of exact type <paramref name="implType"/> resolves to the same implementation in every later version of
        /// its dependencies.
        /// </summary>
        public bool IsVariantInterfaceDispatchVersionResilient(TypeDesc implType, DefType declInterface)
        {
            Debug.Assert(declInterface.IsInterface && declInterface.HasVariance);

            // Variant dispatch picks the first interface on implType that is castable to declInterface. A positive
            // cast result can't change across versions, but a negative one can.
            bool implementsDeclInterface = false;
            foreach (DefType iface in implType.RuntimeInterfaces)
            {
                if (iface == declInterface)
                {
                    implementsDeclInterface = true;
                    continue;
                }

                if (iface.HasSameTypeDefinition(declInterface) && !iface.CanCastTo(declInterface) &&
                    !IsNegativeVariantCastVersionResilient(iface, declInterface))
                {
                    return false;
                }
            }

            if (implementsDeclInterface || implType.IsValueType)
                return true;

            // A base type outside the version bubble can gain a matching interface.
            for (TypeDesc type = implType.BaseType; type is not null && !type.IsObject; type = type.BaseType)
            {
                if (!VersionsWithType(type.GetTypeDefinition()))
                    return false;
            }

            return true;
        }

        // Returns true if the failing cast from iface to declInterface (instantiations of the same variant
        // interface) also fails in every later version: some type argument has to mismatch in a stable way.
        private bool IsNegativeVariantCastVersionResilient(DefType iface, DefType declInterface)
        {
            Instantiation typicalInstantiation = iface.GetTypeDefinition().Instantiation;
            for (int i = 0; i < typicalInstantiation.Length; i++)
            {
                TypeDesc fromArg = iface.Instantiation[i];
                TypeDesc toArg = declInterface.Instantiation[i];
                if (fromArg == toArg)
                    continue;

                GenericVariance variance = ((GenericParameterDesc)typicalInstantiation[i]).Variance;
                if (variance == GenericVariance.None)
                    return true;

                // Variance only applies to reference types.
                if (fromArg.IsValueType || toArg.IsValueType)
                    return true;

                if (variance == GenericVariance.Contravariant)
                    (fromArg, toArg) = (toArg, fromArg);

                if (!fromArg.CanCastTo(toArg) && IsNegativeCastResultVersionResilient(fromArg, toArg))
                    return true;
            }

            return false;
        }
    }
}
