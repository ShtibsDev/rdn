// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Rdn.Serialization.Converters;

namespace Rdn.Serialization.Metadata
{
    public static partial class RdnMetadataServices
    {
        /// <summary>
        /// Creates serialization metadata for a type using a simple converter.
        /// </summary>
        private static RdnTypeInfo<T> CreateCore<T>(RdnConverter converter, RdnSerializerOptions options)
        {
            var typeInfo = new RdnTypeInfo<T>(converter, options);
            typeInfo.PopulatePolymorphismMetadata();
            typeInfo.MapInterfaceTypesToCallbacks();

            // Plug in any converter configuration -- should be run last.
            converter.ConfigureRdnTypeInfo(typeInfo, options);
            typeInfo.IsCustomized = false;
            return typeInfo;
        }

        /// <summary>
        /// Creates serialization metadata for an object.
        /// </summary>
        private static RdnTypeInfo<T> CreateCore<T>(RdnSerializerOptions options, RdnObjectInfoValues<T> objectInfo)
        {
            RdnConverter<T> converter = GetConverter(objectInfo);
            var typeInfo = new RdnTypeInfo<T>(converter, options);
            if (objectInfo.ObjectWithParameterizedConstructorCreator != null)
            {
                // NB parameter metadata must be populated *before* property metadata
                // so that properties can be linked to their associated parameters.
                typeInfo.CreateObjectWithArgs = objectInfo.ObjectWithParameterizedConstructorCreator;
                PopulateParameterInfoValues(typeInfo, objectInfo.ConstructorParameterMetadataInitializer);
            }
            else
            {
                typeInfo.SetCreateObjectIfCompatible(objectInfo.ObjectCreator);
                typeInfo.CreateObjectForExtensionDataProperty = ((RdnTypeInfo)typeInfo).CreateObject;
            }

            if (objectInfo.PropertyMetadataInitializer != null)
            {
                typeInfo.SourceGenDelayedPropertyInitializer = objectInfo.PropertyMetadataInitializer;
            }
            else
            {
                typeInfo.PropertyMetadataSerializationNotSupported = true;
            }

            typeInfo.ConstructorAttributeProviderFactory = objectInfo.ConstructorAttributeProviderFactory;
            typeInfo.SerializeHandler = objectInfo.SerializeHandler;
            typeInfo.NumberHandling = objectInfo.NumberHandling;
            typeInfo.PopulatePolymorphismMetadata();
            typeInfo.MapInterfaceTypesToCallbacks();

            // Plug in any converter configuration -- should be run last.
            converter.ConfigureRdnTypeInfo(typeInfo, options);
            typeInfo.IsCustomized = false;
            return typeInfo;
        }

        /// <summary>
        /// Creates serialization metadata for a collection.
        /// </summary>
        private static RdnTypeInfo<T> CreateCore<T>(
            RdnSerializerOptions options,
            RdnCollectionInfoValues<T> collectionInfo,
            RdnConverter<T> converter,
            object? createObjectWithArgs = null,
            object? addFunc = null)
        {
            ArgumentNullException.ThrowIfNull(collectionInfo);

            converter = collectionInfo.SerializeHandler != null
                ? new RdnMetadataServicesConverter<T>(converter)
                : converter;

            RdnTypeInfo<T> typeInfo = new RdnTypeInfo<T>(converter, options);

            typeInfo.KeyTypeInfo = collectionInfo.KeyInfo;
            typeInfo.ElementTypeInfo = collectionInfo.ElementInfo;
            Debug.Assert(typeInfo.Kind != RdnTypeInfoKind.None);
            typeInfo.NumberHandling = collectionInfo.NumberHandling;
            typeInfo.SerializeHandler = collectionInfo.SerializeHandler;
            typeInfo.CreateObjectWithArgs = createObjectWithArgs;
            typeInfo.AddMethodDelegate = addFunc;
            typeInfo.SetCreateObjectIfCompatible(collectionInfo.ObjectCreator);
            typeInfo.PopulatePolymorphismMetadata();
            typeInfo.MapInterfaceTypesToCallbacks();

            // Plug in any converter configuration -- should be run last.
            converter.ConfigureRdnTypeInfo(typeInfo, options);
            typeInfo.IsCustomized = false;
            return typeInfo;
        }

        private static RdnConverter<T> GetConverter<T>(RdnObjectInfoValues<T> objectInfo)
        {
#pragma warning disable CS8714 // Nullability of type argument 'T' doesn't match 'notnull' constraint.
            RdnConverter<T> converter = objectInfo.ObjectWithParameterizedConstructorCreator != null
                ? new LargeObjectWithParameterizedConstructorConverter<T>()
                : new ObjectDefaultConverter<T>();
#pragma warning restore CS8714

            return objectInfo.SerializeHandler != null
                ? new RdnMetadataServicesConverter<T>(converter)
                : converter;
        }

