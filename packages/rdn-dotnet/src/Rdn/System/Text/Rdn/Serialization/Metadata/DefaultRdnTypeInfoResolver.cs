// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Rdn.Serialization.Metadata
{
    /// <summary>
    /// Defines the default, reflection-based RDN contract resolver used by Rdn.
    /// </summary>
    /// <remarks>
    /// The contract resolver used by <see cref="RdnSerializerOptions.Default"/>.
    /// </remarks>
    public partial class DefaultRdnTypeInfoResolver : IRdnTypeInfoResolver, IBuiltInRdnTypeInfoResolver
    {
        private bool _mutable;

        /// <summary>
        /// Creates a mutable <see cref="DefaultRdnTypeInfoResolver"/> instance.
        /// </summary>
        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        public DefaultRdnTypeInfoResolver() : this(mutable: true)
        {
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private DefaultRdnTypeInfoResolver(bool mutable)
        {
            _mutable = mutable;
        }

        /// <summary>
        /// Resolves a RDN contract for a given <paramref name="type"/> and <paramref name="options"/> configuration.
        /// </summary>
        /// <param name="type">The type for which to resolve a RDN contract.</param>
        /// <param name="options">A <see cref="RdnSerializerOptions"/> instance used to determine contract configuration.</param>
        /// <returns>A <see cref="RdnTypeInfo"/> defining a reflection-derived RDN contract for <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The base implementation of this method will produce a reflection-derived contract
        /// and apply any callbacks from the <see cref="Modifiers"/> list.
        /// </remarks>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "The ctor is marked RequiresUnreferencedCode.")]
        [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
            Justification = "The ctor is marked RequiresDynamicCode.")]
        public virtual RdnTypeInfo GetTypeInfo(Type type, RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(options);

            _mutable = false;

            RdnTypeInfo.ValidateType(type);
            RdnTypeInfo typeInfo = CreateRdnTypeInfo(type, options);
            typeInfo.OriginatingResolver = this;

            // We've finished configuring the metadata, brand the instance as user-unmodified.
            // This should be the last update operation in the resolver to avoid resetting the flag.
            typeInfo.IsCustomized = false;

            if (_modifiers != null)
            {
                foreach (Action<RdnTypeInfo> modifier in _modifiers)
                {
                    modifier(typeInfo);
                }
            }

            return typeInfo;
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private static RdnTypeInfo CreateRdnTypeInfo(Type type, RdnSerializerOptions options)
        {
            RdnConverter converter = GetConverterForType(type, options);
            return CreateTypeInfoCore(type, converter, options);
        }

        /// <summary>
        /// Gets a list of user-defined callbacks that can be used to modify the initial contract.
        /// </summary>
        /// <remarks>
        /// The modifier list will be rendered immutable after the first <see cref="GetTypeInfo(Type, RdnSerializerOptions)"/> invocation.
        ///
        /// Modifier callbacks are called consecutively in the order in which they are specified in the list.
        /// </remarks>
        public IList<Action<RdnTypeInfo>> Modifiers => _modifiers ??= new ModifierCollection(this);
        private ModifierCollection? _modifiers;

        private sealed class ModifierCollection : ConfigurationList<Action<RdnTypeInfo>>
        {
            private readonly DefaultRdnTypeInfoResolver _resolver;

            public ModifierCollection(DefaultRdnTypeInfoResolver resolver)
            {
                _resolver = resolver;
            }

            public override bool IsReadOnly => !_resolver._mutable;
            protected override void OnCollectionModifying()
            {
                if (!_resolver._mutable)
                {
                    ThrowHelper.ThrowInvalidOperationException_DefaultTypeInfoResolverImmutable();
                }
            }
        }

        bool IBuiltInRdnTypeInfoResolver.IsCompatibleWithOptions(RdnSerializerOptions _)
            // Metadata generated by the default resolver is compatible by definition,
            // provided that no user extensions have been made on the class.
            => _modifiers is null or { Count: 0 } && GetType() == typeof(DefaultRdnTypeInfoResolver);

        internal static DefaultRdnTypeInfoResolver DefaultInstance
        {
            [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
            get
            {
                if (s_defaultInstance is DefaultRdnTypeInfoResolver result)
                {
                    return result;
                }

                var newInstance = new DefaultRdnTypeInfoResolver(mutable: false);
                return Interlocked.CompareExchange(ref s_defaultInstance, newInstance, comparand: null) ?? newInstance;
            }
        }

        private static DefaultRdnTypeInfoResolver? s_defaultInstance;
    }
}
