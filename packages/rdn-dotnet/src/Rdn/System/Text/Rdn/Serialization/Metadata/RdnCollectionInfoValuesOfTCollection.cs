// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Rdn.Serialization.Metadata
{
    /// <summary>
    /// Provides serialization metadata about a collection type.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class RdnCollectionInfoValues<TCollection>
    {
        /// <summary>
        /// A <see cref="Func{TResult}"/> to create an instance of the collection when deserializing.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public Func<TCollection>? ObjectCreator { get; init; }

        /// <summary>
        /// If a dictionary type, the <see cref="RdnTypeInfo"/> instance representing the key type.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public RdnTypeInfo? KeyInfo { get; init; }

        /// <summary>
        /// A <see cref="RdnTypeInfo"/> instance representing the element type.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public RdnTypeInfo ElementInfo { get; init; } = null!;

        /// <summary>
        /// The <see cref="RdnNumberHandling"/> option to apply to number collection elements.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public RdnNumberHandling NumberHandling { get; init; }

        /// <summary>
        /// An optimized serialization implementation assuming pre-determined <see cref="RdnSourceGenerationOptionsAttribute"/> defaults.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public Action<Utf8RdnWriter, TCollection>? SerializeHandler { get; init; }
    }
}
