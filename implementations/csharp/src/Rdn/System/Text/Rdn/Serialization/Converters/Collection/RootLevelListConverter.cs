// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// A specialized converter implementation used for root-level value
    /// streaming in the RdnSerializer.DeserializeAsyncEnumerable methods.
    /// </summary>
    internal sealed class RootLevelListConverter<T> : RdnResumableConverter<List<T?>>
    {
        private readonly RdnTypeInfo<T> _elementTypeInfo;
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Enumerable;
        internal override Type? ElementType => typeof(T);

        public RootLevelListConverter(RdnTypeInfo<T> elementTypeInfo)
        {
            IsRootLevelMultiContentStreamingConverter = true;
            _elementTypeInfo = elementTypeInfo;
        }

        internal override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, out List<T?>? value)
        {
            Debug.Assert(reader.AllowMultipleValues, "Can only be used by readers allowing trailing content.");

            RdnConverter<T> elementConverter = _elementTypeInfo.EffectiveConverter;
            state.Current.RdnPropertyInfo = _elementTypeInfo.PropertyInfoForTypeInfo;
            var results = (List<T?>?)state.Current.ReturnValue;

            while (true)
            {
                if (state.Current.PropertyState < StackFramePropertyState.ReadValue)
                {
                    if (!reader.TryAdvanceToNextRootLevelValueWithOptionalReadAhead(elementConverter.RequiresReadAhead, out bool isAtEndOfStream))
                    {
                        if (isAtEndOfStream)
                        {
                            // No more root-level RDN values in the stream
                            // complete the deserialization process.
                            value = results;
                            return true;
                        }

                        // New root-level RDN value found, need to read more data.
                        value = default;
                        return false;
                    }

                    state.Current.PropertyState = StackFramePropertyState.ReadValue;
                }

                // Deserialize the next root-level RDN value.
                if (!elementConverter.TryRead(ref reader, typeof(T), options, ref state, out T? element, out _))
                {
                    value = default;
                    return false;
                }

                if (results is null)
                {
                    state.Current.ReturnValue = results = [];
                }

                results.Add(element);
                state.Current.EndElement();
            }
        }
    }
}
