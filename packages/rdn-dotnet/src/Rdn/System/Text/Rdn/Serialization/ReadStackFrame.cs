// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal struct ReadStackFrame
    {
        // Current property values.
        public RdnPropertyInfo? RdnPropertyInfo;
        public StackFramePropertyState PropertyState;
        public bool UseExtensionProperty;

        // Support RDN Path on exceptions and non-string Dictionary keys.
        // This is Utf8 since we don't want to convert to string until an exception is thrown.
        // For dictionary keys we don't want to convert to TKey until we have both key and value when parsing the dictionary elements on stream cases.
        public byte[]? RdnPropertyName;
        public string? RdnPropertyNameAsString; // This is used for string dictionary keys and re-entry cases that specify a property name.

        // Stores the non-string dictionary keys for continuation.
        public object? DictionaryKey;

        /// <summary>
        /// Records the Utf8RdnReader Depth at the start of the current value.
        /// </summary>
        public int OriginalDepth;
#if DEBUG
        /// <summary>
        /// Records the Utf8RdnReader TokenType at the start of the current value.
        /// Only used to validate debug builds.
        /// </summary>
        public RdnTokenType OriginalTokenType;
#endif

        // Current object (POCO or IEnumerable).
        public object? ReturnValue; // The current return value used for re-entry.
        public RdnTypeInfo RdnTypeInfo;
        public StackFrameObjectState ObjectState; // State tracking the current object.

        // Current object can contain metadata
        public bool CanContainMetadata;
        public MetadataPropertyName LatestMetadataPropertyName;
        public MetadataPropertyName MetadataPropertyNames;

        // Serialization state for value serialized by the current frame.
        public PolymorphicSerializationState PolymorphicSerializationState;

        // Holds any entered polymorphic RdnTypeInfo metadata.
        public RdnTypeInfo? PolymorphicRdnTypeInfo;

        // Gets the initial RdnTypeInfo metadata used when deserializing the current value.
        public RdnTypeInfo BaseRdnTypeInfo
            => PolymorphicSerializationState == PolymorphicSerializationState.PolymorphicReEntryStarted
                ? PolymorphicRdnTypeInfo!
                : RdnTypeInfo;

        // For performance, we order the properties by the first deserialize and PropertyIndex helps find the right slot quicker.
        public int PropertyIndex;

        // Tracks newly encounentered UTF-8 encoded properties during the current deserialization, to be appended to the cache.
        public PropertyRefCacheBuilder? PropertyRefCacheBuilder;

        // Holds relevant state when deserializing objects with parameterized constructors.
        public ArgumentState? CtorArgumentState;

        // Whether to use custom number handling.
        public RdnNumberHandling? NumberHandling;

        // Represents known (non-extension) properties which have value assigned.
        // Each bit corresponds to a property.
        // False means that property is not set (not yet occurred in the payload).
        // Length of the BitArray is equal to number of non-extension properties.
        // Every RdnPropertyInfo has PropertyIndex property which maps to an index in this BitArray.
        public BitArray? AssignedProperties;

        // Tracks state related to property population.
        public bool HasParentObject;
        public bool IsPopulating;

        // Tracks whether we are reading a dictionary from RDN Map format (vs RDN Object format).
        public bool IsReadingMapFormat;

        public void EndConstructorParameter()
        {
            CtorArgumentState!.RdnParameterInfo = null;
            RdnPropertyName = null;
            PropertyState = StackFramePropertyState.None;
        }

        public void EndProperty()
        {
            RdnPropertyInfo = null!;
            RdnPropertyName = null;
            RdnPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;

            // No need to clear these since they are overwritten each time:
            //  NumberHandling
            //  UseExtensionProperty
        }

        public void EndElement()
        {
            RdnPropertyNameAsString = null;
            PropertyState = StackFramePropertyState.None;
        }

        /// <summary>
        /// Is the current object a Dictionary.
        /// </summary>
        public bool IsProcessingDictionary()
        {
            return RdnTypeInfo.Kind is RdnTypeInfoKind.Dictionary;
        }

        /// <summary>
        /// Is the current object an Enumerable.
        /// </summary>
        public bool IsProcessingEnumerable()
        {
            return RdnTypeInfo.Kind is RdnTypeInfoKind.Enumerable;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkPropertyAsRead(RdnPropertyInfo propertyInfo)
        {
            if (AssignedProperties is { })
            {
                if (!propertyInfo.Options.AllowDuplicateProperties)
                {
                    if (AssignedProperties[propertyInfo.PropertyIndex])
                    {
                        ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(propertyInfo);
                    }
                }

                AssignedProperties[propertyInfo.PropertyIndex] = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitializePropertiesValidationState(RdnTypeInfo typeInfo)
        {
            Debug.Assert(AssignedProperties is null);

            if (typeInfo.ShouldTrackRequiredProperties || !typeInfo.Options.AllowDuplicateProperties)
            {
                // This may be slightly larger than required (e.g. if there's an extension property)
                AssignedProperties = new BitArray(typeInfo.Properties.Count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ValidateAllRequiredPropertiesAreRead(RdnTypeInfo typeInfo)
        {
            if (typeInfo.ShouldTrackRequiredProperties)
            {
                Debug.Assert(AssignedProperties is not null);
                Debug.Assert(typeInfo.OptionalPropertiesMask is not null);

                // All properties must be either assigned or optional
                BitArray assignedOrNotRequiredPropertiesSet = AssignedProperties.Or(typeInfo.OptionalPropertiesMask);

                if (!assignedOrNotRequiredPropertiesSet.HasAllSet())
                {
                    ThrowHelper.ThrowRdnException_RdnRequiredPropertyMissing(typeInfo, assignedOrNotRequiredPropertiesSet);
                }
            }

            AssignedProperties = null;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"ConverterStrategy.{RdnTypeInfo?.Converter.ConverterStrategy}, {RdnTypeInfo?.Type.Name}";
    }
}
