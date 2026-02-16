// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Numerics;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class BigIntegerConverter : RdnConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.RdnBigInteger)
            {
                return reader.GetBigInteger();
            }

            // Fallback: try to read a regular number as a BigInteger
            if (reader.TokenType == RdnTokenType.Number)
            {
                ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
                string digitStr = System.Text.Encoding.UTF8.GetString(span);
                if (BigInteger.TryParse(digitStr, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out BigInteger value))
                {
                    return value;
                }
            }

            ThrowHelper.ThrowFormatException();
            return default; // unreachable
        }

        public override void Write(Utf8RdnWriter writer, BigInteger value, RdnSerializerOptions options)
        {
            writer.WriteBigIntegerValue(value);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.Integer };
    }
}
