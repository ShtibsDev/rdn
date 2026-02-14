// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rdn.Nodes
{
    [DebuggerDisplay("{ToRdnString(),nq}")]
    [DebuggerTypeProxy(typeof(RdnValue<>.DebugView))]
    internal abstract class RdnValue<TValue> : RdnValue
    {
        internal readonly TValue Value; // keep as a field for direct access to avoid copies

        protected RdnValue(TValue value, RdnNodeOptions? options) : base(options)
        {
            Debug.Assert(value != null);
            Debug.Assert(value is not RdnElement or RdnElement { ValueKind: not RdnValueKind.Null });
            Debug.Assert(value is not RdnNode);
            Value = value;
        }

        public override T GetValue<T>()
        {
            // If no conversion is needed, just return the raw value.
            if (Value is T returnValue)
            {
                return returnValue;
            }

            // Currently we do not support other conversions.
            // Generics (and also boxing) do not support standard cast operators say from 'long' to 'int',
            //  so attempting to cast here would throw InvalidCastException.
            ThrowHelper.ThrowInvalidOperationException_NodeUnableToConvert(typeof(TValue), typeof(T));
            return default!;
        }

        public override bool TryGetValue<T>([NotNullWhen(true)] out T value)
        {
            // If no conversion is needed, just return the raw value.
            if (Value is T returnValue)
            {
                value = returnValue;
                return true;
            }

            // Currently we do not support other conversions.
            // Generics (and also boxing) do not support standard cast operators say from 'long' to 'int',
            //  so attempting to cast here would throw InvalidCastException.
            value = default!;
            return false;
        }

        /// <summary>
        /// Whether <typeparamref name="TValue"/> is a built-in type that admits primitive RdnValue representation.
        /// </summary>
        internal static bool TypeIsSupportedPrimitive => s_valueKind.HasValue;
        private static readonly RdnValueKind? s_valueKind = DetermineValueKindForType(typeof(TValue));

        /// <summary>
        /// Determines the RdnValueKind for the value of a built-in type.
        /// </summary>
        private protected static RdnValueKind DetermineValueKind(TValue value)
        {
            Debug.Assert(s_valueKind is not null, "Should only be invoked for types that are supported primitives.");

            if (value is bool boolean)
            {
                // Boolean requires special handling since kind varies by value.
                return boolean ? RdnValueKind.True : RdnValueKind.False;
            }

            return s_valueKind.Value;
        }

        /// <summary>
        /// Precomputes the RdnValueKind for a given built-in type where possible.
        /// </summary>
        private static RdnValueKind? DetermineValueKindForType(Type type)
        {
            if (type.IsEnum)
            {
                return null; // Can vary depending on converter configuration and value.
            }

            if (Nullable.GetUnderlyingType(type) is Type underlyingType)
            {
                // Because RdnNode excludes null values, we can identify with the value kind of the underlying type.
                return DetermineValueKindForType(underlyingType);
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
#if NET
                type == typeof(DateOnly) || type == typeof(TimeOnly) ||
#endif
                type == typeof(Guid) || type == typeof(Uri) || type == typeof(Version))
            {
                return RdnValueKind.String;
            }

            if (type == typeof(System.Text.RegularExpressions.Regex))
            {
                return RdnValueKind.RdnRegExp;
            }

#if NET
            if (type == typeof(Half) || type == typeof(UInt128) || type == typeof(Int128))
            {
                return RdnValueKind.Number;
            }
#endif
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Boolean => RdnValueKind.Undefined, // Can vary dependending on value.
                TypeCode.SByte => RdnValueKind.Number,
                TypeCode.Byte => RdnValueKind.Number,
                TypeCode.Int16 => RdnValueKind.Number,
                TypeCode.UInt16 => RdnValueKind.Number,
                TypeCode.Int32 => RdnValueKind.Number,
                TypeCode.UInt32 => RdnValueKind.Number,
                TypeCode.Int64 => RdnValueKind.Number,
                TypeCode.UInt64 => RdnValueKind.Number,
                TypeCode.Single => RdnValueKind.Number,
                TypeCode.Double => RdnValueKind.Number,
                TypeCode.Decimal => RdnValueKind.Number,
                TypeCode.String => RdnValueKind.String,
                TypeCode.Char => RdnValueKind.String,
                _ => null,
            };
        }

        [ExcludeFromCodeCoverage] // Justification = "Design-time"
        [DebuggerDisplay("{Rdn,nq}")]
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public RdnValue<TValue> _node;

            public DebugView(RdnValue<TValue> node)
            {
                _node = node;
            }

            public string Rdn => _node.ToRdnString();
            public string Path => _node.GetPath();
            public TValue? Value => _node.Value;
        }
    }
}
