// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class GuidConverter : RdnPrimitiveConverter<Guid>
    {
        public override Guid Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            return reader.GetGuid();
        }

        public override void Write(Utf8RdnWriter writer, Guid value, RdnSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }

        internal override Guid ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return reader.GetGuidNoValidation();
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, Guid value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling numberHandling) =>
            new() { Type = RdnSchemaType.String, Format = "uuid" };
    }
}
