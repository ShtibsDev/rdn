// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class NullableConverter<T> : RdnConverter<T?> where T : struct // Do not rename FQN (legacy schema generation)
    {
        internal override Type? ElementType => typeof(T);
        internal override RdnConverter? NullableElementConverter => _elementConverter;
        public override bool HandleNull => true;
        internal override bool CanPopulate => _elementConverter.CanPopulate;
        internal override bool ConstructorIsParameterized => _elementConverter.ConstructorIsParameterized;

        // It is possible to cache the underlying converter since this is an internal converter and
        // an instance is created only once for each RdnSerializerOptions instance.
        private readonly RdnConverter<T> _elementConverter; // Do not rename (legacy schema generation)

        public NullableConverter(RdnConverter<T> elementConverter)
        {
            _elementConverter = elementConverter;
            IsInternalConverter = elementConverter.IsInternalConverter;
            IsInternalConverterForNumberType = elementConverter.IsInternalConverterForNumberType;
            ConverterStrategy = elementConverter.ConverterStrategy;
        }

        internal override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, out T? value)
        {
            if (!state.IsContinuation && reader.TokenType == RdnTokenType.Null)
            {
                value = null;
                return true;
            }

            RdnTypeInfo previousTypeInfo = state.Current.RdnTypeInfo;
            state.Current.RdnTypeInfo = state.Current.RdnTypeInfo.ElementTypeInfo!;
            if (_elementConverter.OnTryRead(ref reader, typeof(T), options, ref state, out T element))
            {
                value = element;
                state.Current.RdnTypeInfo = previousTypeInfo;
                return true;
            }

            state.Current.RdnTypeInfo = previousTypeInfo;
            value = null;
            return false;
        }

        internal override bool OnTryWrite(Utf8RdnWriter writer, T? value, RdnSerializerOptions options, ref WriteStack state)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return true;
            }

            state.Current.RdnPropertyInfo = state.Current.RdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            return _elementConverter.TryWrite(writer, value.Value, options, ref state);
        }

        public override T? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.Null)
            {
                return null;
            }

            T value = _elementConverter.Read(ref reader, typeof(T), options);
            return value;
        }

        public override void Write(Utf8RdnWriter writer, T? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                _elementConverter.Write(writer, value.Value, options);
            }
        }

        internal override T? ReadNumberWithCustomHandling(ref Utf8RdnReader reader, RdnNumberHandling numberHandling, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.Null)
            {
                return null;
            }

            T value = _elementConverter.ReadNumberWithCustomHandling(ref reader, numberHandling, options);
            return value;
        }

        internal override void WriteNumberWithCustomHandling(Utf8RdnWriter writer, T? value, RdnNumberHandling handling)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                _elementConverter.WriteNumberWithCustomHandling(writer, value.Value, handling);
            }
        }
    }
}
