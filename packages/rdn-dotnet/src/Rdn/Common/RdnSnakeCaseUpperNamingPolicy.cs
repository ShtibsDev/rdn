// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    internal sealed class RdnSnakeCaseUpperNamingPolicy : RdnSeparatorNamingPolicy
    {
        public RdnSnakeCaseUpperNamingPolicy()
            : base(lowercase: false, separator: '_')
        {
        }
    }
}
