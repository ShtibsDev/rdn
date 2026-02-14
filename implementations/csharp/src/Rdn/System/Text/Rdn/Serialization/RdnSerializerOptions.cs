// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Rdn.Encodings.Web;
using Rdn.Nodes;
using Rdn.Serialization;
using Rdn.Serialization.Converters;
using Rdn.Serialization.Metadata;
using System.Threading;

namespace Rdn
{
    /// <summary>
    /// Provides options to be used with <see cref="RdnSerializer"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed partial class RdnSerializerOptions
    {
        internal const int BufferSizeDefault = 16 * 1024;

        // For backward compatibility the default max depth for RdnSerializer is 64,
        // the minimum of RdnReaderOptions.DefaultMaxDepth and RdnWriterOptions.DefaultMaxDepth.
        internal const int DefaultMaxDepth = RdnReaderOptions.DefaultMaxDepth;

        /// <summary>
        /// Gets a read-only, singleton instance of <see cref="RdnSerializerOptions" /> that uses the default configuration.
        /// </summary>
        /// <remarks>
        /// Each <see cref="RdnSerializerOptions" /> instance encapsulates its own serialization metadata caches,
        /// so using fresh default instances every time one is needed can result in redundant recomputation of converters.
        /// This property provides a shared instance that can be consumed by any number of components without necessitating any converter recomputation.
        /// </remarks>
        private static RdnSerializerOptions? s_defaultOptions;
        public static RdnSerializerOptions Default
        {
            [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
            get => s_defaultOptions ?? GetOrCreateSingleton(ref s_defaultOptions, RdnSerializerDefaults.General);
        }

        /// <summary>
        /// Gets a read-only, singleton instance of <see cref="RdnSerializerOptions" /> that uses the web configuration.
        /// </summary>
        /// <remarks>
        /// Each <see cref="RdnSerializerOptions" /> instance encapsulates its own serialization metadata caches,
        /// so using fresh default instances every time one is needed can result in redundant recomputation of converters.
        /// This property provides a shared instance that can be consumed by any number of components without necessitating any converter recomputation.
        /// </remarks>
        private static RdnSerializerOptions? s_webOptions;
        public static RdnSerializerOptions Web
        {
            [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
            get => s_webOptions ?? GetOrCreateSingleton(ref s_webOptions, RdnSerializerDefaults.Web);
        }

        /// <summary>
        /// Gets a read-only, singleton instance of <see cref="RdnSerializerOptions" /> that uses the strict configuration.
        /// </summary>
        /// <remarks>
        /// Each <see cref="RdnSerializerOptions" /> instance encapsulates its own serialization metadata caches,
        /// so using fresh default instances every time one is needed can result in redundant recomputation of converters.
        /// This property provides a shared instance that can be consumed by any number of components without necessitating any converter recomputation.
        /// </remarks>
        private static RdnSerializerOptions? s_strictOptions;
        public static RdnSerializerOptions Strict
        {
            [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
            [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
            get => s_strictOptions ?? GetOrCreateSingleton(ref s_strictOptions, RdnSerializerDefaults.Strict);
        }

        // For any new option added, consider adding it to the options copied in the copy constructor below
        // and consider updating the EqualtyComparer used for comparing CachingContexts.
        private IRdnTypeInfoResolver? _typeInfoResolver;
        private RdnNamingPolicy? _dictionaryKeyPolicy;
        private RdnNamingPolicy? _rdnPropertyNamingPolicy;
        private RdnCommentHandling _readCommentHandling;
        private ReferenceHandler? _referenceHandler;
        private JavaScriptEncoder? _encoder;
        private ConverterList? _converters;
        private RdnIgnoreCondition _defaultIgnoreCondition;
        private RdnNumberHandling _numberHandling;
        private RdnObjectCreationHandling _preferredObjectCreationHandling;
        private RdnUnknownTypeHandling _unknownTypeHandling;
        private RdnUnmappedMemberHandling _unmappedMemberHandling;

        private int _defaultBufferSize = BufferSizeDefault;
        private int _maxDepth;
        private bool _allowOutOfOrderMetadataProperties;
        private bool _allowTrailingCommas;
        private bool _respectNullableAnnotations = AppContextSwitchHelper.RespectNullableAnnotationsDefault;
        private bool _respectRequiredConstructorParameters = AppContextSwitchHelper.RespectRequiredConstructorParametersDefault;
        private bool _ignoreNullValues;
        private bool _ignoreReadOnlyProperties;
        private bool _ignoreReadonlyFields;
        private bool _includeFields;
        private string? _newLine;
        private bool _propertyNameCaseInsensitive;
        private bool _writeIndented;
        private char _indentCharacter = RdnConstants.DefaultIndentCharacter;
        private int _indentSize = RdnConstants.DefaultIndentSize;
        private bool _allowDuplicateProperties = true;

        /// <summary>
        /// Constructs a new <see cref="RdnSerializerOptions"/> instance.
        /// </summary>
        public RdnSerializerOptions()
        {
            TrackOptionsInstance(this);
        }

        /// <summary>
        /// Copies the options from a <see cref="RdnSerializerOptions"/> instance to a new instance.
        /// </summary>
        /// <param name="options">The <see cref="RdnSerializerOptions"/> instance to copy options from.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        public RdnSerializerOptions(RdnSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // The following fields are not copied intentionally:
            // 1. _cachingContext can only be set in immutable options instances.
            // 2. _typeInfoResolverChain can be created lazily as it relies on
            //    _typeInfoResolver as its source of truth.

            _dictionaryKeyPolicy = options._dictionaryKeyPolicy;
            _rdnPropertyNamingPolicy = options._rdnPropertyNamingPolicy;
            _readCommentHandling = options._readCommentHandling;
            _referenceHandler = options._referenceHandler;
            _converters = options._converters is { } converters ? new(this, converters) : null;
            _encoder = options._encoder;
            _defaultIgnoreCondition = options._defaultIgnoreCondition;
            _numberHandling = options._numberHandling;
            _preferredObjectCreationHandling = options._preferredObjectCreationHandling;
            _unknownTypeHandling = options._unknownTypeHandling;
            _unmappedMemberHandling = options._unmappedMemberHandling;

            _defaultBufferSize = options._defaultBufferSize;
            _maxDepth = options._maxDepth;
            _allowOutOfOrderMetadataProperties = options._allowOutOfOrderMetadataProperties;
            _allowTrailingCommas = options._allowTrailingCommas;
            _respectNullableAnnotations = options._respectNullableAnnotations;
            _respectRequiredConstructorParameters = options._respectRequiredConstructorParameters;
            _ignoreNullValues = options._ignoreNullValues;
            _ignoreReadOnlyProperties = options._ignoreReadOnlyProperties;
            _ignoreReadonlyFields = options._ignoreReadonlyFields;
            _includeFields = options._includeFields;
            _newLine = options._newLine;
            _propertyNameCaseInsensitive = options._propertyNameCaseInsensitive;
            _writeIndented = options._writeIndented;
            _indentCharacter = options._indentCharacter;
            _indentSize = options._indentSize;
            _allowDuplicateProperties = options._allowDuplicateProperties;
            _typeInfoResolver = options._typeInfoResolver;
            EffectiveMaxDepth = options.EffectiveMaxDepth;
            ReferenceHandlingStrategy = options.ReferenceHandlingStrategy;

            TrackOptionsInstance(this);
        }

        /// <summary>
        /// Constructs a new <see cref="RdnSerializerOptions"/> instance with a predefined set of options determined by the specified <see cref="RdnSerializerDefaults"/>.
        /// </summary>
        /// <param name="defaults"> The <see cref="RdnSerializerDefaults"/> to reason about.</param>
        public RdnSerializerOptions(RdnSerializerDefaults defaults) : this()
        {
            // Should be kept in sync with equivalent overload in RdnSourceGenerationOptionsAttribute

            if (defaults == RdnSerializerDefaults.Web)
            {
                _propertyNameCaseInsensitive = true;
                _rdnPropertyNamingPolicy = RdnNamingPolicy.CamelCase;
                _numberHandling = RdnNumberHandling.AllowReadingFromString;
            }
            else if (defaults == RdnSerializerDefaults.Strict)
            {
                _unmappedMemberHandling = RdnUnmappedMemberHandling.Disallow;
                _allowDuplicateProperties = false;
                _respectNullableAnnotations = true;
                _respectRequiredConstructorParameters = true;
            }
            else if (defaults != RdnSerializerDefaults.General)
            {
                throw new ArgumentOutOfRangeException(nameof(defaults));
            }
        }

        /// <summary>Tracks the options instance to enable all instances to be enumerated.</summary>
        private static void TrackOptionsInstance(RdnSerializerOptions options) => TrackedOptionsInstances.All.Add(options, null);

        internal static class TrackedOptionsInstances
        {
            /// <summary>Tracks all live RdnSerializerOptions instances.</summary>
            /// <remarks>Instances are added to the table in their constructor.</remarks>
            public static ConditionalWeakTable<RdnSerializerOptions, object?> All { get; } =
                // TODO https://github.com/dotnet/runtime/issues/51159:
                // Look into linking this away / disabling it when hot reload isn't in use.
                new ConditionalWeakTable<RdnSerializerOptions, object?>();
        }

        /// <summary>
        /// Binds current <see cref="RdnSerializerOptions"/> instance with a new instance of the specified <see cref="Serialization.RdnSerializerContext"/> type.
        /// </summary>
        /// <typeparam name="TContext">The generic definition of the specified context type.</typeparam>
        /// <remarks>
        /// When serializing and deserializing types using the options
        /// instance, metadata for the types will be fetched from the context instance.
        /// </remarks>
        [Obsolete(Obsoletions.RdnSerializerOptionsAddContextMessage, DiagnosticId = Obsoletions.RdnSerializerOptionsAddContextDiagId, UrlFormat = Obsoletions.SharedUrlFormat)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void AddContext<TContext>() where TContext : RdnSerializerContext, new()
        {
            VerifyMutable();
            TContext context = new();
            context.AssociateWithOptions(this);
        }

        /// <summary>
        /// Gets or sets the <see cref="RdnTypeInfo"/> contract resolver used by this instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// A <see langword="null"/> setting is equivalent to using the reflection-based <see cref="DefaultRdnTypeInfoResolver" />.
        /// The property will be populated automatically once used with one of the <see cref="RdnSerializer"/> methods.
        ///
        /// This property is kept in sync with the <see cref="TypeInfoResolverChain"/> property.
        /// Any change made to this property will be reflected by <see cref="TypeInfoResolverChain"/> and vice versa.
        /// </remarks>
        public IRdnTypeInfoResolver? TypeInfoResolver
        {
            get
            {
                return _typeInfoResolver;
            }
            set
            {
                VerifyMutable();

                if (_typeInfoResolverChain is { } resolverChain && !ReferenceEquals(resolverChain, value))
                {
                    // User is setting a new resolver; detach the resolver chain if already created.
                    resolverChain.DetachFromOptions();
                    _typeInfoResolverChain = null;
                }

                _typeInfoResolver = value;
            }
        }

        /// <summary>
        /// Gets the list of chained <see cref="RdnTypeInfo"/> contract resolvers used by this instance.
        /// </summary>
        /// <remarks>
        /// The ordering of the chain is significant: <see cref="RdnSerializerOptions "/> will query each
        /// of the resolvers in their specified order, returning the first result that is non-null.
        /// If all resolvers in the chain return null, then <see cref="RdnSerializerOptions"/> will also return null.
        ///
        /// This property is auxiliary to and is kept in sync with the <see cref="TypeInfoResolver"/> property.
        /// Any change made to this property will be reflected by <see cref="TypeInfoResolver"/> and vice versa.
        /// </remarks>
        public IList<IRdnTypeInfoResolver> TypeInfoResolverChain => _typeInfoResolverChain ??= new(this);
        private OptionsBoundRdnTypeInfoResolverChain? _typeInfoResolverChain;

        /// <summary>
        /// Allows RDN metadata properties to be specified after regular properties in a deserialized RDN object.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// When set to <see langword="true" />, removes the requirement that RDN metadata properties
        /// such as $id and $type should be specified at the very start of the deserialized RDN object.
        ///
        /// It should be noted that enabling this setting can result in over-buffering
        /// when deserializing large RDN payloads in the context of streaming deserialization.
        /// </remarks>
        public bool AllowOutOfOrderMetadataProperties
        {
            get
            {
                return _allowOutOfOrderMetadataProperties;
            }
            set
            {
                VerifyMutable();
                _allowOutOfOrderMetadataProperties = value;
            }
        }

        /// <summary>
        /// Defines whether an extra comma at the end of a list of RDN values in an object or array
        /// is allowed (and ignored) within the RDN payload being deserialized.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// By default, it's set to false, and <exception cref="RdnException"/> is thrown if a trailing comma is encountered.
        /// </remarks>
        public bool AllowTrailingCommas
        {
            get
            {
                return _allowTrailingCommas;
            }
            set
            {
                VerifyMutable();
                _allowTrailingCommas = value;
            }
        }

        /// <summary>
        /// The default buffer size in bytes used when creating temporary buffers.
        /// </summary>
        /// <remarks>The default size is 16K.</remarks>
        /// <exception cref="System.ArgumentException">Thrown when the buffer size is less than 1.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public int DefaultBufferSize
        {
            get
            {
                return _defaultBufferSize;
            }
            set
            {
                VerifyMutable();

                if (value < 1)
                {
                    throw new ArgumentException(SR.SerializationInvalidBufferSize);
                }

                _defaultBufferSize = value;
            }
        }

        /// <summary>
        /// The encoder to use when escaping strings, or <see langword="null" /> to use the default encoder.
        /// </summary>
        public JavaScriptEncoder? Encoder
        {
            get
            {
                return _encoder;
            }
            set
            {
                VerifyMutable();

                _encoder = value;
            }
        }

        /// <summary>
        /// Specifies the policy used to convert a <see cref="System.Collections.IDictionary"/> key's name to another format, such as camel-casing.
        /// </summary>
        /// <remarks>
        /// This property can be set to <see cref="RdnNamingPolicy.CamelCase"/> to specify a camel-casing policy.
        /// It is not used when deserializing.
        /// </remarks>
        public RdnNamingPolicy? DictionaryKeyPolicy
        {
            get
            {
                return _dictionaryKeyPolicy;
            }
            set
            {
                VerifyMutable();
                _dictionaryKeyPolicy = value;
            }
        }

        /// <summary>
        /// Determines whether null values are ignored during serialization and deserialization.
        /// The default value is false.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// or <see cref="DefaultIgnoreCondition"/> has been set to a non-default value. These properties cannot be used together.
        /// </exception>
        [Obsolete(Obsoletions.RdnSerializerOptionsIgnoreNullValuesMessage, DiagnosticId = Obsoletions.RdnSerializerOptionsIgnoreNullValuesDiagId, UrlFormat = Obsoletions.SharedUrlFormat)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IgnoreNullValues
        {
            get
            {
                return _ignoreNullValues;
            }
            set
            {
                VerifyMutable();

                if (value && _defaultIgnoreCondition != RdnIgnoreCondition.Never)
                {
                    throw new InvalidOperationException(SR.DefaultIgnoreConditionAlreadySpecified);
                }

                _ignoreNullValues = value;
            }
        }

        /// <summary>
        /// Specifies a condition to determine when properties with default values are ignored during serialization or deserialization.
        /// The default value is <see cref="RdnIgnoreCondition.Never" />.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if this property is set to <see cref="RdnIgnoreCondition.Always"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred,
        /// or <see cref="IgnoreNullValues"/> has been set to <see langword="true"/>. These properties cannot be used together.
        /// </exception>
        public RdnIgnoreCondition DefaultIgnoreCondition
        {
            get
            {
                return _defaultIgnoreCondition;
            }
            set
            {
                VerifyMutable();

                if (value == RdnIgnoreCondition.Always)
                {
                    throw new ArgumentException(SR.DefaultIgnoreConditionInvalid);
                }

                if (value != RdnIgnoreCondition.Never && _ignoreNullValues)
                {
                    throw new InvalidOperationException(SR.DefaultIgnoreConditionAlreadySpecified);
                }

                _defaultIgnoreCondition = value;
            }
        }

        /// <summary>
        /// Specifies how number types should be handled when serializing or deserializing.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public RdnNumberHandling NumberHandling
        {
            get => _numberHandling;
            set
            {
                VerifyMutable();

                if (!RdnSerializer.IsValidNumberHandlingValue(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _numberHandling = value;
            }
        }

        /// <summary>
        /// Specifies preferred object creation handling for properties when deserializing RDN.
        /// When set to <see cref="RdnObjectCreationHandling.Populate"/> all properties which
        /// are capable of reusing the existing instance will be populated.
        /// </summary>
        /// <remarks>
        /// Only property type is taken into consideration. For example if property is of type
        /// <see cref="IEnumerable{T}"/> but it is assigned <see cref="List{T}"/> it will not be populated
        /// because <see cref="IEnumerable{T}"/> is not capable of populating.
        /// Additionally value types require a setter to be populated.
        /// </remarks>
        public RdnObjectCreationHandling PreferredObjectCreationHandling
        {
            get => _preferredObjectCreationHandling;
            set
            {
                VerifyMutable();

                if (!RdnSerializer.IsValidCreationHandlingValue(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _preferredObjectCreationHandling = value;
            }
        }

        /// <summary>
        /// Determines whether read-only properties are ignored during serialization.
        /// A property is read-only if it contains a public getter but not a public setter.
        /// The default value is false.
        /// </summary>
        /// <remarks>
        /// Read-only properties are not deserialized regardless of this setting.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IgnoreReadOnlyProperties
        {
            get
            {
                return _ignoreReadOnlyProperties;
            }
            set
            {
                VerifyMutable();
                _ignoreReadOnlyProperties = value;
            }
        }

        /// <summary>
        /// Determines whether read-only fields are ignored during serialization.
        /// A field is read-only if it is marked with the <c>readonly</c> keyword.
        /// The default value is false.
        /// </summary>
        /// <remarks>
        /// Read-only fields are not deserialized regardless of this setting.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IgnoreReadOnlyFields
        {
            get
            {
                return _ignoreReadonlyFields;
            }
            set
            {
                VerifyMutable();
                _ignoreReadonlyFields = value;
            }
        }

        /// <summary>
        /// Determines whether fields are handled on serialization and deserialization.
        /// The default value is false.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool IncludeFields
        {
            get
            {
                return _includeFields;
            }
            set
            {
                VerifyMutable();
                _includeFields = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum depth allowed when serializing or deserializing RDN, with the default (i.e. 0) indicating a max depth of 64.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the max depth is set to a negative value.
        /// </exception>
        /// <remarks>
        /// Going past this depth will throw a <exception cref="RdnException"/>.
        /// </remarks>
        public int MaxDepth
        {
            get => _maxDepth;
            set
            {
                VerifyMutable();

                if (value < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(nameof(value));
                }

                _maxDepth = value;
                EffectiveMaxDepth = (value == 0 ? DefaultMaxDepth : value);
            }
        }

        internal int EffectiveMaxDepth { get; private set; } = DefaultMaxDepth;

        /// <summary>
        /// Specifies the policy used to convert a property's name on an object to another format, such as camel-casing.
        /// The resulting property name is expected to match the RDN payload during deserialization, and
        /// will be used when writing the property name during serialization.
        /// </summary>
        /// <remarks>
        /// The policy is not used for properties that have a <see cref="RdnPropertyNameAttribute"/> applied.
        /// This property can be set to <see cref="RdnNamingPolicy.CamelCase"/> to specify a camel-casing policy.
        /// </remarks>
        public RdnNamingPolicy? PropertyNamingPolicy
        {
            get
            {
                return _rdnPropertyNamingPolicy;
            }
            set
            {
                VerifyMutable();
                _rdnPropertyNamingPolicy = value;
            }
        }

        /// <summary>
        /// Determines whether a property's name uses a case-insensitive comparison during deserialization.
        /// The default value is false.
        /// </summary>
        /// <remarks>There is a performance cost associated when the value is true.</remarks>
        public bool PropertyNameCaseInsensitive
        {
            get
            {
                return _propertyNameCaseInsensitive;
            }
            set
            {
                VerifyMutable();
                _propertyNameCaseInsensitive = value;
            }
        }

        /// <summary>
        /// Defines how the comments are handled during deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the comment handling enum is set to a value that is not supported (or not within the <see cref="RdnCommentHandling"/> enum range).
        /// </exception>
        /// <remarks>
        /// By default <exception cref="RdnException"/> is thrown if a comment is encountered.
        /// </remarks>
        public RdnCommentHandling ReadCommentHandling
        {
            get
            {
                return _readCommentHandling;
            }
            set
            {
                VerifyMutable();

                Debug.Assert(value >= 0);
                if (value > RdnCommentHandling.Skip)
                    throw new ArgumentOutOfRangeException(nameof(value), SR.RdnSerializerDoesNotSupportComments);

                _readCommentHandling = value;
            }
        }

        /// <summary>
        /// Defines how deserializing a type declared as an <see cref="object"/> is handled during deserialization.
        /// </summary>
        public RdnUnknownTypeHandling UnknownTypeHandling
        {
            get => _unknownTypeHandling;
            set
            {
                VerifyMutable();
                _unknownTypeHandling = value;
            }
        }

        /// <summary>
        /// Determines how <see cref="RdnSerializer"/> handles RDN properties that
        /// cannot be mapped to a specific .NET member when deserializing object types.
        /// </summary>
        public RdnUnmappedMemberHandling UnmappedMemberHandling
        {
            get => _unmappedMemberHandling;
            set
            {
                VerifyMutable();
                _unmappedMemberHandling = value;
            }
        }

        /// <summary>
        /// Defines whether RDN should pretty print which includes:
        /// indenting nested RDN tokens, adding new lines, and adding white space between property names and values.
        /// By default, the RDN is serialized without any extra white space.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public bool WriteIndented
        {
            get
            {
                return _writeIndented;
            }
            set
            {
                VerifyMutable();
                _writeIndented = value;
            }
        }

        /// <summary>
        /// Defines the indentation character being used when <see cref="WriteIndented" /> is enabled. Defaults to the space character.
        /// </summary>
        /// <remarks>Allowed characters are space and horizontal tab.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains an invalid character.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public char IndentCharacter
        {
            get
            {
                return _indentCharacter;
            }
            set
            {
                RdnWriterHelper.ValidateIndentCharacter(value);
                VerifyMutable();
                _indentCharacter = value;
            }
        }

        /// <summary>
        /// Defines the indentation size being used when <see cref="WriteIndented" /> is enabled. Defaults to two.
        /// </summary>
        /// <remarks>Allowed values are all integers between 0 and 127, included.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is out of the allowed range.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public int IndentSize
        {
            get
            {
                return _indentSize;
            }
            set
            {
                RdnWriterHelper.ValidateIndentSize(value);
                VerifyMutable();
                _indentSize = value;
            }
        }

        /// <summary>
        /// Configures how object references are handled when reading and writing RDN.
        /// </summary>
        public ReferenceHandler? ReferenceHandler
        {
            get => _referenceHandler;
            set
            {
                VerifyMutable();
                _referenceHandler = value;
                ReferenceHandlingStrategy = value?.HandlingStrategy ?? RdnKnownReferenceHandler.Unspecified;
            }
        }

        /// <summary>
        /// Gets or sets the new line string to use when <see cref="WriteIndented"/> is <see langword="true"/>.
        /// The default is the value of <see cref="Environment.NewLine"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the new line string is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the new line string is not <c>\n</c> or <c>\r\n</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        public string NewLine
        {
            get
            {
                return _newLine ??= Environment.NewLine;
            }
            set
            {
                RdnWriterHelper.ValidateNewLine(value);
                VerifyMutable();
                _newLine = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether nullability annotations should be respected during serialization and deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// Nullability annotations are resolved from the properties, fields and constructor parameters
        /// that are used by the serializer. This includes annotations stemming from attributes such as
        /// <see cref="NotNullAttribute"/>, <see cref="MaybeNullAttribute"/>,
        /// <see cref="AllowNullAttribute"/> and <see cref="DisallowNullAttribute"/>.
        ///
        /// Due to restrictions in how nullable reference types are represented at run time,
        /// this setting only governs nullability annotations of non-generic properties and fields.
        /// It cannot be used to enforce nullability annotations of root-level types or generic parameters.
        ///
        /// The default setting for this property can be toggled application-wide using the
        /// "System.Text.Rdn.Serialization.RespectNullableAnnotationsDefault" feature switch.
        /// </remarks>
        public bool RespectNullableAnnotations
        {
            get => _respectNullableAnnotations;
            set
            {
                VerifyMutable();
                _respectNullableAnnotations = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether non-optional constructor parameters should be specified during deserialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// For historical reasons constructor-based deserialization treats all constructor parameters as optional by default.
        /// This flag allows users to toggle that behavior as necessary for each <see cref="RdnSerializerOptions"/> instance.
        ///
        /// The default setting for this property can be toggled application-wide using the
        /// "System.Text.Rdn.Serialization.RespectRequiredConstructorParametersDefault" feature switch.
        /// </remarks>
        public bool RespectRequiredConstructorParameters
        {
            get => _respectRequiredConstructorParameters;
            set
            {
                VerifyMutable();
                _respectRequiredConstructorParameters = value;
            }
        }

        /// <summary>
        /// Defines whether duplicate property names are allowed when deserializing RDN objects.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this property is set after serialization or deserialization has occurred.
        /// </exception>
        /// <remarks>
        /// <para>
        /// By default, it's set to true. If set to false, <see cref="RdnException"/> is thrown
        /// when a duplicate property name is encountered during deserialization.
        /// </para>
        /// <para>
        /// Duplicate property names are not allowed in serialization.
        /// </para>
        /// </remarks>
        public bool AllowDuplicateProperties
        {
            get => _allowDuplicateProperties;
            set
            {
                VerifyMutable();
                _allowDuplicateProperties = value;
            }
        }

        /// <summary>
        /// Returns true if options uses compatible built-in resolvers or a combination of compatible built-in resolvers.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool CanUseFastPathSerializationLogic
        {
            get
            {
                Debug.Assert(IsReadOnly);
                Debug.Assert(TypeInfoResolver != null);
                return _canUseFastPathSerializationLogic ??= TypeInfoResolver.IsCompatibleWithOptions(this);
            }
        }

        private bool? _canUseFastPathSerializationLogic;

        // The cached value used to determine if ReferenceHandler should use Preserve or IgnoreCycles semantics or None of them.
        internal RdnKnownReferenceHandler ReferenceHandlingStrategy = RdnKnownReferenceHandler.Unspecified;

        /// <summary>
        /// Specifies whether the current instance has been locked for user modification.
        /// </summary>
        /// <remarks>
        /// A <see cref="RdnSerializerOptions"/> instance can be locked either if
        /// it has been passed to one of the <see cref="RdnSerializer"/> methods,
        /// has been associated with a <see cref="RdnSerializerContext"/> instance,
        /// or a user explicitly called the <see cref="MakeReadOnly()"/> methods on the instance.
        ///
        /// Read-only instances use caching when querying <see cref="RdnConverter"/> and <see cref="RdnTypeInfo"/> metadata.
        /// </remarks>
        public bool IsReadOnly => _isReadOnly;
        private volatile bool _isReadOnly;

        /// <summary>
        /// Marks the current instance as read-only preventing any further user modification.
        /// </summary>
        /// <exception cref="InvalidOperationException">The instance does not specify a <see cref="TypeInfoResolver"/> setting.</exception>
        /// <remarks>This method is idempotent.</remarks>
        public void MakeReadOnly()
        {
            if (_typeInfoResolver is null)
            {
                ThrowHelper.ThrowInvalidOperationException_RdnSerializerOptionsNoTypeInfoResolverSpecified();
            }

            _isReadOnly = true;
        }

        /// <summary>
        /// Marks the current instance as read-only preventing any further user modification.
        /// </summary>
        /// <param name="populateMissingResolver">Populates unconfigured <see cref="TypeInfoResolver"/> properties with the reflection-based default.</param>
        /// <exception cref="InvalidOperationException">
        /// The instance does not specify a <see cref="TypeInfoResolver"/> setting. Thrown if <paramref name="populateMissingResolver"/> is <see langword="false"/>.
        /// -OR-
        /// The <see cref="RdnSerializer.IsReflectionEnabledByDefault"/> feature switch has been turned off.
        /// </exception>
        /// <remarks>
        /// When <paramref name="populateMissingResolver"/> is set to <see langword="true" />, configures the instance following
        /// the semantics of the <see cref="RdnSerializer"/> methods accepting <see cref="RdnSerializerOptions"/> parameters.
        ///
        /// This method is idempotent.
        /// </remarks>
        [RequiresUnreferencedCode("Populating unconfigured TypeInfoResolver properties with the reflection resolver requires unreferenced code.")]
        [RequiresDynamicCode("Populating unconfigured TypeInfoResolver properties with the reflection resolver requires runtime code generation.")]
        public void MakeReadOnly(bool populateMissingResolver)
        {
            if (populateMissingResolver)
            {
                if (!_isConfiguredForRdnSerializer)
                {
                    ConfigureForRdnSerializer();
                }
            }
            else
            {
                MakeReadOnly();
            }

            Debug.Assert(IsReadOnly);
        }

        /// <summary>
        /// Configures the instance for use by the RdnSerializer APIs, applying reflection-based fallback where applicable.
        /// </summary>
        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private void ConfigureForRdnSerializer()
        {
            if (RdnSerializer.IsReflectionEnabledByDefault)
            {
                // Even if a resolver has already been specified, we need to root
                // the default resolver to gain access to the default converters.
                DefaultRdnTypeInfoResolver defaultResolver = DefaultRdnTypeInfoResolver.DefaultInstance;

                switch (_typeInfoResolver)
                {
                    case null:
                        // Use the default reflection-based resolver if no resolver has been specified.
                        _typeInfoResolver = defaultResolver;
                        break;

                    case RdnSerializerContext ctx when AppContextSwitchHelper.IsSourceGenReflectionFallbackEnabled:
                        // .NET 6 compatibility mode: enable fallback to reflection metadata for RdnSerializerContext
                        _effectiveRdnTypeInfoResolver = RdnTypeInfoResolver.Combine(ctx, defaultResolver);

                        if (_cachingContext is { } cachingContext)
                        {
                            // A cache has already been created by the source generator.
                            // Repeat the same configuration routine for that options instance, if different.
                            // Invalidate any cache entries that have already been stored.
                            if (cachingContext.Options != this && !cachingContext.Options._isConfiguredForRdnSerializer)
                            {
                                cachingContext.Options.ConfigureForRdnSerializer();
                            }
                            else
                            {
                                cachingContext.Clear();
                            }
                        }
                        break;
                }
            }
            else if (_typeInfoResolver is null or EmptyRdnTypeInfoResolver)
            {
                ThrowHelper.ThrowInvalidOperationException_RdnSerializerIsReflectionDisabled();
            }

            Debug.Assert(_typeInfoResolver != null);
            // NB preserve write order.
            _isReadOnly = true;
            _isConfiguredForRdnSerializer = true;
        }

        /// <summary>
        /// This flag is supplementary to <see cref="_isReadOnly"/> and is only used to keep track
        /// of source-gen reflection fallback (assuming the IsSourceGenReflectionFallbackEnabled feature switch is on).
        /// This mode necessitates running the <see cref="ConfigureForRdnSerializer"/> method even
        /// for options instances that have been marked as read-only.
        /// </summary>
        private volatile bool _isConfiguredForRdnSerializer;

        // Only populated in .NET 6 compatibility mode encoding reflection fallback in source gen
        private IRdnTypeInfoResolver? _effectiveRdnTypeInfoResolver;

        private RdnTypeInfo? GetTypeInfoNoCaching(Type type)
        {
            IRdnTypeInfoResolver? resolver = _effectiveRdnTypeInfoResolver ?? _typeInfoResolver;
            if (resolver is null)
            {
                return null;
            }

            RdnTypeInfo? info = resolver.GetTypeInfo(type, this);

            if (info != null)
            {
                if (info.Type != type)
                {
                    ThrowHelper.ThrowInvalidOperationException_ResolverTypeNotCompatible(type, info.Type);
                }

                if (info.Options != this)
                {
                    ThrowHelper.ThrowInvalidOperationException_ResolverTypeInfoOptionsNotCompatible();
                }
            }
            else
            {
                Debug.Assert(_effectiveRdnTypeInfoResolver is null, "an effective resolver always returns metadata");

                if (type == RdnTypeInfo.ObjectType)
                {
                    // If the resolver does not provide a RdnTypeInfo<object> instance, fill
                    // with the serialization-only converter to enable polymorphic serialization.
                    var converter = new SlimObjectConverter(resolver);
                    info = new RdnTypeInfo<object>(converter, this);
                }
            }

            return info;
        }

        internal RdnDocumentOptions GetDocumentOptions()
        {
            return new RdnDocumentOptions
            {
                AllowDuplicateProperties = AllowDuplicateProperties,
                AllowTrailingCommas = AllowTrailingCommas,
                CommentHandling = ReadCommentHandling,
                MaxDepth = MaxDepth,
            };
        }

        internal RdnNodeOptions GetNodeOptions()
        {
            return new RdnNodeOptions
            {
                PropertyNameCaseInsensitive = PropertyNameCaseInsensitive
            };
        }

        internal RdnReaderOptions GetReaderOptions()
        {
            return new RdnReaderOptions
            {
                AllowTrailingCommas = AllowTrailingCommas,
                CommentHandling = ReadCommentHandling,
                MaxDepth = EffectiveMaxDepth
            };
        }

        internal RdnWriterOptions GetWriterOptions()
        {
            return new RdnWriterOptions
            {
                Encoder = Encoder,
                Indented = WriteIndented,
                IndentCharacter = IndentCharacter,
                IndentSize = IndentSize,
                MaxDepth = EffectiveMaxDepth,
                NewLine = NewLine,
#if !DEBUG
                SkipValidation = true
#endif
            };
        }

        internal void VerifyMutable()
        {
            if (_isReadOnly)
            {
                ThrowHelper.ThrowInvalidOperationException_SerializerOptionsReadOnly(_typeInfoResolver as RdnSerializerContext);
            }
        }

        private sealed class ConverterList : ConfigurationList<RdnConverter>
        {
            private readonly RdnSerializerOptions _options;

            public ConverterList(RdnSerializerOptions options, IList<RdnConverter>? source = null)
                : base(source)
            {
                _options = options;
            }

            public override bool IsReadOnly => _options.IsReadOnly;
            protected override void OnCollectionModifying() => _options.VerifyMutable();
        }

        private sealed class OptionsBoundRdnTypeInfoResolverChain : RdnTypeInfoResolverChain
        {
            private RdnSerializerOptions? _options;

            public OptionsBoundRdnTypeInfoResolverChain(RdnSerializerOptions options)
            {
                _options = options;
                AddFlattened(options._typeInfoResolver);
            }

            public void DetachFromOptions()
            {
                _options = null;
            }

            public override bool IsReadOnly => _options?.IsReadOnly is true;

            protected override void ValidateAddedValue(IRdnTypeInfoResolver item)
            {
                Debug.Assert(item is not null);

                if (ReferenceEquals(item, this) || ReferenceEquals(item, _options?._typeInfoResolver))
                {
                    // Cannot add the instances in TypeInfoResolver or TypeInfoResolverChain to the chain itself.
                    ThrowHelper.ThrowInvalidOperationException_InvalidChainedResolver();
                }
            }

            protected override void OnCollectionModifying()
            {
                _options?.VerifyMutable();
            }

            protected override void OnCollectionModified()
            {
                // Collection modified by the user: replace the main
                // resolver with the resolver chain as our source of truth.
                if (_options != null) _options._typeInfoResolver = this;
            }
        }

        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        private static RdnSerializerOptions GetOrCreateSingleton(
            ref RdnSerializerOptions? location,
            RdnSerializerDefaults defaults)
        {
            var options = new RdnSerializerOptions(defaults)
            {
                // Because we're marking the default instance as read-only,
                // we need to specify a resolver instance for the case where
                // reflection is disabled by default: use one that returns null for all types.

                TypeInfoResolver = RdnSerializer.IsReflectionEnabledByDefault
                    ? DefaultRdnTypeInfoResolver.DefaultInstance
                    : RdnTypeInfoResolver.Empty,

                _isReadOnly = true,
            };

            return Interlocked.CompareExchange(ref location, options, null) ?? options;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"TypeInfoResolver = {(TypeInfoResolver?.ToString() ?? "<null>")}, IsReadOnly = {IsReadOnly}";
    }
}
