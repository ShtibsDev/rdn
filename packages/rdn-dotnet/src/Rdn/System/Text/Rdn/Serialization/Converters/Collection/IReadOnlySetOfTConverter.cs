// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class IReadOnlySetOfTConverter<TCollection, TElement>
        : IEnumerableDefaultConverter<TCollection, TElement>
        where TCollection : IReadOnlySet<TElement>
    {
        private readonly bool _isDeserializable = typeof(TCollection).IsAssignableFrom(typeof(HashSet<TElement>));

        protected override void Add(in TElement value, ref ReadStack state)
        {
            // Directly convert to HashSet<TElement> since IReadOnlySet<T> does not have an Add method.
            HashSet<TElement> collection = (HashSet<TElement>)state.Current.ReturnValue!;
            collection.Add(value);
            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            }
        }

        protected override void CreateCollection(ref Utf8RdnReader reader, scoped ref ReadStack state, RdnSerializerOptions options)
        {
            if (!_isDeserializable)
            {
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(Type, ref reader, ref state);
            }

            state.Current.ReturnValue = new HashSet<TElement>();
        }

        internal override void ConfigureRdnTypeInfo(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
        {
            // Deserialize as HashSet<TElement> for interface types that support it.
            if (rdnTypeInfo.CreateObject is null && Type.IsAssignableFrom(typeof(HashSet<TElement>)))
            {
                Debug.Assert(Type.IsInterface);
                rdnTypeInfo.CreateObject = () => new HashSet<TElement>();
            }
        }
    }
}
