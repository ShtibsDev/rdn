// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// When placed on a type, property, or field, indicates what <see cref="RdnNumberHandling"/>
    /// settings should be used when serializing or deserializing numbers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class RdnNumberHandlingAttribute : RdnAttribute
    {
        /// <summary>
        /// Indicates what settings should be used when serializing or deserializing numbers.
        /// </summary>
        public RdnNumberHandling Handling { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="RdnNumberHandlingAttribute"/>.
        /// </summary>
        public RdnNumberHandlingAttribute(RdnNumberHandling handling)
        {
            if (!RdnSerializer.IsValidNumberHandlingValue(handling))
            {
                throw new ArgumentOutOfRangeException(nameof(handling));
            }
            Handling = handling;
        }
    }
}
