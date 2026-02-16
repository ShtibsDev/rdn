// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    internal sealed class RdnKebabCaseUpperNamingPolicy : RdnSeparatorNamingPolicy
    {
        public RdnKebabCaseUpperNamingPolicy()
            : base(lowercase: false, separator: '-')
        {
        }
    }
}
