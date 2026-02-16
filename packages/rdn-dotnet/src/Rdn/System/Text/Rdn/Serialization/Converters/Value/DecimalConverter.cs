// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class DecimalConverter : RdnPrimitiveConverter<decimal>
    {
        public DecimalConverter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override decimal Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetDecimal();
        }

        public override void Write(Utf8RdnWriter writer, decimal value, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            writer.WriteNumberValue(value);
        }

        internal override decimal ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return reader.GetDecimalWithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, decimal value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override decimal ReadNumberWithCustomHandling(ref Utf8RdnReader reader, RdnNumberHandling handling, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.String &&
                (RdnNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return reader.GetDecimalWithQuotes();
            }

            return reader.GetDecimal();
        }

        internal override void WriteNumberWithCustomHandling(Utf8RdnWriter writer, decimal value, RdnNumberHandling handling)
        {
            if ((RdnNumberHandling.WriteAsString & handling) != 0)
            {
                writer.WriteNumberValueAsString(value);
            }
            else
            {
                writer.WriteNumberValue(value);
            }
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling numberHandling) =>
            GetSchemaForNumericType(RdnSchemaType.Number, numberHandling);
    }
}
