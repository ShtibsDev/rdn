// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Nodes
{
    /// <summary>
    ///   Options to control <see cref="RdnNode"/> behavior.
    /// </summary>
    public struct RdnNodeOptions
    {
        /// <summary>
        ///   Specifies whether property names on <see cref="RdnObject"/> are case insensitive.
        /// </summary>
        public bool PropertyNameCaseInsensitive { get; set; }
    }
}
