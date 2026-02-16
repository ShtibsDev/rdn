// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// When placed on a type, determines the <see cref="RdnUnmappedMemberHandling"/> configuration
    /// for the specific type, overriding the global <see cref="RdnSerializerOptions.UnmappedMemberHandling"/> setting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
        AllowMultiple = false, Inherited = false)]
    public class RdnUnmappedMemberHandlingAttribute : RdnAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RdnUnmappedMemberHandlingAttribute"/>.
        /// </summary>
        /// <param name="unmappedMemberHandling">The handling to apply to the current member.</param>
        public RdnUnmappedMemberHandlingAttribute(RdnUnmappedMemberHandling unmappedMemberHandling)
        {
            UnmappedMemberHandling = unmappedMemberHandling;
        }

        /// <summary>
        /// Specifies the unmapped member handling setting for the attribute.
        /// </summary>
        public RdnUnmappedMemberHandling UnmappedMemberHandling { get; }
    }
}
