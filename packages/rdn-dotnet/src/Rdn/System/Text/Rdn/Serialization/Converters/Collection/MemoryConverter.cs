// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Rdn.Serialization.Converters
{
    internal sealed class MemoryConverter<T> : RdnCollectionConverter<Memory<T>, T>
    {
        internal override bool CanHaveMetadata => false;
        public override bool HandleNull => true;

        internal override bool OnTryRead(
            ref Utf8RdnReader reader,
            Type typeToConvert,
            RdnSerializerOptions options,
            scoped ref ReadStack state,
            out Memory<T> value)
        {
            if (reader.TokenType is RdnTokenType.Null && state.Current.ReturnValue is null)
            {
                value = default;
                return true;
            }

            return base.OnTryRead(ref reader, typeToConvert, options, ref state, out value);
        }

        protected override void Add(in T value, ref ReadStack state)
        {
            ((List<T>)state.Current.ReturnValue!).Add(value);
        }

        protected override void CreateCollection(ref Utf8RdnReader reader, scoped ref ReadStack state, RdnSerializerOptions options)
        {
            state.Current.ReturnValue = new List<T>();
        }

        internal sealed override bool IsConvertibleCollection => true;
        protected override void ConvertCollection(ref ReadStack state, RdnSerializerOptions options)
        {
            Memory<T> memory = ((List<T>)state.Current.ReturnValue!).ToArray().AsMemory();
            state.Current.ReturnValue = memory;
        }

        protected override bool OnWriteResume(Utf8RdnWriter writer, Memory<T> value, RdnSerializerOptions options, ref WriteStack state)
        {
            return ReadOnlyMemoryConverter<T>.OnWriteResume(writer, value.Span, options, ref state);
        }
    }
}
