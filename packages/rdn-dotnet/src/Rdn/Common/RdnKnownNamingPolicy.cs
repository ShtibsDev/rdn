// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// The <see cref="Rdn.RdnNamingPolicy"/> to be used at run time.
    /// </summary>
    public enum RdnKnownNamingPolicy
    {
        /// <summary>
        /// Specifies that RDN property names should not be converted.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Specifies that the built-in <see cref="Rdn.RdnNamingPolicy.CamelCase"/> be used to convert RDN property names.
        /// </summary>
        CamelCase = 1,

        /// <summary>
        /// Specifies that the built-in <see cref="Rdn.RdnNamingPolicy.SnakeCaseLower"/> be used to convert RDN property names.
        /// </summary>
        SnakeCaseLower = 2,

        /// <summary>
        /// Specifies that the built-in <see cref="Rdn.RdnNamingPolicy.SnakeCaseUpper"/> be used to convert RDN property names.
        /// </summary>
        SnakeCaseUpper = 3,

        /// <summary>
        /// Specifies that the built-in <see cref="Rdn.RdnNamingPolicy.KebabCaseLower"/> be used to convert RDN property names.
        /// </summary>
        KebabCaseLower = 4,

        /// <summary>
        /// Specifies that the built-in <see cref="Rdn.RdnNamingPolicy.KebabCaseUpper"/> be used to convert RDN property names.
        /// </summary>
        KebabCaseUpper = 5
    }
}
