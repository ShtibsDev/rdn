// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// Specifies compile-time source generator configuration when applied to <see cref="RdnSerializerContext"/> class declarations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    sealed class RdnSourceGenerationOptionsAttribute : RdnAttribute
    {
        /// <summary>
        /// Constructs a new <see cref="RdnSourceGenerationOptionsAttribute"/> instance.
        /// </summary>
        public RdnSourceGenerationOptionsAttribute() { }

        /// <summary>
        /// Constructs a new <see cref="RdnSourceGenerationOptionsAttribute"/> instance with a predefined set of options determined by the specified <see cref="RdnSerializerDefaults"/>.
        /// </summary>
        /// <param name="defaults">The <see cref="RdnSerializerDefaults"/> to reason about.</param>
        /// <exception cref="ArgumentOutOfRangeException">Invalid <paramref name="defaults"/> parameter.</exception>
        public RdnSourceGenerationOptionsAttribute(RdnSerializerDefaults defaults)
        {
            // Constructor kept in sync with equivalent overload in RdnSerializerOptions

            if (defaults is RdnSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true;
                PropertyNamingPolicy = RdnKnownNamingPolicy.CamelCase;
                NumberHandling = RdnNumberHandling.AllowReadingFromString;
            }
            else if (defaults is RdnSerializerDefaults.Strict)
            {
                UnmappedMemberHandling = RdnUnmappedMemberHandling.Disallow;
                AllowDuplicateProperties = false;
                RespectNullableAnnotations = true;
                RespectRequiredConstructorParameters = true;
            }
            else if (defaults is not RdnSerializerDefaults.General)
            {
                throw new ArgumentOutOfRangeException(nameof(defaults));
            }
        }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.AllowOutOfOrderMetadataProperties"/> when set.
        /// </summary>
        public bool AllowOutOfOrderMetadataProperties { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.AllowTrailingCommas"/> when set.
        /// </summary>
        public bool AllowTrailingCommas { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.Converters"/> when set.
        /// </summary>
        public Type[]? Converters { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.DefaultBufferSize"/> when set.
        /// </summary>
        public int DefaultBufferSize { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.DefaultIgnoreCondition"/> when set.
        /// </summary>
        public RdnIgnoreCondition DefaultIgnoreCondition { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.DictionaryKeyPolicy"/> when set.
        /// </summary>
        public RdnKnownNamingPolicy DictionaryKeyPolicy { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.IgnoreReadOnlyFields"/> when set.
        /// </summary>
        public bool IgnoreReadOnlyFields { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.IgnoreReadOnlyProperties"/> when set.
        /// </summary>
        public bool IgnoreReadOnlyProperties { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.IncludeFields"/> when set.
        /// </summary>
        public bool IncludeFields { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.MaxDepth"/> when set.
        /// </summary>
        public int MaxDepth { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.NumberHandling"/> when set.
        /// </summary>
        public RdnNumberHandling NumberHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.PreferredObjectCreationHandling"/> when set.
        /// </summary>
        public RdnObjectCreationHandling PreferredObjectCreationHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.PropertyNameCaseInsensitive"/> when set.
        /// </summary>
        public bool PropertyNameCaseInsensitive { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.PropertyNamingPolicy"/> when set.
        /// </summary>
        public RdnKnownNamingPolicy PropertyNamingPolicy { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.ReadCommentHandling"/> when set.
        /// </summary>
        public RdnCommentHandling ReadCommentHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.ReferenceHandler"/> when set.
        /// </summary>
        public RdnKnownReferenceHandler ReferenceHandler { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.RespectNullableAnnotations"/> when set.
        /// </summary>
        public bool RespectNullableAnnotations { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.RespectRequiredConstructorParameters"/> when set.
        /// </summary>
        public bool RespectRequiredConstructorParameters { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.UnknownTypeHandling"/> when set.
        /// </summary>
        public RdnUnknownTypeHandling UnknownTypeHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.UnmappedMemberHandling"/> when set.
        /// </summary>
        public RdnUnmappedMemberHandling UnmappedMemberHandling { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.WriteIndented"/> when set.
        /// </summary>
        public bool WriteIndented { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.IndentCharacter"/> when set.
        /// </summary>
        public char IndentCharacter { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.IndentCharacter"/> when set.
        /// </summary>
        public int IndentSize { get; set; }

        /// <summary>
        /// Specifies the default source generation mode for type declarations that don't set a <see cref="RdnSerializableAttribute.GenerationMode"/>.
        /// </summary>
        public RdnSourceGenerationMode GenerationMode { get; set; }

        /// <summary>
        /// Instructs the source generator to default to <see cref="RdnStringEnumConverter"/>
        /// instead of numeric serialization for all enum types encountered in its type graph.
        /// </summary>
        public bool UseStringEnumConverter { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.NewLine"/> when set.
        /// </summary>
        public string? NewLine { get; set; }

        /// <summary>
        /// Specifies the default value of <see cref="RdnSerializerOptions.AllowDuplicateProperties"/> when set.
        /// </summary>
        public bool AllowDuplicateProperties { get; set; }
    }
}
