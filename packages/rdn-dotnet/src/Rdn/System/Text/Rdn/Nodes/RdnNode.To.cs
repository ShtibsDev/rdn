// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Nodes
{
    public abstract partial class RdnNode
    {
        /// <summary>
        ///   Converts the current instance to string in RDN format.
        /// </summary>
        /// <param name="options">Options to control the serialization behavior.</param>
        /// <returns>RDN representation of current instance.</returns>
        public string ToRdnString(RdnSerializerOptions? options = null)
        {
            RdnWriterOptions writerOptions = default;
            int defaultBufferSize = RdnSerializerOptions.BufferSizeDefault;
            if (options is not null)
            {
                writerOptions = options.GetWriterOptions();
                defaultBufferSize = options.DefaultBufferSize;
            }

            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(writerOptions, defaultBufferSize, out PooledByteBufferWriter output);
            try
            {
                WriteTo(writer, options);
                writer.Flush();
                return Encoding.UTF8.GetString(output.WrittenSpan);
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        /// <summary>
        ///   Gets a string representation for the current value appropriate to the node type.
        /// </summary>
        /// <returns>A string representation for the current value appropriate to the node type.</returns>
        public override string ToString()
        {
            // Special case for string; don't quote it.
            if (this is RdnValue)
            {
                switch (this)
                {
                    case RdnValuePrimitive<string> rdnString:
                        return rdnString.Value;
                    case RdnValueOfElement { Value.ValueKind: RdnValueKind.String } rdnElement:
                        return rdnElement.Value.GetString()!;
                    case RdnValueOfRdnString rdnValueOfRdnString:
                        return rdnValueOfRdnString.GetValue<string>()!;
                }
            }

            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(new RdnWriterOptions { Indented = true }, RdnSerializerOptions.BufferSizeDefault, out PooledByteBufferWriter output);
            try
            {
                WriteTo(writer);
                writer.Flush();
                return Encoding.UTF8.GetString(output.WrittenSpan);
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        /// <summary>
        ///   Write the <see cref="RdnNode"/> into the provided <see cref="Utf8RdnWriter"/> as RDN.
        /// </summary>
        /// <param name="writer">The <see cref="Utf8RdnWriter"/>.</param>
        /// <exception cref="ArgumentNullException">
        ///   The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <param name="options">Options to control the serialization behavior.</param>
        public abstract void WriteTo(Utf8RdnWriter writer, RdnSerializerOptions? options = null);
    }
}
