// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnValueConverter : RdnConverter<RdnValue?>
    {
        public override void Write(Utf8RdnWriter writer, RdnValue? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override RdnValue? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType is RdnTokenType.Null)
            {
                return null;
            }

            switch (reader.TokenType)
            {
                case RdnTokenType.String:
                case RdnTokenType.False:
                case RdnTokenType.True:
                case RdnTokenType.Number:
                    return ReadNonNullPrimitiveValue(ref reader, options.GetNodeOptions());
                default:
                    RdnElement element = RdnElement.ParseValue(ref reader, options.AllowDuplicateProperties);
                    return RdnValue.CreateFromElement(ref element, options.GetNodeOptions());
            }
        }

        internal static RdnValue ReadNonNullPrimitiveValue(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            Debug.Assert(reader.TokenType is RdnTokenType.String or RdnTokenType.False or RdnTokenType.True or RdnTokenType.Number);
            return RdnValueOfRdnPrimitive.CreatePrimitiveValue(ref reader, options);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => RdnSchema.CreateTrueSchema();
    }
}
