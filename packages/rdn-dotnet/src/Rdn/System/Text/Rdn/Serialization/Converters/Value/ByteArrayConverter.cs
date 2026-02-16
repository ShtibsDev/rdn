// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class ByteArrayConverter : RdnConverter<byte[]?>
    {
        public override byte[]? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == RdnTokenType.RdnBinary)
            {
                return reader.GetRdnBinary();
            }

            // Fallback: existing base64 string path
            return reader.GetBytesFromBase64();
        }

        public override void Write(Utf8RdnWriter writer, byte[]? value, RdnSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else if (options.BinaryFormat == RdnBinaryFormat.Hex)
            {
                writer.WriteRdnBinaryHexValue(value);
            }
            else
            {
                writer.WriteRdnBinaryValue(value);
            }
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.String };
    }
}
