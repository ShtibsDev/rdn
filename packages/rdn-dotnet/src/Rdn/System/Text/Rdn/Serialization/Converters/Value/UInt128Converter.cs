// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class UInt128Converter : RdnPrimitiveConverter<UInt128>
    {
        private const int MaxFormatLength = 39;

        public UInt128Converter()
        {
            IsInternalConverterForNumberType = true;
        }

        public override UInt128 Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                return ReadNumberWithCustomHandling(ref reader, options.NumberHandling, options);
            }

            if (reader.TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader);
        }

        public override void Write(Utf8RdnWriter writer, UInt128 value, RdnSerializerOptions options)
        {
            if (options?.NumberHandling is not null and not RdnNumberHandling.Strict)
            {
                WriteNumberWithCustomHandling(writer, value, options.NumberHandling);
                return;
            }

            WriteCore(writer, value);
        }

        private static UInt128 ReadCore(ref Utf8RdnReader reader)
        {
            int bufferLength = reader.ValueLength;

            byte[]? rentedBuffer = null;
            Span<byte> buffer = bufferLength <= RdnConstants.StackallocByteThreshold
                ? stackalloc byte[RdnConstants.StackallocByteThreshold]
                : (rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferLength));

            int written = reader.CopyValue(buffer);
            if (!UInt128.TryParse(buffer.Slice(0, written), CultureInfo.InvariantCulture, out UInt128 result))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt128);
            }

            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }

            return result;
        }

        private static void WriteCore(Utf8RdnWriter writer, UInt128 value)
        {
            Span<byte> buffer = stackalloc byte[MaxFormatLength];
            Format(buffer, value, out int written);
            writer.WriteRawValue(buffer.Slice(0, written));
        }

        internal override UInt128 ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, UInt128 value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            Span<byte> buffer = stackalloc byte[MaxFormatLength];
            Format(buffer, value, out int written);
            writer.WritePropertyName(buffer.Slice(0, written));
        }

        internal override UInt128 ReadNumberWithCustomHandling(ref Utf8RdnReader reader, RdnNumberHandling handling, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.String &&
                (RdnNumberHandling.AllowReadingFromString & handling) != 0)
            {
                return ReadCore(ref reader);
            }

            if (reader.TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(reader.TokenType);
            }

            return ReadCore(ref reader);
        }

        internal override void WriteNumberWithCustomHandling(Utf8RdnWriter writer, UInt128 value, RdnNumberHandling handling)
        {
            if ((RdnNumberHandling.WriteAsString & handling) != 0)
            {
                const byte Quote = RdnConstants.Quote;
                Span<byte> buffer = stackalloc byte[MaxFormatLength + 2];
                buffer[0] = Quote;
                Format(buffer.Slice(1), value, out int written);

                int length = written + 2;
                buffer[length - 1] = Quote;
                writer.WriteRawValue(buffer.Slice(0, length));
            }
            else
            {
                WriteCore(writer, value);
            }
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling numberHandling) =>
            GetSchemaForNumericType(RdnSchemaType.Integer, numberHandling);

        private static void Format(
            Span<byte> destination,
            UInt128 value, out int written)
        {
            bool formattedSuccessfully = value.TryFormat(destination, out written, provider: CultureInfo.InvariantCulture);
            Debug.Assert(formattedSuccessfully);
        }
    }
}
