// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    /// <summary>
    /// Determines how DateTime and DateTimeOffset values are serialized.
    /// </summary>
    public enum RdnDateTimeFormat
    {
        /// <summary>
        /// Serialize as ISO 8601 @-prefixed literal (e.g. @2024-01-15T10:30:00.000Z). This is the default.
        /// </summary>
        Iso = 0,

        /// <summary>
        /// Serialize as Unix timestamp in milliseconds (e.g. @1705312200000).
        /// </summary>
        UnixMilliseconds = 1
    }
}
