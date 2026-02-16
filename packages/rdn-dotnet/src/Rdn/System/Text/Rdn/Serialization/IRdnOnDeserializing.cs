// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// Specifies that the type should have its <see cref="OnDeserializing"/> method called before deserialization occurs.
    /// </summary>
    /// <remarks>
    /// This behavior is only supported on types representing RDN objects.
    /// Types that have a custom converter or represent either collections or primitive values do not support this behavior.
    /// </remarks>
    public interface IRdnOnDeserializing
    {
        /// <summary>
        /// The method that is called before deserialization.
        /// </summary>
        void OnDeserializing();
    }
}
