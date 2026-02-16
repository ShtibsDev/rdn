// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Nodes;

namespace Rdn.Schema
{
    /// <summary>
    /// Configures the behavior of the <see cref="RdnSchemaExporter"/> APIs.
    /// </summary>
    public sealed class RdnSchemaExporterOptions
    {
        /// <summary>
        /// Gets the default configuration object used by <see cref="RdnSchemaExporter"/>.
        /// </summary>
        public static RdnSchemaExporterOptions Default { get; } = new();

        /// <summary>
        /// Determines whether non-nullable schemas should be generated for null oblivious reference types.
        /// </summary>
        /// <remarks>
        /// Defaults to <see langword="false"/>. Due to restrictions in the run-time representation of nullable reference types
        /// most occurrences are null oblivious and are treated as nullable by the serializer. A notable exception to that rule
        /// are nullability annotations of field, property and constructor parameters which are represented in the contract metadata.
        /// </remarks>
        public bool TreatNullObliviousAsNonNullable { get; init; }

        /// <summary>
        /// Defines a callback that is invoked for every schema that is generated within the type graph.
        /// </summary>
        public Func<RdnSchemaExporterContext, RdnNode, RdnNode>? TransformSchemaNode { get; init; }
    }
}
