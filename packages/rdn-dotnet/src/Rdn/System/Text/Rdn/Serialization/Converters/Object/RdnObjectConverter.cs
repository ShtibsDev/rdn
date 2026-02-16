// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// Base class for non-enumerable, non-primitive objects where public properties
    /// are (de)serialized as a RDN object.
    /// </summary>
    internal abstract class RdnObjectConverter<T> : RdnResumableConverter<T>
    {
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Object;
        internal override bool CanPopulate => true;
    }
}
