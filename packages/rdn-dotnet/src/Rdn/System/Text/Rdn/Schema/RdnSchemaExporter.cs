// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Rdn.Nodes;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn.Schema
{
    /// <summary>
    /// Functionality for exporting RDN schema from serialization contracts defined in <see cref="RdnTypeInfo"/>.
    /// </summary>
    public static class RdnSchemaExporter
    {
        /// <summary>
        /// Gets the RDN schema for <paramref name="type"/> as a <see cref="RdnNode"/> document.
        /// </summary>
        /// <param name="options">The options declaring the contract for the type.</param>
        /// <param name="type">The type for which to resolve a schema.</param>
        /// <param name="exporterOptions">The options object governing the export operation.</param>
        /// <returns>A RDN object containing the schema for <paramref name="type"/>.</returns>
        public static RdnNode GetRdnSchemaAsNode(this RdnSerializerOptions options, Type type, RdnSchemaExporterOptions? exporterOptions = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(type);

            ValidateOptions(options);
            RdnTypeInfo typeInfo = options.GetTypeInfoInternal(type);
            return typeInfo.GetRdnSchemaAsNode(exporterOptions);
        }

        /// <summary>
        /// Gets the RDN schema for <paramref name="typeInfo"/> as a <see cref="RdnNode"/> document.
        /// </summary>
        /// <param name="typeInfo">The contract from which to resolve the RDN schema.</param>
        /// <param name="exporterOptions">The options object governing the export operation.</param>
        /// <returns>A RDN object containing the schema for <paramref name="typeInfo"/>.</returns>
        public static RdnNode GetRdnSchemaAsNode(this RdnTypeInfo typeInfo, RdnSchemaExporterOptions? exporterOptions = null)
        {
            ArgumentNullException.ThrowIfNull(typeInfo);

            ValidateOptions(typeInfo.Options);
            exporterOptions ??= RdnSchemaExporterOptions.Default;

            typeInfo.EnsureConfigured();
            GenerationState state = new(typeInfo.Options, exporterOptions);
            RdnSchema schema = MapRdnSchemaCore(ref state, typeInfo);
            return schema.ToRdnNode(exporterOptions);
        }

        private static RdnSchema MapRdnSchemaCore(
            ref GenerationState state,
            RdnTypeInfo typeInfo,
            RdnPropertyInfo? propertyInfo = null,
            RdnConverter? customConverter = null,
            RdnNumberHandling? customNumberHandling = null,
            RdnTypeInfo? parentPolymorphicTypeInfo = null,
            bool parentPolymorphicTypeContainsTypesWithoutDiscriminator = false,
            bool parentPolymorphicTypeIsNonNullable = false,
            KeyValuePair<string, RdnSchema>? typeDiscriminator = null,
            bool cacheResult = true)
        {
            Debug.Assert(typeInfo.IsConfigured);

            RdnSchemaExporterContext exporterContext = state.CreateContext(typeInfo, propertyInfo, parentPolymorphicTypeInfo);

            if (cacheResult && typeInfo.Kind is not RdnTypeInfoKind.None &&
                state.TryGetExistingRdnPointer(exporterContext, out string? existingRdnPointer))
            {
                // The schema context has already been generated in the schema document, return a reference to it.
                return CompleteSchema(ref state, new RdnSchema { Ref = existingRdnPointer });
            }

            RdnConverter effectiveConverter = customConverter ?? typeInfo.Converter;
            RdnNumberHandling effectiveNumberHandling = customNumberHandling ?? typeInfo.NumberHandling ?? typeInfo.Options.NumberHandling;
            if (effectiveConverter.GetSchema(effectiveNumberHandling) is { } schema)
            {
                // A schema has been provided by the converter.
                return CompleteSchema(ref state, schema);
            }

            if (parentPolymorphicTypeInfo is null && typeInfo.PolymorphismOptions is { DerivedTypes.Count: > 0 } polyOptions)
            {
                // This is the base type of a polymorphic type hierarchy. The schema for this type
                // will include an "anyOf" property with the schemas for all derived types.
                string typeDiscriminatorKey = polyOptions.TypeDiscriminatorPropertyName;
                List<RdnDerivedType> derivedTypes = new(polyOptions.DerivedTypes);

                if (!typeInfo.Type.IsAbstract && !IsPolymorphicTypeThatSpecifiesItselfAsDerivedType(typeInfo))
                {
                    // For non-abstract base types that haven't been explicitly configured,
                    // add a trivial schema to the derived types since we should support it.
                    derivedTypes.Add(new RdnDerivedType(typeInfo.Type));
                }

                bool containsTypesWithoutDiscriminator = derivedTypes.Exists(static derivedTypes => derivedTypes.TypeDiscriminator is null);
                RdnSchemaType schemaType = RdnSchemaType.Any;
                List<RdnSchema>? anyOf = new(derivedTypes.Count);

                state.PushSchemaNode(RdnSchema.AnyOfPropertyName);

                foreach (RdnDerivedType derivedType in derivedTypes)
                {
                    Debug.Assert(derivedType.TypeDiscriminator is null or int or string);

                    KeyValuePair<string, RdnSchema>? derivedTypeDiscriminator = null;
                    if (derivedType.TypeDiscriminator is { } discriminatorValue)
                    {
                        RdnNode discriminatorNode = discriminatorValue switch
                        {
                            string stringId => (RdnNode)stringId,
                            _ => (RdnNode)(int)discriminatorValue,
                        };

                        RdnSchema discriminatorSchema = new() { Constant = discriminatorNode };
                        derivedTypeDiscriminator = new(typeDiscriminatorKey, discriminatorSchema);
                    }

                    RdnTypeInfo derivedTypeInfo = typeInfo.Options.GetTypeInfoInternal(derivedType.DerivedType);

                    state.PushSchemaNode(anyOf.Count.ToString(CultureInfo.InvariantCulture));
                    RdnSchema derivedSchema = MapRdnSchemaCore(
                        ref state,
                        derivedTypeInfo,
                        parentPolymorphicTypeInfo: typeInfo,
                        typeDiscriminator: derivedTypeDiscriminator,
                        parentPolymorphicTypeContainsTypesWithoutDiscriminator: containsTypesWithoutDiscriminator,
                        parentPolymorphicTypeIsNonNullable: propertyInfo is { IsGetNullable: false, IsSetNullable: false },
                        cacheResult: false);

                    state.PopSchemaNode();

                    // Determine if all derived schemas have the same type.
                    if (anyOf.Count == 0)
                    {
                        schemaType = derivedSchema.Type;
                    }
                    else if (schemaType != derivedSchema.Type)
                    {
                        schemaType = RdnSchemaType.Any;
                    }

                    anyOf.Add(derivedSchema);
                }

                state.PopSchemaNode();

                if (schemaType is not RdnSchemaType.Any)
                {
                    // If all derived types have the same schema type, we can simplify the schema
                    // by moving the type keyword to the base schema and removing it from the derived schemas.
                    foreach (RdnSchema derivedSchema in anyOf)
                    {
                        derivedSchema.Type = RdnSchemaType.Any;

                        if (derivedSchema.KeywordCount == 0)
                        {
                            // if removing the type results in an empty schema,
                            // remove the anyOf array entirely since it's always true.
                            anyOf = null;
                            break;
                        }
                    }
                }

                return CompleteSchema(ref state, new()
                {
                    Type = schemaType,
                    AnyOf = anyOf,
                    // If all derived types have a discriminator, we can require it in the base schema.
                    Required = containsTypesWithoutDiscriminator ? null : [typeDiscriminatorKey]
                });
            }

            if (effectiveConverter.NullableElementConverter is { } elementConverter)
            {
                RdnTypeInfo elementTypeInfo = typeInfo.Options.GetTypeInfo(elementConverter.Type!);
                schema = MapRdnSchemaCore(ref state, elementTypeInfo, customConverter: elementConverter, cacheResult: false);

                if (schema.Enum != null)
                {
                    Debug.Assert(elementTypeInfo.Type.IsEnum, "The enum keyword should only be populated by schemas for enum types.");
                    schema.Enum.Add(null); // Append null to the enum array.
                }

                return CompleteSchema(ref state, schema);
            }

            switch (typeInfo.Kind)
            {
                case RdnTypeInfoKind.Object:
                    List<KeyValuePair<string, RdnSchema>>? properties = null;
                    List<string>? required = null;
                    RdnSchema? additionalProperties = null;

                    RdnUnmappedMemberHandling effectiveUnmappedMemberHandling = typeInfo.UnmappedMemberHandling ?? typeInfo.Options.UnmappedMemberHandling;
                    if (effectiveUnmappedMemberHandling is RdnUnmappedMemberHandling.Disallow)
                    {
                        additionalProperties = RdnSchema.CreateFalseSchema();
                    }

                    if (typeDiscriminator is { } typeDiscriminatorPair)
                    {
                        (properties ??= []).Add(typeDiscriminatorPair);
                        if (parentPolymorphicTypeContainsTypesWithoutDiscriminator)
                        {
                            // Require the discriminator here since it's not common to all derived types.
                            (required ??= []).Add(typeDiscriminatorPair.Key);
                        }
                    }

                    state.PushSchemaNode(RdnSchema.PropertiesPropertyName);
                    foreach (RdnPropertyInfo property in typeInfo.Properties)
                    {
                        if (property is { Get: null, Set: null } or { IsExtensionData: true })
                        {
                            continue; // Skip RdnIgnored properties and extension data
                        }

                        state.PushSchemaNode(property.Name);
                        RdnSchema propertySchema = MapRdnSchemaCore(
                            ref state,
                            property.RdnTypeInfo,
                            propertyInfo: property,
                            customConverter: property.EffectiveConverter,
                            customNumberHandling: property.EffectiveNumberHandling);

                        state.PopSchemaNode();

                        if (property.AssociatedParameter is { HasDefaultValue: true } parameterInfo)
                        {
                            RdnSchema.EnsureMutable(ref propertySchema);
                            propertySchema.DefaultValue = RdnSerializer.SerializeToNode(parameterInfo.DefaultValue, property.RdnTypeInfo);
                            propertySchema.HasDefaultValue = true;
                        }

                        (properties ??= []).Add(new(property.Name, propertySchema));

                        // Mark as required if either the property is required or the associated constructor parameter is non-optional.
                        // While the latter implies the former in cases where the RdnSerializerOptions.RespectRequiredConstructorParameters
                        // setting has been enabled, for the case of the schema exporter we always mark non-optional constructor parameters as required.
                        if (property is { IsRequired: true } or { AssociatedParameter.IsRequiredParameter: true })
                        {
                            (required ??= []).Add(property.Name);
                        }
                    }

                    state.PopSchemaNode();
                    return CompleteSchema(ref state, new()
                    {
                        Type = RdnSchemaType.Object,
                        Properties = properties,
                        Required = required,
                        AdditionalProperties = additionalProperties,
                    });

                case RdnTypeInfoKind.Enumerable:
                    Debug.Assert(typeInfo.ElementTypeInfo != null);

                    if (typeDiscriminator is null)
                    {
                        state.PushSchemaNode(RdnSchema.ItemsPropertyName);
                        RdnSchema items = MapRdnSchemaCore(ref state, typeInfo.ElementTypeInfo, customNumberHandling: effectiveNumberHandling);
                        state.PopSchemaNode();

                        return CompleteSchema(ref state, new()
                        {
                            Type = RdnSchemaType.Array,
                            Items = items.IsTrue ? null : items,
                        });
                    }
                    else
                    {
                        // Polymorphic enumerable types are represented using a wrapping object:
                        // { "$type" : "discriminator", "$values" : [element1, element2, ...] }
                        // Which corresponds to the schema
                        // { "properties" : { "$type" : { "const" : "discriminator" }, "$values" : { "type" : "array", "items" : { ... } } } }
                        const string ValuesKeyword = RdnSerializer.ValuesPropertyName;

                        state.PushSchemaNode(RdnSchema.PropertiesPropertyName);
                        state.PushSchemaNode(ValuesKeyword);
                        state.PushSchemaNode(RdnSchema.ItemsPropertyName);

                        RdnSchema items = MapRdnSchemaCore(ref state, typeInfo.ElementTypeInfo, customNumberHandling: effectiveNumberHandling);

                        state.PopSchemaNode();
                        state.PopSchemaNode();
                        state.PopSchemaNode();

                        return CompleteSchema(ref state, new()
                        {
                            Type = RdnSchemaType.Object,
                            Properties =
                            [
                                typeDiscriminator.Value,
                                new(ValuesKeyword,
                                    new RdnSchema()
                                    {
                                        Type = RdnSchemaType.Array,
                                        Items = items.IsTrue ? null : items,
                                    }),
                            ],
                            Required = parentPolymorphicTypeContainsTypesWithoutDiscriminator ? [typeDiscriminator.Value.Key] : null,
                        });
                    }

                case RdnTypeInfoKind.Dictionary:
                    Debug.Assert(typeInfo.ElementTypeInfo != null);

                    List<KeyValuePair<string, RdnSchema>>? dictProps = null;
                    List<string>? dictRequired = null;

                    if (typeDiscriminator is { } dictDiscriminator)
                    {
                        dictProps = [dictDiscriminator];
                        if (parentPolymorphicTypeContainsTypesWithoutDiscriminator)
                        {
                            // Require the discriminator here since it's not common to all derived types.
                            dictRequired = [dictDiscriminator.Key];
                        }
                    }

                    state.PushSchemaNode(RdnSchema.AdditionalPropertiesPropertyName);
                    RdnSchema valueSchema = MapRdnSchemaCore(ref state, typeInfo.ElementTypeInfo, customNumberHandling: effectiveNumberHandling);
                    state.PopSchemaNode();

                    return CompleteSchema(ref state, new()
                    {
                        Type = RdnSchemaType.Object,
                        Properties = dictProps,
                        Required = dictRequired,
                        AdditionalProperties = valueSchema.IsTrue ? null : valueSchema,
                    });

                default:
                    Debug.Assert(typeInfo.Kind is RdnTypeInfoKind.None);
                    // Return a `true` schema for types with user-defined converters.
                    return CompleteSchema(ref state, RdnSchema.CreateTrueSchema());
            }

            RdnSchema CompleteSchema(ref GenerationState state, RdnSchema schema)
            {
                if (schema.Ref is null)
                {
                    if (IsNullableSchema(state.ExporterOptions))
                    {
                        schema.MakeNullable();
                    }

                    bool IsNullableSchema(RdnSchemaExporterOptions options)
                    {
                        // A schema is marked as nullable if either:
                        // 1. We have a schema for a property where either the getter or setter are marked as nullable.
                        // 2. We have a schema for a Nullable<T> type.
                        // 3. We have a schema for a reference type, unless we're explicitly treating null-oblivious types as non-nullable.

                        if (propertyInfo is not null)
                        {
                            return propertyInfo.IsGetNullable || propertyInfo.IsSetNullable;
                        }

                        if (typeInfo.IsNullable)
                        {
                            return true;
                        }

                        return !typeInfo.Type.IsValueType && !parentPolymorphicTypeIsNonNullable && !options.TreatNullObliviousAsNonNullable;
                    }
                }

                if (state.ExporterOptions.TransformSchemaNode != null)
                {
                    // Prime the schema for invocation by the RdnNode transformer.
                    schema.ExporterContext = exporterContext;
                }

                return schema;
            }
        }

        private static void ValidateOptions(RdnSerializerOptions options)
        {
            if (options.ReferenceHandler == ReferenceHandler.Preserve)
            {
                ThrowHelper.ThrowNotSupportedException_RdnSchemaExporterDoesNotSupportReferenceHandlerPreserve();
            }

            options.MakeReadOnly();
        }

        private static bool IsPolymorphicTypeThatSpecifiesItselfAsDerivedType(RdnTypeInfo typeInfo)
        {
            Debug.Assert(typeInfo.PolymorphismOptions is not null);

            foreach (RdnDerivedType derivedType in typeInfo.PolymorphismOptions.DerivedTypes)
            {
                if (derivedType.DerivedType == typeInfo.Type)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly ref struct GenerationState(RdnSerializerOptions options, RdnSchemaExporterOptions exporterOptions)
        {
            private readonly List<string> _currentPath = [];
            private readonly Dictionary<(RdnTypeInfo, RdnPropertyInfo?), string[]> _generated = new();

            public int CurrentDepth => _currentPath.Count;
            public RdnSerializerOptions Options { get; } = options;
            public RdnSchemaExporterOptions ExporterOptions { get; } = exporterOptions;

            public void PushSchemaNode(string nodeId)
            {
                if (CurrentDepth == Options.EffectiveMaxDepth)
                {
                    ThrowHelper.ThrowInvalidOperationException_RdnSchemaExporterDepthTooLarge();
                }

                _currentPath.Add(nodeId);
            }

            public void PopSchemaNode()
            {
                Debug.Assert(CurrentDepth > 0);
                _currentPath.RemoveAt(_currentPath.Count - 1);
            }

            /// <summary>
            /// Registers the current schema node generation context; if it has already been generated return a RDN pointer to its location.
            /// </summary>
            public bool TryGetExistingRdnPointer(in RdnSchemaExporterContext context, [NotNullWhen(true)] out string? existingRdnPointer)
            {
                (RdnTypeInfo TypeInfo, RdnPropertyInfo? PropertyInfo) key = (context.TypeInfo, context.PropertyInfo);
#if NET
                ref string[]? pathToSchema = ref CollectionsMarshal.GetValueRefOrAddDefault(_generated, key, out bool exists);
#else
                bool exists = _generated.TryGetValue(key, out string[]? pathToSchema);
#endif
                if (exists)
                {
                    existingRdnPointer = FormatRdnPointer(pathToSchema);
                    return true;
                }
#if NET
                pathToSchema = context._path;
#else
                _generated[key] = context._path;
#endif
                existingRdnPointer = null;
                return false;
            }

            public RdnSchemaExporterContext CreateContext(RdnTypeInfo typeInfo, RdnPropertyInfo? propertyInfo, RdnTypeInfo? baseTypeInfo)
            {
                return new RdnSchemaExporterContext(typeInfo, propertyInfo, baseTypeInfo, [.. _currentPath]);
            }

            private static string FormatRdnPointer(ReadOnlySpan<string> path)
            {
                if (path.IsEmpty)
                {
                    return "#";
                }

                using ValueStringBuilder sb = new(initialCapacity: path.Length * 10);
                sb.Append('#');

                foreach (string segment in path)
                {
                    ReadOnlySpan<char> span = segment.AsSpan();
                    sb.Append('/');

                    do
                    {
                        // Per RFC 6901 the characters '~' and '/' must be escaped.
                        int pos = span.IndexOfAny('~', '/');
                        if (pos < 0)
                        {
                            sb.Append(span);
                            break;
                        }

                        sb.Append(span.Slice(0, pos));

                        if (span[pos] == '~')
                        {
                            sb.Append("~0");
                        }
                        else
                        {
                            Debug.Assert(span[pos] == '/');
                            sb.Append("~1");
                        }

                        span = span.Slice(pos + 1);
                    }
                    while (!span.IsEmpty);
                }

                return sb.ToString();
            }
        }
    }
}
