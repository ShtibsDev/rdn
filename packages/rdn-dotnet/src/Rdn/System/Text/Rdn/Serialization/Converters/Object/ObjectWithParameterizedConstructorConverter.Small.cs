// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>RdnObjectConverter{T}</cref> that supports the deserialization
    /// of RDN objects using parameterized constructors.
    /// </summary>
    internal sealed class SmallObjectWithParameterizedConstructorConverter<T, TArg0, TArg1, TArg2, TArg3> : ObjectWithParameterizedConstructorConverter<T> where T : notnull
    {
        protected override object CreateObject(ref ReadStackFrame frame)
        {
            var createObject = (RdnTypeInfo.ParameterizedConstructorDelegate<T, TArg0, TArg1, TArg2, TArg3>)
                frame.RdnTypeInfo.CreateObjectWithArgs!;
            var arguments = (Arguments<TArg0, TArg1, TArg2, TArg3>)frame.CtorArgumentState!.Arguments;
            return createObject!(arguments.Arg0, arguments.Arg1, arguments.Arg2, arguments.Arg3);
        }

        protected override bool ReadAndCacheConstructorArgument(
            scoped ref ReadStack state,
            ref Utf8RdnReader reader,
            RdnParameterInfo rdnParameterInfo)
        {
            Debug.Assert(state.Current.CtorArgumentState!.Arguments != null);
            var arguments = (Arguments<TArg0, TArg1, TArg2, TArg3>)state.Current.CtorArgumentState.Arguments;

            bool success;

            switch (rdnParameterInfo.Position)
            {
                case 0:
                    success = TryRead(ref state, ref reader, rdnParameterInfo, out arguments.Arg0);
                    break;
                case 1:
                    success = TryRead(ref state, ref reader, rdnParameterInfo, out arguments.Arg1);
                    break;
                case 2:
                    success = TryRead(ref state, ref reader, rdnParameterInfo, out arguments.Arg2);
                    break;
                case 3:
                    success = TryRead(ref state, ref reader, rdnParameterInfo, out arguments.Arg3);
                    break;
                default:
                    Debug.Fail("More than 4 params: we should be in override for LargeObjectWithParameterizedConstructorConverter.");
                    throw new InvalidOperationException();
            }

            return success;
        }

        private static bool TryRead<TArg>(
            scoped ref ReadStack state,
            ref Utf8RdnReader reader,
            RdnParameterInfo rdnParameterInfo,
            out TArg? arg)
        {
            Debug.Assert(rdnParameterInfo.ShouldDeserialize);

            var info = (RdnParameterInfo<TArg>)rdnParameterInfo;

            bool success = info.EffectiveConverter.TryRead(ref reader, info.ParameterType, info.Options, ref state, out TArg? value, out _);

            if (success)
            {
                if (value is null)
                {
                    if (info.IgnoreNullTokensOnRead)
                    {
                        // Use default value specified on parameter, if any.
                        value = info.EffectiveDefaultValue;
                    }
                    else if (!info.IsNullable && info.Options.RespectNullableAnnotations)
                    {
                        ThrowHelper.ThrowRdnException_ConstructorParameterDisallowNull(info.Name, state.Current.RdnTypeInfo.Type);
                    }
                }
            }

            arg = value;
            return success;
        }

        protected override void InitializeConstructorArgumentCaches(ref ReadStack state, RdnSerializerOptions options)
        {
            RdnTypeInfo typeInfo = state.Current.RdnTypeInfo;

            Debug.Assert(typeInfo.CreateObjectWithArgs != null);

            var arguments = new Arguments<TArg0, TArg1, TArg2, TArg3>();

            foreach (RdnParameterInfo parameterInfo in typeInfo.ParameterCache)
            {
                switch (parameterInfo.Position)
                {
                    case 0:
                        arguments.Arg0 = ((RdnParameterInfo<TArg0>)parameterInfo).EffectiveDefaultValue;
                        break;
                    case 1:
                        arguments.Arg1 = ((RdnParameterInfo<TArg1>)parameterInfo).EffectiveDefaultValue;
                        break;
                    case 2:
                        arguments.Arg2 = ((RdnParameterInfo<TArg2>)parameterInfo).EffectiveDefaultValue;
                        break;
                    case 3:
                        arguments.Arg3 = ((RdnParameterInfo<TArg3>)parameterInfo).EffectiveDefaultValue;
                        break;
                    default:
                        Debug.Fail("More than 4 params: we should be in override for LargeObjectWithParameterizedConstructorConverter.");
                        break;
                }
            }

            state.Current.CtorArgumentState!.Arguments = arguments;
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        internal override void ConfigureRdnTypeInfoUsingReflection(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
        {
            rdnTypeInfo.CreateObjectWithArgs = DefaultRdnTypeInfoResolver.MemberAccessor.CreateParameterizedConstructor<T, TArg0, TArg1, TArg2, TArg3>(ConstructorInfo!);
        }
    }
}
