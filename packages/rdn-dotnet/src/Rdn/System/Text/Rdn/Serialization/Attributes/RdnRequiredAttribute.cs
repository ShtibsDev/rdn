// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Serialization.Metadata;

namespace Rdn.Serialization
{
    /// <summary>
    /// Indicates that the annotated member must bind to a RDN property on deserialization.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> token in RDN will not trigger a validation error.
    /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
    /// this attribute will be mapped to <see cref="RdnPropertyInfo.IsRequired"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class RdnRequiredAttribute : RdnAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RdnRequiredAttribute"/>.
        /// </summary>
        public RdnRequiredAttribute() { }
    }
}
