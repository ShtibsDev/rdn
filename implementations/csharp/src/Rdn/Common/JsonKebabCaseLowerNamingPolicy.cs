// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    internal sealed class JsonKebabCaseLowerNamingPolicy : JsonSeparatorNamingPolicy
    {
        public JsonKebabCaseLowerNamingPolicy()
            : base(lowercase: true, separator: '-')
        {
        }
    }
}
