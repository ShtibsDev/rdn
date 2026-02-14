// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Rdn.Reflection;
using Rdn.Serialization.Converters;

namespace Rdn.Serialization.Metadata
{
    public partial class DefaultRdnTypeInfoResolver
    {
        private static Dictionary<Type, RdnConverter>? s_defaultSimpleConverters;
        private static RdnConverterFactory[]? s_defaultFactoryConverters;

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private static RdnConverterFactory[] GetDefaultFactoryConverters()
        {
            return
            [
                // Check for disallowed types.
                new UnsupportedTypeConverterFactory(),
                // Nullable converter should always be next since it forwards to any nullable type.
                new NullableConverterFactory(),
                new EnumConverterFactory(),
                new RdnNodeConverterFactory(),
                new FSharpTypeConverterFactory(),
                // Tuples must be before IEnumerable since Tuple<> implements IEnumerable.
                new TupleConverterFactory(),
                new MemoryConverterFactory(),
                // IAsyncEnumerable takes precedence over IEnumerable.
                new IAsyncEnumerableConverterFactory(),
                // IEnumerable should always be second to last since they can convert any IEnumerable.
                new IEnumerableConverterFactory(),
                // Object should always be last since it converts any type.
                new ObjectConverterFactory()
            ];
        }

        private static Dictionary<Type, RdnConverter> GetDefaultSimpleConverters()
        {
            const int NumberOfSimpleConverters = 33;
            var converters = new Dictionary<Type, RdnConverter>(NumberOfSimpleConverters);

            // Use a dictionary for simple converters.
            // When adding to this, update NumberOfSimpleConverters above.
            Add(RdnMetadataServices.BooleanConverter);
            Add(RdnMetadataServices.ByteConverter);
            Add(RdnMetadataServices.ByteArrayConverter);
            Add(RdnMetadataServices.CharConverter);
            Add(RdnMetadataServices.DateTimeConverter);
            Add(RdnMetadataServices.DateTimeOffsetConverter);
#if NET
            Add(RdnMetadataServices.DateOnlyConverter);
            Add(RdnMetadataServices.TimeOnlyConverter);
            Add(RdnMetadataServices.HalfConverter);
#endif
            Add(RdnMetadataServices.DoubleConverter);
            Add(RdnMetadataServices.DecimalConverter);
            Add(RdnMetadataServices.GuidConverter);
            Add(RdnMetadataServices.Int16Converter);
            Add(RdnMetadataServices.Int32Converter);
            Add(RdnMetadataServices.Int64Converter);
            Add(RdnMetadataServices.RdnElementConverter);
            Add(RdnMetadataServices.RdnDocumentConverter);
            Add(RdnMetadataServices.MemoryByteConverter);
            Add(RdnMetadataServices.ReadOnlyMemoryByteConverter);
            Add(RdnMetadataServices.ObjectConverter);
            Add(RdnMetadataServices.SByteConverter);
            Add(RdnMetadataServices.SingleConverter);
            Add(RdnMetadataServices.StringConverter);
            Add(RdnMetadataServices.TimeSpanConverter);
            Add(RdnMetadataServices.UInt16Converter);
            Add(RdnMetadataServices.UInt32Converter);
            Add(RdnMetadataServices.UInt64Converter);
#if NET
            Add(RdnMetadataServices.Int128Converter);
            Add(RdnMetadataServices.UInt128Converter);
#endif
            Add(RdnMetadataServices.UriConverter);
            Add(RdnMetadataServices.VersionConverter);
            Add(RdnMetadataServices.RdnDurationConverter);
            Add(RdnMetadataServices.RegexConverter);

            Debug.Assert(converters.Count <= NumberOfSimpleConverters);

            return converters;

            void Add(RdnConverter converter) =>
                converters.Add(converter.Type!, converter);
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private static RdnConverter GetBuiltInConverter(Type typeToConvert)
        {
            s_defaultSimpleConverters ??= GetDefaultSimpleConverters();
            s_defaultFactoryConverters ??= GetDefaultFactoryConverters();

            if (s_defaultSimpleConverters.TryGetValue(typeToConvert, out RdnConverter? converter))
            {
                return converter;
            }
            else
            {
                foreach (RdnConverterFactory factory in s_defaultFactoryConverters)
                {
                    if (factory.CanConvert(typeToConvert))
                    {
                        converter = factory;
                        break;
                    }
                }

                // Since the object and IEnumerable converters cover all types, we should have a converter.
                Debug.Assert(converter != null);
                return converter;
            }
        }

        internal static bool TryGetDefaultSimpleConverter(Type typeToConvert, [NotNullWhen(true)] out RdnConverter? converter)
        {
            if (s_defaultSimpleConverters is null)
            {
                converter = null;
                return false;
            }

            return s_defaultSimpleConverters.TryGetValue(typeToConvert, out converter);
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private static RdnConverter? GetCustomConverterForMember(Type typeToConvert, MemberInfo memberInfo, RdnSerializerOptions options)
        {
            Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
            Debug.Assert(typeToConvert != null);

            RdnConverterAttribute? converterAttribute = memberInfo.GetUniqueCustomAttribute<RdnConverterAttribute>(inherit: false);
            return converterAttribute is null ? null : GetConverterFromAttribute(converterAttribute, typeToConvert, memberInfo, options);
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        internal static RdnConverter GetConverterForType(Type typeToConvert, RdnSerializerOptions options, bool resolveRdnConverterAttribute = true)
        {
            // Priority 1: Attempt to get custom converter from the Converters list.
            RdnConverter? converter = options.GetConverterFromList(typeToConvert);

            // Priority 2: Attempt to get converter from [RdnConverter] on the type being converted.
            if (resolveRdnConverterAttribute && converter == null)
            {
                RdnConverterAttribute? converterAttribute = typeToConvert.GetUniqueCustomAttribute<RdnConverterAttribute>(inherit: false);
                if (converterAttribute != null)
                {
                    converter = GetConverterFromAttribute(converterAttribute, typeToConvert: typeToConvert, memberInfo: null, options);
                }
            }

            // Priority 3: Query the built-in converters.
            converter ??= GetBuiltInConverter(typeToConvert);

            // Expand if factory converter & validate.
            converter = options.ExpandConverterFactory(converter, typeToConvert);
            if (!converter.Type!.IsInSubtypeRelationshipWith(typeToConvert))
            {
                ThrowHelper.ThrowInvalidOperationException_SerializationConverterNotCompatible(converter.GetType(), typeToConvert);
            }

            RdnSerializerOptions.CheckConverterNullabilityIsSameAsPropertyType(converter, typeToConvert);
            return converter;
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private static RdnConverter GetConverterFromAttribute(RdnConverterAttribute converterAttribute, Type typeToConvert, MemberInfo? memberInfo, RdnSerializerOptions options)
        {
            RdnConverter? converter;

            Type declaringType = memberInfo?.DeclaringType ?? typeToConvert;
            Type? converterType = converterAttribute.ConverterType;
            if (converterType == null)
            {
                // Allow the attribute to create the converter.
                converter = converterAttribute.CreateConverter(typeToConvert);
                if (converter == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(declaringType, memberInfo, typeToConvert);
                }
            }
            else
            {
                ConstructorInfo? ctor = converterType.GetConstructor(Type.EmptyTypes);
                if (!typeof(RdnConverter).IsAssignableFrom(converterType) || ctor == null || !ctor.IsPublic)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeInvalid(declaringType, memberInfo);
                }

                converter = (RdnConverter)Activator.CreateInstance(converterType)!;
            }

            Debug.Assert(converter != null);
            if (!converter.CanConvert(typeToConvert))
            {
                Type? underlyingType = Nullable.GetUnderlyingType(typeToConvert);
                if (underlyingType != null && converter.CanConvert(underlyingType))
                {
                    if (converter is RdnConverterFactory converterFactory)
                    {
                        converter = converterFactory.GetConverterInternal(underlyingType, options);
                    }

                    // Allow nullable handling to forward to the underlying type's converter.
                    return NullableConverterFactory.CreateValueConverter(underlyingType, converter);
                }

                ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(declaringType, memberInfo, typeToConvert);
            }

            return converter;
        }
    }
}
