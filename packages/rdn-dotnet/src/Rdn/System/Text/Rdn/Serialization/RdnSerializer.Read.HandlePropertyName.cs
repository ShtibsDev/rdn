// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Rdn.Reflection;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    public static partial class RdnSerializer
    {
        /// <summary>
        /// Lookup the property given its name (obtained from the reader) and return it.
        /// Also sets state.Current.RdnPropertyInfo to a non-null value.
        /// </summary>
        internal static RdnPropertyInfo LookupProperty(
            object? obj,
            ReadOnlySpan<byte> unescapedPropertyName,
            ref ReadStack state,
            RdnSerializerOptions options,
            out bool useExtensionProperty,
            bool createExtensionProperty = true)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;
            useExtensionProperty = false;

            RdnPropertyInfo? rdnPropertyInfo = rdnTypeInfo.GetProperty(
                unescapedPropertyName,
                ref state.Current,
                out byte[] utf8PropertyName);

            // Increment PropertyIndex so GetProperty() checks the next property first when called again.
            state.Current.PropertyIndex++;

            // For case insensitive and missing property support of RdnPath, remember the value on the temporary stack.
            state.Current.RdnPropertyName = utf8PropertyName;

            // Handle missing properties
            if (rdnPropertyInfo is null)
            {
                if (rdnTypeInfo.EffectiveUnmappedMemberHandling is RdnUnmappedMemberHandling.Disallow)
                {
                    Debug.Assert(rdnTypeInfo.ExtensionDataProperty is null, "rdnTypeInfo.Configure() should have caught conflicting configuration.");
                    string stringPropertyName = Encoding.UTF8.GetString(unescapedPropertyName);
                    ThrowHelper.ThrowRdnException_UnmappedRdnProperty(rdnTypeInfo.Type, stringPropertyName);
                }

                // Determine if we should use the extension property.
                if (rdnTypeInfo.ExtensionDataProperty is RdnPropertyInfo { HasGetter: true, HasSetter: true } dataExtProperty)
                {
                    state.Current.RdnPropertyNameAsString = Encoding.UTF8.GetString(unescapedPropertyName);

                    if (createExtensionProperty)
                    {
                        Debug.Assert(obj != null, "obj is null");
                        CreateExtensionDataProperty(obj, dataExtProperty, options);
                    }

                    rdnPropertyInfo = dataExtProperty;
                    useExtensionProperty = true;
                }
                else
                {
                    // Populate with a placeholder value required by RdnPath calculations
                    rdnPropertyInfo = RdnPropertyInfo.s_missingProperty;
                }
            }

            state.Current.RdnPropertyInfo = rdnPropertyInfo;
            state.Current.NumberHandling = rdnPropertyInfo.EffectiveNumberHandling;
            return rdnPropertyInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> GetPropertyName(
            scoped ref ReadStack state,
            ref Utf8RdnReader reader,
            RdnSerializerOptions options,
            out bool isAlreadyReadMetadataProperty)
        {
            ReadOnlySpan<byte> propertyName = reader.GetUnescapedSpan();
            isAlreadyReadMetadataProperty = false;

            if (state.Current.CanContainMetadata)
            {
                if (IsMetadataPropertyName(propertyName, state.Current.BaseRdnTypeInfo.PolymorphicTypeResolver))
                {
                    if (options.AllowOutOfOrderMetadataProperties)
                    {
                        isAlreadyReadMetadataProperty = true;
                    }
                    else
                    {
                        ThrowHelper.ThrowUnexpectedMetadataException(propertyName, ref reader, ref state);
                    }
                }
            }

            return propertyName;
        }

        internal static void CreateExtensionDataProperty(
            object obj,
            RdnPropertyInfo rdnPropertyInfo,
            RdnSerializerOptions options)
        {
            Debug.Assert(rdnPropertyInfo != null);

            object? extensionData = rdnPropertyInfo.GetValueAsObject(obj);

            // For IReadOnlyDictionary, if there's an existing non-null instance, we need to create a new mutable
            // Dictionary seeded with the existing contents so we can add the deserialized extension data to it.
            bool isReadOnlyDictionary = rdnPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, object>) ||
                                        rdnPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, RdnElement>);

            if (extensionData == null || (isReadOnlyDictionary && extensionData != null))
            {
                // Create the appropriate dictionary type. We already verified the types.
#if DEBUG
                Type? underlyingIDictionaryType = rdnPropertyInfo.PropertyType.GetCompatibleGenericInterface(typeof(IDictionary<,>))
                    ?? rdnPropertyInfo.PropertyType.GetCompatibleGenericInterface(typeof(IReadOnlyDictionary<,>));
                Debug.Assert(underlyingIDictionaryType is not null);
                Type[] genericArgs = underlyingIDictionaryType.GetGenericArguments();

                Debug.Assert(underlyingIDictionaryType.IsGenericType);
                Debug.Assert(genericArgs.Length == 2);
                Debug.Assert(genericArgs[0].UnderlyingSystemType == typeof(string));
                Debug.Assert(
                    genericArgs[1].UnderlyingSystemType == RdnTypeInfo.ObjectType ||
                    genericArgs[1].UnderlyingSystemType == typeof(RdnElement) ||
                    genericArgs[1].UnderlyingSystemType == typeof(Nodes.RdnNode));
#endif

                Func<object>? createObjectForExtensionDataProp = rdnPropertyInfo.RdnTypeInfo.CreateObject
                    ?? rdnPropertyInfo.RdnTypeInfo.CreateObjectForExtensionDataProperty;

                if (createObjectForExtensionDataProp == null)
                {
                    // Avoid a reference to the RdnNode type for trimming
                    if (rdnPropertyInfo.PropertyType.FullName == RdnTypeInfo.RdnObjectTypeName)
                    {
                        ThrowHelper.ThrowInvalidOperationException_NodeRdnObjectCustomConverterNotAllowedOnExtensionProperty();
                    }
                    // For IReadOnlyDictionary<string, object> or IReadOnlyDictionary<string, RdnElement> interface types,
                    // create a Dictionary<TKey, TValue> instance seeded with any existing contents.
                    else if (rdnPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, object>))
                    {
                        if (extensionData != null)
                        {
                            var existing = (IReadOnlyDictionary<string, object>)extensionData;
                            var newDict = new Dictionary<string, object>();
                            foreach (KeyValuePair<string, object> kvp in existing)
                            {
                                newDict[kvp.Key] = kvp.Value;
                            }
                            extensionData = newDict;
                        }
                        else
                        {
                            extensionData = new Dictionary<string, object>();
                        }
                        Debug.Assert(rdnPropertyInfo.Set != null);
                        rdnPropertyInfo.Set(obj, extensionData);
                        return;
                    }
                    else if (rdnPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, RdnElement>))
                    {
                        if (extensionData != null)
                        {
                            var existing = (IReadOnlyDictionary<string, RdnElement>)extensionData;
                            var newDict = new Dictionary<string, RdnElement>();
                            foreach (KeyValuePair<string, RdnElement> kvp in existing)
                            {
                                newDict[kvp.Key] = kvp.Value;
                            }
                            extensionData = newDict;
                        }
                        else
                        {
                            extensionData = new Dictionary<string, RdnElement>();
                        }
                        Debug.Assert(rdnPropertyInfo.Set != null);
                        rdnPropertyInfo.Set(obj, extensionData);
                        return;
                    }
                    else
                    {
                        ThrowHelper.ThrowNotSupportedException_SerializationNotSupported(rdnPropertyInfo.PropertyType);
                    }
                }

                extensionData = createObjectForExtensionDataProp();
                Debug.Assert(rdnPropertyInfo.Set != null);
                rdnPropertyInfo.Set(obj, extensionData);
            }

            // We don't add the value to the dictionary here because we need to support the read-ahead functionality for Streams.
        }
    }
}
