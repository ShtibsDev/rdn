// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization;
using Rdn.Serialization.Converters;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    public static partial class RdnSerializer
    {
        internal const string SerializationUnreferencedCodeMessage = "RDN serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a RdnTypeInfo or RdnSerializerContext, or make sure all of the required types are preserved.";
        internal const string SerializationRequiresDynamicCodeMessage = "RDN serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use Rdn source generation for native AOT applications.";

        /// <summary>
        /// Indicates whether unconfigured <see cref="RdnSerializerOptions"/> instances
        /// should be set to use the reflection-based <see cref="DefaultRdnTypeInfoResolver"/>.
        /// </summary>
        /// <remarks>
        /// The value of the property is backed by the "System.Text.Rdn.RdnSerializer.IsReflectionEnabledByDefault"
        /// <see cref="AppContext"/> setting and defaults to <see langword="true"/> if unset.
        /// </remarks>
        [FeatureSwitchDefinition("System.Text.Rdn.RdnSerializer.IsReflectionEnabledByDefault")]
        public static bool IsReflectionEnabledByDefault { get; } =
            AppContext.TryGetSwitch(
                switchName: "System.Text.Rdn.RdnSerializer.IsReflectionEnabledByDefault",
                isEnabled: out bool value)
            ? value : true;

        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        private static RdnTypeInfo GetTypeInfo(RdnSerializerOptions? options, Type inputType)
        {
            Debug.Assert(inputType != null);

            options ??= RdnSerializerOptions.Default;
            options.MakeReadOnly(populateMissingResolver: true);

            // In order to improve performance of polymorphic root-level object serialization,
            // we bypass GetTypeInfoForRootType and cache RdnTypeInfo<object> in a dedicated property.
            // This lets any derived types take advantage of the cache in GetTypeInfoForRootType themselves.
            return inputType == RdnTypeInfo.ObjectType
                ? options.ObjectTypeInfo
                : options.GetTypeInfoForRootType(inputType);
        }

        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        private static RdnTypeInfo<T> GetTypeInfo<T>(RdnSerializerOptions? options)
            => (RdnTypeInfo<T>)GetTypeInfo(options, typeof(T));

        private static RdnTypeInfo GetTypeInfo(RdnSerializerContext context, Type inputType)
        {
            Debug.Assert(context != null);
            Debug.Assert(inputType != null);

            RdnTypeInfo? info = context.GetTypeInfo(inputType);
            if (info is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NoMetadataForType(inputType, context);
            }

            info.EnsureConfigured();
            return info;
        }

        private static void ValidateInputType(object? value, Type inputType)
        {
            ArgumentNullException.ThrowIfNull(inputType);

            if (value is not null)
            {
                Type runtimeType = value.GetType();
                if (!inputType.IsAssignableFrom(runtimeType))
                {
                    ThrowHelper.ThrowArgumentException_DeserializeWrongType(inputType, value);
                }
            }
        }

        internal static bool IsValidNumberHandlingValue(RdnNumberHandling handling) =>
            RdnHelpers.IsInRangeInclusive((int)handling, 0,
                (int)(
                RdnNumberHandling.Strict |
                RdnNumberHandling.AllowReadingFromString |
                RdnNumberHandling.WriteAsString));

        internal static bool IsValidCreationHandlingValue(RdnObjectCreationHandling handling) =>
            handling is RdnObjectCreationHandling.Replace or RdnObjectCreationHandling.Populate;

        internal static bool IsValidUnmappedMemberHandlingValue(RdnUnmappedMemberHandling handling) =>
            handling is RdnUnmappedMemberHandling.Skip or RdnUnmappedMemberHandling.Disallow;

        [return: NotNullIfNotNull(nameof(value))]
        internal static T? UnboxOnRead<T>(object? value)
        {
            if (value is null)
            {
                if (default(T) is not null)
                {
                    // Casting null values to a non-nullable struct throws NullReferenceException.
                    ThrowUnableToCastValue(value);
                }

                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            ThrowUnableToCastValue(value);
            return default!;

            static void ThrowUnableToCastValue(object? value)
            {
                if (value is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_DeserializeUnableToAssignNull(declaredType: typeof(T));
                }
                else
                {
                    ThrowHelper.ThrowInvalidCastException_DeserializeUnableToAssignValue(typeOfValue: value.GetType(), declaredType: typeof(T));
                }
            }
        }

        [return: NotNullIfNotNull(nameof(value))]
        internal static T? UnboxOnWrite<T>(object? value)
        {
            if (default(T) is not null && value is null)
            {
                // Casting null values to a non-nullable struct throws NullReferenceException.
                ThrowHelper.ThrowRdnException_DeserializeUnableToConvertValue(typeof(T));
            }

            return (T?)value;
        }

        private static RdnTypeInfo<List<T?>> GetOrAddListTypeInfoForRootLevelValueMode<T>(RdnTypeInfo<T> elementTypeInfo)
        {
            if (elementTypeInfo._asyncEnumerableRootLevelValueTypeInfo != null)
            {
                return (RdnTypeInfo<List<T?>>)elementTypeInfo._asyncEnumerableRootLevelValueTypeInfo;
            }

            var converter = new RootLevelListConverter<T>(elementTypeInfo);
            var listTypeInfo = new RdnTypeInfo<List<T?>>(converter, elementTypeInfo.Options)
            {
                ElementTypeInfo = elementTypeInfo,
            };

            listTypeInfo.EnsureConfigured();
            elementTypeInfo._asyncEnumerableRootLevelValueTypeInfo = listTypeInfo;
            return listTypeInfo;
        }

        private static RdnTypeInfo<List<T?>> GetOrAddListTypeInfoForArrayMode<T>(RdnTypeInfo<T> elementTypeInfo)
        {
            if (elementTypeInfo._asyncEnumerableArrayTypeInfo != null)
            {
                return (RdnTypeInfo<List<T?>>)elementTypeInfo._asyncEnumerableArrayTypeInfo;
            }

            var converter = new ListOfTConverter<List<T>, T>();
            var listTypeInfo = new RdnTypeInfo<List<T?>>(converter, elementTypeInfo.Options)
            {
                CreateObject = static () => new List<T?>(),
                ElementTypeInfo = elementTypeInfo,
            };

            listTypeInfo.EnsureConfigured();
            elementTypeInfo._asyncEnumerableArrayTypeInfo = listTypeInfo;
            return listTypeInfo;
        }
    }
}
