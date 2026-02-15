// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class DateTimeConverter : RdnPrimitiveConverter<DateTime>
    {
        public override DateTime Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.RdnDateTime)
            {
                ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(typeof(DateTime));
            }
            return reader.GetRdnDateTime();
        }

        public override void Write(Utf8RdnWriter writer, DateTime value, RdnSerializerOptions options)
        {
            writer.WriteRdnDateTimeValue(value);
        }

        internal override DateTime ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return reader.GetDateTimeNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, DateTime value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new RdnSchema { Type = RdnSchemaType.String, Format = "date-time" };
    }
}
