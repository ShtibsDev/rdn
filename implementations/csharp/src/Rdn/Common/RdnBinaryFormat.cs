// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    /// <summary>
    /// Determines how binary data (byte[], Memory&lt;byte&gt;, ReadOnlyMemory&lt;byte&gt;) is serialized.
    /// </summary>
    public enum RdnBinaryFormat
    {
        /// <summary>
        /// Serialize as base64 b"..." literal (e.g. b"SGVsbG8="). This is the default.
        /// </summary>
        Base64 = 0,

        /// <summary>
        /// Serialize as hex x"..." literal (e.g. x"48656C6C6F").
        /// </summary>
        Hex = 1
    }
}
