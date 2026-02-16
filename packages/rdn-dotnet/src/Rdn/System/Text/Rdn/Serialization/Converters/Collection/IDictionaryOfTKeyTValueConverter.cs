// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Converter for <cref>System.Collections.Generic.IDictionary{TKey, TValue}</cref> that
    /// (de)serializes as a RDN object with properties representing the dictionary element key and value.
    /// </summary>
    internal sealed class IDictionaryOfTKeyTValueConverter<TDictionary, TKey, TValue>
        : DictionaryDefaultConverter<TDictionary, TKey, TValue>
        where TDictionary : IDictionary<TKey, TValue>
        where TKey : notnull
    {
        internal override bool CanPopulate => true;

        protected override void Add(TKey key, in TValue value, RdnSerializerOptions options, ref ReadStack state)
        {
            TDictionary collection = (TDictionary)state.Current.ReturnValue!;

            if (options.AllowDuplicateProperties)
            {
                collection[key] = value;
            }
            else
            {
                if (!collection.TryAdd(key, value))
                {
                    ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed();
                }
            }

            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            };
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

        internal override void ConfigureRdnTypeInfo(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
        {
            // Deserialize as Dictionary<TKey,TValue> for interface types that support it.
            if (rdnTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
            {
                Debug.Assert(Type.IsInterface);
                rdnTypeInfo.CreateObject = () => new Dictionary<TKey, TValue>();
            }
        }
    }
}
