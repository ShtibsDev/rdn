// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Numerics;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        /// <summary>
        /// Writes the pre-encoded property name and <see cref="BigInteger"/> value as an RDN BigInteger literal as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteBigInteger(RdnEncodedText propertyName, BigInteger value)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            Debug.Assert(utf8PropertyName.Length <= RdnConstants.MaxUnescapedTokenSize);

            WriteBigIntegerByOptions(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBigInteger;
        }

        /// <summary>
        /// Writes the property name and <see cref="BigInteger"/> value as an RDN BigInteger literal as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteBigInteger(string propertyName, BigInteger value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteBigInteger(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the property name and <see cref="BigInteger"/> value as an RDN BigInteger literal as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteBigInteger(ReadOnlySpan<char> propertyName, BigInteger value)
        {
            RdnWriterHelper.ValidatePropertyNameLength(propertyName);

            WriteBigIntegerEscape(propertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBigInteger;
        }

        /// <summary>
        /// Writes the UTF-8 property name and <see cref="BigInteger"/> value as an RDN BigInteger literal as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteBigInteger(ReadOnlySpan<byte> utf8PropertyName, BigInteger value)
        {
            RdnWriterHelper.ValidatePropertyNameLength(utf8PropertyName);

            WriteBigIntegerEscape(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBigInteger;
        }

        private void WriteBigIntegerEscape(ReadOnlySpan<char> propertyName, BigInteger value)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteBigIntegerEscapeProperty(propertyName, value, propertyIdx);
            }
            else
            {
                WriteBigIntegerByOptions(propertyName, value);
            }
        }

        private void WriteBigIntegerEscape(ReadOnlySpan<byte> utf8PropertyName, BigInteger value)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteBigIntegerEscapeProperty(utf8PropertyName, value, propertyIdx);
            }
            else
            {
                WriteBigIntegerByOptions(utf8PropertyName, value);
            }
        }

        private void WriteBigIntegerEscapeProperty(ReadOnlySpan<char> propertyName, BigInteger value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= RdnConstants.StackallocCharThreshold ?
                stackalloc char[RdnConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteBigIntegerByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteBigIntegerEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, BigInteger value, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteBigIntegerByOptions(escapedPropertyName.Slice(0, written), value);

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteBigIntegerByOptions(ReadOnlySpan<char> propertyName, BigInteger value)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteBigIntegerIndented(propertyName, value);
            }
            else
            {
                WriteBigIntegerMinimized(propertyName, value);
            }
        }

        private void WriteBigIntegerByOptions(ReadOnlySpan<byte> utf8PropertyName, BigInteger value)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteBigIntegerIndented(utf8PropertyName, value);
            }
            else
            {
                WriteBigIntegerMinimized(utf8PropertyName, value);
            }
        }

        private void WriteBigIntegerMinimized(ReadOnlySpan<char> escapedPropertyName, BigInteger value)
        {
            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int utf8ValueLength = System.Text.Encoding.UTF8.GetByteCount(digits);

            Debug.Assert(escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding < int.MaxValue - utf8ValueLength - 5);

            // 2 quotes for property name, 1 colon, 'n' suffix => escapedPropertyName.Length + utf8ValueLength + 4
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + utf8ValueLength + 5;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }
            output[BytesPending++] = RdnConstants.Quote;

            TranscodeAndWrite(escapedPropertyName, output);

            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;

            int bytesWritten = System.Text.Encoding.UTF8.GetBytes(digits.AsSpan(), output.Slice(BytesPending));
            BytesPending += bytesWritten;

            output[BytesPending++] = (byte)'n';
        }

        private void WriteBigIntegerMinimized(ReadOnlySpan<byte> escapedPropertyName, BigInteger value)
        {
            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int utf8ValueLength = System.Text.Encoding.UTF8.GetByteCount(digits);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - utf8ValueLength - 5);

            int maxRequired = escapedPropertyName.Length + utf8ValueLength + 5;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }
            output[BytesPending++] = RdnConstants.Quote;

            escapedPropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedPropertyName.Length;

            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;

            int bytesWritten = System.Text.Encoding.UTF8.GetBytes(digits.AsSpan(), output.Slice(BytesPending));
            BytesPending += bytesWritten;

            output[BytesPending++] = (byte)'n';
        }

        private void WriteBigIntegerIndented(ReadOnlySpan<char> escapedPropertyName, BigInteger value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int utf8ValueLength = System.Text.Encoding.UTF8.GetByteCount(digits);

            Debug.Assert(escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding < int.MaxValue - indent - utf8ValueLength - 6 - _newLineLength);

            // indent + 2 quotes for property name + 1 colon + 1 space + digits + 'n' suffix = indent + escapedPropertyName.Length + utf8ValueLength + 5
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + utf8ValueLength + 6 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            Debug.Assert(_options.SkipValidation || _tokenType != RdnTokenType.PropertyName);

            if (_tokenType != RdnTokenType.None)
            {
                WriteNewLine(output);
            }

            WriteIndentation(output.Slice(BytesPending), indent);
            BytesPending += indent;

            output[BytesPending++] = RdnConstants.Quote;

            TranscodeAndWrite(escapedPropertyName, output);

            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;
            output[BytesPending++] = RdnConstants.Space;

            int bytesWritten = System.Text.Encoding.UTF8.GetBytes(digits.AsSpan(), output.Slice(BytesPending));
            BytesPending += bytesWritten;

            output[BytesPending++] = (byte)'n';
        }

        private void WriteBigIntegerIndented(ReadOnlySpan<byte> escapedPropertyName, BigInteger value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int utf8ValueLength = System.Text.Encoding.UTF8.GetByteCount(digits);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - utf8ValueLength - 6 - _newLineLength);

            int maxRequired = indent + escapedPropertyName.Length + utf8ValueLength + 6 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            Debug.Assert(_options.SkipValidation || _tokenType != RdnTokenType.PropertyName);

            if (_tokenType != RdnTokenType.None)
            {
                WriteNewLine(output);
            }

            WriteIndentation(output.Slice(BytesPending), indent);
            BytesPending += indent;

            output[BytesPending++] = RdnConstants.Quote;

            escapedPropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedPropertyName.Length;

            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;
            output[BytesPending++] = RdnConstants.Space;

            int bytesWritten = System.Text.Encoding.UTF8.GetBytes(digits.AsSpan(), output.Slice(BytesPending));
            BytesPending += bytesWritten;

            output[BytesPending++] = (byte)'n';
        }
    }
}
