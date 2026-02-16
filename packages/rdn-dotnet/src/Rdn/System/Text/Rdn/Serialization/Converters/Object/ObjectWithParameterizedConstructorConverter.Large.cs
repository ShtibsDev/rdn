// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>RdnObjectConverter{T}</cref> that supports the deserialization
    /// of RDN objects using parameterized constructors.
    /// </summary>
    internal class LargeObjectWithParameterizedConstructorConverter<T> : ObjectWithParameterizedConstructorConverter<T> where T : notnull
    {
        protected sealed override bool ReadAndCacheConstructorArgument(scoped ref ReadStack state, ref Utf8RdnReader reader, RdnParameterInfo rdnParameterInfo)
        {
            Debug.Assert(rdnParameterInfo.ShouldDeserialize);

            bool success = rdnParameterInfo.EffectiveConverter.TryReadAsObject(ref reader, rdnParameterInfo.ParameterType, rdnParameterInfo.Options, ref state, out object? arg);

            if (success && !(arg == null && rdnParameterInfo.IgnoreNullTokensOnRead))
            {
                if (arg == null && !rdnParameterInfo.IsNullable && rdnParameterInfo.Options.RespectNullableAnnotations)
                {
                    ThrowHelper.ThrowRdnException_ConstructorParameterDisallowNull(rdnParameterInfo.Name, state.Current.RdnTypeInfo.Type);
                }

                ((object[])state.Current.CtorArgumentState!.Arguments)[rdnParameterInfo.Position] = arg!;
            }

            return success;
        }

        protected sealed override object CreateObject(ref ReadStackFrame frame)
        {
            Debug.Assert(frame.CtorArgumentState != null);
            Debug.Assert(frame.RdnTypeInfo.CreateObjectWithArgs != null);

            object[] arguments = (object[])frame.CtorArgumentState.Arguments;
            frame.CtorArgumentState.Arguments = null!;

            Func<object[], T> createObject = (Func<object[], T>)frame.RdnTypeInfo.CreateObjectWithArgs;

            object obj = createObject(arguments);

            ArrayPool<object>.Shared.Return(arguments, clearArray: true);
            return obj;
        }

        protected sealed override void InitializeConstructorArgumentCaches(ref ReadStack state, RdnSerializerOptions options)
        {
            RdnTypeInfo typeInfo = state.Current.RdnTypeInfo;

            object?[] arguments = ArrayPool<object>.Shared.Rent(typeInfo.ParameterCache.Length);
            foreach (RdnParameterInfo parameterInfo in typeInfo.ParameterCache)
            {
                arguments[parameterInfo.Position] = parameterInfo.EffectiveDefaultValue;
            }

            state.Current.CtorArgumentState!.Arguments = arguments;
        }
    }
}
