// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class JsonSetConverter : JsonConverter<JsonSet?>
    {
        public override void Write(Utf8JsonWriter writer, JsonSet? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override JsonSet? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartSet:
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

        internal static JsonSet ReadAsJsonElement(ref Utf8JsonReader reader, JsonNodeOptions options)
        {
            JsonElement jElement = JsonElement.ParseValue(ref reader);
            return new JsonSet(jElement, options);
        }

        internal static JsonSet ReadAsJsonNode(ref Utf8JsonReader reader, JsonNodeOptions options)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.StartSet);

            JsonSet jSet = new JsonSet(options);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndSet)
                {
                    return jSet;
                }

                JsonNode? item = JsonNodeConverter.ReadAsJsonNode(ref reader, options);
                jSet.Add(item);
            }

            Debug.Fail("End set token not found.");
            ThrowHelper.ThrowJsonException();
            return null;
        }

        internal override JsonSchema? GetSchema(JsonNumberHandling _) => new() { Type = JsonSchemaType.Array };
    }
}
