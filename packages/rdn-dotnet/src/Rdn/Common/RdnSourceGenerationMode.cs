// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// The generation mode for the Rdn source generator.
    /// </summary>
    [Flags]
    public enum RdnSourceGenerationMode
    {
        /// <summary>
        /// When specified on <see cref="RdnSourceGenerationOptionsAttribute.GenerationMode"/>, indicates that both type-metadata initialization logic
        /// and optimized serialization logic should be generated for all types. When specified on <see cref="RdnSerializableAttribute.GenerationMode"/>,
        /// indicates that the setting on <see cref="RdnSourceGenerationOptionsAttribute.GenerationMode"/> should be used.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Instructs the RDN source generator to generate type-metadata initialization logic.
        /// </summary>
        /// <remarks>
        /// This mode supports all <see cref="RdnSerializer"/> features.
        /// </remarks>
        Metadata = 1,

        /// <summary>
        /// Instructs the RDN source generator to generate optimized serialization logic.
        /// </summary>
        /// <remarks>
        /// This mode supports only a subset of <see cref="RdnSerializer"/> features.
        /// </remarks>
        Serialization = 2
    }
}
