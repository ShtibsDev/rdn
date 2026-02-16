// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>RdnObjectConverter{T}</cref> that supports the deserialization
    /// of RDN objects using parameterized constructors.
    /// </summary>
    internal sealed class LargeObjectWithParameterizedConstructorConverterWithReflection<T>
        : LargeObjectWithParameterizedConstructorConverter<T> where T : notnull
    {
        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        public LargeObjectWithParameterizedConstructorConverterWithReflection()
        {
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        internal override void ConfigureRdnTypeInfoUsingReflection(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
        {
            rdnTypeInfo.CreateObjectWithArgs = DefaultRdnTypeInfoResolver.MemberAccessor.CreateParameterizedConstructor<T>(ConstructorInfo!);
        }
    }
}
