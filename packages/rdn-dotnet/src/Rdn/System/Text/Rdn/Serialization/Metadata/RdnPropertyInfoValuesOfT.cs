// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Reflection;

namespace Rdn.Serialization.Metadata
{
    /// <summary>
    /// Provides serialization metadata about a property or field.
    /// </summary>
    /// <typeparam name="T">The type to convert of the <see cref="RdnConverter{T}"/> for the property.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class RdnPropertyInfoValues<T>
    {
        /// <summary>
        /// If <see langword="true"/>, indicates that the member is a property, otherwise indicates the member is a field.
        /// </summary>
        public bool IsProperty { get; init; }

        /// <summary>
        /// Whether the property or field is public.
        /// </summary>
        public bool IsPublic { get; init; }

        /// <summary>
        /// Whether the property or field is a virtual property.
        /// </summary>
        public bool IsVirtual { get; init; }

        /// <summary>
        /// The declaring type of the property or field.
        /// </summary>
        public Type DeclaringType { get; init; } = null!;

        /// <summary>
        /// The <see cref="RdnTypeInfo"/> info for the property or field's type.
        /// </summary>
        public RdnTypeInfo PropertyTypeInfo { get; init; } = null!;

        /// <summary>
        /// A <see cref="RdnConverter"/> for the property or field, specified by <see cref="RdnConverterAttribute"/>.
        /// </summary>
        public RdnConverter<T>? Converter { get; init; }

        /// <summary>
        /// Provides a mechanism to get the property or field's value.
        /// </summary>
        public Func<object, T?>? Getter { get; init; }

        /// <summary>
        /// Provides a mechanism to set the property or field's value.
        /// </summary>
        public Action<object, T?>? Setter { get; init; }

        /// <summary>
        /// Specifies a condition for the member to be ignored.
        /// </summary>
        public RdnIgnoreCondition? IgnoreCondition { get; init; }

        /// <summary>
        /// Whether the property was annotated with <see cref="RdnIncludeAttribute"/>.
        /// </summary>
        public bool HasRdnInclude { get; init; }

        /// <summary>
        /// Whether the property was annotated with <see cref="RdnExtensionDataAttribute"/>.
        /// </summary>
        public bool IsExtensionData { get; init; }

        /// <summary>
        /// If the property or field is a number, specifies how it should processed when serializing and deserializing.
        /// </summary>
        public RdnNumberHandling? NumberHandling { get; init; }

        /// <summary>
        /// The name of the property or field.
        /// </summary>
        public string PropertyName { get; init; } = null!;

        /// <summary>
        /// The name to be used when processing the property or field, specified by <see cref="RdnPropertyNameAttribute"/>.
        /// </summary>
        public string? RdnPropertyName { get; init; }

        /// <summary>
        /// Provides a <see cref="ICustomAttributeProvider"/> factory that maps to <see cref="RdnPropertyInfo.AttributeProvider"/>.
        /// </summary>
        public Func<ICustomAttributeProvider>? AttributeProviderFactory { get; init; }
    }
}
