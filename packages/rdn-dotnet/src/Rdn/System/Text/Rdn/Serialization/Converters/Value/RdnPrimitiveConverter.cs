// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Inherited by built-in converters serializing types as RDN primitives that support property name serialization.
    /// </summary>
    internal abstract class RdnPrimitiveConverter<T> : RdnConverter<T>
    {
        public sealed override void WriteAsPropertyName(Utf8RdnWriter writer, [DisallowNull] T value, RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(value);

            WriteAsPropertyNameCore(writer, value, options, isWritingExtensionDataProperty: false);
        }

        public sealed override T ReadAsPropertyName(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.PropertyName)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedPropertyName(reader.TokenType);
            }

            return ReadAsPropertyNameCore(ref reader, typeToConvert, options);
        }
    }
}
