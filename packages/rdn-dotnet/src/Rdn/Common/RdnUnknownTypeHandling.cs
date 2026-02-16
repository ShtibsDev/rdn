// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// Defines how deserializing a type declared as an <see cref="object"/> is handled during deserialization.
    /// </summary>
    public enum RdnUnknownTypeHandling
    {
        /// <summary>
        /// A type declared as <see cref="object"/> is deserialized as a <see cref="RdnElement"/>.
        /// </summary>
        RdnElement = 0,
        /// <summary>
        /// A type declared as <see cref="object"/> is deserialized as a <see cref="RdnNode"/>.
        /// </summary>
        RdnNode = 1
    }
}
