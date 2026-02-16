// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// Prevents a property or field from being serialized or deserialized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class RdnIgnoreAttribute : RdnAttribute
    {
        /// <summary>
        /// Specifies the condition that must be met before a property or field will be ignored.
        /// </summary>
        /// <remarks>The default value is <see cref="RdnIgnoreCondition.Always"/>.</remarks>
        public RdnIgnoreCondition Condition { get; set; } = RdnIgnoreCondition.Always;

        /// <summary>
        /// Initializes a new instance of <see cref="RdnIgnoreAttribute"/>.
        /// </summary>
        public RdnIgnoreAttribute() { }
    }
}
