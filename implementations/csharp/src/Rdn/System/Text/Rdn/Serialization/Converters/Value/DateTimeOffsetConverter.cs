// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class DateTimeOffsetConverter : RdnPrimitiveConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.RdnDateTime)
            {
                if (reader.TryGetRdnDateTime(out DateTime dt))
                {
                    return new DateTimeOffset(dt, TimeSpan.Zero);
                }
                ThrowHelper.ThrowFormatException(DataType.DateTimeOffset);
            }
            return reader.GetDateTimeOffset();
        }

        public override void Write(Utf8RdnWriter writer, DateTimeOffset value, RdnSerializerOptions options)
        {
            writer.WriteRdnDateTimeOffsetValue(value);
        }

        internal override DateTimeOffset ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return reader.GetDateTimeOffsetNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, DateTimeOffset value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new RdnSchema { Type = RdnSchemaType.String, Format = "date-time" };
    }
}
