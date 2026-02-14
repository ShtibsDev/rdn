// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipelines;
using System.Reflection;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    internal static partial class ThrowHelper
    {
        [DoesNotReturn]
        public static void ThrowArgumentException_DeserializeWrongType(Type type, object value)
        {
            throw new ArgumentException(SR.Format(SR.DeserializeWrongType, type, value.GetType()));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_SerializerDoesNotSupportComments(string paramName)
        {
            throw new ArgumentException(SR.RdnSerializerDoesNotSupportComments, paramName);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_SerializationNotSupported(Type propertyType)
        {
            throw new NotSupportedException(SR.Format(SR.SerializationNotSupportedType, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_TypeRequiresAsyncSerialization(Type propertyType)
        {
            throw new NotSupportedException(SR.Format(SR.TypeRequiresAsyncSerialization, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_DictionaryKeyTypeNotSupported(Type keyType, RdnConverter converter)
        {
            throw new NotSupportedException(SR.Format(SR.DictionaryKeyTypeNotSupported, keyType, converter.GetType()));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_DeserializeUnableToConvertValue(Type propertyType)
        {
            throw new RdnException(SR.Format(SR.DeserializeUnableToConvertValue, propertyType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowInvalidCastException_DeserializeUnableToAssignValue(Type typeOfValue, Type declaredType)
        {
            throw new InvalidCastException(SR.Format(SR.DeserializeUnableToAssignValue, typeOfValue, declaredType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DeserializeUnableToAssignNull(Type declaredType)
        {
            throw new InvalidOperationException(SR.Format(SR.DeserializeUnableToAssignNull, declaredType));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_PropertyGetterDisallowNull(string propertyName, Type declaringType)
        {
            throw new RdnException(SR.Format(SR.PropertyGetterDisallowNull, propertyName, declaringType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowRdnException_PropertySetterDisallowNull(string propertyName, Type declaringType)
        {
            throw new RdnException(SR.Format(SR.PropertySetterDisallowNull, propertyName, declaringType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowRdnException_ConstructorParameterDisallowNull(string parameterName, Type declaringType)
        {
            throw new RdnException(SR.Format(SR.ConstructorParameterDisallowNull, parameterName, declaringType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPopulateNotSupportedByConverter(RdnPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPopulateNotSupportedByConverter, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyMustHaveAGetter(RdnPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyMustHaveAGetter, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyValueTypeMustHaveASetter(RdnPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyValueTypeMustHaveASetter, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowPolymorphicDeserialization(RdnPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyCannotAllowPolymorphicDeserialization, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReadOnlyMember(RdnPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyCannotAllowReadOnlyMember, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReferenceHandling()
        {
            throw new InvalidOperationException(SR.ObjectCreationHandlingPropertyCannotAllowReferenceHandling);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_ObjectCreationHandlingPropertyDoesNotSupportParameterizedConstructors()
        {
            throw new NotSupportedException(SR.ObjectCreationHandlingPropertyDoesNotSupportParameterizedConstructors);
        }

        [DoesNotReturn]
        public static void ThrowRdnException_SerializationConverterRead(RdnConverter? converter)
        {
            throw new RdnException(SR.Format(SR.SerializationConverterRead, converter)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowRdnException_SerializationConverterWrite(RdnConverter? converter)
        {
            throw new RdnException(SR.Format(SR.SerializationConverterWrite, converter)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowRdnException_SerializerCycleDetected(int maxDepth)
        {
            throw new RdnException(SR.Format(SR.SerializerCycleDetected, maxDepth)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowRdnException(string? message = null)
        {
            throw new RdnException(message) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_CannotSerializeInvalidType(string paramName, Type typeToConvert, Type? declaringType, string? propertyName)
        {
            if (declaringType == null)
            {
                Debug.Assert(propertyName == null);
                throw new ArgumentException(SR.Format(SR.CannotSerializeInvalidType, typeToConvert), paramName);
            }

            Debug.Assert(propertyName != null);
            throw new ArgumentException(SR.Format(SR.CannotSerializeInvalidMember, typeToConvert, propertyName, declaringType), paramName);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CannotSerializeInvalidType(Type typeToConvert, Type? declaringType, MemberInfo? memberInfo)
        {
            if (declaringType == null)
            {
                Debug.Assert(memberInfo == null);
                throw new InvalidOperationException(SR.Format(SR.CannotSerializeInvalidType, typeToConvert));
            }

            Debug.Assert(memberInfo != null);
            throw new InvalidOperationException(SR.Format(SR.CannotSerializeInvalidMember, typeToConvert, memberInfo.Name, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationConverterNotCompatible(Type converterType, Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationConverterNotCompatible, converterType, type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ResolverTypeNotCompatible(Type requestedType, Type actualType)
        {
            throw new InvalidOperationException(SR.Format(SR.ResolverTypeNotCompatible, actualType, requestedType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ResolverTypeInfoOptionsNotCompatible()
        {
            throw new InvalidOperationException(SR.ResolverTypeInfoOptionsNotCompatible);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnSerializerOptionsNoTypeInfoResolverSpecified()
        {
            throw new InvalidOperationException(SR.RdnSerializerOptionsNoTypeInfoResolverSpecified);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnSerializerIsReflectionDisabled()
        {
            throw new InvalidOperationException(SR.RdnSerializerIsReflectionDisabled);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationConverterOnAttributeInvalid(Type classType, MemberInfo? memberInfo)
        {
            string location = classType.ToString();
            if (memberInfo != null)
            {
                location += $".{memberInfo.Name}";
            }

            throw new InvalidOperationException(SR.Format(SR.SerializationConverterOnAttributeInvalid, location));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(Type classTypeAttributeIsOn, MemberInfo? memberInfo, Type typeToConvert)
        {
            string location = classTypeAttributeIsOn.ToString();

            if (memberInfo != null)
            {
                location += $".{memberInfo.Name}";
            }

            throw new InvalidOperationException(SR.Format(SR.SerializationConverterOnAttributeNotCompatible, location, typeToConvert));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerOptionsReadOnly(RdnSerializerContext? context)
        {
            string message = context == null
                ? SR.SerializerOptionsReadOnly
                : SR.SerializerContextOptionsReadOnly;

            throw new InvalidOperationException(message);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DefaultTypeInfoResolverImmutable()
        {
            throw new InvalidOperationException(SR.DefaultTypeInfoResolverImmutable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeInfoResolverChainImmutable()
        {
            throw new InvalidOperationException(SR.TypeInfoResolverChainImmutable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeInfoImmutable()
        {
            throw new InvalidOperationException(SR.TypeInfoImmutable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_InvalidChainedResolver()
        {
            throw new InvalidOperationException(SR.SerializerOptions_InvalidChainedResolver);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerPropertyNameConflict(Type type, string propertyName)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerPropertyNameConflict, type, propertyName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerPropertyNameNull(RdnPropertyInfo rdnPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerPropertyNameNull, rdnPropertyInfo.DeclaringType, rdnPropertyInfo.MemberName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnPropertyRequiredAndNotDeserializable(RdnPropertyInfo rdnPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.RdnPropertyRequiredAndNotDeserializable, rdnPropertyInfo.Name, rdnPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnPropertyRequiredAndExtensionData(RdnPropertyInfo rdnPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.RdnPropertyRequiredAndExtensionData, rdnPropertyInfo.Name, rdnPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_RdnRequiredPropertyMissing(RdnTypeInfo parent, BitArray assignedOrNotRequiredPropertiesSet)
        {
            StringBuilder listOfMissingPropertiesBuilder = new();
            bool first = true;

            // Soft cut-off length - once message becomes longer than that we won't be adding more elements
            const int CutOffLength = 60;

            foreach (RdnPropertyInfo property in parent.PropertyCache)
            {
                if (assignedOrNotRequiredPropertiesSet[property.PropertyIndex])
                {
                    continue;
                }

                if (!first)
                {
                    listOfMissingPropertiesBuilder.Append(CultureInfo.CurrentUICulture.TextInfo.ListSeparator);
                    listOfMissingPropertiesBuilder.Append(' ');
                }

                listOfMissingPropertiesBuilder.Append('\'');
                listOfMissingPropertiesBuilder.Append(property.Name);
                listOfMissingPropertiesBuilder.Append('\'');
                first = false;

                if (listOfMissingPropertiesBuilder.Length >= CutOffLength)
                {
                    break;
                }
            }

            throw new RdnException(SR.Format(SR.RdnRequiredPropertiesMissing, parent.Type, listOfMissingPropertiesBuilder.ToString()));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_DuplicatePropertyNotAllowed(RdnPropertyInfo property)
        {
            throw new RdnException(SR.Format(SR.DuplicatePropertiesNotAllowed_RdnPropertyInfo, property.Name, property.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_DuplicatePropertyNotAllowed()
        {
            throw new RdnException(SR.DuplicatePropertiesNotAllowed);
        }

        [DoesNotReturn]
        public static void ThrowRdnException_DuplicatePropertyNotAllowed(string name)
        {
            throw new RdnException(SR.Format(SR.DuplicatePropertiesNotAllowed_NameSpan, Truncate(name)));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_DuplicatePropertyNotAllowed(ReadOnlySpan<byte> nameBytes)
        {
            string name = Encoding.UTF8.GetString(nameBytes);
            throw new RdnException(SR.Format(SR.DuplicatePropertiesNotAllowed_NameSpan, Truncate(name)));
        }

        private static string Truncate(ReadOnlySpan<char> str)
        {
            const int MaxLength = 15;

            if (str.Length <= MaxLength)
            {
                return str.ToString();
            }

            Span<char> builder = stackalloc char[MaxLength + 3];
            str.Slice(0, MaxLength).CopyTo(builder);
            builder[MaxLength] = builder[MaxLength + 1] = builder[MaxLength + 2] = '.';
            return builder.ToString();
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NamingPolicyReturnNull(RdnNamingPolicy namingPolicy)
        {
            throw new InvalidOperationException(SR.Format(SR.NamingPolicyReturnNull, namingPolicy));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerConverterFactoryReturnsNull(Type converterType)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerConverterFactoryReturnsNull, converterType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerConverterFactoryReturnsRdnConverterFactory(Type converterType)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerConverterFactoryReturnsRdnConverterFactory, converterType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_MultiplePropertiesBindToConstructorParameters(
            Type parentType,
            string parameterName,
            string firstMatchName,
            string secondMatchName)
        {
            throw new InvalidOperationException(
                SR.Format(
                    SR.MultipleMembersBindWithConstructorParameter,
                    firstMatchName,
                    secondMatchName,
                    parentType,
                    parameterName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ConstructorParameterIncompleteBinding(Type parentType)
        {
            throw new InvalidOperationException(SR.Format(SR.ConstructorParamIncompleteBinding, parentType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExtensionDataCannotBindToCtorParam(string propertyName, RdnPropertyInfo rdnPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ExtensionDataCannotBindToCtorParam, propertyName, rdnPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnIncludeOnInaccessibleProperty(string memberName, Type declaringType)
        {
            throw new InvalidOperationException(SR.Format(SR.RdnIncludeOnInaccessibleProperty, memberName, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_IgnoreConditionOnValueTypeInvalid(string clrPropertyName, Type propertyDeclaringType)
        {
            throw new InvalidOperationException(SR.Format(SR.IgnoreConditionOnValueTypeInvalid, clrPropertyName, propertyDeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(RdnPropertyInfo rdnPropertyInfo)
        {
            Debug.Assert(!rdnPropertyInfo.IsForTypeInfo);
            throw new InvalidOperationException(SR.Format(SR.NumberHandlingOnPropertyInvalid, rdnPropertyInfo.MemberName, rdnPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ConverterCanConvertMultipleTypes(Type runtimePropertyType, RdnConverter rdnConverter)
        {
            throw new InvalidOperationException(SR.Format(SR.ConverterCanConvertMultipleTypes, rdnConverter.GetType(), rdnConverter.Type, runtimePropertyType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(
            ReadOnlySpan<byte> propertyName,
            ref Utf8RdnReader reader,
            scoped ref ReadStack state)
        {
            RdnTypeInfo rdnTypeInfo = state.GetTopRdnTypeInfoWithParameterizedConstructor();
            state.Current.RdnPropertyName = propertyName.ToArray();

            NotSupportedException ex = new NotSupportedException(
                SR.Format(SR.ObjectWithParameterizedCtorRefMetadataNotSupported, rdnTypeInfo.Type));
            ThrowNotSupportedException(ref state, reader, ex);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnTypeInfoOperationNotPossibleForKind(RdnTypeInfoKind kind)
        {
            throw new InvalidOperationException(SR.Format(SR.InvalidRdnTypeInfoOperationForKind, kind));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnTypeInfoOnDeserializingCallbacksNotSupported(Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.OnDeserializingCallbacksNotSupported, type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CreateObjectConverterNotCompatible(Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.CreateObjectConverterNotCompatible, type));
        }

        [DoesNotReturn]
        public static void ReThrowWithPath(scoped ref ReadStack state, RdnReaderException ex)
        {
            Debug.Assert(ex.Path == null);

            string path = state.RdnPath();
            string message = ex.Message;

            // Insert the "Path" portion before "LineNumber" and "BytePositionInLine".
#if NET
            int iPos = message.AsSpan().LastIndexOf(" LineNumber: ");
#else
            int iPos = message.LastIndexOf(" LineNumber: ", StringComparison.Ordinal);
#endif
            if (iPos >= 0)
            {
                message = $"{message.Substring(0, iPos)} Path: {path} |{message.Substring(iPos)}";
            }
            else
            {
                message += $" Path: {path}.";
            }

            throw new RdnException(message, path, ex.LineNumber, ex.BytePositionInLine, ex);
        }

        [DoesNotReturn]
        public static void ReThrowWithPath(scoped ref ReadStack state, in Utf8RdnReader reader, Exception ex)
        {
            RdnException rdnException = new RdnException(null, ex);
            AddRdnExceptionInformation(ref state, reader, rdnException);
            throw rdnException;
        }

        public static void AddRdnExceptionInformation(scoped ref ReadStack state, in Utf8RdnReader reader, RdnException ex)
        {
            Debug.Assert(ex.Path is null); // do not overwrite existing path information

            long lineNumber = reader.CurrentState._lineNumber;
            ex.LineNumber = lineNumber;

            long bytePositionInLine = reader.CurrentState._bytePositionInLine;
            ex.BytePositionInLine = bytePositionInLine;

            string path = state.RdnPath();
            ex.Path = path;

            string? message = ex._message;

            if (string.IsNullOrEmpty(message))
            {
                // Use a default message.
                Type propertyType = state.Current.RdnPropertyInfo?.PropertyType ?? state.Current.RdnTypeInfo.Type;
                message = SR.Format(SR.DeserializeUnableToConvertValue, propertyType);
                ex.AppendPathInformation = true;
            }

            if (ex.AppendPathInformation)
            {
                message += $" Path: {path} | LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";
                ex.SetMessage(message);
            }
        }

        [DoesNotReturn]
        public static void ReThrowWithPath(ref WriteStack state, Exception ex)
        {
            RdnException rdnException = new RdnException(null, ex);
            AddRdnExceptionInformation(ref state, rdnException);
            throw rdnException;
        }

        public static void AddRdnExceptionInformation(ref WriteStack state, RdnException ex)
        {
            Debug.Assert(ex.Path is null); // do not overwrite existing path information

            string path = state.PropertyPath();
            ex.Path = path;

            string? message = ex._message;
            if (string.IsNullOrEmpty(message))
            {
                // Use a default message.
                message = SR.SerializeUnableToSerialize;
                ex.AppendPathInformation = true;
            }

            if (ex.AppendPathInformation)
            {
                message += $" Path: {path}.";
                ex.SetMessage(message);
            }
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDuplicateAttribute(Type attribute, MemberInfo memberInfo)
        {
            string location = memberInfo is Type type ? type.ToString() : $"{memberInfo.DeclaringType}.{memberInfo.Name}";
            throw new InvalidOperationException(SR.Format(SR.SerializationDuplicateAttribute, attribute, location));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDuplicateTypeAttribute(Type classType, Type attribute)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationDuplicateTypeAttribute, classType, attribute));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDuplicateTypeAttribute<TAttribute>(Type classType)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationDuplicateTypeAttribute, classType, typeof(TAttribute)));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExtensionDataConflictsWithUnmappedMemberHandling(Type classType, RdnPropertyInfo rdnPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ExtensionDataConflictsWithUnmappedMemberHandling, classType, rdnPropertyInfo.MemberName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDataExtensionPropertyInvalid(RdnPropertyInfo rdnPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationDataExtensionPropertyInvalid, rdnPropertyInfo.PropertyType, rdnPropertyInfo.MemberName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PropertyTypeNotNullable(RdnPropertyInfo rdnPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.PropertyTypeNotNullable, rdnPropertyInfo.PropertyType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NodeRdnObjectCustomConverterNotAllowedOnExtensionProperty()
        {
            throw new InvalidOperationException(SR.NodeRdnObjectCustomConverterNotAllowedOnExtensionProperty);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException(scoped ref ReadStack state, in Utf8RdnReader reader, Exception innerException)
        {
            string message = innerException.Message;

            // The caller should check to ensure path is not already set.
            Debug.Assert(!message.Contains(" Path: "));

            // Obtain the type to show in the message.
            Type propertyType = state.Current.RdnPropertyInfo?.PropertyType ?? state.Current.RdnTypeInfo.Type;

            if (!message.Contains(propertyType.ToString()))
            {
                if (message.Length > 0)
                {
                    message += " ";
                }

                message += SR.Format(SR.SerializationNotSupportedParentType, propertyType);
            }

            long lineNumber = reader.CurrentState._lineNumber;
            long bytePositionInLine = reader.CurrentState._bytePositionInLine;
            message += $" Path: {state.RdnPath()} | LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";

            throw new NotSupportedException(message, innerException);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException(ref WriteStack state, Exception innerException)
        {
            string message = innerException.Message;

            // The caller should check to ensure path is not already set.
            Debug.Assert(!message.Contains(" Path: "));

            // Obtain the type to show in the message.
            Type propertyType = state.Current.RdnPropertyInfo?.PropertyType ?? state.Current.RdnTypeInfo.Type;

            if (!message.Contains(propertyType.ToString()))
            {
                if (message.Length > 0)
                {
                    message += " ";
                }

                message += SR.Format(SR.SerializationNotSupportedParentType, propertyType);
            }

            message += $" Path: {state.PropertyPath()}.";

            throw new NotSupportedException(message, innerException);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_DeserializeNoConstructor(RdnTypeInfo typeInfo, ref Utf8RdnReader reader, scoped ref ReadStack state)
        {
            Type type = typeInfo.Type;
            string message;

            if (type.IsInterface || type.IsAbstract)
            {
                if (typeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators is true)
                {
                    message = SR.Format(SR.DeserializationMustSpecifyTypeDiscriminator, type);
                }
                else if (typeInfo.Kind is RdnTypeInfoKind.Enumerable or RdnTypeInfoKind.Dictionary)
                {
                    message = SR.Format(SR.CannotPopulateCollection, type);
                }
                else
                {
                    message = SR.Format(SR.DeserializeInterfaceOrAbstractType, type);
                }
            }
            else
            {
                message = SR.Format(SR.DeserializeNoConstructor, nameof(RdnConstructorAttribute), type);
            }

            ThrowNotSupportedException(ref state, reader, new NotSupportedException(message));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_CannotPopulateCollection(Type type, ref Utf8RdnReader reader, scoped ref ReadStack state)
        {
            ThrowNotSupportedException(ref state, reader, new NotSupportedException(SR.Format(SR.CannotPopulateCollection, type)));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataValuesInvalidToken(RdnTokenType tokenType)
        {
            ThrowRdnException(SR.Format(SR.MetadataInvalidTokenAfterValues, tokenType));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataReferenceNotFound(string id)
        {
            ThrowRdnException(SR.Format(SR.MetadataReferenceNotFound, id));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataValueWasNotString(RdnTokenType tokenType)
        {
            ThrowRdnException(SR.Format(SR.MetadataValueWasNotString, tokenType));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataValueWasNotString(RdnValueKind valueKind)
        {
            ThrowRdnException(SR.Format(SR.MetadataValueWasNotString, valueKind));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataReferenceObjectCannotContainOtherProperties(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state)
        {
            state.Current.RdnPropertyName = propertyName.ToArray();
            ThrowRdnException_MetadataReferenceObjectCannotContainOtherProperties();
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataUnexpectedProperty(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state)
        {
            state.Current.RdnPropertyName = propertyName.ToArray();
            ThrowRdnException(SR.MetadataUnexpectedProperty);
        }

        [DoesNotReturn]
        public static void ThrowRdnException_UnmappedRdnProperty(Type type, string unmappedPropertyName)
        {
            throw new RdnException(SR.Format(SR.UnmappedRdnProperty, unmappedPropertyName, type));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataReferenceObjectCannotContainOtherProperties()
        {
            ThrowRdnException(SR.MetadataReferenceCannotContainOtherProperties);
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataIdCannotBeCombinedWithRef(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state)
        {
            state.Current.RdnPropertyName = propertyName.ToArray();
            ThrowRdnException(SR.MetadataIdCannotBeCombinedWithRef);
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataStandaloneValuesProperty(scoped ref ReadStack state, ReadOnlySpan<byte> propertyName)
        {
            state.Current.RdnPropertyName = propertyName.ToArray();
            ThrowRdnException(SR.MetadataStandaloneValuesProperty);
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataInvalidPropertyWithLeadingDollarSign(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state, in Utf8RdnReader reader)
        {
            // Set PropertyInfo or KeyName to write down the conflicting property name in RdnException.Path
            if (state.Current.IsProcessingDictionary())
            {
                state.Current.RdnPropertyNameAsString = reader.GetString();
            }
            else
            {
                state.Current.RdnPropertyName = propertyName.ToArray();
            }

            ThrowRdnException(SR.MetadataInvalidPropertyWithLeadingDollarSign);
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataDuplicateIdFound(string id)
        {
            ThrowRdnException(SR.Format(SR.MetadataDuplicateIdFound, id));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_DuplicateMetadataProperty(ReadOnlySpan<byte> utf8PropertyName)
        {
            ThrowRdnException(SR.Format(SR.DuplicateMetadataProperty, Encoding.UTF8.GetString(utf8PropertyName)));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataInvalidReferenceToValueType(Type propertyType)
        {
            ThrowRdnException(SR.Format(SR.MetadataInvalidReferenceToValueType, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataInvalidPropertyInArrayMetadata(scoped ref ReadStack state, Type propertyType, in Utf8RdnReader reader)
        {
            state.Current.RdnPropertyName = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray();
            string propertyNameAsString = reader.GetString()!;

            ThrowRdnException(SR.Format(SR.MetadataPreservedArrayFailed,
                SR.Format(SR.MetadataInvalidPropertyInArrayMetadata, propertyNameAsString),
                SR.Format(SR.DeserializeUnableToConvertValue, propertyType)));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataPreservedArrayValuesNotFound(scoped ref ReadStack state, Type propertyType)
        {
            // Missing $values, RDN path should point to the property's object.
            state.Current.RdnPropertyName = null;

            ThrowRdnException(SR.Format(SR.MetadataPreservedArrayFailed,
                SR.MetadataStandaloneValuesProperty,
                SR.Format(SR.DeserializeUnableToConvertValue, propertyType)));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_MetadataCannotParsePreservedObjectIntoImmutable(Type propertyType)
        {
            ThrowRdnException(SR.Format(SR.MetadataCannotParsePreservedObjectToImmutable, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_MetadataReferenceOfTypeCannotBeAssignedToType(string referenceId, Type currentType, Type typeToConvert)
        {
            throw new InvalidOperationException(SR.Format(SR.MetadataReferenceOfTypeCannotBeAssignedToType, referenceId, currentType, typeToConvert));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnPropertyInfoIsBoundToDifferentRdnTypeInfo(RdnPropertyInfo propertyInfo)
        {
            Debug.Assert(propertyInfo.DeclaringTypeInfo != null, "We should not throw this exception when ParentTypeInfo is null");
            throw new InvalidOperationException(SR.Format(SR.RdnPropertyInfoBoundToDifferentParent, propertyInfo.Name, propertyInfo.DeclaringTypeInfo.Type.FullName));
        }

        [DoesNotReturn]
        internal static void ThrowUnexpectedMetadataException(
            ReadOnlySpan<byte> propertyName,
            ref Utf8RdnReader reader,
            scoped ref ReadStack state)
        {

            MetadataPropertyName name = RdnSerializer.GetMetadataPropertyName(propertyName, state.Current.BaseRdnTypeInfo.PolymorphicTypeResolver);
            if (name != 0)
            {
                ThrowRdnException_MetadataUnexpectedProperty(propertyName, ref state);
            }
            else
            {
                ThrowRdnException_MetadataInvalidPropertyWithLeadingDollarSign(propertyName, ref state, reader);
            }
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_NoMetadataForType(Type type, IRdnTypeInfoResolver? resolver)
        {
            throw new NotSupportedException(SR.Format(SR.NoMetadataForType, type, resolver?.ToString() ?? "<null>"));
        }

        public static NotSupportedException GetNotSupportedException_AmbiguousMetadataForType(Type type, Type match1, Type match2)
        {
            return new NotSupportedException(SR.Format(SR.AmbiguousMetadataForType, type, match1, match2));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_ConstructorContainsNullParameterNames(Type declaringType)
        {
            throw new NotSupportedException(SR.Format(SR.ConstructorContainsNullParameterNames, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NoMetadataForType(Type type, IRdnTypeInfoResolver? resolver)
        {
            throw new InvalidOperationException(SR.Format(SR.NoMetadataForType, type, resolver?.ToString() ?? "<null>"));
        }

        public static Exception GetInvalidOperationException_NoMetadataForTypeProperties(IRdnTypeInfoResolver? resolver, Type type)
        {
            return new InvalidOperationException(SR.Format(SR.NoMetadataForTypeProperties, resolver?.ToString() ?? "<null>", type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NoMetadataForTypeProperties(IRdnTypeInfoResolver? resolver, Type type)
        {
            throw GetInvalidOperationException_NoMetadataForTypeProperties(resolver, type);
        }

        [DoesNotReturn]
        public static void ThrowMissingMemberException_MissingFSharpCoreMember(string missingFsharpCoreMember)
        {
            throw new MissingMemberException(SR.Format(SR.MissingFSharpCoreMember, missingFsharpCoreMember));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_BaseConverterDoesNotSupportMetadata(Type derivedType)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_DerivedConverterDoesNotSupportMetadata, derivedType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_DerivedConverterDoesNotSupportMetadata(Type derivedType)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_DerivedConverterDoesNotSupportMetadata, derivedType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_RuntimeTypeNotSupported(Type baseType, Type runtimeType)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_RuntimeTypeNotSupported, runtimeType, baseType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_RuntimeTypeDiamondAmbiguity(Type baseType, Type runtimeType, Type derivedType1, Type derivedType2)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_RuntimeTypeDiamondAmbiguity, runtimeType, derivedType1, derivedType2, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeDoesNotSupportPolymorphism(Type baseType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_TypeDoesNotSupportPolymorphism, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DerivedTypeNotSupported(Type baseType, Type derivedType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_DerivedTypeIsNotSupported, derivedType, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DerivedTypeIsAlreadySpecified(Type baseType, Type derivedType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_DerivedTypeIsAlreadySpecified, baseType, derivedType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeDicriminatorIdIsAlreadySpecified(Type baseType, object typeDiscriminator)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_TypeDicriminatorIdIsAlreadySpecified, baseType, typeDiscriminator));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_InvalidCustomTypeDiscriminatorPropertyName()
        {
            throw new InvalidOperationException(SR.Polymorphism_InvalidCustomTypeDiscriminatorPropertyName);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PropertyConflictsWithMetadataPropertyName(Type type, string propertyName)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_PropertyConflictsWithMetadataPropertyName, type, propertyName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PolymorphicTypeConfigurationDoesNotSpecifyDerivedTypes(Type baseType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_ConfigurationDoesNotSpecifyDerivedTypes, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_UnsupportedEnumIdentifier(Type enumType, string? enumName)
        {
            throw new InvalidOperationException(SR.Format(SR.UnsupportedEnumIdentifier, enumType.Name, enumName));
        }

        [DoesNotReturn]
        public static void ThrowRdnException_UnrecognizedTypeDiscriminator(object typeDiscriminator)
        {
            ThrowRdnException(SR.Format(SR.Polymorphism_UnrecognizedTypeDiscriminator, typeDiscriminator));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_RdnPolymorphismOptionsAssociatedWithDifferentRdnTypeInfo(string parameterName)
        {
            throw new ArgumentException(SR.RdnPolymorphismOptionsAssociatedWithDifferentRdnTypeInfo, paramName: parameterName);
        }

        [DoesNotReturn]
        public static void ThrowOperationCanceledException_PipeWriteCanceled()
        {
            throw new OperationCanceledException(SR.PipeWriterCanceled);
        }

        [DoesNotReturn]
        public static void ThrowOperationCanceledException_PipeReadCanceled()
        {
            throw new OperationCanceledException(SR.PipeReaderCanceled);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PipeWriterDoesNotImplementUnflushedBytes(PipeWriter pipeWriter)
        {
            throw new InvalidOperationException(SR.Format(SR.PipeWriter_DoesNotImplementUnflushedBytes, pipeWriter.GetType().Name));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_RdnSchemaExporterDoesNotSupportReferenceHandlerPreserve()
        {
            throw new NotSupportedException(SR.RdnSchemaExporter_ReferenceHandlerPreserve_NotSupported);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RdnSchemaExporterDepthTooLarge()
        {
            throw new InvalidOperationException(SR.RdnSchemaExporter_DepthTooLarge);
        }
    }
}
