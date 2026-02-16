// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class CharConverter : RdnPrimitiveConverter<char>
    {
        private const int MaxEscapedCharacterLength = RdnConstants.MaxExpansionFactorWhileEscaping;

        public override char Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType is not (RdnTokenType.String or RdnTokenType.PropertyName))
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(reader.TokenType);
            }

            if (!RdnHelpers.IsInRangeInclusive(reader.ValueLength, 1, MaxEscapedCharacterLength))
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedChar(reader.TokenType);
            }

            Span<char> buffer = stackalloc char[MaxEscapedCharacterLength];
            int charsWritten = reader.CopyString(buffer);

            if (charsWritten != 1)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedChar(reader.TokenType);
            }

            return buffer[0];
        }

        public override void Write(Utf8RdnWriter writer, char value, RdnSerializerOptions options)
        {
            writer.WriteStringValue(
#if NET
                new ReadOnlySpan<char>(in value)
#else
                value.ToString()
#endif
                );
        }

        internal override char ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return Read(ref reader, typeToConvert, options);
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, char value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(
#if NET
                new ReadOnlySpan<char>(in value)
#else
                value.ToString()
#endif
                );
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) =>
            new() { Type = RdnSchemaType.String, MinLength = 1, MaxLength = 1 };
    }
}
