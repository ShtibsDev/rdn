// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    /// <summary>
    /// Signifies what default options are used by <see cref="RdnSerializerOptions"/>.
    /// </summary>
    public enum RdnSerializerDefaults
    {
        /// <summary>
        /// Specifies that general-purpose values should be used. These are the same settings applied if a <see cref="RdnSerializerDefaults"/> isn't specified.
        /// </summary>
        /// <remarks>
        /// This option implies that property names are treated as case-sensitive and that "PascalCase" name formatting should be employed.
        /// </remarks>
        General = 0,

        /// <summary>
        /// Specifies that values should be used more appropriate to web-based scenarios.
        /// </summary>
        /// <remarks>
        /// This option implies that property names are treated as case-insensitive and that "camelCase" name formatting should be employed.
        /// </remarks>
        Web = 1,

        /// <summary>
        /// Specifies that stricter policies should be applied when deserializing from RDN.
        /// </summary>
        /// <remarks>
        /// RDN produced with <see cref="General"/> can be deserialized with <see cref="Strict"/>.
        /// The following policies are used:
        /// <list type="bullet">
        /// <item>Property names are treated as case-sensitive and "PascalCase" name formatting is employed.</item>
        /// <item>Properties that cannot be mapped to a .NET member are not allowed.</item>
        /// <item>Properties with duplicate names are not allowed.</item>
        /// <item>Nullable annotations and required constructor parameters are respected.</item>
        /// </list>
        /// </remarks>
        Strict = 2,
    }
}
