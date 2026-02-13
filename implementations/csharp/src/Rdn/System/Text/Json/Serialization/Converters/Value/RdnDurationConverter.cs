// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnDurationConverter : JsonPrimitiveConverter<RdnDuration>
    {
        public override RdnDuration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.RdnDuration)
            {
                return reader.GetRdnDuration();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();
                if (str != null && str.Length > 0 && str[0] == 'P')
                {
                    return new RdnDuration(str);
                }
            }

            ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            return default; // unreachable
        }

        public override void Write(Utf8JsonWriter writer, RdnDuration value, JsonSerializerOptions options)
        {
            writer.WriteRdnDurationValue(value);
        }

        internal override RdnDuration ReadAsPropertyNameCore(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? str = reader.GetString();
                if (str != null)
                {
                    return new RdnDuration(str);
                }
            }
            ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            return default;
        }

        internal override void WriteAsPropertyNameCore(Utf8JsonWriter writer, RdnDuration value, JsonSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value.ToString());
        }

        internal override JsonSchema? GetSchema(JsonNumberHandling _) => new() { Type = JsonSchemaType.String, Format = "duration" };
    }
}
