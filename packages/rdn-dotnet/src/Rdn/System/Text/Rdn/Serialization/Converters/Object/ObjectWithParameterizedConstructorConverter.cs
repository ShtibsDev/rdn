// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Rdn.Serialization.Metadata;

using FoundProperty = System.ValueTuple<Rdn.Serialization.Metadata.RdnPropertyInfo, Rdn.RdnReaderState, long, byte[]?, string?>;
using FoundPropertyAsync = System.ValueTuple<Rdn.Serialization.Metadata.RdnPropertyInfo, object?, string?>;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>RdnObjectConverter{T}</cref> that supports the deserialization
    /// of RDN objects using parameterized constructors.
    /// </summary>
    internal abstract partial class ObjectWithParameterizedConstructorConverter<T> : ObjectDefaultConverter<T> where T : notnull
    {
        internal sealed override bool ConstructorIsParameterized => true;

        internal sealed override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, [MaybeNullWhen(false)] out T value)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;

            if (!rdnTypeInfo.UsesParameterizedConstructor || state.Current.IsPopulating)
            {
                // Fall back to default object converter in following cases:
                // - if user configuration has invalidated the parameterized constructor
                // - we're continuing populating an object.
                return base.OnTryRead(ref reader, typeToConvert, options, ref state, out value);
            }

            object obj;
            ArgumentState argumentState = state.Current.CtorArgumentState!;

            if (!state.SupportContinuation && !state.Current.CanContainMetadata)
            {
                // Fast path that avoids maintaining state variables.

                if (reader.TokenType != RdnTokenType.StartObject)
                {
                    ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                }

                if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                {
                    object populatedObject = state.Current.ReturnValue!;
                    PopulatePropertiesFastPath(populatedObject, rdnTypeInfo, options, ref reader, ref state);
                    value = (T)populatedObject;
                    return true;
                }

                ReadOnlySpan<byte> originalSpan = reader.OriginalSpan;
                ReadOnlySequence<byte> originalSequence = reader.OriginalSequence;

                ReadConstructorArguments(ref state, ref reader, options);

                // We've read all ctor parameters and properties,
                // validate that all required parameters were provided
                // before calling the constructor which may throw.
                state.Current.ValidateAllRequiredPropertiesAreRead(rdnTypeInfo);

                obj = (T)CreateObject(ref state.Current);

                rdnTypeInfo.OnDeserializing?.Invoke(obj);

                if (argumentState.FoundPropertyCount > 0)
                {
                    Utf8RdnReader tempReader;

                    FoundProperty[]? properties = argumentState.FoundProperties;
                    Debug.Assert(properties != null);

                    for (int i = 0; i < argumentState.FoundPropertyCount; i++)
                    {
                        RdnPropertyInfo rdnPropertyInfo = properties[i].Item1;
                        long resumptionByteIndex = properties[i].Item3;
                        byte[]? propertyNameArray = properties[i].Item4;
                        string? dataExtKey = properties[i].Item5;

                        tempReader = originalSequence.IsEmpty
                            ? new Utf8RdnReader(
                                originalSpan.Slice(checked((int)resumptionByteIndex)),
                                isFinalBlock: true,
                                state: properties[i].Item2)
                            : new Utf8RdnReader(
                                originalSequence.Slice(resumptionByteIndex),
                                isFinalBlock: true,
                                state: properties[i].Item2);

                        Debug.Assert(tempReader.TokenType == RdnTokenType.PropertyName);

                        state.Current.RdnPropertyName = propertyNameArray;
                        state.Current.RdnPropertyInfo = rdnPropertyInfo;
                        state.Current.NumberHandling = rdnPropertyInfo.EffectiveNumberHandling;

                        bool useExtensionProperty = dataExtKey != null;

                        if (useExtensionProperty)
                        {
                            Debug.Assert(rdnPropertyInfo == state.Current.RdnTypeInfo.ExtensionDataProperty);
                            state.Current.RdnPropertyNameAsString = dataExtKey;
                            RdnSerializer.CreateExtensionDataProperty(obj, rdnPropertyInfo, options);
                        }

                        ReadPropertyValue(obj, ref state, ref tempReader, rdnPropertyInfo, useExtensionProperty);
                    }

                    FoundProperty[] toReturn = argumentState.FoundProperties!;
                    argumentState.FoundProperties = null;
                    ArrayPool<FoundProperty>.Shared.Return(toReturn, clearArray: true);
                }
            }
            else
            {
                // Slower path that supports continuation and metadata reads.

                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    if (reader.TokenType != RdnTokenType.StartObject)
                    {
                        ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                    }

                    state.Current.ObjectState = StackFrameObjectState.StartToken;
                }

                // Read any metadata properties.
                if (state.Current.CanContainMetadata && state.Current.ObjectState < StackFrameObjectState.ReadMetadata)
                {
                    if (!RdnSerializer.TryReadMetadata(this, rdnTypeInfo, ref reader, ref state))
                    {
                        value = default;
                        return false;
                    }

                    if (state.Current.MetadataPropertyNames == MetadataPropertyName.Ref)
                    {
                        value = RdnSerializer.ResolveReferenceId<T>(ref state);
                        return true;
                    }

                    state.Current.ObjectState = StackFrameObjectState.ReadMetadata;
                }

                // Dispatch to any polymorphic converters: should always be entered regardless of ObjectState progress
                if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Type) != 0 &&
                    state.Current.PolymorphicSerializationState != PolymorphicSerializationState.PolymorphicReEntryStarted &&
                    ResolvePolymorphicConverter(rdnTypeInfo, ref state) is RdnConverter polymorphicConverter)
                {
                    Debug.Assert(!IsValueType);
                    bool success = polymorphicConverter.OnTryReadAsObject(ref reader, polymorphicConverter.Type!, options, ref state, out object? objectResult);
                    value = (T)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                // We need to populate before we started reading constructor arguments.
                // Metadata is disallowed with Populate option and therefore ordering here is irrelevant.
                // Since state.Current.IsPopulating is being checked early on in this method the continuation
                // will be handled there.
                if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                {
                    object populatedObject = state.Current.ReturnValue!;

                    rdnTypeInfo.OnDeserializing?.Invoke(populatedObject);
                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                    state.Current.InitializePropertiesValidationState(rdnTypeInfo);
                    return base.OnTryRead(ref reader, typeToConvert, options, ref state, out value);
                }

                // Handle metadata post polymorphic dispatch
                if (state.Current.ObjectState < StackFrameObjectState.ConstructorArguments)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        RdnSerializer.ValidateMetadataForObjectConverter(ref state);
                    }

                    if (state.Current.MetadataPropertyNames == MetadataPropertyName.Ref)
                    {
                        value = RdnSerializer.ResolveReferenceId<T>(ref state);
                        return true;
                    }

                    BeginRead(ref state, options);

                    state.Current.ObjectState = StackFrameObjectState.ConstructorArguments;
                }

                if (!ReadConstructorArgumentsWithContinuation(ref state, ref reader, options))
                {
                    value = default;
                    return false;
                }

                // We've read all ctor parameters and properties,
                // validate that all required parameters were provided
                // before calling the constructor which may throw.
                state.Current.ValidateAllRequiredPropertiesAreRead(rdnTypeInfo);

                obj = (T)CreateObject(ref state.Current);

                if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                {
                    Debug.Assert(state.ReferenceId != null);
                    Debug.Assert(options.ReferenceHandlingStrategy == RdnKnownReferenceHandler.Preserve);
                    state.ReferenceResolver.AddReference(state.ReferenceId, obj);
                    state.ReferenceId = null;
                }

                rdnTypeInfo.OnDeserializing?.Invoke(obj);

                if (argumentState.FoundPropertyCount > 0)
                {
                    for (int i = 0; i < argumentState.FoundPropertyCount; i++)
                    {
                        RdnPropertyInfo rdnPropertyInfo = argumentState.FoundPropertiesAsync![i].Item1;
                        object? propValue = argumentState.FoundPropertiesAsync![i].Item2;
                        string? dataExtKey = argumentState.FoundPropertiesAsync![i].Item3;

                        if (dataExtKey == null)
                        {
                            Debug.Assert(rdnPropertyInfo.Set != null);

                            if (propValue is not null || !rdnPropertyInfo.IgnoreNullTokensOnRead || default(T) is not null)
                            {
                                rdnPropertyInfo.Set(obj, propValue);
                            }
                        }
                        else
                        {
                            Debug.Assert(rdnPropertyInfo == state.Current.RdnTypeInfo.ExtensionDataProperty);

                            RdnSerializer.CreateExtensionDataProperty(obj, rdnPropertyInfo, options);
                            object extDictionary = rdnPropertyInfo.GetValueAsObject(obj)!;

                            if (extDictionary is IDictionary<string, RdnElement> dict)
                            {
                                if (options.AllowDuplicateProperties)
                                {
                                    dict[dataExtKey] = (RdnElement)propValue!;
                                }
                                else if (!dict.TryAdd(dataExtKey, (RdnElement)propValue!))
                                {
                                    ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(dataExtKey);
                                }
                            }
                            else
                            {
                                IDictionary<string, object> objDict = (IDictionary<string, object>)extDictionary;

                                if (options.AllowDuplicateProperties)
                                {
                                    objDict[dataExtKey] = propValue!;
                                }
                                else if (!objDict.TryAdd(dataExtKey, propValue!))
                                {
                                    ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(dataExtKey);
                                }
                            }
                        }
                    }

                    FoundPropertyAsync[] toReturn = argumentState.FoundPropertiesAsync!;
                    argumentState.FoundPropertiesAsync = null;
                    ArrayPool<FoundPropertyAsync>.Shared.Return(toReturn, clearArray: true);
                }
            }

            rdnTypeInfo.OnDeserialized?.Invoke(obj);

            // Unbox
            Debug.Assert(obj != null);
            value = (T)obj;

            // Check if we are trying to update the UTF-8 property cache.
            if (state.Current.PropertyRefCacheBuilder != null)
            {
                rdnTypeInfo.UpdateUtf8PropertyCache(ref state.Current);
            }

            return true;
        }

        protected abstract void InitializeConstructorArgumentCaches(ref ReadStack state, RdnSerializerOptions options);

        protected abstract bool ReadAndCacheConstructorArgument(scoped ref ReadStack state, ref Utf8RdnReader reader, RdnParameterInfo rdnParameterInfo);

        protected abstract object CreateObject(ref ReadStackFrame frame);

        /// <summary>
        /// Performs a full first pass of the RDN input and deserializes the ctor args.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadConstructorArguments(scoped ref ReadStack state, ref Utf8RdnReader reader, RdnSerializerOptions options)
        {
            BeginRead(ref state, options);

            while (true)
            {
                // Read the next property name or EndObject.
                reader.ReadWithVerify();

                RdnTokenType tokenType = reader.TokenType;

                if (tokenType == RdnTokenType.EndObject)
                {
                    return;
                }

                // Read method would have thrown if otherwise.
                Debug.Assert(tokenType == RdnTokenType.PropertyName);

                ReadOnlySpan<byte> unescapedPropertyName = RdnSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                if (isAlreadyReadMetadataProperty)
                {
                    Debug.Assert(options.AllowOutOfOrderMetadataProperties);
                    reader.SkipWithVerify();
                    state.Current.EndProperty();
                    continue;
                }

                if (TryLookupConstructorParameter(
                    unescapedPropertyName,
                    ref state,
                    options,
                    out RdnPropertyInfo rdnPropertyInfo,
                    out RdnParameterInfo? rdnParameterInfo))
                {
                    // Set the property value.
                    reader.ReadWithVerify();

                    if (!rdnParameterInfo.ShouldDeserialize)
                    {
                        // The Utf8RdnReader.Skip() method will fail fast if it detects that we're reading
                        // from a partially read buffer, regardless of whether the next value is available.
                        // This can result in erroneous failures in cases where a custom converter is calling
                        // into a built-in converter (cf. https://github.com/dotnet/runtime/issues/74108).
                        // For this reason we need to call the TrySkip() method instead -- the serializer
                        // should guarantee sufficient read-ahead has been performed for the current object.
                        bool success = reader.TrySkip();
                        Debug.Assert(success, "Serializer should guarantee sufficient read-ahead has been done.");

                        state.Current.EndConstructorParameter();
                        continue;
                    }

                    Debug.Assert(rdnParameterInfo.MatchingProperty != null);
                    ReadAndCacheConstructorArgument(ref state, ref reader, rdnParameterInfo);

                    state.Current.EndConstructorParameter();
                }
                else
                {
                    if (rdnPropertyInfo.CanDeserialize)
                    {
                        ArgumentState argumentState = state.Current.CtorArgumentState!;

                        if (argumentState.FoundProperties == null)
                        {
                            argumentState.FoundProperties =
                                ArrayPool<FoundProperty>.Shared.Rent(Math.Max(1, state.Current.RdnTypeInfo.PropertyCache.Length));
                        }
                        else if (argumentState.FoundPropertyCount == argumentState.FoundProperties.Length)
                        {
                            // Rare case where we can't fit all the RDN properties in the rented pool; we have to grow.
                            // This could happen if there are duplicate properties in the RDN.

                            var newCache = ArrayPool<FoundProperty>.Shared.Rent(argumentState.FoundProperties.Length * 2);

                            argumentState.FoundProperties.CopyTo(newCache, 0);

                            FoundProperty[] toReturn = argumentState.FoundProperties;
                            argumentState.FoundProperties = newCache!;

                            ArrayPool<FoundProperty>.Shared.Return(toReturn, clearArray: true);
                        }

                        argumentState.FoundProperties[argumentState.FoundPropertyCount++] = (
                            rdnPropertyInfo,
                            reader.CurrentState,
                            reader.BytesConsumed,
                            state.Current.RdnPropertyName,
                            state.Current.RdnPropertyNameAsString);
                    }

                    reader.SkipWithVerify();
                    state.Current.EndProperty();
                }
            }
        }

        private bool ReadConstructorArgumentsWithContinuation(scoped ref ReadStack state, ref Utf8RdnReader reader, RdnSerializerOptions options)
        {
            // Process all properties.
            while (true)
            {
                // Determine the property.
                if (state.Current.PropertyState == StackFramePropertyState.None)
                {
                    if (!reader.Read())
                    {
                        return false;
                    }

                    state.Current.PropertyState = StackFramePropertyState.ReadName;
                }

                RdnParameterInfo? rdnParameterInfo;
                RdnPropertyInfo? rdnPropertyInfo;

                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    RdnTokenType tokenType = reader.TokenType;

                    if (tokenType == RdnTokenType.EndObject)
                    {
                        return true;
                    }

                    // Read method would have thrown if otherwise.
                    Debug.Assert(tokenType == RdnTokenType.PropertyName);

                    ReadOnlySpan<byte> unescapedPropertyName = RdnSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                    if (isAlreadyReadMetadataProperty)
                    {
                        Debug.Assert(options.AllowOutOfOrderMetadataProperties);
                        reader.SkipWithVerify();
                        state.Current.EndProperty();
                        continue;
                    }

                    if (TryLookupConstructorParameter(
                        unescapedPropertyName,
                        ref state,
                        options,
                        out rdnPropertyInfo,
                        out rdnParameterInfo))
                    {
                        rdnPropertyInfo = null;
                    }

                    state.Current.PropertyState = StackFramePropertyState.Name;
                }
                else
                {
                    rdnParameterInfo = state.Current.CtorArgumentState!.RdnParameterInfo;
                    rdnPropertyInfo = state.Current.RdnPropertyInfo;
                }

                if (rdnParameterInfo != null)
                {
                    Debug.Assert(rdnPropertyInfo == null);

                    if (!HandleConstructorArgumentWithContinuation(ref state, ref reader, rdnParameterInfo))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!HandlePropertyWithContinuation(ref state, ref reader, rdnPropertyInfo!))
                    {
                        return false;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HandleConstructorArgumentWithContinuation(
            scoped ref ReadStack state,
            ref Utf8RdnReader reader,
            RdnParameterInfo rdnParameterInfo)
        {
            if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
            {
                if (!rdnParameterInfo.ShouldDeserialize)
                {
                    if (!reader.TrySkipPartial(targetDepth: state.Current.OriginalDepth + 1))
                    {
                        return false;
                    }

                    state.Current.EndConstructorParameter();
                    return true;
                }

                if (!reader.TryAdvanceWithOptionalReadAhead(rdnParameterInfo.EffectiveConverter.RequiresReadAhead))
                {
                    return false;
                }

                state.Current.PropertyState = StackFramePropertyState.ReadValue;
            }

            if (!ReadAndCacheConstructorArgument(ref state, ref reader, rdnParameterInfo))
            {
                return false;
            }

            state.Current.EndConstructorParameter();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandlePropertyWithContinuation(
            scoped ref ReadStack state,
            ref Utf8RdnReader reader,
            RdnPropertyInfo rdnPropertyInfo)
        {
            if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
            {
                if (!rdnPropertyInfo.CanDeserialize)
                {
                    if (!reader.TrySkipPartial(targetDepth: state.Current.OriginalDepth + 1))
                    {
                        return false;
                    }

                    state.Current.EndProperty();
                    return true;
                }

                if (!ReadAheadPropertyValue(ref state, ref reader, rdnPropertyInfo))
                {
                    return false;
                }

                state.Current.PropertyState = StackFramePropertyState.ReadValue;
            }

            object? propValue;

            if (state.Current.UseExtensionProperty)
            {
                if (!rdnPropertyInfo.ReadRdnExtensionDataValue(ref state, ref reader, out propValue))
                {
                    return false;
                }
            }
            else
            {
                if (!rdnPropertyInfo.ReadRdnAsObject(ref state, ref reader, out propValue))
                {
                    return false;
                }
            }

            Debug.Assert(rdnPropertyInfo.CanDeserialize);

            // Ensure that the cache has enough capacity to add this property.

            ArgumentState argumentState = state.Current.CtorArgumentState!;

            if (argumentState.FoundPropertiesAsync == null)
            {
                argumentState.FoundPropertiesAsync = ArrayPool<FoundPropertyAsync>.Shared.Rent(Math.Max(1, state.Current.RdnTypeInfo.PropertyCache.Length));
            }
            else if (argumentState.FoundPropertyCount == argumentState.FoundPropertiesAsync!.Length)
            {
                // Rare case where we can't fit all the RDN properties in the rented pool; we have to grow.
                // This could happen if there are duplicate properties in the RDN.
                var newCache = ArrayPool<FoundPropertyAsync>.Shared.Rent(argumentState.FoundPropertiesAsync!.Length * 2);

                argumentState.FoundPropertiesAsync!.CopyTo(newCache, 0);

                FoundPropertyAsync[] toReturn = argumentState.FoundPropertiesAsync!;
                argumentState.FoundPropertiesAsync = newCache!;

                ArrayPool<FoundPropertyAsync>.Shared.Return(toReturn, clearArray: true);
            }

            // Cache the property name and value.
            argumentState.FoundPropertiesAsync![argumentState.FoundPropertyCount++] = (
                rdnPropertyInfo,
                propValue,
                state.Current.RdnPropertyNameAsString);

            state.Current.EndProperty();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BeginRead(scoped ref ReadStack state, RdnSerializerOptions options)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;

            rdnTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();

            if (rdnTypeInfo.ParameterCount != rdnTypeInfo.ParameterCache.Length)
            {
                ThrowHelper.ThrowInvalidOperationException_ConstructorParameterIncompleteBinding(Type);
            }

            state.Current.InitializePropertiesValidationState(rdnTypeInfo);

            // Set current RdnPropertyInfo to null to avoid conflicts on push.
            state.Current.RdnPropertyInfo = null;

            Debug.Assert(state.Current.CtorArgumentState != null);

            InitializeConstructorArgumentCaches(ref state, options);
        }

        /// <summary>
        /// Lookup the constructor parameter given its name in the reader.
        /// </summary>
        protected static bool TryLookupConstructorParameter(
            scoped ReadOnlySpan<byte> unescapedPropertyName,
            scoped ref ReadStack state,
            RdnSerializerOptions options,
            out RdnPropertyInfo rdnPropertyInfo,
            [NotNullWhen(true)] out RdnParameterInfo? rdnParameterInfo)
        {
            Debug.Assert(state.Current.RdnTypeInfo.Kind is RdnTypeInfoKind.Object);
            Debug.Assert(state.Current.CtorArgumentState != null);

            rdnPropertyInfo = RdnSerializer.LookupProperty(
                obj: null,
                unescapedPropertyName,
                ref state,
                options,
                out bool useExtensionProperty,
                createExtensionProperty: false);

            // Mark the property as read from the payload if it is mapped to a non-extension member.
            if (!useExtensionProperty && rdnPropertyInfo != RdnPropertyInfo.s_missingProperty)
            {
                state.Current.MarkPropertyAsRead(rdnPropertyInfo);
            }

            rdnParameterInfo = rdnPropertyInfo.AssociatedParameter;
            if (rdnParameterInfo != null)
            {
                state.Current.RdnPropertyInfo = null;
                state.Current.CtorArgumentState!.RdnParameterInfo = rdnParameterInfo;
                state.Current.NumberHandling = rdnParameterInfo.NumberHandling;
                return true;
            }
            else
            {
                state.Current.UseExtensionProperty = useExtensionProperty;
                return false;
            }
        }
    }
}
