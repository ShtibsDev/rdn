// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace Rdn.Serialization.Metadata
{
    /// <summary>
    /// Provides RDN serialization-related metadata about a property or field defined in an object.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class RdnPropertyInfo
    {
        internal static readonly RdnPropertyInfo s_missingProperty = GetPropertyPlaceholder();

        internal RdnTypeInfo? DeclaringTypeInfo { get; private set; }

        /// <summary>
        /// Converter after applying CustomConverter (i.e. RdnConverterAttribute)
        /// </summary>
        internal RdnConverter EffectiveConverter
        {
            get
            {
                Debug.Assert(_effectiveConverter != null);
                return _effectiveConverter;
            }
        }

        private protected RdnConverter? _effectiveConverter;

        /// <summary>
        /// Gets or sets a custom converter override for the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// It is possible to use <see cref="RdnConverterFactory"/> instances with this property.
        ///
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/>, the value of
        /// <see cref="CustomConverter"/> will be mapped from <see cref="RdnConverterAttribute" /> annotations.
        /// </remarks>
        public RdnConverter? CustomConverter
        {
            get => _customConverter;
            set
            {
                VerifyMutable();
                _customConverter = value;
            }
        }

        private RdnConverter? _customConverter;

        /// <summary>
        /// Gets or sets a getter delegate for the property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// Setting to <see langword="null"/> will result in the property being skipped on serialization.
        /// </remarks>
        public Func<object, object?>? Get
        {
            get => _untypedGet;
            set
            {
                VerifyMutable();
                SetGetter(value);
            }
        }

        /// <summary>
        /// Gets or sets a setter delegate for the property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// Setting to <see langword="null"/> will result in the property being skipped on deserialization.
        /// </remarks>
        public Action<object, object?>? Set
        {
            get => _untypedSet;
            set
            {
                VerifyMutable();
                SetSetter(value);
                _isUserSpecifiedSetter = true;
            }
        }

        private protected Func<object, object?>? _untypedGet;
        private protected Action<object, object?>? _untypedSet;
        private bool _isUserSpecifiedSetter;

        private protected abstract void SetGetter(Delegate? getter);
        private protected abstract void SetSetter(Delegate? setter);

        /// <summary>
        /// Gets or sets a predicate deciding whether the current property value should be serialized.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// The first parameter denotes the parent object, the second parameter denotes the property value.
        ///
        /// Setting the predicate to <see langword="null"/> is equivalent to always serializing the property value.
        ///
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/>,
        /// the value of <see cref="RdnIgnoreAttribute.Condition"/> will map to this predicate.
        /// </remarks>
        public Func<object, object?, bool>? ShouldSerialize
        {
            get => _shouldSerialize;
            set
            {
                VerifyMutable();
                SetShouldSerialize(value);
                // Invalidate any RdnIgnore configuration if delegate set manually by user
                _isUserSpecifiedShouldSerialize = true;
                IgnoreDefaultValuesOnWrite = false;
            }
        }

        private protected Func<object, object?, bool>? _shouldSerialize;
        private bool _isUserSpecifiedShouldSerialize;
        private protected abstract void SetShouldSerialize(Delegate? predicate);

        internal RdnIgnoreCondition? IgnoreCondition
        {
            get => _ignoreCondition;
            set
            {
                Debug.Assert(!IsConfigured);
                ConfigureIgnoreCondition(value);
                _ignoreCondition = value;
            }
        }

        private RdnIgnoreCondition? _ignoreCondition;
        private protected abstract void ConfigureIgnoreCondition(RdnIgnoreCondition? ignoreCondition);

        /// <summary>
        /// Gets or sets a custom attribute provider for the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// When resolving metadata via the built-in resolvers this
        /// will be populated with the underlying <see cref="MemberInfo" /> of the serialized property or field.
        ///
        /// Setting a custom attribute provider will have no impact on the contract model,
        /// but serves as metadata for downstream contract modifiers.
        /// </remarks>
        public ICustomAttributeProvider? AttributeProvider
        {
            get
            {
                Func<ICustomAttributeProvider>? attributeProviderFactory = Volatile.Read(ref AttributeProviderFactory);
                ICustomAttributeProvider? attributeProvider = _attributeProvider;

                if (attributeProvider is null && attributeProviderFactory is not null)
                {
                    _attributeProvider = attributeProvider = attributeProviderFactory();
                    Volatile.Write(ref AttributeProviderFactory, null);
                }

                return attributeProvider;
            }
            set
            {
                VerifyMutable();

                _attributeProvider = value;
                Volatile.Write(ref AttributeProviderFactory, null);
            }
        }

        // Metadata emanating from the source generator use delayed attribute provider initialization
        // ensuring that reflection metadata resolution remains pay-for-play and is trimmable.
        internal Func<ICustomAttributeProvider>? AttributeProviderFactory;
        private ICustomAttributeProvider? _attributeProvider;

        /// <summary>
        /// Gets or sets a value indicating if the property or field should be replaced or populated during deserialization.
        /// </summary>
        /// <remarks>
        /// Initial value for this property is based on the presence of <see cref="RdnObjectCreationHandlingAttribute"/> attribute on the property.
        /// When <see langword="null"/> effective handling will be resolved based on
        /// capability of property converter to populate, containing type's <see cref="RdnTypeInfo.PreferredPropertyObjectCreationHandling"/>.
        /// and <see cref="RdnSerializerOptions.PreferredObjectCreationHandling"/> value.
        /// </remarks>
        public RdnObjectCreationHandling? ObjectCreationHandling
        {
            get => _objectCreationHandling;
            set
            {
                VerifyMutable();

                if (value != null)
                {
                    if (!RdnSerializer.IsValidCreationHandlingValue(value.Value))
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                }

                _objectCreationHandling = value;
            }
        }

        private RdnObjectCreationHandling? _objectCreationHandling;
        internal RdnObjectCreationHandling EffectiveObjectCreationHandling { get; private set; }

        internal string? MemberName { get; set; } // Do not rename (legacy schema generation)
        internal MemberTypes MemberType { get; set; }
        internal bool IsVirtual { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the return type of the getter is annotated as nullable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// The current <see cref="PropertyType"/> is not a reference type or <see cref="Nullable{T}"/>.
        /// </exception>
        /// <remarks>
        /// Contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// derive the value of this property from nullable reference type annotations, including annotations
        /// from attributes such as <see cref="NotNullAttribute"/> or <see cref="MaybeNullAttribute"/>.
        ///
        /// This property has no effect on serialization unless the <see cref="RdnSerializerOptions.RespectNullableAnnotations"/>
        /// property has been enabled, in which case the serializer will reject any <see langword="null"/> values returned by the getter.
        /// </remarks>
        public bool IsGetNullable
        {
            get => _isGetNullable;
            set
            {
                VerifyMutable();

                if (value && !PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowInvalidOperationException_PropertyTypeNotNullable(this);
                }

                _isGetNullable = value;
            }
        }

        private bool _isGetNullable;

        /// <summary>
        /// Gets or sets a value indicating whether the input type of the setter is annotated as nullable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// The current <see cref="PropertyType"/> is not a reference type or <see cref="Nullable{T}"/>.
        /// </exception>
        /// <remarks>
        /// Contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// derive the value of this property from nullable reference type annotations, including annotations
        /// from attributes such as <see cref="AllowNullAttribute"/> or <see cref="DisallowNullAttribute"/>.
        ///
        /// This property has no effect on deserialization unless the <see cref="RdnSerializerOptions.RespectNullableAnnotations"/>
        /// property has been enabled, in which case the serializer will reject any <see langword="null"/> deserialization results.
        ///
        /// If the property has been associated with a deserialization constructor parameter,
        /// this setting reflected the nullability annotation of the parameter and not the property setter.
        /// </remarks>
        public bool IsSetNullable
        {
            get => _isSetNullable;
            set
            {
                VerifyMutable();

                if (value && !PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowInvalidOperationException_PropertyTypeNotNullable(this);
                }

                _isSetNullable = value;
            }
        }

        private protected bool _isSetNullable;

        /// <summary>
        /// Specifies whether the current property is a special extension data property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        ///
        /// -or-
        ///
        /// The current <see cref="PropertyType"/> is not valid for use with extension data.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="RdnExtensionDataAttribute"/> annotations.
        /// </remarks>
        public bool IsExtensionData
        {
            get => _isExtensionDataProperty;
            set
            {
                VerifyMutable();

                if (value && !RdnTypeInfo.IsValidExtensionDataProperty(PropertyType))
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationDataExtensionPropertyInvalid(this);
                }

                _isExtensionDataProperty = value;
            }
        }

        private bool _isExtensionDataProperty;

        /// <summary>
        /// Specifies whether the current property is required for deserialization to be successful.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="RdnRequiredAttribute"/> annotations.
        ///
        /// For contracts using <see cref="DefaultRdnTypeInfoResolver"/>, properties using the <see langword="required"/> keyword
        /// will also map to this setting, unless deserialization uses a SetsRequiredMembersAttribute on a constructor that populates all required properties.
        /// <see langword="required"/> keyword is currently not supported in <see cref="RdnSerializerContext"/> contracts.
        /// </remarks>
        public bool IsRequired
        {
            get => _isRequired;
            set
            {
                VerifyMutable();
                _isRequired = value;
            }
        }

        private protected bool _isRequired;

        /// <summary>
        /// Gets the constructor parameter associated with the current property.
        /// </summary>
        /// <remarks>
        /// Returns the <see cref="RdnParameterInfo"/> metadata for the parameter in the
        /// deserialization constructor that has been associated with the current property.
        ///
        /// A constructor parameter is matched to a property or field if they are of the
        /// same type and have the same name, up to case insensitivity. Each constructor
        /// parameter must be matched to exactly one property of field.
        /// </remarks>
        public RdnParameterInfo? AssociatedParameter { get; internal set; }

        internal RdnPropertyInfo(Type declaringType, Type propertyType, RdnTypeInfo? declaringTypeInfo, RdnSerializerOptions options)
        {
            Debug.Assert(declaringTypeInfo is null || declaringType.IsAssignableFrom(declaringTypeInfo.Type));

            DeclaringType = declaringType;
            PropertyType = propertyType;
            DeclaringTypeInfo = declaringTypeInfo; // null declaringTypeInfo means it's not tied yet
            Options = options;

            _isGetNullable = _isSetNullable = PropertyTypeCanBeNull;
        }

        internal static RdnPropertyInfo GetPropertyPlaceholder()
        {
            RdnPropertyInfo info = new RdnPropertyInfo<object>(typeof(object), declaringTypeInfo: null, options: null!);

            Debug.Assert(!info.IsForTypeInfo);
            Debug.Assert(!info.CanSerialize);
            Debug.Assert(!info.CanDeserialize);

            info.Name = string.Empty;

            return info;
        }

        /// <summary>
        /// Gets the declaring type of the property.
        /// </summary>
        public Type DeclaringType { get; }

        /// <summary>
        /// Gets the type of the current property.
        /// </summary>
        public Type PropertyType { get; }

        private protected void VerifyMutable()
        {
            DeclaringTypeInfo?.VerifyMutable();
        }

        internal bool IsConfigured { get; private set; }

        internal void Configure()
        {
            Debug.Assert(DeclaringTypeInfo != null);
            Debug.Assert(!IsConfigured);

            if (IsIgnored)
            {
                // Avoid configuring RdnIgnore.Always properties
                // to avoid failing on potentially unsupported types.
                CanSerialize = false;
                CanDeserialize = false;
            }
            else
            {
                _rdnTypeInfo ??= Options.GetTypeInfoInternal(PropertyType);
                _rdnTypeInfo.EnsureConfigured();

                DetermineEffectiveConverter(_rdnTypeInfo);
                DetermineNumberHandlingForProperty();
                DetermineEffectiveObjectCreationHandlingForProperty();
                DetermineSerializationCapabilities();
                DetermineIgnoreCondition();
            }

            if (IsForTypeInfo)
            {
                DetermineNumberHandlingForTypeInfo();
            }
            else
            {
                ValidateAndCachePropertyName();
            }

            if (IsRequired)
            {
                if (!CanDeserialize &&
                    !(AssociatedParameter?.IsRequiredParameter is true &&
                      Options.RespectRequiredConstructorParameters))
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnPropertyRequiredAndNotDeserializable(this);
                }

                if (IsExtensionData)
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnPropertyRequiredAndExtensionData(this);
                }

                Debug.Assert(!IgnoreNullTokensOnRead);
            }

            IsConfigured = true;
        }

        private protected abstract void DetermineEffectiveConverter(RdnTypeInfo rdnTypeInfo);

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        internal abstract void DetermineReflectionPropertyAccessors(MemberInfo memberInfo, bool useNonPublicAccessors);

        private void ValidateAndCachePropertyName()
        {
            Debug.Assert(Name != null);

            if (Options.ReferenceHandlingStrategy is RdnKnownReferenceHandler.Preserve &&
                this is { DeclaringType.IsValueType: false, IsIgnored: false, IsExtensionData: false } &&
                Name is RdnSerializer.IdPropertyName or RdnSerializer.RefPropertyName)
            {
                // Validate potential conflicts with reference preservation metadata property names.
                // Conflicts with polymorphic type discriminators are contextual and need to be
                // handled separately by the PolymorphicTypeResolver type.

                ThrowHelper.ThrowInvalidOperationException_PropertyConflictsWithMetadataPropertyName(DeclaringType, Name);
            }

            NameAsUtf8Bytes = Encoding.UTF8.GetBytes(Name);
            EscapedNameSection = RdnHelpers.GetEscapedPropertyNameSection(NameAsUtf8Bytes, Options.Encoder);
        }

        private void DetermineIgnoreCondition()
        {
            if (_ignoreCondition != null)
            {
                // Do not apply global policy if already configured on the property level.
                return;
            }

#pragma warning disable SYSLIB0020 // RdnSerializerOptions.IgnoreNullValues is obsolete
            if (Options.IgnoreNullValues)
#pragma warning restore SYSLIB0020
            {
                Debug.Assert(Options.DefaultIgnoreCondition == RdnIgnoreCondition.Never);
                if (PropertyTypeCanBeNull)
                {
                    IgnoreNullTokensOnRead = !_isUserSpecifiedSetter && !IsRequired;
                    IgnoreDefaultValuesOnWrite = ShouldSerialize is null;
                }
            }
            else if (Options.DefaultIgnoreCondition == RdnIgnoreCondition.WhenWritingNull)
            {
                if (PropertyTypeCanBeNull)
                {
                    IgnoreDefaultValuesOnWrite = ShouldSerialize is null;
                }
            }
            else if (Options.DefaultIgnoreCondition == RdnIgnoreCondition.WhenWritingDefault)
            {
                IgnoreDefaultValuesOnWrite = ShouldSerialize is null;
            }
        }

        private void DetermineSerializationCapabilities()
        {
            Debug.Assert(EffectiveConverter != null, "Must have calculated the effective converter.");
            CanSerialize = HasGetter;
            CanDeserialize = HasSetter;

            Debug.Assert(MemberType is 0 or MemberTypes.Field or MemberTypes.Property);
            if (MemberType == 0 || _ignoreCondition != null)
            {
                // No policy to be applied if either:
                // 1. RdnPropertyInfo is a custom instance (not generated via reflection or sourcegen).
                // 2. A RdnIgnoreCondition has been specified on the property level.
                CanDeserializeOrPopulate = CanDeserialize || EffectiveObjectCreationHandling == RdnObjectCreationHandling.Populate;
                return;
            }

            if ((EffectiveConverter.ConverterStrategy & (ConverterStrategy.Enumerable | ConverterStrategy.Dictionary)) != 0)
            {
                // Properties of collections types that only have setters are not supported.
                if (Get == null && Set != null && !_isUserSpecifiedSetter)
                {
                    CanDeserialize = false;
                }
            }
            else
            {
                // For read-only properties of non-collection types, apply IgnoreReadOnlyProperties/Fields policy,
                // unless a `ShouldSerialize` predicate has been explicitly applied by the user (null or non-null).
                if (Get != null && Set == null && IgnoreReadOnlyMember && !_isUserSpecifiedShouldSerialize)
                {
                    CanSerialize = false;
                }
            }

            CanDeserializeOrPopulate = CanDeserialize || EffectiveObjectCreationHandling == RdnObjectCreationHandling.Populate;
        }

        private void DetermineNumberHandlingForTypeInfo()
        {
            Debug.Assert(DeclaringTypeInfo != null, "We should have ensured parent is assigned in RdnTypeInfo");
            Debug.Assert(!DeclaringTypeInfo.IsConfigured);

            RdnNumberHandling? declaringTypeNumberHandling = DeclaringTypeInfo.NumberHandling;

            if (declaringTypeNumberHandling != null && declaringTypeNumberHandling != RdnNumberHandling.Strict && !EffectiveConverter.IsInternalConverter)
            {
                ThrowHelper.ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(this);
            }

            if (NumberHandingIsApplicable())
            {
                // This logic is to honor RdnNumberHandlingAttribute placed on
                // custom collections e.g. public class MyNumberList : List<int>.

                // Priority 1: Get handling from the type (parent type in this case is the type itself).
                EffectiveNumberHandling = declaringTypeNumberHandling;

                // Priority 2: Get handling from RdnSerializerOptions instance.
                if (!EffectiveNumberHandling.HasValue && Options.NumberHandling != RdnNumberHandling.Strict)
                {
                    EffectiveNumberHandling = Options.NumberHandling;
                }
            }
        }

        private void DetermineNumberHandlingForProperty()
        {
            Debug.Assert(DeclaringTypeInfo != null, "We should have ensured parent is assigned in RdnTypeInfo");
            Debug.Assert(!IsConfigured, "Should not be called post-configuration.");
            Debug.Assert(_rdnTypeInfo != null, "Must have already been determined on configuration.");

            bool numberHandlingIsApplicable = NumberHandingIsApplicable();

            if (numberHandlingIsApplicable)
            {
                // Priority 1: Get handling from attribute on property/field, its parent class type or property type.
                RdnNumberHandling? handling = NumberHandling ?? DeclaringTypeInfo.NumberHandling ?? _rdnTypeInfo.NumberHandling;

                // Priority 2: Get handling from RdnSerializerOptions instance.
                if (!handling.HasValue && Options.NumberHandling != RdnNumberHandling.Strict)
                {
                    handling = Options.NumberHandling;
                }

                EffectiveNumberHandling = handling;
            }
            else if (NumberHandling.HasValue && NumberHandling != RdnNumberHandling.Strict)
            {
                ThrowHelper.ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(this);
            }
        }

        private void DetermineEffectiveObjectCreationHandlingForProperty()
        {
            Debug.Assert(EffectiveConverter != null, "Must have calculated the effective converter.");
            Debug.Assert(DeclaringTypeInfo != null, "We should have ensured parent is assigned in RdnTypeInfo");
            Debug.Assert(!IsConfigured, "Should not be called post-configuration.");

            RdnObjectCreationHandling effectiveObjectCreationHandling = RdnObjectCreationHandling.Replace;
            if (ObjectCreationHandling == null)
            {
                // Consult type-level configuration, then global configuration.
                // Ignore global configuration if we're using a parameterized constructor.
                RdnObjectCreationHandling preferredCreationHandling =
                    DeclaringTypeInfo.PreferredPropertyObjectCreationHandling
                    ?? (DeclaringTypeInfo.DetermineUsesParameterizedConstructor()
                        ? RdnObjectCreationHandling.Replace
                        : Options.PreferredObjectCreationHandling);

                bool canPopulate =
                    preferredCreationHandling == RdnObjectCreationHandling.Populate &&
                    EffectiveConverter.CanPopulate &&
                    Get != null &&
                    (!PropertyType.IsValueType || Set != null) &&
                    !DeclaringTypeInfo.SupportsPolymorphicDeserialization &&
                    !(Set == null && IgnoreReadOnlyMember);

                effectiveObjectCreationHandling = canPopulate ? RdnObjectCreationHandling.Populate : RdnObjectCreationHandling.Replace;
            }
            else if (ObjectCreationHandling == RdnObjectCreationHandling.Populate)
            {
                if (!EffectiveConverter.CanPopulate)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPopulateNotSupportedByConverter(this);
                }

                if (Get == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyMustHaveAGetter(this);
                }

                if (PropertyType.IsValueType && Set == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyValueTypeMustHaveASetter(this);
                }

                Debug.Assert(_rdnTypeInfo != null);
                Debug.Assert(_rdnTypeInfo.IsConfigurationStarted);
                if (RdnTypeInfo.SupportsPolymorphicDeserialization)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowPolymorphicDeserialization(this);
                }

                if (Set == null && IgnoreReadOnlyMember)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReadOnlyMember(this);
                }

                effectiveObjectCreationHandling = RdnObjectCreationHandling.Populate;
            }

            if (effectiveObjectCreationHandling is RdnObjectCreationHandling.Populate)
            {
                if (DeclaringTypeInfo.DetermineUsesParameterizedConstructor())
                {
                    ThrowHelper.ThrowNotSupportedException_ObjectCreationHandlingPropertyDoesNotSupportParameterizedConstructors();
                }

                if (Options.ReferenceHandlingStrategy != RdnKnownReferenceHandler.Unspecified)
                {
                    ThrowHelper.ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReferenceHandling();
                }
            }

            // Validation complete, commit configuration.
            EffectiveObjectCreationHandling = effectiveObjectCreationHandling;
        }

        private bool NumberHandingIsApplicable()
        {
            if (EffectiveConverter.IsInternalConverterForNumberType)
            {
                return true;
            }

            Type potentialNumberType;
            if (!EffectiveConverter.IsInternalConverter ||
                ((ConverterStrategy.Enumerable | ConverterStrategy.Dictionary) & EffectiveConverter.ConverterStrategy) == 0)
            {
                potentialNumberType = PropertyType;
            }
            else
            {
                Debug.Assert(EffectiveConverter.ElementType != null);
                potentialNumberType = EffectiveConverter.ElementType;
            }

            potentialNumberType = Nullable.GetUnderlyingType(potentialNumberType) ?? potentialNumberType;

            return potentialNumberType == typeof(byte) ||
                potentialNumberType == typeof(decimal) ||
                potentialNumberType == typeof(double) ||
                potentialNumberType == typeof(short) ||
                potentialNumberType == typeof(int) ||
                potentialNumberType == typeof(long) ||
                potentialNumberType == typeof(sbyte) ||
                potentialNumberType == typeof(float) ||
                potentialNumberType == typeof(ushort) ||
                potentialNumberType == typeof(uint) ||
                potentialNumberType == typeof(ulong) ||
#if NET
                potentialNumberType == typeof(Half) ||
#endif
#if NET
                potentialNumberType == typeof(Int128) ||
                potentialNumberType == typeof(UInt128) ||
#endif
                potentialNumberType == RdnTypeInfo.ObjectType;
        }

        /// <summary>
        /// Creates a <see cref="RdnPropertyInfo"/> instance whose type matches that of the current property.
        /// </summary>
        internal abstract void AddRdnParameterInfo(RdnParameterInfoValues parameterInfoValues);

        internal abstract bool GetMemberAndWriteRdn(object obj, ref WriteStack state, Utf8RdnWriter writer);
        internal abstract bool GetMemberAndWriteRdnExtensionData(object obj, ref WriteStack state, Utf8RdnWriter writer);

        internal abstract object? GetValueAsObject(object obj);

        internal bool HasGetter => _untypedGet is not null;
        internal bool HasSetter => _untypedSet is not null;
        internal bool IgnoreNullTokensOnRead { get; private protected set; }
        internal bool IgnoreDefaultValuesOnWrite { get; private protected set; }

        internal bool IgnoreReadOnlyMember
        {
            get
            {
                Debug.Assert(MemberType == MemberTypes.Property || MemberType == MemberTypes.Field || MemberType == default);
                return MemberType switch
                {
                    MemberTypes.Property => Options.IgnoreReadOnlyProperties,
                    MemberTypes.Field => Options.IgnoreReadOnlyFields,
                    _ => false,
                };
            }
        }

        /// <summary>
        /// True if the corresponding cref="RdnTypeInfo.PropertyInfoForTypeInfo"/> is this instance.
        /// </summary>
        internal bool IsForTypeInfo { get; init; }

        // There are 3 copies of the property name:
        // 1) Name. The unescaped property name.
        // 2) NameAsUtf8Bytes. The Utf8 version of Name. Used during deserialization for property lookup.
        // 3) EscapedNameSection. The escaped version of NameAsUtf8Bytes plus the wrapping quotes and a trailing colon. Used during serialization.

        /// <summary>
        /// Gets or sets the RDN property name used when serializing the property.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// The value of <see cref="Name"/> cannot conflict with that of other <see cref="RdnPropertyInfo"/> defined in the declaring <see cref="RdnTypeInfo"/>.
        ///
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value typically reflects the underlying .NET member name, the name derived from <see cref="RdnSerializerOptions.PropertyNamingPolicy" />,
        /// or the value specified in <see cref="RdnPropertyNameAttribute" />.
        /// </remarks>
        public string Name
        {
            get
            {
                Debug.Assert(_name != null);
                return _name;
            }
            set
            {
                VerifyMutable();

                ArgumentNullException.ThrowIfNull(value);

                _name = value;
            }
        }

        private string? _name;

        /// <summary>
        /// Utf8 version of Name.
        /// </summary>
        internal byte[] NameAsUtf8Bytes { get; private set; } = null!;

        /// <summary>
        /// The escaped name passed to the writer.
        /// </summary>
        internal byte[] EscapedNameSection { get; private set; } = null!;

        /// <summary>
        /// Gets the <see cref="RdnSerializerOptions"/> value associated with the current contract instance.
        /// </summary>
        public RdnSerializerOptions Options { get; }

        /// <summary>
        /// Gets or sets the serialization order for the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="RdnPropertyOrderAttribute"/> annotations.
        /// </remarks>
        public int Order
        {
            get => _order;
            set
            {
                VerifyMutable();
                _order = value;
            }
        }

        private int _order;

        internal bool ReadRdnAndAddExtensionProperty(
            object obj,
            scoped ref ReadStack state,
            ref Utf8RdnReader reader)
        {
            object propValue = GetValueAsObject(obj)!;

            if (propValue is IDictionary<string, object?> dictionaryObjectValue)
            {
                if (reader.TokenType == RdnTokenType.Null)
                {
                    // A null RDN value is treated as a null object reference.
                    AddProperty(in state.Current, dictionaryObjectValue, null);
                }
                else
                {
                    RdnConverter<object> converter = GetDictionaryValueConverter<object>();
                    object value = converter.Read(ref reader, RdnTypeInfo.ObjectType, Options)!;
                    AddProperty(in state.Current, dictionaryObjectValue, value);
                }
            }
            else if (propValue is IDictionary<string, RdnElement> dictionaryElementValue)
            {
                RdnConverter<RdnElement> converter = GetDictionaryValueConverter<RdnElement>();
                RdnElement value = converter.Read(ref reader, typeof(RdnElement), Options);
                AddProperty(in state.Current, dictionaryElementValue, value);
            }
            else
            {
                // Avoid a type reference to RdnObject and its converter to support trimming.
                Debug.Assert(propValue is Nodes.RdnObject);
                EffectiveConverter.ReadElementAndSetProperty(propValue, state.Current.RdnPropertyNameAsString!, ref reader, Options, ref state);
            }

            return true;

            RdnConverter<TValue> GetDictionaryValueConverter<TValue>()
            {
                RdnTypeInfo dictionaryValueInfo =
                    RdnTypeInfo.ElementTypeInfo
                    // Slower path for non-generic types that implement IDictionary<,>.
                    // It is possible to cache this converter on RdnTypeInfo if we assume the property value
                    // will always be the same type for all instances.
                    ?? Options.GetTypeInfoInternal(typeof(TValue));

                Debug.Assert(dictionaryValueInfo is RdnTypeInfo<TValue>);
                return ((RdnTypeInfo<TValue>)dictionaryValueInfo).EffectiveConverter;
            }

            void AddProperty<TValue>(ref readonly ReadStackFrame current, IDictionary<string, TValue> d, TValue value)
            {
                string property = current.RdnPropertyNameAsString!;
                if (Options.AllowDuplicateProperties)
                {
                    d[property] = value;
                }
                else
                {
#if NET
                    if (!d.TryAdd(property, value))
#else
                    if (d.ContainsKey(property))
#endif
                    {
                        ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(current.RdnPropertyInfo!);
                    }

#if !NET
                    d[property] = value;
#endif
                }
            }
        }

        internal abstract bool ReadRdnAndSetMember(object obj, scoped ref ReadStack state, ref Utf8RdnReader reader);

        internal abstract bool ReadRdnAsObject(scoped ref ReadStack state, ref Utf8RdnReader reader, out object? value);

        internal bool ReadRdnExtensionDataValue(scoped ref ReadStack state, ref Utf8RdnReader reader, out object? value)
        {
            Debug.Assert(this == state.Current.RdnTypeInfo.ExtensionDataProperty);

            if (RdnTypeInfo.ElementType == RdnTypeInfo.ObjectType && reader.TokenType == RdnTokenType.Null)
            {
                value = null;
                return true;
            }

            RdnConverter<RdnElement> converter = (RdnConverter<RdnElement>)Options.GetConverterInternal(typeof(RdnElement));
            if (!converter.TryRead(ref reader, typeof(RdnElement), Options, ref state, out RdnElement rdnElement, out _))
            {
                // RdnElement is a struct that must be read in full.
                value = null;
                return false;
            }

            value = rdnElement;
            return true;
        }

        internal void EnsureChildOf(RdnTypeInfo parent)
        {
            if (DeclaringTypeInfo is null)
            {
                DeclaringTypeInfo = parent;
            }
            else if (DeclaringTypeInfo != parent)
            {
                ThrowHelper.ThrowInvalidOperationException_RdnPropertyInfoIsBoundToDifferentRdnTypeInfo(this);
            }

            DeclaringTypeInfo.ResolveMatchingParameterInfo(this);
        }

        /// <summary>
        /// Tries to get pre-populated value from the property if populating is enabled.
        /// If property value is <see langword="null"/> this method will return false.
        /// </summary>
        internal bool TryGetPrePopulatedValue(scoped ref ReadStack state)
        {
            if (EffectiveObjectCreationHandling != RdnObjectCreationHandling.Populate)
                return false;

            Debug.Assert(EffectiveConverter.CanPopulate, "Property is marked with Populate but converter cannot populate. This should have been validated in Configure");
            Debug.Assert(state.Parent.ReturnValue != null, "Parent object is null");
            Debug.Assert(!state.Current.IsPopulating, "We've called TryGetPrePopulatedValue more than once");
            object? value = Get!(state.Parent.ReturnValue);
            state.Current.ReturnValue = value;
            state.Current.IsPopulating = value != null;
            return value != null;
        }

        internal RdnTypeInfo RdnTypeInfo
        {
            get
            {
                Debug.Assert(_rdnTypeInfo?.IsConfigurationStarted == true);
                // Even though this instance has already been configured,
                // it is possible for contending threads to call the property
                // while the wider RdnTypeInfo graph is still being configured.
                // Call EnsureConfigured() to force synchronization if necessary.
                RdnTypeInfo rdnTypeInfo = _rdnTypeInfo;
                rdnTypeInfo.EnsureConfigured();
                return rdnTypeInfo;
            }
            set
            {
                _rdnTypeInfo = value;
            }
        }

        private RdnTypeInfo? _rdnTypeInfo;

        /// <summary>
        /// Returns true if <see cref="RdnTypeInfo"/> has been configured.
        /// This might be false even if <see cref="IsConfigured"/> is true
        /// in cases of recursive types or <see cref="IsIgnored"/> is true.
        /// </summary>
        internal bool IsPropertyTypeInfoConfigured => _rdnTypeInfo?.IsConfigured == true;

        /// <summary>
        /// Property was marked RdnIgnoreCondition.Always and also hasn't been configured by the user.
        /// </summary>
        internal bool IsIgnored => _ignoreCondition is RdnIgnoreCondition.Always && Get is null && Set is null;

        /// <summary>
        /// Reflects the value of <see cref="HasGetter"/> combined with any additional global ignore policies.
        /// </summary>
        internal bool CanSerialize { get; private set; }
        /// <summary>
        /// Reflects the value of <see cref="HasSetter"/> combined with any additional global ignore policies.
        /// </summary>
        internal bool CanDeserialize { get; private set; }

        /// <summary>
        /// Reflects the value can be deserialized or populated
        /// </summary>
        internal bool CanDeserializeOrPopulate { get; private set; }

        /// <summary>
        /// Relevant to source generated metadata: did the property have the <see cref="RdnIncludeAttribute"/>?
        /// </summary>
        internal bool SrcGen_HasRdnInclude { get; set; }

        /// <summary>
        /// Relevant to source generated metadata: is the property public?
        /// </summary>
        internal bool SrcGen_IsPublic { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="RdnNumberHandling"/> applied to the current property.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnPropertyInfo"/> instance has been locked for further modification.
        /// </exception>
        /// <remarks>
        /// For contracts originating from <see cref="DefaultRdnTypeInfoResolver"/> or <see cref="RdnSerializerContext"/>,
        /// the value of this property will be mapped from <see cref="RdnNumberHandlingAttribute"/> annotations.
        /// </remarks>
        public RdnNumberHandling? NumberHandling
        {
            get => _numberHandling;
            set
            {
                VerifyMutable();
                _numberHandling = value;
            }
        }

        private RdnNumberHandling? _numberHandling;

        /// <summary>
        /// Number handling after considering options and declaring type number handling
        /// </summary>
        internal RdnNumberHandling? EffectiveNumberHandling { get; private set; }

        //  Whether the property type can be null.
        internal abstract bool PropertyTypeCanBeNull { get; }

        /// <summary>
        /// Default value used for parameterized ctor invocation.
        /// </summary>
        internal abstract object? DefaultValue { get; }

        /// <summary>
        /// Property index on the list of RdnTypeInfo properties.
        /// It is used as a unique identifier for properties.
        /// It is set just before property is configured and does not change afterward.
        /// It is not equivalent to index on the properties list
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal int PropertyIndex
        {
            get
            {
                Debug.Assert(IsConfigured);
                return _propertyIndex;
            }
            set
            {
                Debug.Assert(!IsConfigured);
                _propertyIndex = value;
            }
        }

        private int _propertyIndex;

        internal bool IsOverriddenOrShadowedBy(RdnPropertyInfo other)
            => MemberName == other.MemberName && DeclaringType.IsAssignableFrom(other.DeclaringType);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Name = {Name}, PropertyType = {PropertyType}";
    }
}
