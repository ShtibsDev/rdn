// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Rdn.Reflection;

namespace Rdn.Serialization.Converters
{
    [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
    internal sealed class NullableConverterFactory : RdnConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsNullableOfT();
        }

        public override RdnConverter CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(typeToConvert.IsNullableOfT());

            Type valueTypeToConvert = typeToConvert.GetGenericArguments()[0];
            RdnConverter valueConverter = options.GetConverterInternal(valueTypeToConvert);

            // If the value type has an interface or object converter, just return that converter directly.
            if (!valueConverter.Type!.IsValueType && valueTypeToConvert.IsValueType)
            {
                return valueConverter;
            }

            return CreateValueConverter(valueTypeToConvert, valueConverter);
        }

        public static RdnConverter CreateValueConverter(Type valueTypeToConvert, RdnConverter valueConverter)
        {
            Debug.Assert(valueTypeToConvert.IsValueType && !valueTypeToConvert.IsNullableOfT());
            return (RdnConverter)Activator.CreateInstance(
                GetNullableConverterType(valueTypeToConvert),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: new object[] { valueConverter },
                culture: null)!;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2071:UnrecognizedReflectionPattern",
            Justification = "'NullableConverter<T> where T : struct' implies 'T : new()', so the trimmer is warning calling MakeGenericType here because valueTypeToConvert's constructors are not annotated. " +
            "But NullableConverter doesn't call new T(), so this is safe.")]
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        private static Type GetNullableConverterType(Type valueTypeToConvert) => typeof(NullableConverter<>).MakeGenericType(valueTypeToConvert);
    }
}
