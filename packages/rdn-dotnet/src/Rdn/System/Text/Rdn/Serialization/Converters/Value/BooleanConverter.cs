// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class BooleanConverter : RdnPrimitiveConverter<bool>
    {
        public override bool Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            return reader.GetBoolean();
        }

        public override void Write(Utf8RdnWriter writer, bool value, RdnSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }

        internal override bool ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            ReadOnlySpan<byte> propertyName = reader.GetUnescapedSpan();
            if (!(Utf8Parser.TryParse(propertyName, out bool value, out int bytesConsumed)
                  && propertyName.Length == bytesConsumed))
            {
                ThrowHelper.ThrowFormatException(DataType.Boolean);
            }

            return value;
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, bool value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.Boolean };
    }
}
