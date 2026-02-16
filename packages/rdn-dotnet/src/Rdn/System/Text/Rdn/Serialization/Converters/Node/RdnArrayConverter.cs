// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnArrayConverter : RdnConverter<RdnArray?>
    {
        public override void Write(Utf8RdnWriter writer, RdnArray? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override RdnArray? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case RdnTokenType.StartArray:
                    return options.AllowDuplicateProperties
                        ? ReadAsRdnElement(ref reader, options.GetNodeOptions())
                        : ReadAsRdnNode(ref reader, options.GetNodeOptions());
                case RdnTokenType.Null:
                    return null;
                default:
                    throw ThrowHelper.GetInvalidOperationException_ExpectedArray(reader.TokenType);
            }
        }

        internal static RdnArray ReadAsRdnElement(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            RdnElement jElement = RdnElement.ParseValue(ref reader);
            return new RdnArray(jElement, options);
        }

        internal static RdnArray ReadAsRdnNode(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.StartArray);

            RdnArray jArray = new RdnArray(options);

            while (reader.Read())
            {
                if (reader.TokenType == RdnTokenType.EndArray)
                {
                    return jArray;
                }

                RdnNode? item = RdnNodeConverter.ReadAsRdnNode(ref reader, options);
                jArray.Add(item);
            }

            // RDN is invalid so reader would have already thrown.
            Debug.Fail("End array token not found.");
            ThrowHelper.ThrowRdnException();
            return null;
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.Array };
    }
}
