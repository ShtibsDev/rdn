// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RegexConverter : JsonPrimitiveConverter<Regex>
    {
        public override Regex Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.RdnRegExp)
            {
                string source = reader.GetRdnRegExpSource();
                string flags = reader.GetRdnRegExpFlags();
                return new Regex(source, MapFlags(flags));
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();
                if (str != null && str.Length >= 2 && str[0] == '/')
                {
                    int lastSlash = str.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        string source = str.Substring(1, lastSlash - 1);
                        string flags = str.Substring(lastSlash + 1);
                        return new Regex(source, MapFlags(flags));
                    }
                }
            }

            ThrowHelper.ThrowFormatException();
            return default!; // unreachable
        }

        public override void Write(Utf8JsonWriter writer, Regex value, JsonSerializerOptions options)
        {
            string source = value.ToString();
            string flags = MapOptionsToFlags(value.Options);
            writer.WriteRdnRegExpValue(source, flags);
        }

        internal override Regex ReadAsPropertyNameCore(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? str = reader.GetString();
                if (str != null && str.Length >= 2 && str[0] == '/')
                {
                    int lastSlash = str.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        string source = str.Substring(1, lastSlash - 1);
                        string flags = str.Substring(lastSlash + 1);
                        return new Regex(source, MapFlags(flags));
                    }
                }
            }
            ThrowHelper.ThrowFormatException();
            return default!;
        }

        internal override void WriteAsPropertyNameCore(Utf8JsonWriter writer, Regex value, JsonSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            string source = value.ToString();
            string flags = MapOptionsToFlags(value.Options);
            writer.WritePropertyName($"/{source}/{flags}");
        }

        internal override JsonSchema? GetSchema(JsonNumberHandling _) => new() { Type = JsonSchemaType.String, Format = "regex" };

        private static RegexOptions MapFlags(string flags)
        {
            RegexOptions options = RegexOptions.None;
            foreach (char c in flags)
            {
                switch (c)
                {
                    case 'i': options |= RegexOptions.IgnoreCase; break;
                    case 'm': options |= RegexOptions.Multiline; break;
                    case 's': options |= RegexOptions.Singleline; break;
                    // g, d, u, v, y have no C# equivalent — accepted but not mapped
                }
            }
            return options;
        }

        private static string MapOptionsToFlags(RegexOptions options)
        {
            var flags = new System.Text.StringBuilder(8);
            if ((options & RegexOptions.IgnoreCase) != 0) flags.Append('i');
            if ((options & RegexOptions.Multiline) != 0) flags.Append('m');
            if ((options & RegexOptions.Singleline) != 0) flags.Append('s');
            return flags.ToString();
        }
    }
}
