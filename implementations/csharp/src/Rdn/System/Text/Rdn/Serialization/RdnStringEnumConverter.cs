// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Converters;

namespace Rdn.Serialization
{
    /// <summary>
    /// Converter to convert enums to and from strings.
    /// </summary>
    /// <remarks>
    /// Reading is case insensitive, writing can be customized via a <see cref="RdnNamingPolicy" />.
    /// </remarks>
    /// <typeparam name="TEnum">The enum type that this converter targets.</typeparam>
    public class RdnStringEnumConverter<TEnum> : RdnConverterFactory
        where TEnum : struct, Enum
    {
        private readonly RdnNamingPolicy? _namingPolicy;
        private readonly EnumConverterOptions _converterOptions;

        /// <summary>
        /// Constructor. Creates the <see cref="RdnStringEnumConverter"/> with the
        /// default naming policy and allows integer values.
        /// </summary>
        public RdnStringEnumConverter() : this(namingPolicy: null, allowIntegerValues: true)
        {
            // An empty constructor is needed for construction via attributes
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="namingPolicy">
        /// Optional naming policy for writing enum values.
        /// </param>
        /// <param name="allowIntegerValues">
        /// True to allow undefined enum values. When true, if an enum value isn't
        /// defined it will output as a number rather than a string.
        /// </param>
        public RdnStringEnumConverter(RdnNamingPolicy? namingPolicy = null, bool allowIntegerValues = true)
        {
            _namingPolicy = namingPolicy;
            _converterOptions = allowIntegerValues
                ? EnumConverterOptions.AllowNumbers | EnumConverterOptions.AllowStrings
                : EnumConverterOptions.AllowStrings;
        }

        /// <inheritdoc />
        public sealed override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TEnum);

        /// <inheritdoc />
        public sealed override RdnConverter? CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            if (typeToConvert != typeof(TEnum))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_RdnConverterFactory_TypeNotSupported(typeToConvert);
            }

            return EnumConverterFactory.Helpers.Create<TEnum>(_converterOptions, options, _namingPolicy);
        }
    }

    /// <summary>
    /// Converter to convert enums to and from strings.
    /// </summary>
    /// <remarks>
    /// Reading is case insensitive, writing can be customized via a <see cref="RdnNamingPolicy" />.
    /// </remarks>
    [RequiresDynamicCode(
        "RdnStringEnumConverter cannot be statically analyzed and requires runtime code generation. " +
        "Applications should use the generic RdnStringEnumConverter<TEnum> instead.")]
    public class RdnStringEnumConverter : RdnConverterFactory
    {
        private readonly RdnNamingPolicy? _namingPolicy;
        private readonly EnumConverterOptions _converterOptions;

        /// <summary>
        /// Constructor. Creates the <see cref="RdnStringEnumConverter"/> with the
        /// default naming policy and allows integer values.
        /// </summary>
        public RdnStringEnumConverter() : this(namingPolicy: null, allowIntegerValues: true)
        {
            // An empty constructor is needed for construction via attributes
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="namingPolicy">
        /// Optional naming policy for writing enum values.
        /// </param>
        /// <param name="allowIntegerValues">
        /// True to allow undefined enum values. When true, if an enum value isn't
        /// defined it will output as a number rather than a string.
        /// </param>
        public RdnStringEnumConverter(RdnNamingPolicy? namingPolicy = null, bool allowIntegerValues = true)
        {
            _namingPolicy = namingPolicy;
            _converterOptions = allowIntegerValues
                ? EnumConverterOptions.AllowNumbers | EnumConverterOptions.AllowStrings
                : EnumConverterOptions.AllowStrings;
        }

        /// <inheritdoc />
        public sealed override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        /// <inheritdoc />
        public sealed override RdnConverter CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            if (!typeToConvert.IsEnum)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_RdnConverterFactory_TypeNotSupported(typeToConvert);
            }

            return EnumConverterFactory.Create(typeToConvert, _converterOptions, _namingPolicy, options);
        }
    }
}
