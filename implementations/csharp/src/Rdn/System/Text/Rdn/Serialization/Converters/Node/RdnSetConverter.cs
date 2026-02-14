// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnSetConverter : RdnConverter<RdnSet?>
    {
        public override void Write(Utf8RdnWriter writer, RdnSet? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override RdnSet? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case RdnTokenType.StartSet:
                    return options.AllowDuplicateProperties
                        ? ReadAsRdnElement(ref reader, options.GetNodeOptions())
                        : ReadAsRdnNode(ref reader, options.GetNodeOptions());
                case RdnTokenType.Null:
                    return null;
                default:
                    Debug.Assert(false);
                    throw new RdnException();
            }
        }

        internal static RdnSet ReadAsRdnElement(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            RdnElement jElement = RdnElement.ParseValue(ref reader);
            return new RdnSet(jElement, options);
        }

        internal static RdnSet ReadAsRdnNode(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.StartSet);

            RdnSet jSet = new RdnSet(options);

            while (reader.Read())
            {
                if (reader.TokenType == RdnTokenType.EndSet)
                {
                    return jSet;
                }

                RdnNode? item = RdnNodeConverter.ReadAsRdnNode(ref reader, options);
                jSet.Add(item);
            }

            Debug.Fail("End set token not found.");
            ThrowHelper.ThrowRdnException();
            return null;
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.Array };
    }
}
