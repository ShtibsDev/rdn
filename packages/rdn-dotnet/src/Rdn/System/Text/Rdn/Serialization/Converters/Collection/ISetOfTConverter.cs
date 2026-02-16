// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class ISetOfTConverter<TCollection, TElement>
        : IEnumerableDefaultConverter<TCollection, TElement>
        where TCollection : ISet<TElement>
    {
        internal override bool CanPopulate => true;

        protected override void Add(in TElement value, ref ReadStack state)
        {
            TCollection collection = (TCollection)state.Current.ReturnValue!;
            collection.Add(value);
            if (IsValueType)
            {
                state.Current.ReturnValue = collection;
            };
        }

        protected override void CreateCollection(ref Utf8RdnReader reader, scoped ref ReadStack state, RdnSerializerOptions options)
        {
            base.CreateCollection(ref reader, ref state, options);
            TCollection returnValue = (TCollection)state.Current.ReturnValue!;
            if (returnValue.IsReadOnly)
            {
                state.Current.ReturnValue = null; // clear out for more accurate RdnPath reporting.
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(Type, ref reader, ref state);
            }
        }

        internal override bool OnTryWrite(Utf8RdnWriter writer, TCollection value, RdnSerializerOptions options, ref WriteStack state)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return true;
            }

            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;

            if (!state.Current.ProcessedStartToken)
            {
                state.Current.ProcessedStartToken = true;

                rdnTypeInfo.OnSerializing?.Invoke(value);

                if (state.CurrentContainsMetadata && CanHaveMetadata)
                {
                    state.Current.MetadataPropertyName = RdnSerializer.WriteMetadataForCollection(this, ref state, writer);
                }

                writer.WriteStartSet(forceTypeName: value.Count == 0);
                state.Current.RdnPropertyInfo = rdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            }

            bool success = OnWriteResume(writer, value, options, ref state);
            if (success)
            {
                if (!state.Current.ProcessedEndToken)
                {
                    state.Current.ProcessedEndToken = true;
                    writer.WriteEndSet();

                    if (state.Current.MetadataPropertyName != 0)
                    {
                        writer.WriteEndObject();
                    }
                }

                rdnTypeInfo.OnSerialized?.Invoke(value);
            }

            return success;
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
