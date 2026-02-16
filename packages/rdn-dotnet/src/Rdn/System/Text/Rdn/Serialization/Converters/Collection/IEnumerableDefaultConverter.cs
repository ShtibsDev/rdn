// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Default base class implementation of <cref>RdnIEnumerableConverter{TCollection, TElement}</cref>.
    /// </summary>
    internal abstract class IEnumerableDefaultConverter<TCollection, TElement> : RdnCollectionConverter<TCollection, TElement>
        where TCollection : IEnumerable<TElement>
    {
        internal override bool CanHaveMetadata => true;

        protected override bool OnWriteResume(Utf8RdnWriter writer, TCollection value, RdnSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(value is not null);

            IEnumerator<TElement> enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
                state.Current.CollectionEnumerator = enumerator;
                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
                    return true;
                }
            }
            else
            {
                Debug.Assert(state.Current.CollectionEnumerator is IEnumerator<TElement>);
                enumerator = (IEnumerator<TElement>)state.Current.CollectionEnumerator;
            }

            RdnConverter<TElement> converter = GetElementConverter(ref state);
            do
            {
                if (ShouldFlush(ref state, writer))
                {
                    return false;
                }

                TElement element = enumerator.Current;
                if (!converter.TryWrite(writer, element, options, ref state))
                {
                    return false;
                }

                state.Current.EndCollectionElement();
            } while (enumerator.MoveNext());

            enumerator.Dispose();
            return true;
        }
    }
}
