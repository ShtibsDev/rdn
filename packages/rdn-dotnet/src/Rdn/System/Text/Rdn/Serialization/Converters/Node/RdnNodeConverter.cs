// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Converter for RdnNode-derived types. The {T} value must be Object and not RdnNode
    /// since we allow Object-declared members\variables to deserialize as {RdnNode}.
    /// </summary>
    internal sealed class RdnNodeConverter : RdnConverter<RdnNode?>
    {
        internal static RdnNodeConverter Instance { get; } = new RdnNodeConverter();

        public override void Write(Utf8RdnWriter writer, RdnNode? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.WriteTo(writer, options);
            }
        }

        public override RdnNode? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            return options.AllowDuplicateProperties
                ? ReadAsRdnElement(ref reader, options.GetNodeOptions())
                : ReadAsRdnNode(ref reader, options.GetNodeOptions());
        }

        internal static RdnNode? ReadAsRdnElement(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            switch (reader.TokenType)
            {
                case RdnTokenType.String:
                case RdnTokenType.False:
                case RdnTokenType.True:
                case RdnTokenType.Number:
                    return RdnValueConverter.ReadNonNullPrimitiveValue(ref reader, options);
                case RdnTokenType.StartObject:
                    return RdnObjectConverter.ReadAsRdnElement(ref reader, options);
                case RdnTokenType.StartArray:
                    return RdnArrayConverter.ReadAsRdnElement(ref reader, options);
                case RdnTokenType.StartSet:
                    return RdnSetConverter.ReadAsRdnElement(ref reader, options);
                case RdnTokenType.StartMap:
                    return RdnMapConverter.ReadAsRdnElement(ref reader, options);
                case RdnTokenType.Null:
                    return null;
                default:
                    Debug.Assert(false);
                    throw new RdnException();
            }
        }

        internal static RdnNode? ReadAsRdnNode(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            switch (reader.TokenType)
            {
                case RdnTokenType.String:
                case RdnTokenType.False:
                case RdnTokenType.True:
                case RdnTokenType.Number:
                    return RdnValueConverter.ReadNonNullPrimitiveValue(ref reader, options);
                case RdnTokenType.StartObject:
                    return RdnObjectConverter.ReadAsRdnNode(ref reader, options);
                case RdnTokenType.StartArray:
                    return RdnArrayConverter.ReadAsRdnNode(ref reader, options);
                case RdnTokenType.StartSet:
                    return RdnSetConverter.ReadAsRdnNode(ref reader, options);
                case RdnTokenType.StartMap:
                    return RdnMapConverter.ReadAsRdnNode(ref reader, options);
                case RdnTokenType.Null:
                    return null;
                default:
                    Debug.Assert(false);
                    throw new RdnException();
            }
        }

        public static RdnNode? Create(RdnElement element, RdnNodeOptions? options)
        {
            RdnNode? node;

            switch (element.ValueKind)
            {
                case RdnValueKind.Null:
                    node = null;
                    break;
                case RdnValueKind.Object:
                    node = new RdnObject(element, options);
                    break;
                case RdnValueKind.Array:
                    node = new RdnArray(element, options);
                    break;
                case RdnValueKind.Set:
                    node = new RdnSet(element, options);
                    break;
                case RdnValueKind.Map:
                    node = new RdnMap(element, options);
                    break;
                default:
                    node = new RdnValueOfElement(element, options);
                    break;
            }

            return node;
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => RdnSchema.CreateTrueSchema();
    }
}
