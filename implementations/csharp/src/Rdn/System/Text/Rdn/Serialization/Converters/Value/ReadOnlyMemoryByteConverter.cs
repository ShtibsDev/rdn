// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class ReadOnlyMemoryByteConverter : RdnConverter<ReadOnlyMemory<byte>>
    {
        public override bool HandleNull => true;

        public override ReadOnlyMemory<byte> Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType is RdnTokenType.Null)
                return default;
            if (reader.TokenType == RdnTokenType.RdnBinary)
                return reader.GetRdnBinary();
            return reader.GetBytesFromBase64();
        }

        public override void Write(Utf8RdnWriter writer, ReadOnlyMemory<byte> value, RdnSerializerOptions options)
        {
            if (options.BinaryFormat == RdnBinaryFormat.Hex)
            {
                writer.WriteRdnBinaryHexValue(value.Span);
            }
            else
            {
                writer.WriteRdnBinaryValue(value.Span);
            }
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.String };
    }
}
