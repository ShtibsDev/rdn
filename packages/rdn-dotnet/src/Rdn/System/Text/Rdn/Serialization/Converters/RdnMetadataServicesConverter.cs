// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    /// <summary>
    /// Provides a mechanism to invoke "fast-path" serialization logic via
    /// <see cref="RdnTypeInfo{T}.SerializeHandler"/>. This type holds an optional
    /// reference to an actual <see cref="RdnConverter{T}"/> for the type
    /// <typeparamref name="T"/>, to provide a fallback when the fast path cannot be used.
    /// </summary>
    /// <typeparam name="T">The type to converter</typeparam>
    internal sealed class RdnMetadataServicesConverter<T> : RdnResumableConverter<T>
    {
        // A backing converter for when fast-path logic cannot be used.
        internal RdnConverter<T> Converter { get; }

        internal override Type? KeyType => Converter.KeyType;
        internal override Type? ElementType => Converter.ElementType;
        internal override RdnConverter? NullableElementConverter => Converter.NullableElementConverter;
        public override bool HandleNull { get; }

        internal override bool ConstructorIsParameterized => Converter.ConstructorIsParameterized;
        internal override bool SupportsCreateObjectDelegate => Converter.SupportsCreateObjectDelegate;
        internal override bool CanHaveMetadata => Converter.CanHaveMetadata;

        internal override bool CanPopulate => Converter.CanPopulate;

        public RdnMetadataServicesConverter(RdnConverter<T> converter)
        {
            Converter = converter;
            ConverterStrategy = converter.ConverterStrategy;
            IsInternalConverter = converter.IsInternalConverter;
            IsInternalConverterForNumberType = converter.IsInternalConverterForNumberType;
            CanBePolymorphic = converter.CanBePolymorphic;

            // Ensure HandleNull values reflect the exact configuration of the source converter
            HandleNullOnRead = converter.HandleNullOnRead;
            HandleNullOnWrite = converter.HandleNullOnWrite;
            HandleNull = converter.HandleNullOnWrite;
        }

        internal override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, out T? value)
             => Converter.OnTryRead(ref reader, typeToConvert, options, ref state, out value);

        internal override bool OnTryWrite(Utf8RdnWriter writer, T value, RdnSerializerOptions options, ref WriteStack state)
        {
            RdnTypeInfo rdnTypeInfo = state.Current.RdnTypeInfo;
            Debug.Assert(rdnTypeInfo is RdnTypeInfo<T> typeInfo && typeInfo.SerializeHandler != null);

            if (!state.SupportContinuation &&
                rdnTypeInfo.CanUseSerializeHandler &&
                !RdnHelpers.RequiresSpecialNumberHandlingOnWrite(state.Current.NumberHandling) &&
                !state.CurrentContainsMetadata) // Do not use the fast path if state needs to write metadata.
            {
                ((RdnTypeInfo<T>)rdnTypeInfo).SerializeHandler!(writer, value);
                return true;
            }

            return Converter.OnTryWrite(writer, value, options, ref state);
        }

        internal override void ConfigureRdnTypeInfo(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
            => Converter.ConfigureRdnTypeInfo(rdnTypeInfo, options);

        internal override RdnSchema? GetSchema(RdnNumberHandling numberHandling)
            => Converter.GetSchema(numberHandling);
    }
}
