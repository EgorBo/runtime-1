// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.TypeSystem;

using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

namespace ILCompiler
{
    /// <summary>
    /// Reports whether a RyuJIT discovery scan expanded its generic dictionary layouts.
    /// </summary>
    public interface IRyuJitILScanResults
    {
        bool HasNewGenericDictionaryEntries { get; }
        IReadOnlyCollection<TypeSystemEntity> ChangedGenericDictionaryEntities { get; }
        IReadOnlyCollection<TypeSystemEntity> ForcedLazyDictionaryOwners { get; }
    }

    internal sealed class RyuJitILScannerBuilder : IILScannerBuilder
    {
        private readonly CompilerTypeSystemContext _context;
        private readonly CompilationModuleGroup _compilationGroup;
        private readonly NameMangler _nameMangler;
        private readonly ILProvider _ilProvider;
        private readonly PreinitializationManager _preinitializationManager;
        private readonly InstructionSetSupport _instructionSetSupport;
        private readonly ProfileDataManager _profileDataManager;
        private readonly DevirtualizationManager _devirtualizationManager;
        private readonly IInliningPolicy _inliningPolicy;
        private readonly VTableSliceProvider _vtableSliceProvider;
        private readonly DictionaryLayoutProvider _dictionaryLayoutProvider;
        private readonly InlinedThreadStatics _inlinedThreadStatics;
        private readonly MethodImportationErrorProvider _methodImportationErrorProvider;
        private readonly ReadOnlyFieldPolicy _readOnlyFieldPolicy;
        private readonly RyuJitCompilationOptions _compilationOptions;
        private readonly MethodBodyFoldingMode _methodBodyFolding;
        private readonly bool _allowNewGenericDictionaryEntries;

        private Logger _logger = Logger.Null;
        private DependencyTrackingLevel _dependencyTrackingLevel = DependencyTrackingLevel.None;
        private IEnumerable<ICompilationRootProvider> _compilationRoots = Array.Empty<ICompilationRootProvider>();
        private MetadataManager _metadataManager;
        private InteropStubManager _interopStubManager = new EmptyInteropStubManager();
        private TypeMapManager _typeMapManager = new UsageBasedTypeMapManager(TypeMapMetadata.Empty);
        private int _parallelism = -1;

        internal RyuJitILScannerBuilder(
            CompilerTypeSystemContext context,
            CompilationModuleGroup compilationGroup,
            NameMangler nameMangler,
            ILProvider ilProvider,
            PreinitializationManager preinitializationManager,
            InstructionSetSupport instructionSetSupport,
            ProfileDataManager profileDataManager,
            DevirtualizationManager devirtualizationManager,
            IInliningPolicy inliningPolicy,
            VTableSliceProvider vtableSliceProvider,
            DictionaryLayoutProvider dictionaryLayoutProvider,
            InlinedThreadStatics inlinedThreadStatics,
            MethodImportationErrorProvider methodImportationErrorProvider,
            ReadOnlyFieldPolicy readOnlyFieldPolicy,
            RyuJitCompilationOptions compilationOptions,
            MethodBodyFoldingMode methodBodyFolding,
            bool allowNewGenericDictionaryEntries)
        {
            _context = context;
            _compilationGroup = compilationGroup;
            _nameMangler = nameMangler;
            _ilProvider = ilProvider;
            _preinitializationManager = preinitializationManager;
            _instructionSetSupport = instructionSetSupport;
            _profileDataManager = profileDataManager;
            _devirtualizationManager = devirtualizationManager;
            _inliningPolicy = inliningPolicy;
            _vtableSliceProvider = vtableSliceProvider;
            _dictionaryLayoutProvider = dictionaryLayoutProvider;
            _inlinedThreadStatics = inlinedThreadStatics;
            _methodImportationErrorProvider = methodImportationErrorProvider;
            _readOnlyFieldPolicy = readOnlyFieldPolicy;
            _compilationOptions = compilationOptions;
            _methodBodyFolding = methodBodyFolding;
            _allowNewGenericDictionaryEntries = allowNewGenericDictionaryEntries;
            _metadataManager = new AnalysisBasedMetadataManager(context);
        }

        public IILScannerBuilder UseDependencyTracking(DependencyTrackingLevel trackingLevel)
        {
            _dependencyTrackingLevel = trackingLevel;
            return this;
        }

        public IILScannerBuilder UseCompilationRoots(IEnumerable<ICompilationRootProvider> compilationRoots)
        {
            _compilationRoots = compilationRoots;
            return this;
        }

        public IILScannerBuilder UseMetadataManager(MetadataManager metadataManager)
        {
            _metadataManager = metadataManager;
            return this;
        }

        public IILScannerBuilder UseInteropStubManager(InteropStubManager interopStubManager)
        {
            _interopStubManager = interopStubManager;
            return this;
        }

        public IILScannerBuilder UseTypeMapManager(TypeMapManager typeMapManager)
        {
            _typeMapManager = typeMapManager;
            return this;
        }

        public IILScannerBuilder UseParallelism(int parallelism)
        {
            _parallelism = parallelism;
            return this;
        }

