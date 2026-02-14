// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Rdn.Serialization.Converters
{
    [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
    internal sealed class MemoryConverterFactory : RdnConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType || !typeToConvert.IsValueType)
            {
                return false;
            }

            Type typeDef = typeToConvert.GetGenericTypeDefinition();
            return typeDef == typeof(Memory<>) || typeDef == typeof(ReadOnlyMemory<>);
        }

        public override RdnConverter? CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(CanConvert(typeToConvert));

            Type converterType = typeToConvert.GetGenericTypeDefinition() == typeof(Memory<>) ?
                typeof(MemoryConverter<>) : typeof(ReadOnlyMemoryConverter<>);

            Type elementType = typeToConvert.GetGenericArguments()[0];

            return (RdnConverter)Activator.CreateInstance(
                converterType.MakeGenericType(elementType))!;
        }
    }
}
