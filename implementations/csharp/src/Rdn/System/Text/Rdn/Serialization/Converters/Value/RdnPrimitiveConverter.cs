// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Inherited by built-in converters serializing types as RDN primitives that support property name serialization.
    /// </summary>
    internal abstract class RdnPrimitiveConverter<T> : RdnConverter<T>
    {
        public sealed override void WriteAsPropertyName(Utf8RdnWriter writer, [DisallowNull] T value, RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(value);

            WriteAsPropertyNameCore(writer, value, options, isWritingExtensionDataProperty: false);
        }

        public sealed override T ReadAsPropertyName(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.PropertyName)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedPropertyName(reader.TokenType);
            }

            return ReadAsPropertyNameCore(ref reader, typeToConvert, options);
        }

        private protected static RdnSchema GetSchemaForNumericType(RdnSchemaType schemaType, RdnNumberHandling numberHandling, bool isIeeeFloatingPoint = false)
        {
            Debug.Assert(schemaType is RdnSchemaType.Integer or RdnSchemaType.Number);
            Debug.Assert(!isIeeeFloatingPoint || schemaType is RdnSchemaType.Number);
#if NET
            Debug.Assert(isIeeeFloatingPoint == (typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(Half)));
#endif
            string? pattern = null;

            if ((numberHandling & (RdnNumberHandling.AllowReadingFromString | RdnNumberHandling.WriteAsString)) != 0)
            {
                pattern = schemaType is RdnSchemaType.Integer
                    ? @"^-?(?:0|[1-9]\d*)$"
                    : isIeeeFloatingPoint
                        ? @"^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$"
                        : @"^-?(?:0|[1-9]\d*)(?:\.\d+)?$";

                schemaType |= RdnSchemaType.String;
            }

            if (isIeeeFloatingPoint && (numberHandling & RdnNumberHandling.AllowNamedFloatingPointLiterals) != 0)
            {
                return new RdnSchema
                {
                    AnyOf =
                    [
                        new RdnSchema { Type = schemaType, Pattern = pattern },
                        new RdnSchema { Enum = [(RdnNode)"NaN", (RdnNode)"Infinity", (RdnNode)"-Infinity"] },
                    ]
                };
            }

            return new RdnSchema { Type = schemaType, Pattern = pattern };
        }
    }
}
