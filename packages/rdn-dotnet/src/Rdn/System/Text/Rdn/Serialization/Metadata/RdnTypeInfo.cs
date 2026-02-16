// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Rdn.Reflection;
using Rdn.Serialization.Converters;
using System.Threading;
using System.Threading.Tasks;

namespace Rdn.Serialization.Metadata
{
    /// <summary>
    /// Provides RDN serialization-related metadata about a type.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract partial class RdnTypeInfo
    {
        internal const string MetadataFactoryRequiresUnreferencedCode = "RDN serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use Rdn source generation for native AOT applications.";

        internal const string RdnObjectTypeName = "System.Text.Rdn.Nodes.RdnObject";

        internal delegate T ParameterizedConstructorDelegate<T, TArg0, TArg1, TArg2, TArg3>(TArg0? arg0, TArg1? arg1, TArg2? arg2, TArg3? arg3);

        /// <summary>
        /// Negated bitmask of the required properties, indexed by <see cref="RdnPropertyInfo.PropertyIndex"/>.
        /// </summary>
        internal BitArray? OptionalPropertiesMask { get; private set; }
        internal bool ShouldTrackRequiredProperties => OptionalPropertiesMask is not null;

        private Action<object>? _onSerializing;
        private Action<object>? _onSerialized;
        private Action<object>? _onDeserializing;
        private Action<object>? _onDeserialized;

        internal RdnTypeInfo(Type type, RdnConverter converter, RdnSerializerOptions options)
        {
            Type = type;
            Options = options;
            Converter = converter;
            Kind = GetTypeInfoKind(type, converter);
            PropertyInfoForTypeInfo = CreatePropertyInfoForTypeInfo();
            ElementType = converter.ElementType;
            KeyType = converter.KeyType;
        }

        /// <summary>
        /// Gets the element type corresponding to an enumerable, dictionary or optional type.
        /// </summary>
        /// <remarks>
        /// Returns the element type for enumerable types, the value type for dictionary types,
        /// and the underlying type for <see cref="Nullable{T}"/> or F# optional types.
        ///
        /// Returns <see langword="null"/> for all other types or types using custom converters.
        /// </remarks>
        public Type? ElementType { get; }

        /// <summary>
        /// Gets the key type corresponding to a dictionary type.
        /// </summary>
        /// <remarks>
        /// Returns the key type for dictionary types.
        ///
        /// Returns <see langword="null"/> for all other types or types using custom converters.
        /// </remarks>
        public Type? KeyType { get; }

        /// <summary>
        /// Gets or sets a parameterless factory to be used on deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// A parameterless factory is not supported for the current metadata <see cref="Kind"/>.
        /// </exception>
        /// <remarks>
        /// If set to <see langword="null" />, any attempt to deserialize instances of the given type will result in an exception.
        ///
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// types with a single default constructor or default constructors annotated with <see cref="RdnConstructorAttribute"/>
        /// will be mapped to this delegate.
        /// </remarks>
        public Func<object>? CreateObject
        {
            get => _createObject;
            set
            {
                SetCreateObject(value);
            }
        }

        private protected abstract void SetCreateObject(Delegate? createObject);
        private protected Func<object>? _createObject;

        internal Func<object>? CreateObjectForExtensionDataProperty { get; set; }

        /// <summary>
        /// Gets or sets a callback to be invoked before serialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="RdnTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IRdnOnSerializing"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnSerializing
        {
            get => _onSerializing;
            set
            {
                VerifyMutable();

                if (Kind is not (RdnTypeInfoKind.Object or RdnTypeInfoKind.Enumerable or RdnTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(Kind);
                }

                _onSerializing = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback to be invoked after serialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="RdnTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IRdnOnSerialized"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnSerialized
        {
            get => _onSerialized;
            set
            {
                VerifyMutable();

                if (Kind is not (RdnTypeInfoKind.Object or RdnTypeInfoKind.Enumerable or RdnTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(Kind);
                }

                _onSerialized = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback to be invoked before deserialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="RdnTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IRdnOnDeserializing"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnDeserializing
        {
            get => _onDeserializing;
            set
            {
                VerifyMutable();

                if (Kind is not (RdnTypeInfoKind.Object or RdnTypeInfoKind.Enumerable or RdnTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(Kind);
                }

                if (Converter.IsConvertibleCollection)
                {
                    // The values for convertible collections aren't available at the start of deserialization.
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOnDeserializingCallbacksNotSupported(Type);
                }

                _onDeserializing = value;
            }
        }

        /// <summary>
        /// Gets or sets a callback to be invoked after deserialization occurs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Serialization callbacks are only supported for <see cref="RdnTypeInfoKind.Object"/> metadata.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="IRdnOnDeserialized"/> implementation on the type.
        /// </remarks>
        public Action<object>? OnDeserialized
        {
            get => _onDeserialized;
            set
            {
                VerifyMutable();

                if (Kind is not (RdnTypeInfoKind.Object or RdnTypeInfoKind.Enumerable or RdnTypeInfoKind.Dictionary))
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(Kind);
                }

                _onDeserialized = value;
            }
        }

        /// <summary>
        /// Gets the list of <see cref="RdnPropertyInfo"/> metadata corresponding to the current type.
        /// </summary>
        /// <remarks>
        /// Property is only applicable to metadata of kind <see cref="RdnTypeInfoKind.Object"/>.
        /// For other kinds an empty, read-only list will be returned.
        ///
        /// The order of <see cref="RdnPropertyInfo"/> entries in the list determines the serialization order,
        /// unless either of the entries specifies a non-zero <see cref="RdnPropertyInfo.Order"/> value,
        /// in which case the properties will be stable sorted by <see cref="RdnPropertyInfo.Order"/>.
        ///
        /// It is required that added <see cref="RdnPropertyInfo"/> entries are unique up to <see cref="RdnPropertyInfo.Name"/>,
        /// however this will only be validated on serialization, once the metadata instance gets locked for further modification.
        /// </remarks>
        public IList<RdnPropertyInfo> Properties => PropertyList;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal RdnPropertyInfoList PropertyList
        {
            get
            {
                return _properties ?? CreatePropertyList();
                RdnPropertyInfoList CreatePropertyList()
                {
                    var list = new RdnPropertyInfoList(this);
                    if (_sourceGenDelayedPropertyInitializer is { } propInit)
                    {
                        // .NET 6 source gen backward compatibility -- ensure that the
                        // property initializer delegate is invoked lazily.
                        RdnMetadataServices.PopulateProperties(this, list, propInit);
                    }

                    RdnPropertyInfoList? result = Interlocked.CompareExchange(ref _properties, list, null);
                    _sourceGenDelayedPropertyInitializer = null;
                    return result ?? list;
                }
            }
        }

        /// <summary>
        /// Stores the .NET 6-style property initialization delegate for delayed evaluation.
        /// </summary>
        internal Func<RdnSerializerContext, RdnPropertyInfo[]>? SourceGenDelayedPropertyInitializer
        {
            get => _sourceGenDelayedPropertyInitializer;
            set
            {
                Debug.Assert(!IsReadOnly);
                Debug.Assert(_properties is null, "must not be set if a property list has been initialized.");
                _sourceGenDelayedPropertyInitializer = value;
            }
        }

        private Func<RdnSerializerContext, RdnPropertyInfo[]>? _sourceGenDelayedPropertyInitializer;
        private RdnPropertyInfoList? _properties;

        /// <summary>
        /// Gets or sets a configuration object specifying polymorphism metadata.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="value" /> has been associated with a different <see cref="RdnTypeInfo"/> instance.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Polymorphic serialization is not supported for the current metadata <see cref="Kind"/>.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the configuration of this setting will be mapped from any <see cref="RdnDerivedTypeAttribute"/> or <see cref="RdnPolymorphicAttribute"/> annotations.
        /// </remarks>
        public RdnPolymorphismOptions? PolymorphismOptions
        {
            get => _polymorphismOptions;
            set
            {
                VerifyMutable();

                if (value != null)
                {
                    if (Kind == RdnTypeInfoKind.None)
                    {
                        ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(Kind);
                    }

                    if (value.DeclaringTypeInfo != null && value.DeclaringTypeInfo != this)
                    {
                        ThrowHelper.ThrowArgumentException_RdnPolymorphismOptionsAssociatedWithDifferentRdnTypeInfo(nameof(value));
                    }

                    value.DeclaringTypeInfo = this;
                }

                _polymorphismOptions = value;
            }
        }

        /// <summary>
        /// Specifies whether the current instance has been locked for modification.
        /// </summary>
        /// <remarks>
        /// A <see cref="RdnTypeInfo"/> instance can be locked either if
        /// it has been passed to one of the <see cref="RdnSerializer"/> methods,
        /// has been associated with a <see cref="RdnSerializerContext"/> instance,
        /// or a user explicitly called the <see cref="MakeReadOnly"/> method on the instance.
        /// </remarks>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Locks the current instance for further modification.
        /// </summary>
        /// <remarks>This method is idempotent.</remarks>
        public void MakeReadOnly() => IsReadOnly = true;

        private protected RdnPolymorphismOptions? _polymorphismOptions;

        internal object? CreateObjectWithArgs { get; set; }

        // Add method delegate for non-generic Stack and Queue; and types that derive from them.
        internal object? AddMethodDelegate { get; set; }

        internal RdnPropertyInfo? ExtensionDataProperty { get; private set; }

        internal PolymorphicTypeResolver? PolymorphicTypeResolver { get; private set; }

        // Indicates that SerializeHandler is populated.
        internal bool HasSerializeHandler { get; private protected set; }

        // Indicates that SerializeHandler is populated and is compatible with the associated contract metadata.
        internal bool CanUseSerializeHandler { get; private set; }

        // Configure would normally have thrown why initializing properties for source gen but type had SerializeHandler
        // so it is allowed to be used for fast-path serialization but it will throw if used for metadata-based serialization
        internal bool PropertyMetadataSerializationNotSupported { get; set; }

        internal bool IsNullable => Converter.NullableElementConverter is not null;
        internal bool CanBeNull => PropertyInfoForTypeInfo.PropertyTypeCanBeNull;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ValidateCanBeUsedForPropertyMetadataSerialization()
        {
            if (PropertyMetadataSerializationNotSupported)
            {
                ThrowHelper.ThrowInvalidOperationException_NoMetadataForTypeProperties(Options.TypeInfoResolver, Type);
            }
        }

        /// <summary>
        /// Return the RdnTypeInfo for the element type, or null if the type is not an enumerable or dictionary.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal RdnTypeInfo? ElementTypeInfo
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(_elementTypeInfo is null or { IsConfigurationStarted: true });
                // Even though this instance has already been configured,
                // it is possible for contending threads to call the property
                // while the wider RdnTypeInfo graph is still being configured.
                // Call EnsureConfigured() to force synchronization if necessary.
                RdnTypeInfo? elementTypeInfo = _elementTypeInfo;
                elementTypeInfo?.EnsureConfigured();
                return elementTypeInfo;
            }
            set
            {
                Debug.Assert(!IsReadOnly);
                Debug.Assert(value is null || value.Type == ElementType);
                _elementTypeInfo = value;
            }
        }

        /// <summary>
        /// Return the RdnTypeInfo for the key type, or null if the type is not a dictionary.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal RdnTypeInfo? KeyTypeInfo
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(_keyTypeInfo is null or { IsConfigurationStarted: true });
                // Even though this instance has already been configured,
                // it is possible for contending threads to call the property
                // while the wider RdnTypeInfo graph is still being configured.
                // Call EnsureConfigured() to force synchronization if necessary.
                RdnTypeInfo? keyTypeInfo = _keyTypeInfo;
                keyTypeInfo?.EnsureConfigured();
                return keyTypeInfo;
            }
            set
            {
                Debug.Assert(!IsReadOnly);
                Debug.Assert(value is null || value.Type == KeyType);
                _keyTypeInfo = value;
            }
        }

        private RdnTypeInfo? _elementTypeInfo;
        private RdnTypeInfo? _keyTypeInfo;

        /// <summary>
        /// Gets the <see cref="RdnSerializerOptions"/> value associated with the current <see cref="RdnTypeInfo" /> instance.
        /// </summary>
        public RdnSerializerOptions Options { get; }

        /// <summary>
        /// Gets the <see cref="Type"/> for which the RDN serialization contract is being defined.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the <see cref="RdnConverter"/> associated with the current type.
        /// </summary>
        /// <remarks>
        /// The <see cref="RdnConverter"/> associated with the type determines the value of <see cref="Kind"/>,
        /// and by extension the types of metadata that are configurable in the current RDN contract.
        /// As such, the value of the converter cannot be changed once a <see cref="RdnTypeInfo"/> instance has been created.
        /// </remarks>
        public RdnConverter Converter { get; }

        /// <summary>
        /// Determines the kind of contract metadata that the current instance is specifying.
        /// </summary>
        /// <remarks>
        /// The value of <see cref="Kind"/> determines what aspects of the RDN contract are configurable.
        /// For example, it is only possible to configure the <see cref="Properties"/> list for metadata
        /// of kind <see cref="RdnTypeInfoKind.Object"/>.
        ///
        /// The value of <see cref="Kind"/> is determined exclusively by the <see cref="RdnConverter"/>
        /// resolved for the current type, and cannot be changed once resolution has happened.
        /// User-defined custom converters (specified either via <see cref="RdnConverterAttribute"/> or <see cref="RdnSerializerOptions.Converters"/>)
        /// are metadata-agnostic and thus always resolve to <see cref="RdnTypeInfoKind.None"/>.
        /// </remarks>
        public RdnTypeInfoKind Kind { get; }

        /// <summary>
        /// Dummy <see cref="RdnPropertyInfo"/> instance corresponding to the declaring type of this <see cref="RdnTypeInfo"/>.
        /// </summary>
        /// <remarks>
        /// Used as convenience in cases where we want to serialize property-like values that do not define property metadata, such as:
        /// 1. a collection element type,
        /// 2. a dictionary key or value type or,
        /// 3. the property metadata for the root-level value.
        /// For example, for a property returning <see cref="List{T}"/> where T is a string,
        /// a RdnTypeInfo will be created with .Type=typeof(string) and .PropertyInfoForTypeInfo=RdnPropertyInfo{string}.
        /// </remarks>
        internal RdnPropertyInfo PropertyInfoForTypeInfo { get; }

        private protected abstract RdnPropertyInfo CreatePropertyInfoForTypeInfo();

        /// <summary>
        /// Gets or sets the type-level <see cref="RdnSerializerOptions.NumberHandling"/> override.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Specified an invalid <see cref="RdnNumberHandling"/> value.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="RdnNumberHandlingAttribute"/> annotations.
        /// </remarks>
        public RdnNumberHandling? NumberHandling
        {
            get => _numberHandling;
            set
            {
                VerifyMutable();

                if (value is not null && !RdnSerializer.IsValidNumberHandlingValue(value.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _numberHandling = value;
            }
        }

        internal RdnNumberHandling EffectiveNumberHandling => _numberHandling ?? Options.NumberHandling;
        private RdnNumberHandling? _numberHandling;

        /// <summary>
        /// Gets or sets the type-level <see cref="RdnUnmappedMemberHandling"/> override.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Unmapped member handling only supported for <see cref="RdnTypeInfoKind.Object"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Specified an invalid <see cref="RdnUnmappedMemberHandling"/> value.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this callback will be mapped from any <see cref="RdnUnmappedMemberHandlingAttribute"/> annotations.
        /// </remarks>
        public RdnUnmappedMemberHandling? UnmappedMemberHandling
        {
            get => _unmappedMemberHandling;
            set
            {
                VerifyMutable();

                if (Kind != RdnTypeInfoKind.Object)
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(Kind);
                }

                if (value is not null && !RdnSerializer.IsValidUnmappedMemberHandlingValue(value.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _unmappedMemberHandling = value;
            }
        }

        private RdnUnmappedMemberHandling? _unmappedMemberHandling;

        internal RdnUnmappedMemberHandling EffectiveUnmappedMemberHandling { get; private set; }

        private RdnObjectCreationHandling? _preferredPropertyObjectCreationHandling;

        /// <summary>
        /// Gets or sets the preferred <see cref="RdnObjectCreationHandling"/> value for properties contained in the type.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// Unmapped member handling only supported for <see cref="RdnTypeInfoKind.Object"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Specified an invalid <see cref="RdnObjectCreationHandling"/> value.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this callback will be mapped from <see cref="RdnObjectCreationHandlingAttribute"/> annotations on types.
        /// </remarks>
        public RdnObjectCreationHandling? PreferredPropertyObjectCreationHandling
        {
            get => _preferredPropertyObjectCreationHandling;
            set
            {
                VerifyMutable();

                if (Kind != RdnTypeInfoKind.Object)
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(Kind);
                }

                if (value is not null && !RdnSerializer.IsValidCreationHandlingValue(value.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _preferredPropertyObjectCreationHandling = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IRdnTypeInfoResolver"/> from which this metadata instance originated.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnTypeInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// Metadata used to determine the <see cref="RdnSerializerContext.GeneratedSerializerOptions"/>
        /// configuration for the current metadata instance.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IRdnTypeInfoResolver? OriginatingResolver
        {
            get => _originatingResolver;
            set
            {
                VerifyMutable();

                if (value is RdnSerializerContext)
                {
                    // The source generator uses this property setter to brand the metadata instance as user-unmodified.
                    // Even though users could call the same property setter to unset this flag, this is generally speaking fine.
                    // This flag is only used to determine fast-path invalidation, worst case scenario this would lead to a false negative.
                    IsCustomized = false;
                }

                _originatingResolver = value;
            }
        }

        private IRdnTypeInfoResolver? _originatingResolver;

        /// <summary>
        /// Gets or sets an attribute provider corresponding to the deserialization constructor.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// When resolving metadata via the built-in resolvers this will be populated with
        /// the underlying <see cref="ConstructorInfo" /> of the serialized property or field.
        /// </remarks>
        public ICustomAttributeProvider? ConstructorAttributeProvider
        {
            get
            {
                Func<ICustomAttributeProvider>? ctorAttrProviderFactory = Volatile.Read(ref ConstructorAttributeProviderFactory);
                ICustomAttributeProvider? ctorAttrProvider = _constructorAttributeProvider;

                if (ctorAttrProvider is null && ctorAttrProviderFactory is not null)
                {
                    _constructorAttributeProvider = ctorAttrProvider = ctorAttrProviderFactory();
                    Volatile.Write(ref ConstructorAttributeProviderFactory, null);
                }

                return ctorAttrProvider;
            }
            internal set
            {
                Debug.Assert(!IsReadOnly);

                _constructorAttributeProvider = value;
                Volatile.Write(ref ConstructorAttributeProviderFactory, null);
            }
        }

        // Metadata emanating from the source generator use delayed attribute provider initialization
        // ensuring that reflection metadata resolution remains pay-for-play and is trimmable.
        internal Func<ICustomAttributeProvider>? ConstructorAttributeProviderFactory;
        private ICustomAttributeProvider? _constructorAttributeProvider;

        internal void VerifyMutable()
        {
            if (IsReadOnly)
            {
                ThrowHelper.ThrowInvalidOperationException_TypeInfoImmutable();
            }

            IsCustomized = true;
        }

        /// <summary>
        /// Indicates that the current RdnTypeInfo might contain user modifications.
        /// Defaults to true, and is only unset by the built-in contract resolvers.
        /// </summary>
        internal bool IsCustomized { get; set; } = true;

        internal bool IsConfigured => _configurationState == ConfigurationState.Configured;
        internal bool IsConfigurationStarted => _configurationState is not ConfigurationState.NotConfigured;
        private volatile ConfigurationState _configurationState;
        private enum ConfigurationState : byte
        {
            NotConfigured = 0,
            Configuring = 1,
            Configured = 2
        };

        private ExceptionDispatchInfo? _cachedConfigureError;

        internal void EnsureConfigured()
        {
            if (!IsConfigured)
                ConfigureSynchronized();

            void ConfigureSynchronized()
            {
                Options.MakeReadOnly();
                MakeReadOnly();

                _cachedConfigureError?.Throw();

                lock (Options.CacheContext)
                {
                    if (_configurationState != ConfigurationState.NotConfigured)
                    {
                        // The value of _configurationState is either
                        //    'Configuring': recursive instance configured by this thread or
                        //    'Configured' : instance already configured by another thread.
                        // We can safely yield the configuration operation in both cases.
                        return;
                    }

                    _cachedConfigureError?.Throw();

                    try
                    {
                        _configurationState = ConfigurationState.Configuring;
                        Configure();
                        _configurationState = ConfigurationState.Configured;
                    }
                    catch (Exception e)
                    {
                        _cachedConfigureError = ExceptionDispatchInfo.Capture(e);
                        _configurationState = ConfigurationState.NotConfigured;
                        throw;
                    }
                }
            }
        }

        private void Configure()
        {
            Debug.Assert(Monitor.IsEntered(Options.CacheContext), "Configure called directly, use EnsureConfigured which synchronizes access to this method");
            Debug.Assert(Options.IsReadOnly);
            Debug.Assert(IsReadOnly);

            PropertyInfoForTypeInfo.Configure();

            if (PolymorphismOptions != null)
            {
                // This needs to be done before ConfigureProperties() is called
                // RdnPropertyInfo.Configure() must have this value available in order to detect Polymoprhic + cyclic class case
                PolymorphicTypeResolver = new PolymorphicTypeResolver(Options, PolymorphismOptions, Type, Converter.CanHaveMetadata);
            }

            if (Kind == RdnTypeInfoKind.Object)
            {
                ConfigureProperties();

                if (DetermineUsesParameterizedConstructor())
                {
                    ConfigureConstructorParameters();
                }
            }

            if (ElementType != null)
            {
                _elementTypeInfo ??= Options.GetTypeInfoInternal(ElementType);
                _elementTypeInfo.EnsureConfigured();
            }

            if (KeyType != null)
            {
                _keyTypeInfo ??= Options.GetTypeInfoInternal(KeyType);
                _keyTypeInfo.EnsureConfigured();
            }

            DetermineIsCompatibleWithCurrentOptions();
            CanUseSerializeHandler = HasSerializeHandler && IsCompatibleWithCurrentOptions;
        }

        /// <summary>
        /// Gets any ancestor polymorphic types that declare
        /// a type discriminator for the current type. Consulted
        /// when serializing polymorphic values as objects.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal RdnTypeInfo? AncestorPolymorphicType
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(Type != typeof(object));

                if (!_isAncestorPolymorphicTypeResolved)
                {
                    _ancestorPolymorhicType = PolymorphicTypeResolver.FindNearestPolymorphicBaseType(this);
                    _isAncestorPolymorphicTypeResolved = true;
                }

                return _ancestorPolymorhicType;
            }
        }

        private RdnTypeInfo? _ancestorPolymorhicType;
        private volatile bool _isAncestorPolymorphicTypeResolved;

        /// <summary>
        /// Determines if the transitive closure of all RdnTypeInfo metadata referenced
        /// by the current type (property types, key types, element types, ...) are
        /// compatible with the settings as specified in RdnSerializerOptions.
        /// </summary>
        private void DetermineIsCompatibleWithCurrentOptions()
        {
            // Defines a recursive algorithm validating that the `IsCurrentNodeCompatible`
            // predicate is valid for every node in the type graph. This method only checks
            // the immediate children, with recursion being driven by the Configure() method.
            // Therefore, this method must be called _after_ the child nodes have been configured.

            Debug.Assert(IsReadOnly);
            Debug.Assert(!IsConfigured);

            if (!IsCurrentNodeCompatible())
            {
                IsCompatibleWithCurrentOptions = false;
                return;
            }

            if (_properties != null)
            {
                foreach (RdnPropertyInfo property in _properties)
                {
                    Debug.Assert(property.IsConfigured);

                    if (!property.IsPropertyTypeInfoConfigured)
                    {
                        // Either an ignored property or property is part of a cycle.
                        // In both cases we can ignore these instances.
                        continue;
                    }

                    if (!property.RdnTypeInfo.IsCompatibleWithCurrentOptions)
                    {
                        IsCompatibleWithCurrentOptions = false;
                        return;
                    }
                }
            }

            if (_elementTypeInfo?.IsCompatibleWithCurrentOptions == false ||
                _keyTypeInfo?.IsCompatibleWithCurrentOptions == false)
            {
                IsCompatibleWithCurrentOptions = false;
                return;
            }

            Debug.Assert(IsCompatibleWithCurrentOptions);

            // Defines the core predicate that must be checked for every node in the type graph.
            bool IsCurrentNodeCompatible()
            {
                if (IsCustomized)
                {
                    // Return false if we have detected contract customization by the user.
                    return false;
                }

                if (Options.CanUseFastPathSerializationLogic)
                {
                    // Simple case/backward compatibility: options uses a combination of compatible built-in converters.
                    return true;
                }

                return OriginatingResolver.IsCompatibleWithOptions(Options);
            }
        }

        /// <summary>
        /// Holds the result of the above algorithm -- NB must default to true
        /// to establish a base case for recursive types and any RdnIgnored property types.
        /// </summary>
        private bool IsCompatibleWithCurrentOptions { get; set; } = true;

        /// <summary>
        /// Determine if the current configuration is compatible with using a parameterized constructor.
        /// </summary>
        internal bool DetermineUsesParameterizedConstructor()
            => Converter.ConstructorIsParameterized && CreateObject is null;

        /// <summary>
        /// Creates a blank <see cref="RdnTypeInfo{T}"/> instance.
        /// </summary>
        /// <typeparam name="T">The type for which contract metadata is specified.</typeparam>
        /// <param name="options">The <see cref="RdnSerializerOptions"/> instance the metadata is associated with.</param>
        /// <returns>A blank <see cref="RdnTypeInfo{T}"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <remarks>
        /// The returned <see cref="RdnTypeInfo{T}"/> will be blank, with the exception of the
        /// <see cref="Converter"/> property which will be resolved either from
        /// <see cref="RdnSerializerOptions.Converters"/> or the built-in converters for the type.
        /// Any converters specified via <see cref="RdnConverterAttribute"/> on the type declaration
        /// will not be resolved by this method.
        ///
        /// What converter does get resolved influences the value of <see cref="Kind"/>,
        /// which constrains the type of metadata that can be modified in the <see cref="RdnTypeInfo"/> instance.
        /// </remarks>
        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        public static RdnTypeInfo<T> CreateRdnTypeInfo<T>(RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            RdnConverter converter = DefaultRdnTypeInfoResolver.GetConverterForType(typeof(T), options, resolveRdnConverterAttribute: false);
            return new RdnTypeInfo<T>(converter, options);
        }

        /// <summary>
        /// Creates a blank <see cref="RdnTypeInfo"/> instance.
        /// </summary>
        /// <param name="type">The type for which contract metadata is specified.</param>
        /// <param name="options">The <see cref="RdnSerializerOptions"/> instance the metadata is associated with.</param>
        /// <returns>A blank <see cref="RdnTypeInfo"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="type"/> cannot be used for serialization.</exception>
        /// <remarks>
        /// The returned <see cref="RdnTypeInfo"/> will be blank, with the exception of the
        /// <see cref="Converter"/> property which will be resolved either from
        /// <see cref="RdnSerializerOptions.Converters"/> or the built-in converters for the type.
        /// Any converters specified via <see cref="RdnConverterAttribute"/> on the type declaration
        /// will not be resolved by this method.
        ///
        /// What converter does get resolved influences the value of <see cref="Kind"/>,
        /// which constrains the type of metadata that can be modified in the <see cref="RdnTypeInfo"/> instance.
        /// </remarks>
        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        public static RdnTypeInfo CreateRdnTypeInfo(Type type, RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(options);

            if (IsInvalidForSerialization(type))
            {
                ThrowHelper.ThrowArgumentException_CannotSerializeInvalidType(nameof(type), type, null, null);
            }

            RdnConverter converter = DefaultRdnTypeInfoResolver.GetConverterForType(type, options, resolveRdnConverterAttribute: false);
            return CreateRdnTypeInfo(type, converter, options);
        }

        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        internal static RdnTypeInfo CreateRdnTypeInfo(Type type, RdnConverter converter, RdnSerializerOptions options)
        {
            RdnTypeInfo rdnTypeInfo;

            if (converter.Type == type)
            {
                // For performance, avoid doing a reflection-based instantiation
                // if the converter type matches that of the declared type.
                rdnTypeInfo = converter.CreateRdnTypeInfo(options);
            }
            else
            {
                Type rdnTypeInfoType = typeof(RdnTypeInfo<>).MakeGenericType(type);
                rdnTypeInfo = (RdnTypeInfo)rdnTypeInfoType.CreateInstanceNoWrapExceptions(
                    parameterTypes: [typeof(RdnConverter), typeof(RdnSerializerOptions)],
                    parameters: new object[] { converter, options })!;
            }

            Debug.Assert(rdnTypeInfo.Type == type);
            return rdnTypeInfo;
        }

        /// <summary>
        /// Creates a blank <see cref="RdnPropertyInfo"/> instance for the current <see cref="RdnTypeInfo"/>.
        /// </summary>
        /// <param name="propertyType">The declared type for the property.</param>
        /// <param name="name">The property name used in RDN serialization and deserialization.</param>
        /// <returns>A blank <see cref="RdnPropertyInfo"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="propertyType"/> or <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="propertyType"/> cannot be used for serialization.</exception>
        /// <exception cref="InvalidOperationException">The <see cref="RdnTypeInfo"/> instance has been locked for further modification.</exception>
        [RequiresUnreferencedCode(MetadataFactoryRequiresUnreferencedCode)]
        [RequiresDynamicCode(MetadataFactoryRequiresUnreferencedCode)]
        public RdnPropertyInfo CreateRdnPropertyInfo(Type propertyType, string name)
        {
            ArgumentNullException.ThrowIfNull(propertyType);
            ArgumentNullException.ThrowIfNull(name);

            if (IsInvalidForSerialization(propertyType))
            {
                ThrowHelper.ThrowArgumentException_CannotSerializeInvalidType(nameof(propertyType), propertyType, Type, name);
            }

            VerifyMutable();
            RdnPropertyInfo propertyInfo = CreatePropertyUsingReflection(propertyType, declaringType: null);
            propertyInfo.Name = name;

            return propertyInfo;
        }

        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        internal RdnPropertyInfo CreatePropertyUsingReflection(Type propertyType, Type? declaringType)
        {
            RdnPropertyInfo rdnPropertyInfo;

            if (Options.TryGetTypeInfoCached(propertyType, out RdnTypeInfo? rdnTypeInfo))
            {
                // If a RdnTypeInfo has already been cached for the property type,
                // avoid reflection-based initialization by delegating construction
                // of RdnPropertyInfo<T> construction to the property type metadata.
                rdnPropertyInfo = rdnTypeInfo.CreateRdnPropertyInfo(declaringTypeInfo: this, declaringType, Options);
            }
            else
            {
                // Metadata for `propertyType` has not been registered yet.
                // Use reflection to instantiate the correct RdnPropertyInfo<T>
                Type propertyInfoType = typeof(RdnPropertyInfo<>).MakeGenericType(propertyType);
                rdnPropertyInfo = (RdnPropertyInfo)propertyInfoType.CreateInstanceNoWrapExceptions(
                    parameterTypes: [typeof(Type), typeof(RdnTypeInfo), typeof(RdnSerializerOptions)],
                    parameters: new object[] { declaringType ?? Type, this, Options })!;
            }

            Debug.Assert(rdnPropertyInfo.PropertyType == propertyType);
            return rdnPropertyInfo;
        }

        /// <summary>
        /// Creates a RdnPropertyInfo whose property type matches the type of this RdnTypeInfo instance.
        /// </summary>
        private protected abstract RdnPropertyInfo CreateRdnPropertyInfo(RdnTypeInfo declaringTypeInfo, Type? declaringType, RdnSerializerOptions options);

        private protected Dictionary<ParameterLookupKey, RdnParameterInfoValues>? _parameterInfoValuesIndex;

        // Untyped, root-level serialization methods
        internal abstract void SerializeAsObject(Utf8RdnWriter writer, object? rootValue);
        internal abstract Task SerializeAsObjectAsync(PipeWriter pipeWriter, object? rootValue, int flushThreshold, CancellationToken cancellationToken);
        internal abstract Task SerializeAsObjectAsync(Stream utf8Rdn, object? rootValue, CancellationToken cancellationToken);
        internal abstract Task SerializeAsObjectAsync(PipeWriter utf8Rdn, object? rootValue, CancellationToken cancellationToken);
        internal abstract void SerializeAsObject(Stream utf8Rdn, object? rootValue);

        // Untyped, root-level deserialization methods
        internal abstract object? DeserializeAsObject(ref Utf8RdnReader reader, ref ReadStack state);
        internal abstract ValueTask<object?> DeserializeAsObjectAsync(PipeReader utf8Rdn, CancellationToken cancellationToken);
        internal abstract ValueTask<object?> DeserializeAsObjectAsync(Stream utf8Rdn, CancellationToken cancellationToken);
        internal abstract object? DeserializeAsObject(Stream utf8Rdn);

        internal ref struct PropertyHierarchyResolutionState(RdnSerializerOptions options)
        {
            public Dictionary<string, (RdnPropertyInfo, int index)> AddedProperties = new(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            public Dictionary<string, RdnPropertyInfo>? IgnoredProperties;
            public bool IsPropertyOrderSpecified;
        }

        private protected readonly struct ParameterLookupKey(Type type, string name) : IEquatable<ParameterLookupKey>
        {
            public Type Type { get; } = type;
            public string Name { get; } = name;
            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
            public bool Equals(ParameterLookupKey other) => Type == other.Type && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            public override bool Equals([NotNullWhen(true)] object? obj) => obj is ParameterLookupKey key && Equals(key);
        }

        internal void ConfigureProperties()
        {
            Debug.Assert(Kind == RdnTypeInfoKind.Object);
            Debug.Assert(_propertyCache is null);
            Debug.Assert(_propertyIndex is null);
            Debug.Assert(ExtensionDataProperty is null);

            RdnPropertyInfoList properties = PropertyList;
            StringComparer comparer = Options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            Dictionary<string, RdnPropertyInfo> propertyIndex = new(properties.Count, comparer);
            List<RdnPropertyInfo> propertyCache = new(properties.Count);

            bool arePropertiesSorted = true;
            int previousPropertyOrder = int.MinValue;
            BitArray? requiredPropertiesMask = null;

            for (int i = 0; i < properties.Count; i++)
            {
                RdnPropertyInfo property = properties[i];
                Debug.Assert(property.DeclaringTypeInfo == this);

                if (property.IsExtensionData)
                {
                    if (UnmappedMemberHandling is RdnUnmappedMemberHandling.Disallow)
                    {
                        ThrowHelper.ThrowInvalidOperationException_ExtensionDataConflictsWithUnmappedMemberHandling(Type, property);
                    }

                    if (ExtensionDataProperty != null)
                    {
                        ThrowHelper.ThrowInvalidOperationException_SerializationDuplicateTypeAttribute(Type, typeof(RdnExtensionDataAttribute));
                    }

                    ExtensionDataProperty = property;
                }
                else
                {
                    property.PropertyIndex = i;

                    if (property.IsRequired)
                    {
                        (requiredPropertiesMask ??= new BitArray(properties.Count))[i] = true;
                    }

                    if (arePropertiesSorted)
                    {
                        arePropertiesSorted = previousPropertyOrder <= property.Order;
                        previousPropertyOrder = property.Order;
                    }

                    if (!propertyIndex.TryAdd(property.Name, property))
                    {
                        ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(Type, property.Name);
                    }

                    propertyCache.Add(property);
                }

                property.Configure();
            }

            if (!arePropertiesSorted)
            {
                // Properties have been configured by the user and require sorting.
                properties.SortProperties();
                propertyCache.StableSortByKey(static propInfo => propInfo.Order);
            }

            OptionalPropertiesMask = requiredPropertiesMask?.Not();
            _propertyCache = propertyCache.ToArray();
            _propertyIndex = propertyIndex;

            // Override global UnmappedMemberHandling configuration
            // if type specifies an extension data property.
            EffectiveUnmappedMemberHandling = UnmappedMemberHandling ??
                (ExtensionDataProperty is null
                    ? Options.UnmappedMemberHandling
                    : RdnUnmappedMemberHandling.Skip);
        }

        internal void PopulateParameterInfoValues(RdnParameterInfoValues[] parameterInfoValues)
        {
            if (parameterInfoValues.Length == 0)
            {
                return;
            }

            Dictionary<ParameterLookupKey, RdnParameterInfoValues> parameterIndex = new(parameterInfoValues.Length);
            foreach (RdnParameterInfoValues parameterInfoValue in parameterInfoValues)
            {
                ParameterLookupKey paramKey = new(parameterInfoValue.ParameterType, parameterInfoValue.Name);
                parameterIndex.TryAdd(paramKey, parameterInfoValue); // Ignore conflicts since they are reported at serialization time.
            }

            ParameterCount = parameterInfoValues.Length;
            _parameterInfoValuesIndex = parameterIndex;
        }

        internal void ResolveMatchingParameterInfo(RdnPropertyInfo propertyInfo)
        {
            Debug.Assert(
                CreateObjectWithArgs is null || _parameterInfoValuesIndex is not null,
                "Metadata with parameterized constructors must have populated parameter info metadata.");

            if (_parameterInfoValuesIndex is not { } index)
            {
                return;
            }

            string propertyName = propertyInfo.MemberName ?? propertyInfo.Name;
            ParameterLookupKey propKey = new(propertyInfo.PropertyType, propertyName);
            if (index.TryGetValue(propKey, out RdnParameterInfoValues? matchingParameterInfoValues))
            {
                propertyInfo.AddRdnParameterInfo(matchingParameterInfoValues);
            }
        }

        internal void ConfigureConstructorParameters()
        {
            Debug.Assert(Kind == RdnTypeInfoKind.Object);
            Debug.Assert(DetermineUsesParameterizedConstructor());
            Debug.Assert(_propertyCache is not null);
            Debug.Assert(_parameterCache is null);

            List<RdnParameterInfo> parameterCache = new(ParameterCount);
            Dictionary<ParameterLookupKey, RdnParameterInfo> parameterIndex = new(ParameterCount);

            foreach (RdnPropertyInfo propertyInfo in _propertyCache)
            {
                RdnParameterInfo? parameterInfo = propertyInfo.AssociatedParameter;
                if (parameterInfo is null)
                {
                    continue;
                }

                string propertyName = propertyInfo.MemberName ?? propertyInfo.Name;
                ParameterLookupKey paramKey = new(propertyInfo.PropertyType, propertyName);
                if (!parameterIndex.TryAdd(paramKey, parameterInfo))
                {
                    // Multiple object properties cannot bind to the same constructor parameter.
                    ThrowHelper.ThrowInvalidOperationException_MultiplePropertiesBindToConstructorParameters(
                        Type,
                        parameterInfo.Name,
                        propertyInfo.Name,
                        parameterIndex[paramKey].MatchingProperty.Name);
                }

                parameterCache.Add(parameterInfo);
            }

            if (ExtensionDataProperty is { AssociatedParameter: not null })
            {
                Debug.Assert(ExtensionDataProperty.MemberName != null, "Custom property info cannot be data extension property");
                ThrowHelper.ThrowInvalidOperationException_ExtensionDataCannotBindToCtorParam(ExtensionDataProperty.MemberName, ExtensionDataProperty);
            }

            _parameterCache = parameterCache.ToArray();
            _parameterInfoValuesIndex = null;
        }

        internal static void ValidateType(Type type)
        {
            if (IsInvalidForSerialization(type))
            {
                ThrowHelper.ThrowInvalidOperationException_CannotSerializeInvalidType(type, declaringType: null, memberInfo: null);
            }
        }

        internal static bool IsInvalidForSerialization(Type type)
        {
            return type == typeof(void) || type.IsPointer || type.IsByRef || IsByRefLike(type) || type.ContainsGenericParameters;
        }

        internal void PopulatePolymorphismMetadata()
        {
            Debug.Assert(!IsReadOnly);

            RdnPolymorphismOptions? options = RdnPolymorphismOptions.CreateFromAttributeDeclarations(Type);
            if (options != null)
            {
                options.DeclaringTypeInfo = this;
                _polymorphismOptions = options;
            }
        }

        internal void MapInterfaceTypesToCallbacks()
        {
            Debug.Assert(!IsReadOnly);

            if (Kind is RdnTypeInfoKind.Object or RdnTypeInfoKind.Enumerable or RdnTypeInfoKind.Dictionary)
            {
                if (typeof(IRdnOnSerializing).IsAssignableFrom(Type))
                {
                    OnSerializing = static obj => ((IRdnOnSerializing)obj).OnSerializing();
                }

                if (typeof(IRdnOnSerialized).IsAssignableFrom(Type))
                {
                    OnSerialized = static obj => ((IRdnOnSerialized)obj).OnSerialized();
                }

                if (typeof(IRdnOnDeserializing).IsAssignableFrom(Type))
                {
                    OnDeserializing = static obj => ((IRdnOnDeserializing)obj).OnDeserializing();
                }

                if (typeof(IRdnOnDeserialized).IsAssignableFrom(Type))
                {
                    OnDeserialized = static obj => ((IRdnOnDeserialized)obj).OnDeserialized();
                }
            }
        }

        internal void SetCreateObjectIfCompatible(Delegate? createObject)
        {
            Debug.Assert(!IsReadOnly);

            // Guard against the reflection resolver/source generator attempting to pass
            // a CreateObject delegate to converters/metadata that do not support it.
            if (Converter.SupportsCreateObjectDelegate && !Converter.ConstructorIsParameterized)
            {
                SetCreateObject(createObject);
            }
        }

        private static bool IsByRefLike(Type type)
        {
#if NET
            return type.IsByRefLike;
#else
            if (!type.IsValueType)
            {
                return false;
            }

            object[] attributes = type.GetCustomAttributes(inherit: false);

            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].GetType().FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute")
                {
                    return true;
                }
            }

            return false;
#endif
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool SupportsPolymorphicDeserialization
        {
            get
            {
                Debug.Assert(IsConfigurationStarted);
                return PolymorphicTypeResolver?.UsesTypeDiscriminators == true;
            }
        }

        internal static bool IsValidExtensionDataProperty(Type propertyType)
        {
            return typeof(IDictionary<string, object>).IsAssignableFrom(propertyType) ||
                typeof(IDictionary<string, RdnElement>).IsAssignableFrom(propertyType) ||
                propertyType == typeof(IReadOnlyDictionary<string, object>) ||
                propertyType == typeof(IReadOnlyDictionary<string, RdnElement>) ||
                // Avoid a reference to typeof(RdnNode) to support trimming.
                (propertyType.FullName == RdnObjectTypeName && ReferenceEquals(propertyType.Assembly, typeof(RdnTypeInfo).Assembly));
        }

        private static RdnTypeInfoKind GetTypeInfoKind(Type type, RdnConverter converter)
        {
            if (type == typeof(object) && converter.CanBePolymorphic)
            {
                // System.Object is polymorphic and will not respect Properties
                Debug.Assert(converter is ObjectConverter);
                return RdnTypeInfoKind.None;
            }

            switch (converter.ConverterStrategy)
            {
                case ConverterStrategy.Value: return RdnTypeInfoKind.None;
                case ConverterStrategy.Object: return RdnTypeInfoKind.Object;
                case ConverterStrategy.Enumerable: return RdnTypeInfoKind.Enumerable;
                case ConverterStrategy.Dictionary: return RdnTypeInfoKind.Dictionary;
                case ConverterStrategy.None:
                    Debug.Assert(converter is RdnConverterFactory);
                    ThrowHelper.ThrowNotSupportedException_SerializationNotSupported(type);
                    return default;
                default:
                    Debug.Fail($"Unexpected class type: {converter.ConverterStrategy}");
                    throw new InvalidOperationException();
            }
        }

        internal sealed class RdnPropertyInfoList : ConfigurationList<RdnPropertyInfo>
        {
            private readonly RdnTypeInfo _rdnTypeInfo;

            public RdnPropertyInfoList(RdnTypeInfo rdnTypeInfo)
            {
                _rdnTypeInfo = rdnTypeInfo;
            }

            public override bool IsReadOnly => _rdnTypeInfo._properties == this && _rdnTypeInfo.IsReadOnly || _rdnTypeInfo.Kind != RdnTypeInfoKind.Object;
            protected override void OnCollectionModifying()
            {
                if (_rdnTypeInfo._properties == this)
                {
                    _rdnTypeInfo.VerifyMutable();
                }

                if (_rdnTypeInfo.Kind != RdnTypeInfoKind.Object)
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(_rdnTypeInfo.Kind);
                }
            }

            protected override void ValidateAddedValue(RdnPropertyInfo item)
            {
                item.EnsureChildOf(_rdnTypeInfo);
            }

            public void SortProperties()
            {
                _list.StableSortByKey(static propInfo => propInfo.Order);
            }

            /// <summary>
            /// Used by the built-in resolvers to add property metadata applying conflict resolution.
            /// </summary>
            public void AddPropertyWithConflictResolution(RdnPropertyInfo rdnPropertyInfo, ref PropertyHierarchyResolutionState state)
            {
                Debug.Assert(!_rdnTypeInfo.IsConfigured);
                Debug.Assert(rdnPropertyInfo.MemberName != null, "MemberName can be null in custom RdnPropertyInfo instances and should never be passed in this method");

                // Algorithm should be kept in sync with the Roslyn equivalent in RdnSourceGenerator.Parser.cs
                string memberName = rdnPropertyInfo.MemberName;

                if (state.AddedProperties.TryAdd(rdnPropertyInfo.Name, (rdnPropertyInfo, Count)))
                {
                    Add(rdnPropertyInfo);
                    state.IsPropertyOrderSpecified |= rdnPropertyInfo.Order != 0;
                }
                else
                {
                    // The RdnPropertyNameAttribute or naming policy resulted in a collision.
                    (RdnPropertyInfo other, int index) = state.AddedProperties[rdnPropertyInfo.Name];

                    if (other.IsIgnored)
                    {
                        // Overwrite previously cached property since it has [RdnIgnore].
                        state.AddedProperties[rdnPropertyInfo.Name] = (rdnPropertyInfo, index);
                        this[index] = rdnPropertyInfo;
                        state.IsPropertyOrderSpecified |= rdnPropertyInfo.Order != 0;
                    }
                    else
                    {
                        bool ignoreCurrentProperty =
                            // Does the current property have `RdnIgnoreAttribute`?
                            rdnPropertyInfo.IsIgnored ||
                            // Is the current property hidden by the previously cached property
                            // (with `new` keyword, or by overriding)?
                            rdnPropertyInfo.IsOverriddenOrShadowedBy(other) ||
                            // Was a property with the same CLR name ignored? That property hid the current property,
                            // thus, if it was ignored, the current property should be ignored too.
                            (state.IgnoredProperties?.TryGetValue(memberName, out RdnPropertyInfo? ignored) == true && rdnPropertyInfo.IsOverriddenOrShadowedBy(ignored));

                        if (!ignoreCurrentProperty)
                        {
                            ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(_rdnTypeInfo.Type, rdnPropertyInfo.Name);
                        }
                    }
                }

                if (rdnPropertyInfo.IsIgnored)
                {
                    (state.IgnoredProperties ??= new())[memberName] = rdnPropertyInfo;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Type = {Type.Name}, Kind = {Kind}";
    }
}
