// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class StackOrQueueConverterWithReflection<TCollection>
        : StackOrQueueConverter<TCollection>
        where TCollection : IEnumerable
    {
        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        public StackOrQueueConverterWithReflection() { }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        internal override void ConfigureRdnTypeInfoUsingReflection(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
        {
            rdnTypeInfo.AddMethodDelegate = DefaultRdnTypeInfoResolver.MemberAccessor.CreateAddMethodDelegate<TCollection>();
        }
    }
}
