// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Serialization.Metadata;

namespace Rdn.Schema
{
    /// <summary>
    /// Defines the context for the generated RDN schema for a particular node in a type graph.
    /// </summary>
    public readonly struct RdnSchemaExporterContext
    {
        internal readonly string[] _path;

        internal RdnSchemaExporterContext(
            RdnTypeInfo typeInfo,
            RdnPropertyInfo? propertyInfo,
            RdnTypeInfo? baseTypeInfo,
            string[] path)
        {
            TypeInfo = typeInfo;
            PropertyInfo = propertyInfo;
            BaseTypeInfo = baseTypeInfo;
            _path = path;
        }

        /// <summary>
        /// The <see cref="RdnTypeInfo"/> for the type being processed.
        /// </summary>
        public RdnTypeInfo TypeInfo { get; }

        /// <summary>
        /// The <see cref="RdnPropertyInfo"/> if the schema is being generated for a property.
        /// </summary>
        public RdnPropertyInfo? PropertyInfo { get; }

        /// <summary>
        /// Gets the <see cref="RdnTypeInfo"/> for polymorphic base type if the schema is being generated for a derived type.
        /// </summary>
        public RdnTypeInfo? BaseTypeInfo { get; }

        /// <summary>
        /// The path to the current node in the generated RDN schema.
        /// </summary>
        public ReadOnlySpan<string> Path => _path;
    }
}
