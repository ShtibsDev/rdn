// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class DateTimeOffsetConverter : JsonPrimitiveConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.RdnDateTime)
            {
                if (reader.TryGetRdnDateTime(out DateTime dt))
                {
                    return new DateTimeOffset(dt, TimeSpan.Zero);
                }
                ThrowHelper.ThrowFormatException(DataType.DateTimeOffset);
            }
            return reader.GetDateTimeOffset();
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteRdnDateTimeOffsetValue(value);
        }

        internal override DateTimeOffset ReadAsPropertyNameCore(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
            return reader.GetDateTimeOffsetNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override JsonSchema? GetSchema(JsonNumberHandling _) => new JsonSchema { Type = JsonSchemaType.String, Format = "date-time" };
    }
}
