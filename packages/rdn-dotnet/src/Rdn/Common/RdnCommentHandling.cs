// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    /// <summary>
    /// This enum defines the various ways the <see cref="Utf8RdnReader"/> can deal with comments.
    /// </summary>
    public enum RdnCommentHandling : byte
    {
        /// <summary>
        /// By default, do no allow comments within the RDN input.
        /// Comments are treated as invalid RDN if found and a
        /// <see cref="RdnException"/> is thrown.
        /// </summary>
        Disallow = 0,
        /// <summary>
        /// Allow comments within the RDN input and ignore them.
        /// The <see cref="Utf8RdnReader"/> will behave as if no comments were present.
        /// </summary>
        Skip = 1,
        /// <summary>
        /// Allow comments within the RDN input and treat them as valid tokens.
        /// While reading, the caller will be able to access the comment values.
        /// </summary>
        Allow = 2,
    }
}
