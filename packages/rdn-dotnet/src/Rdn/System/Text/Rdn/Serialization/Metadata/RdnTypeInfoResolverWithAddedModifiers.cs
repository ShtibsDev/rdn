// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Rdn.Serialization.Metadata
{
    internal sealed class RdnTypeInfoResolverWithAddedModifiers : IRdnTypeInfoResolver
    {
        private readonly IRdnTypeInfoResolver _source;
        private readonly Action<RdnTypeInfo>[] _modifiers;

        public RdnTypeInfoResolverWithAddedModifiers(IRdnTypeInfoResolver source, Action<RdnTypeInfo>[] modifiers)
        {
            Debug.Assert(modifiers.Length > 0);
            _source = source;
            _modifiers = modifiers;
        }

        public RdnTypeInfoResolverWithAddedModifiers WithAddedModifier(Action<RdnTypeInfo> modifier)
        {
            var newModifiers = new Action<RdnTypeInfo>[_modifiers.Length + 1];
            _modifiers.CopyTo(newModifiers, 0);
            newModifiers[_modifiers.Length] = modifier;

            return new RdnTypeInfoResolverWithAddedModifiers(_source, newModifiers);
        }

        public RdnTypeInfo? GetTypeInfo(Type type, RdnSerializerOptions options)
        {
            RdnTypeInfo? typeInfo = _source.GetTypeInfo(type, options);

            if (typeInfo != null)
            {
                foreach (Action<RdnTypeInfo> modifier in _modifiers)
                {
                    modifier(typeInfo);
                }
            }

            return typeInfo;
        }
    }
}
