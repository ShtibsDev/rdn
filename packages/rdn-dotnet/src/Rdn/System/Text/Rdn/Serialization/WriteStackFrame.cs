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

        // Whether to use custom number handling.
        public RdnNumberHandling? NumberHandling;

        public void EndCollectionElement()
        {
        }

        public void EndDictionaryEntry()
        {
            PropertyState = StackFramePropertyState.None;
        }

        public void EndProperty()
        {
            RdnPropertyInfo = null!;
            RdnPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;
        }

        /// <summary>
        /// Returns the RdnTypeInfo instance for the nested value we are trying to access.
        /// </summary>
        public readonly RdnTypeInfo GetNestedRdnTypeInfo()
        {
            return RdnPropertyInfo!.RdnTypeInfo;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string DebuggerDisplay => $"ConverterStrategy.{RdnTypeInfo?.Converter.ConverterStrategy}, {RdnTypeInfo?.Type.Name}";
    }
}
