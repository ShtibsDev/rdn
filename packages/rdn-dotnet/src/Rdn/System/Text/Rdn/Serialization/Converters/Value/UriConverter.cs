// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class UriConverter : RdnPrimitiveConverter<Uri?>
    {
        public override Uri? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            return reader.TokenType is RdnTokenType.Null ? null : ReadCore(ref reader);
        }

        public override void Write(Utf8RdnWriter writer, Uri? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.OriginalString);
        }

        internal override Uri ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType is RdnTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        private static Uri ReadCore(ref Utf8RdnReader reader)
        {
            string? uriString = reader.GetString();

            if (!Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out Uri? value))
            {
                ThrowHelper.ThrowRdnException();
            }

            return value;
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, Uri value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ArgumentNullException.ThrowIfNull(value);

            writer.WritePropertyName(value.OriginalString);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) =>
            new() { Type = RdnSchemaType.String, Format = "uri" };
    }
}
