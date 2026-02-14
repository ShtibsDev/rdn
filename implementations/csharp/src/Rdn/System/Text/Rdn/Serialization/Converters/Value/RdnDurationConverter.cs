// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnDurationConverter : RdnPrimitiveConverter<RdnDuration>
    {
        public override RdnDuration Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.RdnDuration)
            {
                return reader.GetRdnDuration();
            }

            if (reader.TokenType == RdnTokenType.String)
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

        public override void Write(Utf8RdnWriter writer, RdnDuration value, RdnSerializerOptions options)
        {
            writer.WriteRdnDurationValue(value);
        }

        internal override RdnDuration ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.PropertyName)
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

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, RdnDuration value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value.ToString());
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.String, Format = "duration" };
    }
}
