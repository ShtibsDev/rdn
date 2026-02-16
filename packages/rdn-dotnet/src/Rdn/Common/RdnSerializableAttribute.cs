// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !BUILDING_SOURCE_GENERATOR
using Rdn.Serialization.Metadata;
#endif

namespace Rdn.Serialization
{
    /// <summary>
    /// Instructs the Rdn source generator to generate source code to help optimize performance
    /// when serializing and deserializing instances of the specified type and types in its object graph.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]

#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    sealed class RdnSerializableAttribute : RdnAttribute
    {
#pragma warning disable IDE0060
        /// <summary>
        /// Initializes a new instance of <see cref="RdnSerializableAttribute"/> with the specified type.
        /// </summary>
        /// <param name="type">The type to generate source code for.</param>
        public RdnSerializableAttribute(Type type) { }
#pragma warning restore IDE0060

        /// <summary>
        /// The name of the property for the generated <see cref="RdnTypeInfo{T}"/> for
        /// the type on the generated, derived <see cref="RdnSerializerContext"/> type.
        /// </summary>
        /// <remarks>
        /// Useful to resolve a name collision with another type in the compilation closure.
        /// </remarks>
        public string? TypeInfoPropertyName { get; set; }

        /// <summary>
        /// Determines what the source generator should generate for the type. If the value is <see cref="RdnSourceGenerationMode.Default"/>,
        /// then the setting specified on <see cref="RdnSourceGenerationOptionsAttribute.GenerationMode"/> will be used.
        /// </summary>
        public RdnSourceGenerationMode GenerationMode { get; set; }
    }
}
