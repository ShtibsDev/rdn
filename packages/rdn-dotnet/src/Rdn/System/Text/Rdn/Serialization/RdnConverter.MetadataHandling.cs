// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization
{
    public partial class RdnConverter
    {
        /// <summary>
        /// Initializes the state for polymorphic cases and returns the appropriate derived converter.
        /// </summary>
        internal RdnConverter? ResolvePolymorphicConverter(RdnTypeInfo rdnTypeInfo, ref ReadStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(CanHaveMetadata);
            Debug.Assert((state.Current.MetadataPropertyNames & MetadataPropertyName.Type) != 0);
            Debug.Assert(state.Current.PolymorphicSerializationState != PolymorphicSerializationState.PolymorphicReEntryStarted);
            Debug.Assert(rdnTypeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators == true);

            RdnConverter? polymorphicConverter = null;

            switch (state.Current.PolymorphicSerializationState)
            {
                case PolymorphicSerializationState.None:
                    Debug.Assert(!state.IsContinuation);
                    Debug.Assert(state.PolymorphicTypeDiscriminator != null);

                    PolymorphicTypeResolver resolver = rdnTypeInfo.PolymorphicTypeResolver;
                    if (resolver.TryGetDerivedRdnTypeInfo(state.PolymorphicTypeDiscriminator, out RdnTypeInfo? resolvedType))
                    {
                        Debug.Assert(Type!.IsAssignableFrom(resolvedType.Type));

                        polymorphicConverter = state.InitializePolymorphicReEntry(resolvedType);
                        if (!polymorphicConverter.CanHaveMetadata)
                        {
                            ThrowHelper.ThrowNotSupportedException_DerivedConverterDoesNotSupportMetadata(resolvedType.Type);
                        }
                    }
                    else
                    {
                        state.Current.PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryNotFound;
                    }

                    state.PolymorphicTypeDiscriminator = null;
                    break;

                case PolymorphicSerializationState.PolymorphicReEntrySuspended:
                    polymorphicConverter = state.ResumePolymorphicReEntry();
                    Debug.Assert(Type!.IsAssignableFrom(polymorphicConverter.Type));
                    break;

                case PolymorphicSerializationState.PolymorphicReEntryNotFound:
                    Debug.Assert(state.Current.PolymorphicRdnTypeInfo is null);
                    break;

                default:
                    Debug.Fail("Unexpected PolymorphicSerializationState.");
                    break;
            }

            return polymorphicConverter;
        }

        /// <summary>
        /// Initializes the state for polymorphic cases and returns the appropriate derived converter.
        /// </summary>
        internal RdnConverter? ResolvePolymorphicConverter(object value, RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options, ref WriteStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(value != null && Type!.IsAssignableFrom(value.GetType()));
            Debug.Assert(CanBePolymorphic || rdnTypeInfo.PolymorphicTypeResolver != null);
            Debug.Assert(state.PolymorphicTypeDiscriminator is null);

            RdnConverter? polymorphicConverter = null;

            switch (state.Current.PolymorphicSerializationState)
            {
                case PolymorphicSerializationState.None:
                    Debug.Assert(!state.IsContinuation);

                    Type runtimeType = value.GetType();

                    if (CanBePolymorphic && runtimeType != Type)
                    {
                        Debug.Assert(Type == typeof(object));
                        rdnTypeInfo = state.Current.InitializePolymorphicReEntry(runtimeType, options);
                        polymorphicConverter = rdnTypeInfo.Converter;
                    }

                    if (rdnTypeInfo.PolymorphicTypeResolver is PolymorphicTypeResolver resolver)
                    {
                        Debug.Assert(rdnTypeInfo.Converter.CanHaveMetadata);

                        if (resolver.TryGetDerivedRdnTypeInfo(runtimeType, out RdnTypeInfo? derivedRdnTypeInfo, out object? typeDiscriminator))
                        {
                            polymorphicConverter = state.Current.InitializePolymorphicReEntry(derivedRdnTypeInfo);

                            if (typeDiscriminator is not null)
                            {
                                if (!polymorphicConverter.CanHaveMetadata)
                                {
                                    ThrowHelper.ThrowNotSupportedException_DerivedConverterDoesNotSupportMetadata(derivedRdnTypeInfo.Type);
                                }

                                state.PolymorphicTypeDiscriminator = typeDiscriminator;
                                state.PolymorphicTypeResolver = resolver;
                            }
                        }
                    }

                    if (polymorphicConverter is null)
                    {
                        state.Current.PolymorphicSerializationState = PolymorphicSerializationState.PolymorphicReEntryNotFound;
                    }

                    break;

                case PolymorphicSerializationState.PolymorphicReEntrySuspended:
                    Debug.Assert(state.IsContinuation);
                    polymorphicConverter = state.Current.ResumePolymorphicReEntry();
                    Debug.Assert(Type.IsAssignableFrom(polymorphicConverter.Type));
                    break;

                case PolymorphicSerializationState.PolymorphicReEntryNotFound:
                    Debug.Assert(state.IsContinuation);
                    break;

                default:
                    Debug.Fail("Unexpected PolymorphicSerializationState.");
                    break;
            }

            return polymorphicConverter;
        }

        internal bool TryHandleSerializedObjectReference(Utf8RdnWriter writer, object value, RdnSerializerOptions options, RdnConverter? polymorphicConverter, ref WriteStack state)
        {
            Debug.Assert(!IsValueType);
            Debug.Assert(!state.IsContinuation);
            Debug.Assert(value != null);

            switch (options.ReferenceHandlingStrategy)
            {
                case RdnKnownReferenceHandler.IgnoreCycles:
                    ReferenceResolver resolver = state.ReferenceResolver;
                    if (resolver.ContainsReferenceForCycleDetection(value))
                    {
                        writer.WriteNullValue();

                        if (polymorphicConverter is not null)
                        {
                            // Clear out any polymorphic state.
                            state.PolymorphicTypeDiscriminator = null;
                            state.PolymorphicTypeResolver = null;
                        }
                        return true;
                    }

                    resolver.PushReferenceForCycleDetection(value);
                    // WriteStack reuses root-level stack frames for its children as a performance optimization;
                    // we want to avoid writing any data for the root-level object to avoid corrupting the stack.
                    // This is fine since popping the root object at the end of serialization is not essential.
                    state.Current.IsPushedReferenceForCycleDetection = state.CurrentDepth > 0;
                    break;

                case RdnKnownReferenceHandler.Preserve:
                    bool canHaveIdMetadata = polymorphicConverter?.CanHaveMetadata ?? CanHaveMetadata;
                    if (canHaveIdMetadata && RdnSerializer.TryGetReferenceForValue(value, ref state, writer))
                    {
                        // We found a repeating reference and wrote the relevant metadata; serialization complete.
                        return true;
                    }
                    break;

                default:
                    Debug.Fail("Unexpected ReferenceHandlingStrategy.");
                    break;
            }

            return false;
        }
    }
}
