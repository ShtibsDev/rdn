// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Text;
using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;

namespace Rdn.Serialization.Converters
{
    internal sealed class TimeSpanConverter : RdnPrimitiveConverter<TimeSpan>
    {
        private const int MinimumTimeSpanFormatLength = 1; // d
        private const int MaximumTimeSpanFormatLength = 26; // -dddddddd.hh:mm:ss.fffffff
        private const int MaximumEscapedTimeSpanFormatLength = RdnConstants.MaxExpansionFactorWhileEscaping * MaximumTimeSpanFormatLength;

        public override TimeSpan Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType == RdnTokenType.RdnDuration)
            {
                RdnDuration duration = reader.GetRdnDuration();
                if (duration.TryToTimeSpan(out TimeSpan ts))
                {
                    return ts;
                }
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }

            if (reader.TokenType != RdnTokenType.String)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(reader.TokenType);
            }

            return ReadCore(ref reader);
        }

        internal override TimeSpan ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.PropertyName);
            return ReadCore(ref reader);
        }

        private static TimeSpan ReadCore(ref Utf8RdnReader reader)
        {
            Debug.Assert(reader.TokenType is RdnTokenType.String or RdnTokenType.PropertyName);

            if (!RdnHelpers.IsInRangeInclusive(reader.ValueLength, MinimumTimeSpanFormatLength, MaximumEscapedTimeSpanFormatLength))
            {
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }

            scoped ReadOnlySpan<byte> source;
            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
            {
                source = reader.ValueSpan;
            }
            else
            {
                Span<byte> stackSpan = stackalloc byte[MaximumEscapedTimeSpanFormatLength];
                int bytesWritten = reader.CopyString(stackSpan);
                source = stackSpan.Slice(0, bytesWritten);
            }

            byte firstChar = source[0];
            if (!RdnHelpers.IsDigit(firstChar) && firstChar != '-')
            {
                // Note: Utf8Parser.TryParse allows for leading whitespace so we
                // need to exclude that case here.
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }

            bool result = Utf8Parser.TryParse(source, out TimeSpan tmpValue, out int bytesConsumed, 'c');

            // Note: Utf8Parser.TryParse will return true for invalid input so
            // long as it starts with an integer. Example: "2021-06-18" or
            // "1$$$$$$$$$$". We need to check bytesConsumed to know if the
            // entire source was actually valid.

            if (!result || source.Length != bytesConsumed)
            {
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }

            return tmpValue;
        }

        public override void Write(Utf8RdnWriter writer, TimeSpan value, RdnSerializerOptions options)
        {
            writer.WriteRdnTimeSpanValue(value);
        }

        internal override void WriteAsPropertyNameCore(Utf8RdnWriter writer, TimeSpan value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            Span<byte> output = stackalloc byte[MaximumTimeSpanFormatLength];

            bool result = Utf8Formatter.TryFormat(value, output, out int bytesWritten, 'c');
            Debug.Assert(result);

            writer.WritePropertyName(output.Slice(0, bytesWritten));
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new()
        {
            Type = RdnSchemaType.String,
            Comment = "Represents a System.TimeSpan value.",
            Pattern = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$"
        };
    }
}
