// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class JsonMapConverter : JsonConverter<JsonMap?>
    {
        public override void Write(Utf8JsonWriter writer, JsonMap? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override JsonMap? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartMap:
                    return options.AllowDuplicateProperties
                        ? ReadAsJsonElement(ref reader, options.GetNodeOptions())
                        : ReadAsJsonNode(ref reader, options.GetNodeOptions());
                case JsonTokenType.Null:
                    return null;
                default:
                    Debug.Assert(false);
                    throw new JsonException();
            }
        }

        internal static JsonMap ReadAsJsonElement(ref Utf8JsonReader reader, JsonNodeOptions options)
        {
            JsonElement jElement = JsonElement.ParseValue(ref reader);
            return new JsonMap(jElement, options);
        }

        internal static JsonMap ReadAsJsonNode(ref Utf8JsonReader reader, JsonNodeOptions options)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.StartMap);

            JsonMap jMap = new JsonMap(options);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndMap)
                {
                    return jMap;
                }

                // Read key
                JsonNode? key = JsonNodeConverter.ReadAsJsonNode(ref reader, options);

                // Read value
                if (!reader.Read())
                {
                    Debug.Fail("Expected value after map key.");
                    ThrowHelper.ThrowJsonException();
                }

                JsonNode? value = JsonNodeConverter.ReadAsJsonNode(ref reader, options);

                jMap.Add(key, value);
            }

            Debug.Fail("End map token not found.");
            ThrowHelper.ThrowJsonException();
            return null;
        }

        internal override JsonSchema? GetSchema(JsonNumberHandling _) => new() { Type = JsonSchemaType.Array };
    }
}
