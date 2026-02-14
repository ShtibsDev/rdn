// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class JsonNodeConverterFactory : JsonConverterFactory
    {
        private static readonly JsonArrayConverter s_arrayConverter = new JsonArrayConverter();
        private static readonly JsonObjectConverter s_objectConverter = new JsonObjectConverter();
        private static readonly JsonValueConverter s_valueConverter = new JsonValueConverter();
        private static readonly JsonSetConverter s_setConverter = new JsonSetConverter();
        private static readonly JsonMapConverter s_mapConverter = new JsonMapConverter();

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeof(JsonValue).IsAssignableFrom(typeToConvert))
            {
                return s_valueConverter;
            }

            if (typeof(JsonObject) == typeToConvert)
            {
                return s_objectConverter;
            }

            if (typeof(JsonArray) == typeToConvert)
            {
                return s_arrayConverter;
            }

            if (typeof(JsonSet) == typeToConvert)
            {
                return s_setConverter;
            }

            if (typeof(JsonMap) == typeToConvert)
            {
                return s_mapConverter;
            }

            Debug.Assert(typeof(JsonNode) == typeToConvert);
            return JsonNodeConverter.Instance;
        }

        public override bool CanConvert(Type typeToConvert) => typeof(JsonNode).IsAssignableFrom(typeToConvert);
    }
}
