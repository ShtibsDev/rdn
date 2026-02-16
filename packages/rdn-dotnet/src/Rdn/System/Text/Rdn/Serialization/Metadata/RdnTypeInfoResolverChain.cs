// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization.Metadata
{
    internal class RdnTypeInfoResolverChain : ConfigurationList<IRdnTypeInfoResolver>, IRdnTypeInfoResolver, IBuiltInRdnTypeInfoResolver
    {
        public RdnTypeInfoResolverChain() : base(null) { }
        public override bool IsReadOnly => true;
        protected override void OnCollectionModifying()
            => ThrowHelper.ThrowInvalidOperationException_TypeInfoResolverChainImmutable();

        public RdnTypeInfo? GetTypeInfo(Type type, RdnSerializerOptions options)
        {
            foreach (IRdnTypeInfoResolver resolver in _list)
            {
                RdnTypeInfo? typeInfo = resolver.GetTypeInfo(type, options);
                if (typeInfo != null)
                {
                    return typeInfo;
                }
            }

            return null;
        }

        internal void AddFlattened(IRdnTypeInfoResolver? resolver)
        {
            switch (resolver)
            {
                case null or EmptyRdnTypeInfoResolver:
                    break;

                case RdnTypeInfoResolverChain otherChain:
                    _list.AddRange(otherChain);
                    break;

                default:
                    _list.Add(resolver);
                    break;
            }
        }

        bool IBuiltInRdnTypeInfoResolver.IsCompatibleWithOptions(RdnSerializerOptions options)
        {
            foreach (IRdnTypeInfoResolver component in _list)
            {
                if (!component.IsCompatibleWithOptions(options))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder("[");
            foreach (IRdnTypeInfoResolver resolver in _list)
            {
                sb.Append(resolver);
                sb.Append(", ");
            }

            if (_list.Count > 0)
                sb.Length -= 2;

            sb.Append(']');
            return sb.ToString();
        }
    }
}
