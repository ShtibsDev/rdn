// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Serialization.Metadata;

namespace Rdn.Serialization
{
    /// <summary>
    /// Base class for converters that are able to resume after reading or writing to a buffer.
    /// This is used when the Stream-based serialization APIs are used.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class RdnResumableConverter<T> : RdnConverter<T>
    {
        public override bool HandleNull => false;

        public sealed override T? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Bridge from resumable to value converters.

            ReadStack state = default;
            RdnTypeInfo rdnTypeInfo = options.GetTypeInfoInternal(typeToConvert);
            state.Initialize(rdnTypeInfo);

            TryRead(ref reader, typeToConvert, options, ref state, out T? value, out _);
            return value;
        }

        public sealed override void Write(Utf8RdnWriter writer, T value, RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Bridge from resumable to value converters.
            WriteStack state = default;
            RdnTypeInfo typeInfo = options.GetTypeInfoInternal(typeof(T));
            state.Initialize(typeInfo);

            try
            {
                TryWrite(writer, value, options, ref state);
            }
            catch
            {
                state.DisposePendingDisposablesOnException();
                throw;
            }
        }
    }
}
