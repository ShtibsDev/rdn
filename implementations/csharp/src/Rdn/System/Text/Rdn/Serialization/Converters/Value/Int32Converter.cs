// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class Int32Converter : RdnPrimitiveConverter<int>
    {
        public Int32Converter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override int Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetInt32();
        }

        public override void Write(Utf8RdnWriter writer, int value, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            // For performance, lift up the writer implementation.
            writer.WriteNumberValue((long)value);
        }

        internal override int ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return reader.GetInt32WithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, int value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override int ReadNumberWithCustomHandling(ref Utf8RdnReader reader, RdnNumberHandling handling, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.String &&
                (RdnNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return reader.GetInt32WithQuotes();
            }

            return reader.GetInt32();
        }

        internal override void WriteNumberWithCustomHandling(Utf8RdnWriter writer, int value, RdnNumberHandling handling)
        {
            if ((RdnNumberHandling.WriteAsString & handling) != 0)
            {
                writer.WriteNumberValueAsString(value);
            }
            else
            {
                // For performance, lift up the writer implementation.
                writer.WriteNumberValue((long)value);
            }
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling numberHandling) =>
            GetSchemaForNumericType(RdnSchemaType.Integer, numberHandling);
    }
}
