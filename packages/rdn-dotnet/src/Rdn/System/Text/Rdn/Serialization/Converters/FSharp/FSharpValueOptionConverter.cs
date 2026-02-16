// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    // Converter for F# struct optional values: https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpvalueoption-1.html
    // Serializes `ValueSome(value)` using the format of `value` and `ValueNone` values as `null`.
    internal sealed class FSharpValueOptionConverter<TValueOption, TElement> : RdnConverter<TValueOption>
        where TValueOption : struct, IEquatable<TValueOption>
    {
        internal override Type? ElementType => typeof(TElement);
        internal override RdnConverter? NullableElementConverter => _elementConverter;
        // 'ValueNone' is encoded using 'default' at runtime and serialized as 'null' in RDN.
        public override bool HandleNull => true;

        private readonly RdnConverter<TElement> _elementConverter;
        private readonly FSharpCoreReflectionProxy.StructGetter<TValueOption, TElement> _optionValueGetter;
        private readonly Func<TElement?, TValueOption> _optionConstructor;

        [RequiresUnreferencedCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        [RequiresDynamicCode(FSharpCoreReflectionProxy.FSharpCoreUnreferencedCodeMessage)]
        public FSharpValueOptionConverter(RdnConverter<TElement> elementConverter)
        {
            _elementConverter = elementConverter;
            _optionValueGetter = FSharpCoreReflectionProxy.Instance.CreateFSharpValueOptionValueGetter<TValueOption, TElement>();
            _optionConstructor = FSharpCoreReflectionProxy.Instance.CreateFSharpValueOptionSomeConstructor<TValueOption, TElement>();
            ConverterStrategy = elementConverter.ConverterStrategy;
        }

        internal override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, out TValueOption value)
        {
            // `null` values deserialize as `ValueNone`
            if (!state.IsContinuation && reader.TokenType == RdnTokenType.Null)
            {
                value = default;
                return true;
            }

            state.Current.RdnPropertyInfo = state.Current.RdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            if (_elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, out TElement? element, out _))
            {
                value = _optionConstructor(element);
                return true;
            }

            value = default;
            return false;
        }

        internal override bool OnTryWrite(Utf8RdnWriter writer, TValueOption value, RdnSerializerOptions options, ref WriteStack state)
        {
            if (value.Equals(default))
            {
                // Write `ValueNone` values as null
                writer.WriteNullValue();
                return true;
            }

            TElement element = _optionValueGetter(ref value);

            state.Current.RdnPropertyInfo = state.Current.RdnTypeInfo.ElementTypeInfo!.PropertyInfoForTypeInfo;
            return _elementConverter.TryWrite(writer, element, options, ref state);
        }

        // Since this is a hybrid converter (ConverterStrategy depends on the element converter),
        // we need to override the value converter Write and Read methods too.

        public override void Write(Utf8RdnWriter writer, TValueOption value, RdnSerializerOptions options)
        {
            if (value.Equals(default))
            {
                // Write `ValueNone` values as null
                writer.WriteNullValue();
            }
            else
            {
                TElement element = _optionValueGetter(ref value);
                _elementConverter.Write(writer, element, options);
            }
        }

        public override TValueOption Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.Null)
            {
                return default;
            }

            TElement? element = _elementConverter.Read(ref reader, typeToConvert, options);
            return _optionConstructor(element);
        }
    }
}
