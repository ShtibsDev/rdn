// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
    internal sealed class TupleConverterFactory : RdnConverterFactory
    {
        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        public TupleConverterFactory() { }

        public override bool CanConvert(Type typeToConvert)
        {
            return IsTupleType(typeToConvert);
        }

        internal static bool IsTupleType(Type type)
        {
            if (!type.IsGenericType)
                return false;

            Type genericDef = type.GetGenericTypeDefinition();
            return IsValueTupleDefinition(genericDef) || IsReferenceTupleDefinition(genericDef);
        }

        internal static bool IsValueTupleType(Type type)
        {
            if (!type.IsGenericType)
                return false;

            return IsValueTupleDefinition(type.GetGenericTypeDefinition());
        }

        internal static bool IsReferenceTupleType(Type type)
        {
            if (!type.IsGenericType)
                return false;

            return IsReferenceTupleDefinition(type.GetGenericTypeDefinition());
        }

        private static bool IsValueTupleDefinition(Type genericDef)
        {
            return genericDef == typeof(ValueTuple<>)
                || genericDef == typeof(ValueTuple<,>)
                || genericDef == typeof(ValueTuple<,,>)
                || genericDef == typeof(ValueTuple<,,,>)
                || genericDef == typeof(ValueTuple<,,,,>)
                || genericDef == typeof(ValueTuple<,,,,,>)
                || genericDef == typeof(ValueTuple<,,,,,,>)
                || genericDef == typeof(ValueTuple<,,,,,,,>);
        }

        private static bool IsReferenceTupleDefinition(Type genericDef)
        {
            return genericDef == typeof(Tuple<>)
                || genericDef == typeof(Tuple<,>)
                || genericDef == typeof(Tuple<,,>)
                || genericDef == typeof(Tuple<,,,>)
                || genericDef == typeof(Tuple<,,,,>)
                || genericDef == typeof(Tuple<,,,,,>)
                || genericDef == typeof(Tuple<,,,,,,>)
                || genericDef == typeof(Tuple<,,,,,,,>);
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "The ctor is marked RequiresUnreferencedCode.")]
        public override RdnConverter CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            Type[] genericArgs = typeToConvert.GetGenericArguments();
            bool isValueTuple = IsValueTupleType(typeToConvert);

            // Collect the element types, flattening nested Rest tuples for 8+ element tuples
            var elementTypes = new System.Collections.Generic.List<Type>();
            CollectElementTypes(typeToConvert, elementTypes);

            Type converterType = elementTypes.Count switch
            {
                1 => typeof(TupleConverter<,>).MakeGenericType(typeToConvert, elementTypes[0]),
                2 => typeof(TupleConverter<,,>).MakeGenericType(typeToConvert, elementTypes[0], elementTypes[1]),
                3 => typeof(TupleConverter<,,,>).MakeGenericType(typeToConvert, elementTypes[0], elementTypes[1], elementTypes[2]),
                4 => typeof(TupleConverter<,,,,>).MakeGenericType(typeToConvert, elementTypes[0], elementTypes[1], elementTypes[2], elementTypes[3]),
                5 => typeof(TupleConverter<,,,,,>).MakeGenericType(typeToConvert, elementTypes[0], elementTypes[1], elementTypes[2], elementTypes[3], elementTypes[4]),
                6 => typeof(TupleConverter<,,,,,,>).MakeGenericType(typeToConvert, elementTypes[0], elementTypes[1], elementTypes[2], elementTypes[3], elementTypes[4], elementTypes[5]),
                7 => typeof(TupleConverter<,,,,,,,>).MakeGenericType(typeToConvert, elementTypes[0], elementTypes[1], elementTypes[2], elementTypes[3], elementTypes[4], elementTypes[5], elementTypes[6]),
                _ => throw new NotSupportedException($"Tuples with {elementTypes.Count} elements are not supported for RDN serialization. Maximum is 7."),
            };

            return (RdnConverter)Activator.CreateInstance(
                converterType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: null,
                culture: null)!;
        }

        private static void CollectElementTypes(Type tupleType, System.Collections.Generic.List<Type> elementTypes)
        {
            Type[] genericArgs = tupleType.GetGenericArguments();
            int directElements = Math.Min(genericArgs.Length, 7);

            for (int i = 0; i < directElements; i++)
            {
                elementTypes.Add(genericArgs[i]);
            }

            // For 8+ element tuples, the 8th generic arg is the rest tuple - flatten it
            if (genericArgs.Length == 8 && IsTupleType(genericArgs[7]))
            {
                CollectElementTypes(genericArgs[7], elementTypes);
            }
        }
    }
}
