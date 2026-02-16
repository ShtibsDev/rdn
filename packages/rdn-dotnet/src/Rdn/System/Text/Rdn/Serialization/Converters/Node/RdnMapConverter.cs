// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnMapConverter : RdnConverter<RdnMap?>
    {
        public override void Write(Utf8RdnWriter writer, RdnMap? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override RdnMap? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case RdnTokenType.StartMap:
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

        internal static RdnMap ReadAsRdnElement(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            RdnElement jElement = RdnElement.ParseValue(ref reader);
            return new RdnMap(jElement, options);
        }

        internal static RdnMap ReadAsRdnNode(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.StartMap);

            RdnMap jMap = new RdnMap(options);

            while (reader.Read())
            {
                if (reader.TokenType == RdnTokenType.EndMap)
                {
                    return jMap;
                }

                // Read key
                RdnNode? key = RdnNodeConverter.ReadAsRdnNode(ref reader, options);

                // Read value
                if (!reader.Read())
                {
                    Debug.Fail("Expected value after map key.");
                    ThrowHelper.ThrowRdnException();
                }

                RdnNode? value = RdnNodeConverter.ReadAsRdnNode(ref reader, options);

                jMap.Add(key, value);
            }

            Debug.Fail("End map token not found.");
            ThrowHelper.ThrowRdnException();
            return null;
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.Array };
    }
}
