// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Default base class implementation of <cref>RdnObjectConverter{T}</cref>.
    /// </summary>
    internal class ObjectDefaultConverter<T> : RdnObjectConverter<T> where T : notnull
    {
        internal override bool CanHaveMetadata => true;
        internal override bool SupportsCreateObjectDelegate => true;

        internal override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, [MaybeNullWhen(false)] out T value)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;

            object obj;

            if (!state.SupportContinuation && !state.Current.CanContainMetadata)
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                if (reader.TokenType != RdnTokenType.StartObject)
                {
                    ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                }

                if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                {
                    obj = state.Current.ReturnValue!;
                }
                else
                {
                    if (rdnTypeInfo.CreateObject == null)
                    {
                        ThrowHelper.ThrowNotSupportedException_DeserializeNoConstructor(rdnTypeInfo, ref reader, ref state);
                    }

                    obj = rdnTypeInfo.CreateObject();
                }

                PopulatePropertiesFastPath(obj, rdnTypeInfo, options, ref reader, ref state);
                Debug.Assert(obj != null);
                value = (T)obj;
                return true;
            }
            else
            {
                // Slower path that supports continuation and reading metadata.

                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    if (reader.TokenType != RdnTokenType.StartObject)
                    {
                        ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                    }

                    state.Current.ObjectState = StackFrameObjectState.StartToken;
                }

                // Handle the metadata properties.
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

                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
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

                    if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
                    {
                        obj = state.Current.ReturnValue!;
                    }
                    else
                    {
                        if (rdnTypeInfo.CreateObject == null)
                        {
                            ThrowHelper.ThrowNotSupportedException_DeserializeNoConstructor(rdnTypeInfo, ref reader, ref state);
                        }

                        obj = rdnTypeInfo.CreateObject();
                    }

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId != null);
                        Debug.Assert(options.ReferenceHandlingStrategy == RdnKnownReferenceHandler.Preserve);
                        state.ReferenceResolver.AddReference(state.ReferenceId, obj);
                        state.ReferenceId = null;
                    }

                    rdnTypeInfo.OnDeserializing?.Invoke(obj);

                    state.Current.ReturnValue = obj;
                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                    state.Current.InitializePropertiesValidationState(rdnTypeInfo);
                }
                else
                {
                    obj = state.Current.ReturnValue!;
                    Debug.Assert(obj != null);
                }

                // Process all properties.
                while (true)
                {
                    // Determine the property.
                    if (state.Current.PropertyState == StackFramePropertyState.None)
                    {
                        if (!reader.Read())
                        {
                            state.Current.ReturnValue = obj;
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadName;
                    }

                    RdnPropertyInfo rdnPropertyInfo;

                    if (state.Current.PropertyState < StackFramePropertyState.Name)
                    {
                        RdnTokenType tokenType = reader.TokenType;
                        if (tokenType == RdnTokenType.EndObject)
                        {
                            break;
                        }

                        // Read method would have thrown if otherwise.
                        Debug.Assert(tokenType == RdnTokenType.PropertyName);

                        rdnTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();
                        ReadOnlySpan<byte> unescapedPropertyName = RdnSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                        if (isAlreadyReadMetadataProperty)
                        {
                            Debug.Assert(options.AllowOutOfOrderMetadataProperties);
                            reader.SkipWithVerify();
                            state.Current.EndProperty();
                            continue;
                        }

                        rdnPropertyInfo = RdnSerializer.LookupProperty(
                            obj,
                            unescapedPropertyName,
                            ref state,
                            options,
                            out bool useExtensionProperty);

                        state.Current.UseExtensionProperty = useExtensionProperty;
                        state.Current.PropertyState = StackFramePropertyState.Name;
                    }
                    else
                    {
                        Debug.Assert(state.Current.RdnPropertyInfo != null);
                        rdnPropertyInfo = state.Current.RdnPropertyInfo!;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                    {
                        if (!rdnPropertyInfo.CanDeserializeOrPopulate)
                        {
                            if (!reader.TrySkipPartial(targetDepth: state.Current.OriginalDepth + 1))
                            {
                                state.Current.ReturnValue = obj;
                                value = default;
                                return false;
                            }

                            state.Current.EndProperty();
                            continue;
                        }

                        if (!ReadAheadPropertyValue(ref state, ref reader, rdnPropertyInfo))
                        {
                            state.Current.ReturnValue = obj;
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadValue;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                    {
                        // Obtain the CLR value from the RDN and set the member.
                        if (!state.Current.UseExtensionProperty)
                        {
                            if (!rdnPropertyInfo.ReadRdnAndSetMember(obj, ref state, ref reader))
                            {
                                state.Current.ReturnValue = obj;
                                value = default;
                                return false;
                            }
                        }
                        else
                        {
                            if (!rdnPropertyInfo.ReadRdnAndAddExtensionProperty(obj, ref state, ref reader))
                            {
                                // No need to set 'value' here since RdnElement must be read in full.
                                state.Current.ReturnValue = obj;
                                value = default;
                                return false;
                            }
                        }

                        state.Current.EndProperty();
                    }
                }
            }

            rdnTypeInfo.OnDeserialized?.Invoke(obj);
            state.Current.ValidateAllRequiredPropertiesAreRead(rdnTypeInfo);

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

        // This method is using aggressive inlining to avoid extra stack frame for deep object graphs.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void PopulatePropertiesFastPath(object obj, RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options, ref Utf8RdnReader reader, scoped ref ReadStack state)
        {
            rdnTypeInfo.OnDeserializing?.Invoke(obj);
            state.Current.InitializePropertiesValidationState(rdnTypeInfo);

            // Process all properties.
            while (true)
            {
                // Read the property name or EndObject.
                reader.ReadWithVerify();

                RdnTokenType tokenType = reader.TokenType;

                if (tokenType == RdnTokenType.EndObject)
                {
                    break;
                }

                // Read method would have thrown if otherwise.
                Debug.Assert(tokenType == RdnTokenType.PropertyName);

                ReadOnlySpan<byte> unescapedPropertyName = RdnSerializer.GetPropertyName(ref state, ref reader, options, out bool isAlreadyReadMetadataProperty);
                Debug.Assert(!isAlreadyReadMetadataProperty, "Only possible for types that can read metadata, which do not call into the fast-path method.");

                rdnTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();
                RdnPropertyInfo rdnPropertyInfo = RdnSerializer.LookupProperty(
                    obj,
                    unescapedPropertyName,
                    ref state,
                    options,
                    out bool useExtensionProperty);

                ReadPropertyValue(obj, ref state, ref reader, rdnPropertyInfo, useExtensionProperty);
            }

            rdnTypeInfo.OnDeserialized?.Invoke(obj);
            state.Current.ValidateAllRequiredPropertiesAreRead(rdnTypeInfo);

            // Check if we are trying to update the UTF-8 property cache.
            if (state.Current.PropertyRefCacheBuilder != null)
            {
                rdnTypeInfo.UpdateUtf8PropertyCache(ref state.Current);
            }
        }

        internal sealed override bool OnTryWrite(
            Utf8RdnWriter writer,
            T value,
            RdnSerializerOptions options,
            ref WriteStack state)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;
            rdnTypeInfo.ValidateCanBeUsedForPropertyMetadataSerialization();

            object obj = value; // box once

            if (!state.SupportContinuation)
            {
                rdnTypeInfo.OnSerializing?.Invoke(obj);

                writer.WriteStartObject();

                if (state.CurrentContainsMetadata && CanHaveMetadata)
                {
                    RdnSerializer.WriteMetadataForObject(this, ref state, writer);
                }

                foreach (RdnPropertyInfo rdnPropertyInfo in rdnTypeInfo.PropertyCache)
                {
                    if (rdnPropertyInfo.CanSerialize)
                    {
                        // Remember the current property for RdnPath support if an exception is thrown.
                        state.Current.RdnPropertyInfo = rdnPropertyInfo;
                        state.Current.NumberHandling = rdnPropertyInfo.EffectiveNumberHandling;

                        bool success = rdnPropertyInfo.GetMemberAndWriteRdn(obj, ref state, writer);
                        // Converters only return 'false' when out of data which is not possible in fast path.
                        Debug.Assert(success);

                        state.Current.EndProperty();
                    }
                }

                // Write extension data after the normal properties.
                RdnPropertyInfo? extensionDataProperty = rdnTypeInfo.ExtensionDataProperty;
                if (extensionDataProperty?.CanSerialize == true)
                {
                    // Remember the current property for RdnPath support if an exception is thrown.
                    state.Current.RdnPropertyInfo = extensionDataProperty;
                    state.Current.NumberHandling = extensionDataProperty.EffectiveNumberHandling;

                    bool success = extensionDataProperty.GetMemberAndWriteRdnExtensionData(obj, ref state, writer);
                    Debug.Assert(success);

                    state.Current.EndProperty();
                }

                writer.WriteEndObject();
            }
            else
            {
                if (!state.Current.ProcessedStartToken)
                {
                    writer.WriteStartObject();

                    if (state.CurrentContainsMetadata && CanHaveMetadata)
                    {
                        RdnSerializer.WriteMetadataForObject(this, ref state, writer);
                    }

                    rdnTypeInfo.OnSerializing?.Invoke(obj);

                    state.Current.ProcessedStartToken = true;
                }

                ReadOnlySpan<RdnPropertyInfo> propertyCache = rdnTypeInfo.PropertyCache;
                while (state.Current.EnumeratorIndex < propertyCache.Length)
                {
                    RdnPropertyInfo rdnPropertyInfo = propertyCache[state.Current.EnumeratorIndex];
                    if (rdnPropertyInfo.CanSerialize)
                    {
                        state.Current.RdnPropertyInfo = rdnPropertyInfo;
                        state.Current.NumberHandling = rdnPropertyInfo.EffectiveNumberHandling;

                        if (!rdnPropertyInfo.GetMemberAndWriteRdn(obj!, ref state, writer))
                        {
                            Debug.Assert(rdnPropertyInfo.EffectiveConverter.ConverterStrategy != ConverterStrategy.Value);
                            return false;
                        }

                        state.Current.EndProperty();
                        state.Current.EnumeratorIndex++;

                        if (ShouldFlush(ref state, writer))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        state.Current.EnumeratorIndex++;
                    }
                }

                // Write extension data after the normal properties.
                if (state.Current.EnumeratorIndex == propertyCache.Length)
                {
                    RdnPropertyInfo? extensionDataProperty = rdnTypeInfo.ExtensionDataProperty;
                    if (extensionDataProperty?.CanSerialize == true)
                    {
                        // Remember the current property for RdnPath support if an exception is thrown.
                        state.Current.RdnPropertyInfo = extensionDataProperty;
                        state.Current.NumberHandling = extensionDataProperty.EffectiveNumberHandling;

                        if (!extensionDataProperty.GetMemberAndWriteRdnExtensionData(obj, ref state, writer))
                        {
                            return false;
                        }

                        state.Current.EndProperty();
                        state.Current.EnumeratorIndex++;

                        if (ShouldFlush(ref state, writer))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        state.Current.EnumeratorIndex++;
                    }
                }

                if (!state.Current.ProcessedEndToken)
                {
                    state.Current.ProcessedEndToken = true;
                    writer.WriteEndObject();
                }
            }

            rdnTypeInfo.OnSerialized?.Invoke(obj);

            return true;
        }

        // AggressiveInlining since this method is only called from two locations and is on a hot path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void ReadPropertyValue(
            object obj,
            scoped ref ReadStack state,
            ref Utf8RdnReader reader,
            RdnPropertyInfo rdnPropertyInfo,
            bool useExtensionProperty)
        {
            // Skip the property if not found.
            if (!rdnPropertyInfo.CanDeserializeOrPopulate)
            {
                // The Utf8RdnReader.Skip() method will fail fast if it detects that we're reading
                // from a partially read buffer, regardless of whether the next value is available.
                // This can result in erroneous failures in cases where a custom converter is calling
                // into a built-in converter (cf. https://github.com/dotnet/runtime/issues/74108).
                // For this reason we need to call the TrySkip() method instead -- the serializer
                // should guarantee sufficient read-ahead has been performed for the current object.
                bool success = reader.TrySkip();
                Debug.Assert(success, "Serializer should guarantee sufficient read-ahead has been done.");
            }
            else
            {
                // Set the property value.
                reader.ReadWithVerify();

                if (!useExtensionProperty)
                {
                    rdnPropertyInfo.ReadRdnAndSetMember(obj, ref state, ref reader);
                }
                else
                {
                    rdnPropertyInfo.ReadRdnAndAddExtensionProperty(obj, ref state, ref reader);
                }
            }

            // Ensure any exception thrown in the next read does not have a property in its RdnPath.
            state.Current.EndProperty();
        }

        protected static bool ReadAheadPropertyValue(scoped ref ReadStack state, ref Utf8RdnReader reader, RdnPropertyInfo rdnPropertyInfo)
        {
            // Extension properties can use the RdnElement converter and thus require read-ahead.
            bool requiresReadAhead = rdnPropertyInfo.EffectiveConverter.RequiresReadAhead || state.Current.UseExtensionProperty;
            return reader.TryAdvanceWithOptionalReadAhead(requiresReadAhead);
        }
    }
}
