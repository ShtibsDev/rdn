// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rdn.Serialization.Metadata
{
    /// <summary>
    /// Validates and indexes polymorphic type configuration,
    /// providing derived RdnTypeInfo resolution methods
    /// in both serialization and deserialization scenaria.
    /// </summary>
    internal sealed class PolymorphicTypeResolver
    {
        private readonly ConcurrentDictionary<Type, DerivedRdnTypeInfo?> _typeToDiscriminatorId = new();
        private readonly Dictionary<object, DerivedRdnTypeInfo>? _discriminatorIdtoType;
        private readonly RdnSerializerOptions _options;

        public PolymorphicTypeResolver(RdnSerializerOptions options, RdnPolymorphismOptions polymorphismOptions, Type baseType, bool converterCanHaveMetadata)
        {
            UnknownDerivedTypeHandling = polymorphismOptions.UnknownDerivedTypeHandling;
            IgnoreUnrecognizedTypeDiscriminators = polymorphismOptions.IgnoreUnrecognizedTypeDiscriminators;
            BaseType = baseType;
            _options = options;

            if (!IsSupportedPolymorphicBaseType(BaseType))
            {
                ThrowHelper.ThrowInvalidOperationException_TypeDoesNotSupportPolymorphism(BaseType);
            }

            bool containsDerivedTypes = false;
            foreach ((Type derivedType, object? typeDiscriminator) in polymorphismOptions.DerivedTypes)
            {
                Debug.Assert(typeDiscriminator is null or int or string);

                if (!IsSupportedDerivedType(BaseType, derivedType) ||
                    (derivedType.IsAbstract && UnknownDerivedTypeHandling != RdnUnknownDerivedTypeHandling.FallBackToNearestAncestor))
                {
                    ThrowHelper.ThrowInvalidOperationException_DerivedTypeNotSupported(BaseType, derivedType);
                }

                RdnTypeInfo derivedTypeInfo = options.GetTypeInfoInternal(derivedType);
                DerivedRdnTypeInfo derivedTypeInfoHolder = new(typeDiscriminator, derivedTypeInfo);

                if (!_typeToDiscriminatorId.TryAdd(derivedType, derivedTypeInfoHolder))
                {
                    ThrowHelper.ThrowInvalidOperationException_DerivedTypeIsAlreadySpecified(BaseType, derivedType);
                }

                if (typeDiscriminator is not null)
                {
                    if (!(_discriminatorIdtoType ??= new()).TryAdd(typeDiscriminator, derivedTypeInfoHolder))
                    {
                        ThrowHelper.ThrowInvalidOperationException_TypeDicriminatorIdIsAlreadySpecified(BaseType, typeDiscriminator);
                    }

                    UsesTypeDiscriminators = true;
                }

                containsDerivedTypes = true;
            }

            if (!containsDerivedTypes)
            {
                ThrowHelper.ThrowInvalidOperationException_PolymorphicTypeConfigurationDoesNotSpecifyDerivedTypes(BaseType);
            }

            if (UsesTypeDiscriminators)
            {
                Debug.Assert(_discriminatorIdtoType != null, "Discriminator index must have been populated.");

                if (!converterCanHaveMetadata)
                {
                    ThrowHelper.ThrowNotSupportedException_BaseConverterDoesNotSupportMetadata(BaseType);
                }

                string propertyName = polymorphismOptions.TypeDiscriminatorPropertyName;
                if (!propertyName.Equals(RdnSerializer.TypePropertyName, StringComparison.Ordinal))
                {
                    byte[] utf8EncodedName = Encoding.UTF8.GetBytes(propertyName);

                    // Check if the property name conflicts with other metadata property names
                    if ((RdnSerializer.GetMetadataPropertyName(utf8EncodedName, resolver: null) & ~MetadataPropertyName.Type) != 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException_InvalidCustomTypeDiscriminatorPropertyName();
                    }

                    CustomTypeDiscriminatorPropertyNameUtf8 = utf8EncodedName;
                    CustomTypeDiscriminatorPropertyNameRdnEncoded = RdnEncodedText.Encode(propertyName, options.Encoder);
                }

                // Check if the discriminator property name conflicts with any derived property names.
                foreach (DerivedRdnTypeInfo derivedTypeInfo in _discriminatorIdtoType.Values)
                {
                    if (derivedTypeInfo.RdnTypeInfo.Kind is RdnTypeInfoKind.Object)
                    {
                        foreach (RdnPropertyInfo property in derivedTypeInfo.RdnTypeInfo.Properties)
                        {
                            if (property is { IsIgnored: false, IsExtensionData: false } && property.Name == propertyName)
                            {
                                ThrowHelper.ThrowInvalidOperationException_PropertyConflictsWithMetadataPropertyName(derivedTypeInfo.RdnTypeInfo.Type, propertyName);
                            }
                        }
                    }
                }
            }
        }

        public Type BaseType { get; }
        public RdnUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; }
        public bool UsesTypeDiscriminators { get; }
        public bool IgnoreUnrecognizedTypeDiscriminators { get; }
        public byte[]? CustomTypeDiscriminatorPropertyNameUtf8 { get; }
        public RdnEncodedText? CustomTypeDiscriminatorPropertyNameRdnEncoded { get; }

        public bool TryGetDerivedRdnTypeInfo(Type runtimeType, [NotNullWhen(true)] out RdnTypeInfo? rdnTypeInfo, out object? typeDiscriminator)
        {
            Debug.Assert(BaseType.IsAssignableFrom(runtimeType));

            if (!_typeToDiscriminatorId.TryGetValue(runtimeType, out DerivedRdnTypeInfo? result))
            {
                switch (UnknownDerivedTypeHandling)
                {
                    case RdnUnknownDerivedTypeHandling.FallBackToNearestAncestor:
                        // Calculate (and cache the result) of the nearest ancestor for given runtime type.
                        // A `null` result denotes no matching ancestor type, we also cache that.
                        result = CalculateNearestAncestor(runtimeType);
                        _typeToDiscriminatorId[runtimeType] = result;
                        break;
                    case RdnUnknownDerivedTypeHandling.FallBackToBaseType:
                        // Recover the polymorphic contract (i.e. any type discriminators) for the base type, if it exists.
                        _typeToDiscriminatorId.TryGetValue(BaseType, out result);
                        _typeToDiscriminatorId[runtimeType] = result;
                        break;

                    case RdnUnknownDerivedTypeHandling.FailSerialization:
                    default:
                        if (runtimeType != BaseType)
                        {
                            ThrowHelper.ThrowNotSupportedException_RuntimeTypeNotSupported(BaseType, runtimeType);
                        }
                        break;
                }
            }

            if (result is null)
            {
                rdnTypeInfo = null;
                typeDiscriminator = null;
                return false;
            }
            else
            {
                rdnTypeInfo = result.RdnTypeInfo;
                typeDiscriminator = result.TypeDiscriminator;
                return true;
            }
        }

        public bool TryGetDerivedRdnTypeInfo(object typeDiscriminator, [NotNullWhen(true)] out RdnTypeInfo? rdnTypeInfo)
        {
            Debug.Assert(typeDiscriminator is int or string);
            Debug.Assert(UsesTypeDiscriminators);
            Debug.Assert(_discriminatorIdtoType != null);

            if (_discriminatorIdtoType.TryGetValue(typeDiscriminator, out DerivedRdnTypeInfo? result))
            {
                Debug.Assert(typeDiscriminator.Equals(result.TypeDiscriminator));
                rdnTypeInfo = result.RdnTypeInfo;
                return true;
            }

            if (!IgnoreUnrecognizedTypeDiscriminators)
            {
                ThrowHelper.ThrowRdnException_UnrecognizedTypeDiscriminator(typeDiscriminator);
            }

            rdnTypeInfo = null;
            return false;
        }

        public static bool IsSupportedPolymorphicBaseType(Type? type) =>
            type != null &&
            (type.IsClass || type.IsInterface) &&
            !type.IsSealed &&
            !type.IsGenericTypeDefinition &&
            !type.IsPointer &&
            type != RdnTypeInfo.ObjectType;

        public static bool IsSupportedDerivedType(Type baseType, Type? derivedType) =>
            baseType.IsAssignableFrom(derivedType) && !derivedType.IsGenericTypeDefinition;

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
            Justification = "The call to GetInterfaces will cross-reference results with interface types " +
                            "already declared as derived types of the polymorphic base type.")]
        private DerivedRdnTypeInfo? CalculateNearestAncestor(Type type)
        {
            Debug.Assert(!type.IsAbstract);
            Debug.Assert(BaseType.IsAssignableFrom(type));
            Debug.Assert(UnknownDerivedTypeHandling == RdnUnknownDerivedTypeHandling.FallBackToNearestAncestor);

            if (type == BaseType)
            {
                return null;
            }

            DerivedRdnTypeInfo? result = null;

            // First, walk up the class hierarchy for any supported types.
            for (Type? candidate = type.BaseType; BaseType.IsAssignableFrom(candidate); candidate = candidate.BaseType)
            {
                Debug.Assert(candidate != null);

                if (_typeToDiscriminatorId.TryGetValue(candidate, out result))
                {
                    break;
                }
            }

            // Interface hierarchies admit the possibility of diamond ambiguities in type discriminators.
            // Examine all interface implementations and identify potential conflicts.
            if (BaseType.IsInterface)
            {
                foreach (Type interfaceTy in type.GetInterfaces())
                {
                    if (interfaceTy != BaseType && BaseType.IsAssignableFrom(interfaceTy) &&
                        _typeToDiscriminatorId.TryGetValue(interfaceTy, out DerivedRdnTypeInfo? interfaceResult) &&
                        interfaceResult is not null)
                    {
                        if (result is null)
                        {
                            result = interfaceResult;
                        }
                        else
                        {
                            ThrowHelper.ThrowNotSupportedException_RuntimeTypeDiamondAmbiguity(BaseType, type, result.RdnTypeInfo.Type, interfaceResult.RdnTypeInfo.Type);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Walks the type hierarchy above the current type for any types that use polymorphic configuration.
        /// </summary>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075:UnrecognizedReflectionPattern",
            Justification = "The call to GetInterfaces will cross-reference results with interface types " +
                            "already declared as derived types of the polymorphic base type.")]
        internal static RdnTypeInfo? FindNearestPolymorphicBaseType(RdnTypeInfo typeInfo)
        {
            Debug.Assert(typeInfo.IsConfigured);

            if (typeInfo.PolymorphismOptions != null)
            {
                // Type defines its own polymorphic configuration.
                return null;
            }

            RdnTypeInfo? matchingResult = null;

            // First, walk up the class hierarchy for any supported types.
            for (Type? candidate = typeInfo.Type.BaseType; candidate != null; candidate = candidate.BaseType)
            {
                RdnTypeInfo? candidateInfo = ResolveAncestorTypeInfo(candidate, typeInfo.Options);
                if (candidateInfo?.PolymorphismOptions != null)
                {
                    // stop on the first ancestor that has a match
                    matchingResult = candidateInfo;
                    break;
                }
            }

            // Now, walk the interface hierarchy for any polymorphic interface declarations.
            foreach (Type interfaceType in typeInfo.Type.GetInterfaces())
            {
                RdnTypeInfo? candidateInfo = ResolveAncestorTypeInfo(interfaceType, typeInfo.Options);
                if (candidateInfo?.PolymorphismOptions != null)
                {
                    if (matchingResult != null)
                    {
                        // Resolve any conflicting matches.
                        if (matchingResult.Type.IsAssignableFrom(interfaceType))
                        {
                            // interface is more derived than previous match, replace it.
                            matchingResult = candidateInfo;
                        }
                        else if (interfaceType.IsAssignableFrom(matchingResult.Type))
                        {
                            // interface is less derived than previous match, keep the previous one.
                            continue;
                        }
                        else
                        {
                            // Diamond ambiguity, do not report any ancestors.
                            return null;
                        }
                    }
                    else
                    {
                        matchingResult = candidateInfo;
                    }
                }
            }

            return matchingResult;

            static RdnTypeInfo? ResolveAncestorTypeInfo(Type type, RdnSerializerOptions options)
            {
                try
                {
                    return options.GetTypeInfoInternal(type, ensureNotNull: null);
                }
                catch
                {
                    // The resolver produced an exception when resolving the ancestor type.
                    // Eat the exception and report no result instead.
                    return null;
                }
            }
        }

        /// <summary>
        /// RdnTypeInfo result holder for a derived type.
        /// </summary>
        private sealed class DerivedRdnTypeInfo
        {
            public DerivedRdnTypeInfo(object? typeDiscriminator, RdnTypeInfo derivedTypeInfo)
            {
                Debug.Assert(typeDiscriminator is null or int or string);

                TypeDiscriminator = typeDiscriminator;
                RdnTypeInfo = derivedTypeInfo;
            }

            public object? TypeDiscriminator { get; }
            public RdnTypeInfo RdnTypeInfo { get; }
        }
    }
}