        public IILScannerBuilder UseLogger(Logger logger)
        {
            _logger = logger;
            return this;
        }

        public IILScanner ToILScanner()
        {
            MethodBodyDeduplicator methodBodyDeduplicator = RyuJitCompilationBuilder.CreateMethodBodyDeduplicator(_methodBodyFolding);
            VTableSliceProvider vtableSliceProvider = _allowNewGenericDictionaryEntries ?
                new LazyVTableSliceProvider() :
                _vtableSliceProvider;
            RyuJitScannerDictionaryLayoutProvider collectingDictionaryLayoutProvider = null;
            DictionaryLayoutProvider dictionaryLayoutProvider = _dictionaryLayoutProvider;
            if (_allowNewGenericDictionaryEntries)
            {
                collectingDictionaryLayoutProvider = new RyuJitScannerDictionaryLayoutProvider(_dictionaryLayoutProvider);
                dictionaryLayoutProvider = collectingDictionaryLayoutProvider;
            }

            var nodeFactory = new RyuJitNodeFactory(
                _context,
                _compilationGroup,
                _metadataManager,
                _interopStubManager,
                _nameMangler,
                vtableSliceProvider,
                dictionaryLayoutProvider,
                _inlinedThreadStatics,
                _preinitializationManager,
                _devirtualizationManager,
                ObjectDataInterner.Null,
                methodBodyDeduplicator,
                _typeMapManager,
                relocationsOnly: true);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(nodeFactory);

            return new RyuJitILScanner(
                graph,
                nodeFactory,
                [.._compilationRoots, _typeMapManager],
                _ilProvider,
                _logger,
                _inliningPolicy,
                _instructionSetSupport,
                _profileDataManager,
                _methodImportationErrorProvider,
                _readOnlyFieldPolicy,
                _compilationOptions,
                _parallelism,
                collectingDictionaryLayoutProvider is null ? null : collectingDictionaryLayoutProvider.GetChangedEntities);
        }

        private DependencyAnalyzerBase<NodeFactory> CreateDependencyGraph(NodeFactory nodeFactory)
        {
            return _dependencyTrackingLevel switch
            {
                DependencyTrackingLevel.None when EventSourceLogStrategy<NodeFactory>.IsEventSourceEnabled =>
                    new DependencyAnalyzer<EventSourceLogStrategy<NodeFactory>, NodeFactory>(nodeFactory, null),
                DependencyTrackingLevel.None =>
                    new DependencyAnalyzer<NoLogStrategy<NodeFactory>, NodeFactory>(nodeFactory, null),
                DependencyTrackingLevel.First =>
                    new DependencyAnalyzer<FirstMarkLogStrategy<NodeFactory>, NodeFactory>(nodeFactory, null),
                DependencyTrackingLevel.All =>
                    new DependencyAnalyzer<FullGraphLogStrategy<NodeFactory>, NodeFactory>(nodeFactory, null),
                _ => throw new InvalidOperationException(),
            };
        }

        private sealed class RyuJitScannerDictionaryLayoutProvider : DictionaryLayoutProvider
        {
            private readonly DictionaryLayoutProvider _seed;
            private readonly ConcurrentDictionary<TypeSystemEntity, DictionaryLayoutNode> _layouts = new();
            private readonly ConcurrentDictionary<TypeSystemEntity, byte> _changedEntities = new();

            public RyuJitScannerDictionaryLayoutProvider(DictionaryLayoutProvider seed)
            {
                _seed = seed;
            }

            public override DictionaryLayoutNode GetLayout(TypeSystemEntity methodOrType)
            {
                return _layouts.GetOrAdd(methodOrType, CreateLayout);
            }

            public TypeSystemEntity[] GetChangedEntities()
            {
                TypeSystemEntity[] entities = new TypeSystemEntity[_changedEntities.Count];
                _changedEntities.Keys.CopyTo(entities, 0);
                Array.Sort(entities, static (x, y) => StringComparer.Ordinal.Compare(x.ToString(), y.ToString()));
                return entities;
            }

            private DictionaryLayoutNode CreateLayout(TypeSystemEntity methodOrType)
            {
                if (_seed.TryGetLayout(methodOrType, out DictionaryLayoutNode layout))
                {
                    // Forced-lazy layouts are intentionally outside fixed-point convergence.
                    if (!layout.HasFixedSlots)
                        return layout;

                    return new RyuJitScannerDictionaryLayoutNode(methodOrType, layout.Entries, RecordChange);
                }

                return new RyuJitScannerDictionaryLayoutNode(methodOrType, Array.Empty<GenericLookupResult>(), RecordChange);
            }

            private void RecordChange(TypeSystemEntity entity)
            {
                _changedEntities.TryAdd(entity, 0);
            }
        }

        private sealed class RyuJitScannerDictionaryLayoutNode : DictionaryLayoutNode
        {
            private readonly object _lock = new();
            private readonly List<GenericLookupResult> _entries = new();
            private readonly Dictionary<GenericLookupResult, int> _slots = new();
            private readonly Action<TypeSystemEntity> _recordChange;

