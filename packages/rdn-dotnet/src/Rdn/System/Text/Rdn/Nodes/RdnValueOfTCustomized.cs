// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Nodes
{
    /// <summary>
    /// A RdnValue that encapsulates arbitrary .NET type configurations.
    /// Paradoxically, instances of this type can be of any RdnValueKind
    /// (including objects and arrays) and introspecting these values is
    /// generally slower compared to the other RdnValue implementations.
    /// </summary>
    internal sealed class RdnValueCustomized<TValue> : RdnValue<TValue>
    {
        private readonly RdnTypeInfo<TValue> _rdnTypeInfo;
        private RdnValueKind? _valueKind;

        public RdnValueCustomized(TValue value, RdnTypeInfo<TValue> rdnTypeInfo, RdnNodeOptions? options = null): base(value, options)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            _rdnTypeInfo = rdnTypeInfo;
        }

        private protected override RdnValueKind GetValueKindCore() => _valueKind ??= ComputeValueKind();
        internal override RdnNode DeepCloneCore() => RdnSerializer.SerializeToNode(Value, _rdnTypeInfo)!;

        public override void WriteTo(Utf8RdnWriter writer, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            RdnTypeInfo<TValue> rdnTypeInfo = _rdnTypeInfo;

            if (options != null && options != rdnTypeInfo.Options)
            {
                options.MakeReadOnly();
                rdnTypeInfo = (RdnTypeInfo<TValue>)options.GetTypeInfoInternal(typeof(TValue));
            }

            rdnTypeInfo.Serialize(writer, Value);
        }

        /// <summary>
        /// Computes the RdnValueKind of the value by serializing it and reading the resultant RDN.
        /// </summary>
        private RdnValueKind ComputeValueKind()
        {
            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(options: default, RdnSerializerOptions.BufferSizeDefault, out PooledByteBufferWriter output);
            try
            {
                WriteTo(writer);
                writer.Flush();
                Utf8RdnReader reader = new(output.WrittenSpan);
                bool success = reader.Read();
                Debug.Assert(success);
                return RdnReaderHelper.ToValueKind(reader.TokenType);
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
