// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    /// <summary>
    ///   Specifies the data type of a RDN value.
    /// </summary>
    public enum RdnValueKind : byte
    {
        /// <summary>
        ///   Indicates that there is no value (as distinct from <see cref="Null"/>).
        /// </summary>
        Undefined,

        /// <summary>
        ///   Indicates that a value is a RDN object.
        /// </summary>
        Object,

        /// <summary>
        ///   Indicates that a value is a RDN array.
        /// </summary>
        Array,

        /// <summary>
        ///   Indicates that a value is a RDN string.
        /// </summary>
        String,

        /// <summary>
        ///   Indicates that a value is a RDN number.
        /// </summary>
        Number,

        /// <summary>
        ///   Indicates that a value is the RDN value <c>true</c>.
        /// </summary>
        True,

        /// <summary>
        ///   Indicates that a value is the RDN value <c>false</c>.
        /// </summary>
        False,

        /// <summary>
        ///   Indicates that a value is the RDN value <c>null</c>.
        /// </summary>
        Null,

        /// <summary>
        ///   Indicates that a value is an RDN DateTime literal.
        /// </summary>
        RdnDateTime,

        /// <summary>
        ///   Indicates that a value is an RDN TimeOnly literal.
        /// </summary>
        RdnTimeOnly,

        /// <summary>
        ///   Indicates that a value is an RDN Duration literal.
        /// </summary>
        RdnDuration,

        /// <summary>
        ///   Indicates that a value is an RDN RegExp literal.
        /// </summary>
        RdnRegExp,

        /// <summary>
        ///   Indicates that a value is an RDN Set.
        /// </summary>
        Set,

        /// <summary>
        ///   Indicates that a value is an RDN Map.
        /// </summary>
        Map,
    }
}