            public RyuJitScannerDictionaryLayoutNode(TypeSystemEntity methodOrType, IEnumerable<GenericLookupResult> entries, Action<TypeSystemEntity> recordChange)
                : base(methodOrType)
            {
                _recordChange = recordChange;

                foreach (GenericLookupResult entry in entries)
                    AddEntry(entry, recordChange: false);
            }

            public override bool HasFixedSlots => true;

            public override bool HasUnfixedSlots => true;

            public override bool IsEmpty
            {
                get
                {
                    lock (_lock)
                        return _entries.Count == 0;
                }
            }

            public override IEnumerable<GenericLookupResult> Entries
            {
                get
                {
                    GenericLookupResult[] entries;
                    lock (_lock)
                        entries = _entries.ToArray();

                    var comparer = new GenericLookupResult.Comparer(TypeSystemComparer.Instance);
                    Array.Sort(entries, comparer.Compare);
                    return entries;
                }
            }

            public override void EnsureEntry(GenericLookupResult entry)
            {
                lock (_lock)
                    AddEntry(entry, recordChange: true);
            }

            public override bool TryGetSlotForEntry(GenericLookupResult entry, out int slot)
            {
                lock (_lock)
                    slot = AddEntry(entry, recordChange: true);

                return true;
            }

            private int AddEntry(GenericLookupResult entry, bool recordChange)
            {
                if (_slots.TryGetValue(entry, out int slot))
                    return slot;

                // Discovery scan output is discarded. The sorted Entries collection becomes
                // the fixed layout used by the verification scan and final compilation.
                slot = _entries.Count;
                _entries.Add(entry);
                _slots.Add(entry, slot);
                if (recordChange)
                    _recordChange(OwningMethodOrType);
                return slot;
            }
        }
    }

    internal sealed class RyuJitILScanner : RyuJitCompilation, IILScanner
    {
        private readonly Func<TypeSystemEntity[]> _getChangedDictionaryEntities;

        internal RyuJitILScanner(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            Logger logger,
            IInliningPolicy inliningPolicy,
            InstructionSetSupport instructionSetSupport,
            ProfileDataManager profileDataManager,
            MethodImportationErrorProvider errorProvider,
            ReadOnlyFieldPolicy readOnlyFieldPolicy,
            RyuJitCompilationOptions options,
            int parallelism,
            Func<TypeSystemEntity[]> getChangedDictionaryEntities)
            : base(
                  dependencyGraph,
                  nodeFactory,
                  roots,
                  ilProvider,
                  new NullDebugInformationProvider(),
                  logger,
                  inliningPolicy,
                  instructionSetSupport,
                  profileDataManager,
                  errorProvider,
                  readOnlyFieldPolicy,
                  options,
                  default,
                  default,
                  parallelism,
                  orderFile: null)
        {
            _getChangedDictionaryEntities = getChangedDictionaryEntities;
        }

        public ILScanResults Scan()
        {
            _dependencyGraph.ComputeMarkedNodes();
            _nodeFactory.SetMarkingComplete();
            _dependencyGraph.ComputeDependencyRoutine -= ComputeDependencyNodeDependencies;
            return new RyuJitILScanResults(_dependencyGraph, _nodeFactory, _getChangedDictionaryEntities?.Invoke() ?? Array.Empty<TypeSystemEntity>());
        }

        protected override void ReportCompilationError(MethodDesc method, TypeSystemException exception)
        {
        }

        internal override void AddDependenciesDueToGenericLookup(ref DependencyList dependencies, MethodDesc contextMethod, GenericLookupResult lookupSignature)
        {
            dependencies ??= new DependencyList();
            TypeSystemEntity dictionaryOwner;
            if (contextMethod.RequiresInstMethodDescArg())
            {
                dictionaryOwner = contextMethod;
            }
            else
            {
                dictionaryOwner = contextMethod.OwningType;
                dependencies.Add(_nodeFactory.VTable(contextMethod.OwningType), "Owning type vtable");
            }

            dependencies.Add(_nodeFactory.GenericDictionaryLayout(dictionaryOwner), "Generic dictionary layout");
            foreach (DependencyNodeCore<NodeFactory> dependency in lookupSignature.NonRelocDependenciesFromUsage(_nodeFactory))
                dependencies.Add(dependency, "Generic lookup");
        }

        private sealed class RyuJitILScanResults : ILScanResults, IRyuJitILScanResults
        {
            internal RyuJitILScanResults(DependencyAnalyzerBase<NodeFactory> graph, NodeFactory factory, TypeSystemEntity[] changedGenericDictionaryEntities)
                : base(graph, factory)
            {
                ChangedGenericDictionaryEntities = changedGenericDictionaryEntities;
                ForcedLazyDictionaryOwners = GetForcedLazyDictionaryOwners();
            }

            public bool HasNewGenericDictionaryEntries => ChangedGenericDictionaryEntities.Count != 0;
            public IReadOnlyCollection<TypeSystemEntity> ChangedGenericDictionaryEntities { get; }
            public IReadOnlyCollection<TypeSystemEntity> ForcedLazyDictionaryOwners { get; }
        }
    }
}
