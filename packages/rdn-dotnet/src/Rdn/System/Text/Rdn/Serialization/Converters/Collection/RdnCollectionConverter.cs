// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization
{
    /// <summary>
    /// Base class for all collections. Collections are assumed to implement <see cref="IEnumerable{T}"/>
    /// or a variant thereof e.g. <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    internal abstract class RdnCollectionConverter<TCollection, TElement> : RdnResumableConverter<TCollection>
    {
        internal override bool SupportsCreateObjectDelegate => true;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Enumerable;
        internal override Type ElementType => typeof(TElement);

        protected abstract void Add(in TElement value, ref ReadStack state);

        /// <summary>
        /// When overridden, create the collection. It may be a temporary collection or the final collection.
        /// </summary>
        protected virtual void CreateCollection(ref Utf8RdnReader reader, scoped ref ReadStack state, RdnSerializerOptions options)
        {
            if (state.ParentProperty?.TryGetPrePopulatedValue(ref state) == true)
            {
                return;
            }

            RdnTypeInfo typeInfo = state.Current.RdnTypeInfo;

            if (typeInfo.CreateObject is null)
            {
                ThrowHelper.ThrowNotSupportedException_DeserializeNoConstructor(typeInfo, ref reader, ref state);
            }

            state.Current.ReturnValue = typeInfo.CreateObject();
            Debug.Assert(state.Current.ReturnValue is TCollection);
        }

        /// <summary>
        /// When overridden, converts the temporary collection held in state.Current.ReturnValue to the final collection.
        /// The <see cref="RdnConverter.IsConvertibleCollection"/> property must also be set to <see langword="true"/>.
        /// </summary>
        protected virtual void ConvertCollection(ref ReadStack state, RdnSerializerOptions options) { }

        protected static RdnConverter<TElement> GetElementConverter(RdnTypeInfo elementTypeInfo)
        {
            return ((RdnTypeInfo<TElement>)elementTypeInfo).EffectiveConverter;
        }

        protected static RdnConverter<TElement> GetElementConverter(ref WriteStack state)
        {
            Debug.Assert(state.Current.RdnPropertyInfo != null);
            return (RdnConverter<TElement>)state.Current.RdnPropertyInfo.EffectiveConverter;
        }

        internal override bool OnTryRead(
            ref Utf8RdnReader reader,
            Type typeToConvert,
            RdnSerializerOptions options,
            scoped ref ReadStack state,
            [MaybeNullWhen(false)] out TCollection value)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;
            RdnTypeInfo elementTypeInfo = rdnTypeInfo.ElementTypeInfo!;

            if (!state.SupportContinuation && !state.Current.CanContainMetadata)
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                if (reader.TokenType != RdnTokenType.StartArray && reader.TokenType != RdnTokenType.StartSet)
                {
                    ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                }

                CreateCollection(ref reader, ref state, options);

                rdnTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                state.Current.RdnPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                RdnConverter<TElement> elementConverter = GetElementConverter(elementTypeInfo);
                if (elementConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling == null)
                {
                    // Fast path that avoids validation and extra indirection.
                    while (true)
                    {
                        reader.ReadWithVerify();
                        if (reader.TokenType == RdnTokenType.EndArray || reader.TokenType == RdnTokenType.EndSet)
                        {
                            break;
                        }

                        // Obtain the CLR value from the RDN and apply to the object.
                        TElement? element = elementConverter.Read(ref reader, elementConverter.Type, options);
                        Add(element!, ref state);
                    }
                }
                else
                {
                    // Process all elements.
                    while (true)
                    {
                        reader.ReadWithVerify();
                        if (reader.TokenType == RdnTokenType.EndArray || reader.TokenType == RdnTokenType.EndSet)
                        {
                            break;
                        }

                        // Get the value from the converter and add it.
                        elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _);
                        Add(element!, ref state);
                    }
                }
            }
            else
            {
                // Slower path that supports continuation and reading metadata.
                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    if (reader.TokenType == RdnTokenType.StartArray || reader.TokenType == RdnTokenType.StartSet)
                    {
                        state.Current.ObjectState = StackFrameObjectState.ReadMetadata;
                    }
                    else if (state.Current.CanContainMetadata)
                    {
                        if (reader.TokenType != RdnTokenType.StartObject)
                        {
                            ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                        }

                        state.Current.ObjectState = StackFrameObjectState.StartToken;
                    }
                    else
                    {
                        ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                    }
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
                        value = RdnSerializer.ResolveReferenceId<TCollection>(ref state);
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
                    value = (TCollection)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        RdnSerializer.ValidateMetadataForArrayConverter(this, ref reader, ref state);
                    }

                    CreateCollection(ref reader, ref state, options);

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId != null);
                        Debug.Assert(options.ReferenceHandlingStrategy == RdnKnownReferenceHandler.Preserve);
                        Debug.Assert(state.Current.ReturnValue is TCollection);
                        state.ReferenceResolver.AddReference(state.ReferenceId, state.Current.ReturnValue);
                        state.ReferenceId = null;
                    }

                    rdnTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                }

                if (state.Current.ObjectState < StackFrameObjectState.ReadElements)
                {
                    RdnConverter<TElement> elementConverter = GetElementConverter(elementTypeInfo);
                    state.Current.RdnPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;

                    // Process all elements.
                    while (true)
                    {
                        if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                        {
                            if (!reader.TryAdvanceWithOptionalReadAhead(elementConverter.RequiresReadAhead))
                            {
                                value = default;
                                return false;
                            }

                            state.Current.PropertyState = StackFramePropertyState.ReadValue;
                        }

                        if (state.Current.PropertyState < StackFramePropertyState.ReadValueIsEnd)
                        {
                            if (reader.TokenType == RdnTokenType.EndArray || reader.TokenType == RdnTokenType.EndSet)
                            {
                                break;
                            }

                            state.Current.PropertyState = StackFramePropertyState.ReadValueIsEnd;
                        }

                        if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                        {
                            // Get the value from the converter and add it.
                            if (!elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _))
                            {
                                value = default;
                                return false;
                            }

                            Add(element!, ref state);

                            // No need to set PropertyState to TryRead since we're done with this element now.
                            state.Current.EndElement();
                        }
                    }

                    state.Current.ObjectState = StackFrameObjectState.ReadElements;
                }

                if (state.Current.ObjectState < StackFrameObjectState.EndToken)
                {
                    // Array payload is nested inside a $values metadata property.
                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                    {
                        if (!reader.Read())
                        {
                            value = default;
                            return false;
                        }
                    }

                    state.Current.ObjectState = StackFrameObjectState.EndToken;
                }

                if (state.Current.ObjectState < StackFrameObjectState.EndTokenValidation)
                {
                    // Array payload is nested inside a $values metadata property.
                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Values) != 0)
                    {
                        if (reader.TokenType != RdnTokenType.EndObject)
                        {
                            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
                            if (options.AllowOutOfOrderMetadataProperties)
                            {
                                Debug.Assert(RdnSerializer.IsMetadataPropertyName(reader.GetUnescapedSpan(), (state.Current.BaseRdnTypeInfo ?? rdnTypeInfo).PolymorphicTypeResolver), "should only be hit if metadata property.");
                                bool result = reader.TrySkipPartial(reader.CurrentDepth - 1); // skip to the end of the object
                                Debug.Assert(result, "Metadata reader must have buffered all contents.");
                                Debug.Assert(reader.TokenType is RdnTokenType.EndObject);
                            }
                            else
                            {
                                ThrowHelper.ThrowRdnException_MetadataInvalidPropertyInArrayMetadata(ref state, typeToConvert, reader);
                            }
                        }
                    }
                }
            }

            ConvertCollection(ref state, options);
            object returnValue = state.Current.ReturnValue!;
            rdnTypeInfo.OnDeserialized?.Invoke(returnValue);
            value = (TCollection)returnValue;

            return true;
        }

        internal override bool OnTryWrite(
            Utf8RdnWriter writer,
            TCollection value,
            RdnSerializerOptions options,
            ref WriteStack state)
        {
            bool success;

            if (value == null)
            {
                writer.WriteNullValue();
                success = true;
            }
            else
            {
                RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;

                if (!state.Current.ProcessedStartToken)
                {
                    state.Current.ProcessedStartToken = true;

                    rdnTypeInfo.OnSerializing?.Invoke(value);

                    if (state.CurrentContainsMetadata && CanHaveMetadata)
                    {
                        state.Current.MetadataPropertyName = RdnSerializer.WriteMetadataForCollection(this, ref state, writer);
                    }

                    // Writing the start of the array must happen after any metadata
                    writer.WriteStartArray();
                    state.Current.RdnPropertyInfo = rdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
                }

                success = OnWriteResume(writer, value, options, ref state);
                if (success)
                {
                    if (!state.Current.ProcessedEndToken)
                    {
                        state.Current.ProcessedEndToken = true;
                        writer.WriteEndArray();

                        if (state.Current.MetadataPropertyName != 0)
                        {
                            // Write the EndObject for $values.
                            writer.WriteEndObject();
                        }
                    }

                    rdnTypeInfo.OnSerialized?.Invoke(value);
                }
            }

            return success;
        }

        protected abstract bool OnWriteResume(Utf8RdnWriter writer, TCollection value, RdnSerializerOptions options, ref WriteStack state);
    }
}
