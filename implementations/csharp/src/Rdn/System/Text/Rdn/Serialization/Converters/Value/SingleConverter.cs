// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class SingleConverter : RdnPrimitiveConverter<float>
    {

        public SingleConverter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override float Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            return reader.GetSingle();
        }

        public override void Write(Utf8RdnWriter writer, float value, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            writer.WriteNumberValue(value);
        }

        internal override float ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return reader.GetSingleWithQuotes();
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, float value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            writer.WritePropertyName(value);
        }

        internal override float ReadNumberWithCustomHandling(ref Utf8RdnReader reader, RdnNumberHandling handling, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.String)
            {
                if ((RdnNumberHandling.AllowReadingFromString & handling) != 0)
                {
                    return reader.GetSingleWithQuotes();
                }
                else if ((RdnNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
                {
                    return reader.GetSingleFloatingPointConstant();
                }
            }

            return reader.GetSingle();
        }

        internal override void WriteNumberWithCustomHandling(Utf8RdnWriter writer, float value, RdnNumberHandling handling)
        {
            if ((RdnNumberHandling.WriteAsString & handling) != 0)
            {
                writer.WriteNumberValueAsString(value);
            }
            else if ((RdnNumberHandling.AllowNamedFloatingPointLiterals & handling) != 0)
            {
                writer.WriteFloatingPointConstant(value);
            }
            else
            {
                writer.WriteNumberValue(value);
            }
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling numberHandling) =>
            GetSchemaForNumericType(RdnSchemaType.Number, numberHandling, isIeeeFloatingPoint: true);
    }
}
