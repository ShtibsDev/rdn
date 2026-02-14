// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Serialization.Converters;

namespace Rdn.Serialization
{
    /// <summary>
    /// Converter to convert enums to and from numeric values.
    /// </summary>
    /// <typeparam name="TEnum">The enum type that this converter targets.</typeparam>
    /// <remarks>
    /// This is the default converter for enums and can be used to override
    /// <see cref="RdnSourceGenerationOptionsAttribute.UseStringEnumConverter"/>
    /// on individual types or properties.
    /// </remarks>
    public sealed class RdnNumberEnumConverter<TEnum> : RdnConverterFactory
        where TEnum : struct, Enum
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RdnNumberEnumConverter{TEnum}"/>.
        /// </summary>
        public RdnNumberEnumConverter() { }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TEnum);

        /// <inheritdoc />
        public override RdnConverter? CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            if (typeToConvert != typeof(TEnum))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_RdnConverterFactory_TypeNotSupported(typeToConvert);
            }

            return EnumConverterFactory.Helpers.Create<TEnum>(EnumConverterOptions.AllowNumbers, options);
        }
    }
}