        private static void PopulateParameterInfoValues(RdnTypeInfo typeInfo, Func<RdnParameterInfoValues[]?>? paramFactory)
        {
            Debug.Assert(typeInfo.Kind is RdnTypeInfoKind.Object);
            Debug.Assert(!typeInfo.IsReadOnly);

            if (paramFactory?.Invoke() is RdnParameterInfoValues[] parameterInfoValues)
            {
                typeInfo.PopulateParameterInfoValues(parameterInfoValues);
            }
            else
            {
                typeInfo.PropertyMetadataSerializationNotSupported = true;
            }
        }

        internal static void PopulateProperties(RdnTypeInfo typeInfo, RdnTypeInfo.RdnPropertyInfoList propertyList, Func<RdnSerializerContext, RdnPropertyInfo[]> propInitFunc)
        {
            Debug.Assert(typeInfo.Kind is RdnTypeInfoKind.Object);
            Debug.Assert(!typeInfo.IsConfigured);
            Debug.Assert(typeInfo.Type != RdnTypeInfo.ObjectType);
            Debug.Assert(typeInfo.Converter.ElementType is null);

            RdnSerializerContext? context = typeInfo.Options.TypeInfoResolver as RdnSerializerContext;
            RdnPropertyInfo[] properties = propInitFunc(context!);

            // Regardless of the source generator we need to re-run the naming conflict resolution algorithm
            // at run time since it is possible that the naming policy or other configs can be different then.
            RdnTypeInfo.PropertyHierarchyResolutionState state = new(typeInfo.Options);

            foreach (RdnPropertyInfo rdnPropertyInfo in properties)
            {
                if (!rdnPropertyInfo.SrcGen_IsPublic)
                {
                    if (rdnPropertyInfo.SrcGen_HasRdnInclude)
                    {
                        Debug.Assert(rdnPropertyInfo.MemberName != null, "MemberName is not set by source gen");
                        ThrowHelper.ThrowInvalidOperationException_RdnIncludeOnInaccessibleProperty(rdnPropertyInfo.MemberName, rdnPropertyInfo.DeclaringType);
                    }

                    continue;
                }

                if (rdnPropertyInfo.MemberType == MemberTypes.Field && !rdnPropertyInfo.SrcGen_HasRdnInclude && !typeInfo.Options.IncludeFields)
                {
                    continue;
                }

                propertyList.AddPropertyWithConflictResolution(rdnPropertyInfo, ref state);
            }

            if (state.IsPropertyOrderSpecified)
            {
                propertyList.SortProperties();
            }
        }

        private static RdnPropertyInfo<T> CreatePropertyInfoCore<T>(RdnPropertyInfoValues<T> propertyInfoValues, RdnSerializerOptions options)
        {
            var propertyInfo = new RdnPropertyInfo<T>(propertyInfoValues.DeclaringType, declaringTypeInfo: null, options);

            DeterminePropertyName(propertyInfo,
                declaredPropertyName: propertyInfoValues.PropertyName,
                declaredRdnPropertyName: propertyInfoValues.RdnPropertyName);

            propertyInfo.MemberName = propertyInfoValues.PropertyName;
            propertyInfo.MemberType = propertyInfoValues.IsProperty ? MemberTypes.Property : MemberTypes.Field;
            propertyInfo.SrcGen_IsPublic = propertyInfoValues.IsPublic;
            propertyInfo.SrcGen_HasRdnInclude = propertyInfoValues.HasRdnInclude;
            propertyInfo.IsExtensionData = propertyInfoValues.IsExtensionData;
            propertyInfo.CustomConverter = propertyInfoValues.Converter;

            if (propertyInfo.IgnoreCondition != RdnIgnoreCondition.Always)
            {
                propertyInfo.Get = propertyInfoValues.Getter!;
                propertyInfo.Set = propertyInfoValues.Setter;
            }

            propertyInfo.IgnoreCondition = propertyInfoValues.IgnoreCondition;
            propertyInfo.RdnTypeInfo = propertyInfoValues.PropertyTypeInfo;
            propertyInfo.NumberHandling = propertyInfoValues.NumberHandling;
            propertyInfo.AttributeProviderFactory = propertyInfoValues.AttributeProviderFactory;

            return propertyInfo;
        }

        private static void DeterminePropertyName(
            RdnPropertyInfo propertyInfo,
            string declaredPropertyName,
            string? declaredRdnPropertyName)
        {
            string? name;

            // Property name settings.
            if (declaredRdnPropertyName != null)
            {
                name = declaredRdnPropertyName;
            }
            else if (propertyInfo.Options.PropertyNamingPolicy == null)
            {
                name = declaredPropertyName;
            }
            else
            {
                name = propertyInfo.Options.PropertyNamingPolicy.ConvertName(declaredPropertyName);
            }

            // Compat: We need to do validation before we assign Name so that we get InvalidOperationException rather than ArgumentNullException
            if (name == null)
            {
                ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(propertyInfo);
            }

            propertyInfo.Name = name;
        }
    }
}
