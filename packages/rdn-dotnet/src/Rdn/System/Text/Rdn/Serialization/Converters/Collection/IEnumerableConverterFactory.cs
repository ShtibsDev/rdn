// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Rdn.Reflection;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Converter factory for all IEnumerable types.
    /// </summary>
    [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
    internal sealed class IEnumerableConverterFactory : RdnConverterFactory
    {
        private static readonly IDictionaryConverter<IDictionary> s_converterForIDictionary = new IDictionaryConverter<IDictionary>();
        private static readonly IEnumerableConverter<IEnumerable> s_converterForIEnumerable = new IEnumerableConverter<IEnumerable>();
        private static readonly IListConverter<IList> s_converterForIList = new IListConverter<IList>();

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        public IEnumerableConverterFactory() { }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(IEnumerable).IsAssignableFrom(typeToConvert);
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "The ctor is marked RequiresUnreferencedCode.")]
        public override RdnConverter CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            Type converterType;
            Type[] genericArgs;
            Type? elementType = null;
            Type? dictionaryKeyType = null;
            Type? actualTypeToConvert;

            // Array
            if (typeToConvert.IsArray)
            {
                // Verify that we don't have a multidimensional array.
                if (typeToConvert.GetArrayRank() > 1)
                {
                    return UnsupportedTypeConverterFactory.CreateUnsupportedConverterForType(typeToConvert);
                }

                converterType = typeof(ArrayConverter<,>);
                elementType = typeToConvert.GetElementType();
            }
            // List<> or deriving from List<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericBaseClass(typeof(List<>))) != null)
            {
                converterType = typeof(ListOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // Dictionary<TKey, TValue> or deriving from Dictionary<TKey, TValue>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericBaseClass(typeof(Dictionary<,>))) != null)
            {
                genericArgs = actualTypeToConvert.GetGenericArguments();
                converterType = typeof(DictionaryOfTKeyTValueConverter<,,>);
                dictionaryKeyType = genericArgs[0];
                elementType = genericArgs[1];
            }
            // IDictionary<TKey, TValue> or deriving from IDictionary<TKey, TValue>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IDictionary<,>))) != null)
            {
                genericArgs = actualTypeToConvert.GetGenericArguments();
                converterType = typeof(IDictionaryOfTKeyTValueConverter<,,>);
                dictionaryKeyType = genericArgs[0];
                elementType = genericArgs[1];
            }
            // IReadOnlyDictionary<TKey, TValue> or deriving from IReadOnlyDictionary<TKey, TValue>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IReadOnlyDictionary<,>))) != null)
            {
                genericArgs = actualTypeToConvert.GetGenericArguments();
                converterType = typeof(IReadOnlyDictionaryOfTKeyTValueConverter<,,>);
                dictionaryKeyType = genericArgs[0];
                elementType = genericArgs[1];
            }
            // IList<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IList<>))) != null)
            {
                converterType = typeof(IListOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // ISet<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(ISet<>))) != null)
            {
                converterType = typeof(ISetOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
#if NET
            // IReadOnlySet<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IReadOnlySet<>))) != null)
            {
                converterType = typeof(IReadOnlySetOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
#endif
            // ICollection<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(ICollection<>))) != null)
            {
                converterType = typeof(ICollectionOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // IEnumerable<>, types assignable from List<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IEnumerable<>))) != null)
            {
                converterType = typeof(IEnumerableOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // Check for non-generics after checking for generics.
            else if (typeof(IDictionary).IsAssignableFrom(typeToConvert))
            {
                if (typeToConvert == typeof(IDictionary))
                {
                    return s_converterForIDictionary;
                }

                converterType = typeof(IDictionaryConverter<>);
            }
            else if (typeof(IList).IsAssignableFrom(typeToConvert))
            {
                if (typeToConvert == typeof(IList))
                {
                    return s_converterForIList;
                }

                converterType = typeof(IListConverter<>);
            }
            else
            {
                Debug.Assert(typeof(IEnumerable).IsAssignableFrom(typeToConvert));
                if (typeToConvert == typeof(IEnumerable))
                {
                    return s_converterForIEnumerable;
                }

                converterType = typeof(IEnumerableConverter<>);
            }

            Type genericType;
            int numberOfGenericArgs = converterType.GetGenericArguments().Length;
            if (numberOfGenericArgs == 1)
            {
                genericType = converterType.MakeGenericType(typeToConvert);
            }
            else if (numberOfGenericArgs == 2)
            {
                RdnTypeInfo.ValidateType(elementType!);

                genericType = converterType.MakeGenericType(typeToConvert, elementType!);
            }
            else
            {
                Debug.Assert(numberOfGenericArgs == 3);

                RdnTypeInfo.ValidateType(elementType!);
                RdnTypeInfo.ValidateType(dictionaryKeyType!);

                genericType = converterType.MakeGenericType(typeToConvert, dictionaryKeyType!, elementType!);
            }

            RdnConverter converter = (RdnConverter)Activator.CreateInstance(
                genericType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: null,
                culture: null)!;

            return converter;
        }
    }
}
