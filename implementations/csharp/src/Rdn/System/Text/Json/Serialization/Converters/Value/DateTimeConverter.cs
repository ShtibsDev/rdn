// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class DateTimeConverter : JsonPrimitiveConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.RdnDateTime)
            {
                return reader.GetRdnDateTime();
            }
            return reader.GetDateTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteRdnDateTimeValue(value);
        }

        internal override DateTime ReadAsPropertyNameCore(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
            return reader.GetDateTimeNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override JsonSchema? GetSchema(JsonNumberHandling _) => new JsonSchema { Type = JsonSchemaType.String, Format = "date-time" };
    }
}
