// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Converter for <cref>System.Collections.IDictionary</cref> that (de)serializes as a RDN object with properties
    /// representing the dictionary element key and value.
    /// </summary>
    internal sealed class IDictionaryConverter<TDictionary>
        : RdnDictionaryConverter<TDictionary, string, object?>
        where TDictionary : IDictionary
    {
        internal override bool CanPopulate => true;

        protected override void Add(string key, in object? value, RdnSerializerOptions options, ref ReadStack state)
        {
            TDictionary collection = (TDictionary)state.Current.ReturnValue!;

            if (!options.AllowDuplicateProperties && collection.Contains(key))
            {
                ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(key);
            }

            collection[key] = value;

            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            }
        }

        protected override void CreateCollection(ref Utf8RdnReader reader, scoped ref ReadStack state)
        {
            base.CreateCollection(ref reader, ref state);
            TDictionary returnValue = (TDictionary)state.Current.ReturnValue!;
            if (returnValue.IsReadOnly)
            {
                state.Current.ReturnValue = null; // clear out for more accurate RdnPath reporting.
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(Type, ref reader, ref state);
            }
        }

        protected internal override bool OnWriteResume(Utf8RdnWriter writer, TDictionary value, RdnSerializerOptions options, ref WriteStack state)
        {
            IDictionaryEnumerator enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
                state.Current.CollectionEnumerator = enumerator;
                if (!enumerator.MoveNext())
                {
                    return true;
                }
            }
            else
            {
                enumerator = (IDictionaryEnumerator)state.Current.CollectionEnumerator;
            }

            RdnTypeInfo typeInfo = state.Current.RdnTypeInfo;
            _valueConverter ??= GetConverter<object?>(typeInfo.ElementTypeInfo!);

            do
            {
                if (ShouldFlush(ref state, writer))
                {
                    return false;
                }

                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    state.Current.PropertyState = StackFramePropertyState.Name;
                    object key = enumerator.Key;
                    // Optimize for string since that's the hot path.
                    if (key is string keyString)
                    {
                        _keyConverter ??= GetConverter<string>(typeInfo.KeyTypeInfo!);
                        _keyConverter.Write(writer, keyString, options);
                    }
                    else
                    {
                        // IDictionary is a special case since it has polymorphic object semantics on serialization
                        // but needs to use RdnConverter<string> on deserialization.
                        _valueConverter.Write(writer, key, options);
                    }
                    writer.WriteMapArrow();
                }

                object? element = enumerator.Value;
                if (!_valueConverter.TryWrite(writer, element, options, ref state))
                {
                    return false;
                }

                state.Current.EndDictionaryEntry();
            } while (enumerator.MoveNext());

            return true;
        }

        internal override void ConfigureRdnTypeInfo(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
        {
            // Deserialize as Dictionary<TKey,TValue> for interface types that support it.
            if (rdnTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(Dictionary<string, object?>)))
            {
                Debug.Assert(Type.IsInterface);
                rdnTypeInfo.CreateObject = () => new Dictionary<string, object?>();
            }
        }
    }
}
