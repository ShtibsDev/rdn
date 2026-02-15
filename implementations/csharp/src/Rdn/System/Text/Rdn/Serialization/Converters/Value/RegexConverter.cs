// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Text.RegularExpressions;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RegexConverter : RdnPrimitiveConverter<Regex>
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

        public override Regex Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.RdnRegExp)
            {
                string source = reader.GetRdnRegExpSource();
                string flags = reader.GetRdnRegExpFlags();
                return new Regex(source, MapFlags(flags), RegexTimeout);
            }

            if (reader.TokenType == RdnTokenType.String)
            {
                string? str = reader.GetString();
                if (str != null && str.Length >= 2 && str[0] == '/')
                {
                    int lastSlash = str.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        string source = str.Substring(1, lastSlash - 1);
                        string flags = str.Substring(lastSlash + 1);
                        return new Regex(source, MapFlags(flags), RegexTimeout);
                    }
                }
            }

            ThrowHelper.ThrowFormatException();
            return default!; // unreachable
        }

        public override void Write(Utf8RdnWriter writer, Regex value, RdnSerializerOptions options)
        {
            string source = value.ToString();
            string flags = MapOptionsToFlags(value.Options);
            writer.WriteRdnRegExpValue(source, flags);
        }

        internal override Regex ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.PropertyName)
            {
                string? str = reader.GetString();
                if (str != null && str.Length >= 2 && str[0] == '/')
                {
                    int lastSlash = str.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        string source = str.Substring(1, lastSlash - 1);
                        string flags = str.Substring(lastSlash + 1);
                        return new Regex(source, MapFlags(flags), RegexTimeout);
                    }
                }
            }
            ThrowHelper.ThrowFormatException();
            return default!;
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, Regex value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            string source = value.ToString();
            string flags = MapOptionsToFlags(value.Options);
            int totalLength = 1 + source.Length + 1 + flags.Length;
            char[]? rented = null;
            Span<char> buf = totalLength <= RdnConstants.StackallocCharThreshold
                ? stackalloc char[RdnConstants.StackallocCharThreshold]
                : (rented = ArrayPool<char>.Shared.Rent(totalLength));
            buf[0] = '/';
            source.AsSpan().CopyTo(buf.Slice(1));
            buf[1 + source.Length] = '/';
            flags.AsSpan().CopyTo(buf.Slice(2 + source.Length));
            writer.WritePropertyName(new string(buf.Slice(0, totalLength)));
            if (rented != null) ArrayPool<char>.Shared.Return(rented);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.String, Format = "regex" };

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
            Span<char> buf = stackalloc char[3];
            int len = 0;
            if ((options & RegexOptions.IgnoreCase) != 0) buf[len++] = 'i';
            if ((options & RegexOptions.Multiline) != 0) buf[len++] = 'm';
            if ((options & RegexOptions.Singleline) != 0) buf[len++] = 's';
            return new string(buf.Slice(0, len));
        }
    }
}
