// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization
{
    /// <summary>
    /// Base class for dictionary converters such as IDictionary, Hashtable, Dictionary{,} IDictionary{,} and SortedList.
    /// </summary>
    internal abstract class RdnDictionaryConverter<TDictionary> : RdnResumableConverter<TDictionary>
    {
        internal override bool SupportsCreateObjectDelegate => true;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Dictionary;

        protected internal abstract bool OnWriteResume(Utf8RdnWriter writer, TDictionary dictionary, RdnSerializerOptions options, ref WriteStack state);
    }

    /// <summary>
    /// Base class for dictionary converters such as IDictionary, Hashtable, Dictionary{,} IDictionary{,} and SortedList.
    /// </summary>
    internal abstract class RdnDictionaryConverter<TDictionary, TKey, TValue> : RdnDictionaryConverter<TDictionary>
        where TKey : notnull
    {
        /// <summary>
        /// When overridden, adds the value to the collection.
        /// </summary>
        protected abstract void Add(TKey key, in TValue value, RdnSerializerOptions options, ref ReadStack state);

        /// <summary>
        /// When overridden, converts the temporary collection held in state.Current.ReturnValue to the final collection.
        /// This is used with immutable collections.
        /// </summary>
        protected virtual void ConvertCollection(ref ReadStack state, RdnSerializerOptions options) { }

        /// <summary>
        /// When overridden, create the collection. It may be a temporary collection or the final collection.
        /// </summary>
        protected virtual void CreateCollection(ref Utf8RdnReader reader, scoped ref ReadStack state)
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
            Debug.Assert(state.Current.ReturnValue is TDictionary);
        }

        internal override Type ElementType => typeof(TValue);

        internal override Type KeyType => typeof(TKey);


        protected RdnConverter<TKey>? _keyConverter;
        protected RdnConverter<TValue>? _valueConverter;

        protected static RdnConverter<T> GetConverter<T>(RdnTypeInfo typeInfo)
        {
            return ((RdnTypeInfo<T>)typeInfo).EffectiveConverter;
        }

        internal sealed override bool OnTryRead(
            ref Utf8RdnReader reader,
            Type typeToConvert,
            RdnSerializerOptions options,
            scoped ref ReadStack state,
            [MaybeNullWhen(false)] out TDictionary value)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;
            RdnTypeInfo keyTypeInfo = rdnTypeInfo.KeyTypeInfo!;
            RdnTypeInfo elementTypeInfo = rdnTypeInfo.ElementTypeInfo!;

            if (!state.SupportContinuation && !state.Current.CanContainMetadata)
            {
                // Fast path that avoids maintaining state variables and dealing with preserved references.

                bool isMap = reader.TokenType == RdnTokenType.StartMap;
                if (!isMap && reader.TokenType != RdnTokenType.StartObject)
                {
                    ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(Type);
                }

                CreateCollection(ref reader, ref state);

                rdnTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                _keyConverter ??= GetConverter<TKey>(keyTypeInfo);
                _valueConverter ??= GetConverter<TValue>(elementTypeInfo);

                RdnTokenType endToken = isMap ? RdnTokenType.EndMap : RdnTokenType.EndObject;

                if (_valueConverter.CanUseDirectReadOrWrite && state.Current.NumberHandling == null)
                {
                    // Process all elements.
                    while (true)
                    {
                        // Read the key name.
                        reader.ReadWithVerify();

                        if (reader.TokenType == endToken)
                        {
                            break;
                        }

                        state.Current.RdnPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        TKey key;
                        if (isMap)
                        {
                            // Map keys are regular values; arrow is consumed silently by reader.
                            key = _keyConverter.Read(ref reader, typeof(TKey), options)!;
                        }
                        else
                        {
                            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
                            key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);
                        }

                        // Read the value and add.
                        reader.ReadWithVerify();
                        state.Current.RdnPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        TValue? element = _valueConverter.Read(ref reader, ElementType, options);
                        Add(key, element!, options, ref state);
                    }
                }
                else
                {
                    // Process all elements.
                    while (true)
                    {
                        // Read the key name.
                        reader.ReadWithVerify();

                        if (reader.TokenType == endToken)
                        {
                            break;
                        }

                        state.Current.RdnPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                        TKey key;
                        if (isMap)
                        {
                            key = _keyConverter.Read(ref reader, typeof(TKey), options)!;
                        }
                        else
                        {
                            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
                            key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);
                        }

                        reader.ReadWithVerify();

                        // Get the value from the converter and add it.
                        state.Current.RdnPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        _valueConverter.TryRead(ref reader, ElementType, options, ref state, out TValue? element, out _);
                        Add(key, element!, options, ref state);
                    }
                }
            }
            else
            {
                // Slower path that supports continuation and reading metadata.
                if (state.Current.ObjectState == StackFrameObjectState.None)
                {
                    if (reader.TokenType == RdnTokenType.StartMap)
                    {
                        state.Current.IsReadingMapFormat = true;
                    }
                    else if (reader.TokenType != RdnTokenType.StartObject)
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
                        value = RdnSerializer.ResolveReferenceId<TDictionary>(ref state);
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
                    value = (TDictionary)objectResult!;
                    state.ExitPolymorphicConverter(success);
                    return success;
                }

                // Create the dictionary.
                if (state.Current.ObjectState < StackFrameObjectState.CreatedObject)
                {
                    if (state.Current.CanContainMetadata)
                    {
                        RdnSerializer.ValidateMetadataForObjectConverter(ref state);
                    }

                    CreateCollection(ref reader, ref state);

                    if ((state.Current.MetadataPropertyNames & MetadataPropertyName.Id) != 0)
                    {
                        Debug.Assert(state.ReferenceId != null);
                        Debug.Assert(options.ReferenceHandlingStrategy == RdnKnownReferenceHandler.Preserve);
                        Debug.Assert(state.Current.ReturnValue is TDictionary);
                        state.ReferenceResolver.AddReference(state.ReferenceId, state.Current.ReturnValue);
                        state.ReferenceId = null;
                    }

                    rdnTypeInfo.OnDeserializing?.Invoke(state.Current.ReturnValue!);

                    state.Current.ObjectState = StackFrameObjectState.CreatedObject;
                }

                // Process all elements.
                _keyConverter ??= GetConverter<TKey>(keyTypeInfo);
                _valueConverter ??= GetConverter<TValue>(elementTypeInfo);
                while (true)
                {
                    if (state.Current.PropertyState == StackFramePropertyState.None)
                    {
                        // Read the key name.
                        if (!reader.Read())
                        {
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadName;
                    }

                    // Determine the property.
                    TKey key;
                    if (state.Current.PropertyState < StackFramePropertyState.Name)
                    {
                        RdnTokenType endToken = state.Current.IsReadingMapFormat ? RdnTokenType.EndMap : RdnTokenType.EndObject;
                        if (reader.TokenType == endToken)
                        {
                            break;
                        }

                        state.Current.PropertyState = StackFramePropertyState.Name;

                        if (state.Current.IsReadingMapFormat)
                        {
                            // Map keys are regular values; arrow is consumed silently by reader.
                            state.Current.RdnPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                            key = _keyConverter.Read(ref reader, typeof(TKey), options)!;
                        }
                        else
                        {
                            // Read method would have thrown if otherwise.
                            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);

                            if (state.Current.CanContainMetadata)
                            {
                                ReadOnlySpan<byte> propertyName = reader.GetUnescapedSpan();
                                if (RdnSerializer.IsMetadataPropertyName(propertyName, state.Current.BaseRdnTypeInfo.PolymorphicTypeResolver))
                                {
                                    if (options.AllowOutOfOrderMetadataProperties)
                                    {
                                        reader.SkipWithVerify();
                                        state.Current.EndElement();
                                        continue;
                                    }
                                    else
                                    {
                                        ThrowHelper.ThrowUnexpectedMetadataException(propertyName, ref reader, ref state);
                                    }
                                }
                            }

                            state.Current.RdnPropertyInfo = keyTypeInfo.PropertyInfoForTypeInfo;
                            key = ReadDictionaryKey(_keyConverter, ref reader, ref state, options);
                        }
                    }
                    else
                    {
                        // DictionaryKey is assigned before all return false cases, null value is unreachable
                        key = (TKey)state.Current.DictionaryKey!;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                    {
                        if (!reader.TryAdvanceWithOptionalReadAhead(_valueConverter.RequiresReadAhead))
                        {
                            state.Current.DictionaryKey = key;
                            value = default;
                            return false;
                        }

                        state.Current.PropertyState = StackFramePropertyState.ReadValue;
                    }

                    if (state.Current.PropertyState < StackFramePropertyState.TryRead)
                    {
                        // Get the value from the converter and add it.
                        state.Current.RdnPropertyInfo = elementTypeInfo.PropertyInfoForTypeInfo;
                        bool success = _valueConverter.TryRead(ref reader, typeof(TValue), options, ref state, out TValue? element, out _);
                        if (!success)
                        {
                            state.Current.DictionaryKey = key;
                            value = default;
                            return false;
                        }

                        Add(key, element!, options, ref state);
                        state.Current.EndElement();
                    }
                }
            }

            ConvertCollection(ref state, options);
            object result = state.Current.ReturnValue!;
            rdnTypeInfo.OnDeserialized?.Invoke(result);
            value = (TDictionary)result;

            return true;

            static TKey ReadDictionaryKey(RdnConverter<TKey> keyConverter, ref Utf8RdnReader reader, scoped ref ReadStack state, RdnSerializerOptions options)
            {
                TKey key;
                string unescapedPropertyNameAsString = reader.GetString()!;
                state.Current.RdnPropertyNameAsString = unescapedPropertyNameAsString; // Copy key name for RDN Path support in case of error.

                // Special case string to avoid calling GetString twice and save one allocation.
                if (keyConverter.IsInternalConverter && keyConverter.Type == typeof(string))
                {
                    key = (TKey)(object)unescapedPropertyNameAsString;
                }
                else
                {
                    key = keyConverter.ReadAsPropertyNameCore(ref reader, keyConverter.Type, options);
                }

                return key;
            }
        }

        internal sealed override bool OnTryWrite(
            Utf8RdnWriter writer,
            TDictionary dictionary,
            RdnSerializerOptions options,
            ref WriteStack state)
        {
            if (dictionary == null)
            {
                writer.WriteNullValue();
                return true;
            }

            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;

            if (!state.Current.ProcessedStartToken)
            {
                state.Current.ProcessedStartToken = true;

                rdnTypeInfo.OnSerializing?.Invoke(dictionary);

                bool isEmpty = dictionary is System.Collections.ICollection c ? c.Count == 0 : false;
                writer.WriteStartMap(forceTypeName: isEmpty);

                if (state.CurrentContainsMetadata && CanHaveMetadata)
                {
                    RdnSerializer.WriteMetadataForObject(this, ref state, writer);
                }

                state.Current.RdnPropertyInfo = rdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            }

            bool success = OnWriteResume(writer, dictionary, options, ref state);
            if (success)
            {
                if (!state.Current.ProcessedEndToken)
                {
                    state.Current.ProcessedEndToken = true;
                    writer.WriteEndMap();
                }

                rdnTypeInfo.OnSerialized?.Invoke(dictionary);
            }

            return success;
        }
    }
}
