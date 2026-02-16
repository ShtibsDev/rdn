// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class Int64Converter : RdnPrimitiveConverter<long>
    {
        public Int64Converter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override long Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetInt64();
        }

        public override void Write(Utf8RdnWriter writer, long value, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            writer.WriteNumberValue(value);
        }

        internal override long ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return reader.GetInt64WithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, long value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override long ReadNumberWithCustomHandling(ref Utf8RdnReader reader, RdnNumberHandling handling, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.String &&
                (RdnNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return reader.GetInt64WithQuotes();
            }

            return reader.GetInt64();
        }

        internal override void WriteNumberWithCustomHandling(Utf8RdnWriter writer, long value, RdnNumberHandling handling)
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
            GetSchemaForNumericType(RdnSchemaType.Integer, numberHandling);
    }
}
