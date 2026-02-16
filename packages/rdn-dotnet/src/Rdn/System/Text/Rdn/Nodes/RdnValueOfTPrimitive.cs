// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Rdn.Serialization;

namespace Rdn.Nodes
{
    /// <summary>
    /// A RdnValue encapsulating a primitive value using a built-in converter for the type.
    /// </summary>
    internal sealed class RdnValuePrimitive<TValue> : RdnValue<TValue>
    {
        private readonly RdnConverter<TValue> _converter;
        private readonly RdnValueKind _valueKind;

        public RdnValuePrimitive(TValue value, RdnConverter<TValue> converter, RdnNodeOptions? options) : base(value, options)
        {
            Debug.Assert(TypeIsSupportedPrimitive, $"The type {typeof(TValue)} is not a supported primitive.");
            Debug.Assert(converter is { IsInternalConverter: true, ConverterStrategy: ConverterStrategy.Value });

            _converter = converter;
            _valueKind = DetermineValueKind(value);
        }

        private protected override RdnValueKind GetValueKindCore() => _valueKind;
        internal override RdnNode DeepCloneCore() => new RdnValuePrimitive<TValue>(Value, _converter, Options);

        internal override bool DeepEqualsCore(RdnNode otherNode)
        {
            if (otherNode is RdnValue otherValue && otherValue.TryGetValue(out TValue? v))
            {
                // Because TValue is equatable and otherNode returns a matching
                // type we can short circuit the comparison in this case.
                return EqualityComparer<TValue>.Default.Equals(Value, v);
            }

            return base.DeepEqualsCore(otherNode);
        }

        public override void WriteTo(Utf8RdnWriter writer, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            RdnConverter<TValue> converter = _converter;
            options ??= s_defaultOptions;

            if (converter.IsInternalConverterForNumberType)
            {
                converter.WriteNumberWithCustomHandling(writer, Value, options.NumberHandling);
            }
            else
            {
                converter.Write(writer, Value, options);
            }
        }
    }
}
