// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal struct WriteStackFrame
    {
        /// <summary>
        /// The enumerator for resumable collections.
        /// </summary>
        public IEnumerator? CollectionEnumerator;

        /// <summary>
        /// The enumerator for resumable async disposables.
        /// </summary>
        public IAsyncDisposable? AsyncDisposable;

        /// <summary>
        /// The current stackframe has suspended serialization due to a pending task,
        /// stored in the <see cref="WriteStack.PendingTask"/> property.
        /// </summary>
        public bool AsyncEnumeratorIsPendingCompletion;

        /// <summary>
        /// The original RdnPropertyInfo that is not changed. It contains all properties.
        /// </summary>
        /// <remarks>
        /// For objects, it is either the actual (real) RdnPropertyInfo or the <see cref="RdnTypeInfo.PropertyInfoForTypeInfo"/> for the class.
        /// For collections, it is the <see cref="RdnTypeInfo.PropertyInfoForTypeInfo"/> for the class and current element.
        /// </remarks>
        public RdnPropertyInfo? RdnPropertyInfo;

        /// <summary>
        /// Used when processing extension data dictionaries.
        /// </summary>
        public bool IsWritingExtensionDataProperty;

        /// <summary>
        /// The class (POCO or IEnumerable) that is being populated.
        /// </summary>
        public RdnTypeInfo RdnTypeInfo;

        /// <summary>
        /// Validation state for a class.
        /// </summary>
        public int OriginalDepth;

        // Class-level state for collections.
        public bool ProcessedStartToken;
        public bool ProcessedEndToken;

        /// <summary>
        /// Property or Element state.
        /// </summary>
        public StackFramePropertyState PropertyState;

        /// <summary>
        /// The enumerator index for resumable collections.
        /// </summary>
        public int EnumeratorIndex;

        // This is used for re-entry cases for exception handling.
        public string? RdnPropertyNameAsString;

        // Preserve Reference
        public MetadataPropertyName MetadataPropertyName;

        // Serialization state for the child value serialized by the current frame.
        public PolymorphicSerializationState PolymorphicSerializationState;
        // Holds the entered polymorphic type info and acts as an LRU cache for element/field serializations.
        public RdnTypeInfo? PolymorphicTypeInfo;

        // Whether to use custom number handling.
        public RdnNumberHandling? NumberHandling;

        public bool IsPushedReferenceForCycleDetection;

        public void EndCollectionElement()
        {
            PolymorphicSerializationState = PolymorphicSerializationState.None;
        }

        public void EndDictionaryEntry()
        {
            PropertyState = StackFramePropertyState.None;
            PolymorphicSerializationState = PolymorphicSerializationState.None;
        }

        public void EndProperty()
        {
            RdnPropertyInfo = null!;
            RdnPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;
            PolymorphicSerializationState = PolymorphicSerializationState.None;
        }

        /// <summary>
        /// Returns the RdnTypeInfo instance for the nested value we are trying to access.
        /// </summary>
        public readonly RdnTypeInfo GetNestedRdnTypeInfo()
        {
            return PolymorphicSerializationState is PolymorphicSerializationState.PolymorphicReEntryStarted
                ? PolymorphicTypeInfo!
                : RdnPropertyInfo!.RdnTypeInfo;
        }

        /// <summary>
        /// Configures the next stack frame for a polymorphic converter.
        /// </summary>
        public RdnTypeInfo InitializePolymorphicReEntry(Type runtimeType, RdnSerializerOptions options)
        {
            Debug.Assert(PolymorphicSerializationState == PolymorphicSerializationState.None);

            // For perf, avoid the dictionary lookup in GetTypeInfoInternal() for every element of a collection
            // if the current element is the same type as the previous element.
            if (PolymorphicTypeInfo?.Type != runtimeType)
            {
                // To determine the contract for an object value:
                // 1. Find the RdnTypeInfo for the runtime type with fallback to the nearest ancestor, if not available.
                // 2. If the resolved type is deriving from a polymorphic type, use the contract of the polymorphic type instead.
                RdnTypeInfo typeInfo = options.GetTypeInfoInternal(runtimeType, fallBackToNearestAncestorType: true);
                PolymorphicTypeInfo = typeInfo.AncestorPolymorphicType ?? typeInfo;
            }

            PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            return PolymorphicTypeInfo;
        }

        /// <summary>
        /// Configures the next stack frame for a polymorphic converter.
        /// </summary>
        public RdnConverter InitializePolymorphicReEntry(RdnTypeInfo derivedRdnTypeInfo)
        {
            Debug.Assert(PolymorphicSerializationState is PolymorphicSerializationState.None or PolymorphicSerializationState.PolymorphicReEntryStarted);

            PolymorphicTypeInfo = derivedRdnTypeInfo;
            PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            return derivedRdnTypeInfo.Converter;
        }

        /// <summary>
        /// Configures the next frame for a continuation of a polymorphic converter.
        /// </summary>
        public RdnConverter ResumePolymorphicReEntry()
        {
            Debug.Assert(PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntrySuspended);
            Debug.Assert(PolymorphicTypeInfo is not null);
            PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryStarted;
            return PolymorphicTypeInfo.Converter;
        }

        /// <summary>
        /// Updates frame state after a polymorphic converter has returned.
        /// </summary>
        public void ExitPolymorphicConverter(bool success)
        {
            PolymorphicSerializationState = success ? PolymorphicSerializationState.None : PolymorphicSerializationState.PolymorphicReEntrySuspended;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string DebuggerDisplay => $"ConverterStrategy.{RdnTypeInfo?.Converter.ConverterStrategy}, {RdnTypeInfo?.Type.Name}";
    }
}
