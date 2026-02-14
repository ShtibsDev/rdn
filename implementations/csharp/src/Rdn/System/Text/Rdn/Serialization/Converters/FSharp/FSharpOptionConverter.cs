// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    // Converter for F# optional values: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-option-1.html
    // Serializes `Some(value)` using the format of `value` and `None` values as `null`.
    internal sealed class FSharpOptionConverter<TOption, TElement> : RdnConverter<TOption>
        where TOption : class
    {
        internal override Type? ElementType => typeof(TElement);
        internal override RdnConverter? NullableElementConverter => _elementConverter;
        // 'None' is encoded using 'null' at runtime and serialized as 'null' in RDN.
        public override bool HandleNull => true;

        private readonly RdnConverter<TElement> _elementConverter;
        private readonly Func<TOption, TElement> _optionValueGetter;
        private readonly Func<TElement?, TOption> _optionConstructor;

        [RequiresUnreferencedCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        [RequiresDynamicCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        public FSharpOptionConverter(RdnConverter<TElement> elementConverter)
        {
            _elementConverter = elementConverter;
            _optionValueGetter = FSharpCoreReflectionProxy.Instance.CreateFSharpOptionValueGetter<TOption, TElement>();
            _optionConstructor = FSharpCoreReflectionProxy.Instance.CreateFSharpOptionSomeConstructor<TOption, TElement>();
            ConverterStrategy = elementConverter.ConverterStrategy;
        }

        internal override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, out TOption? value)
        {
            // `null` values deserialize as `None`
            if (!state.IsContinuation && reader.TokenType == RdnTokenType.Null)
            {
                value = null;
                return true;
            }

            state.Current.RdnPropertyInfo = state.Current.RdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            if (_elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _))
            {
                value = _optionConstructor(element);
                return true;
            }

            value = null;
            return false;
        }

        internal override bool OnTryWrite(Utf8RdnWriter writer, TOption value, RdnSerializerOptions options, ref WriteStack state)
        {
            if (value is null)
            {
                // Write `None` values as null
                writer.WriteNullValue();
                return true;
            }

            TElement element = _optionValueGetter(value);
            state.Current.RdnPropertyInfo = state.Current.RdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            return _elementConverter.TryWrite(writer, element, options, ref state);
        }

        // Since this is a hybrid converter (ConverterStrategy depends on the element converter),
        // we need to override the value converter Write and Read methods too.

        public override void Write(Utf8RdnWriter writer, TOption value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                TElement element = _optionValueGetter(value);
                _elementConverter.Write(writer, element, options);
            }
        }

        public override TOption? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.Null)
            {
                return null;
            }

            TElement? element = _elementConverter.Read(ref reader, typeToConvert, options);
            return _optionConstructor(element);
        }
    }
}
