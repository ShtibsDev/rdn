// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        /// <summary>
        /// Writes the pre-encoded property name and raw bytes value as an RDN binary literal (b"...") as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteRdnBinary(RdnEncodedText propertyName, ReadOnlySpan<byte> bytes)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            Debug.Assert(utf8PropertyName.Length <= RdnConstants.MaxUnescapedTokenSize);

            WriteRdnBinaryByOptions(utf8PropertyName, bytes);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBinary;
        }

        /// <summary>
        /// Writes the property name and raw bytes value as an RDN binary literal (b"...") as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteRdnBinary(string propertyName, ReadOnlySpan<byte> bytes)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteRdnBinary(propertyName.AsSpan(), bytes);
        }

        /// <summary>
        /// Writes the property name and raw bytes value as an RDN binary literal (b"...") as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteRdnBinary(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
        {
            RdnWriterHelper.ValidatePropertyNameLength(propertyName);

            WriteRdnBinaryEscape(propertyName, bytes);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBinary;
        }

        /// <summary>
        /// Writes the UTF-8 property name and raw bytes value as an RDN binary literal (b"...") as part of a name/value pair of a RDN object.
        /// </summary>
        public void WriteRdnBinary(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
        {
            RdnWriterHelper.ValidatePropertyNameLength(utf8PropertyName);

            WriteRdnBinaryEscape(utf8PropertyName, bytes);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBinary;
        }

        private void WriteRdnBinaryEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteRdnBinaryEscapeProperty(propertyName, bytes, propertyIdx);
            }
            else
            {
                WriteRdnBinaryByOptions(propertyName, bytes);
            }
        }

        private void WriteRdnBinaryEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteRdnBinaryEscapeProperty(utf8PropertyName, bytes, propertyIdx);
            }
            else
            {
                WriteRdnBinaryByOptions(utf8PropertyName, bytes);
            }
        }

        private void WriteRdnBinaryEscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= RdnConstants.StackallocCharThreshold ?
                stackalloc char[RdnConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteRdnBinaryByOptions(escapedPropertyName.Slice(0, written), bytes);

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteRdnBinaryEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteRdnBinaryByOptions(escapedPropertyName.Slice(0, written), bytes);

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteRdnBinaryByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteRdnBinaryIndented(propertyName, bytes);
            }
            else
            {
                WriteRdnBinaryMinimized(propertyName, bytes);
            }
        }

        private void WriteRdnBinaryByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteRdnBinaryIndented(utf8PropertyName, bytes);
            }
            else
            {
                WriteRdnBinaryMinimized(utf8PropertyName, bytes);
            }
        }

        private void WriteRdnBinaryMinimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding < int.MaxValue - encodedLength - 7);

            // 2 quotes for property name, 1 colon, b, 2 quotes for value => escapedPropertyName.Length + encodedLength + 6
            // Optionally, 1 list separator, and up to 3x growth when transcoding.
            int maxRequired = (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + encodedLength + 7;

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

            output[BytesPending++] = RdnConstants.LetterB;
            output[BytesPending++] = RdnConstants.Quote;
            Base64EncodeAndWrite(bytes, output);
            output[BytesPending++] = RdnConstants.Quote;
        }

        private void WriteRdnBinaryMinimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - encodedLength - 7);

            int maxRequired = escapedPropertyName.Length + encodedLength + 7;

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

            output[BytesPending++] = RdnConstants.LetterB;
            output[BytesPending++] = RdnConstants.Quote;
            Base64EncodeAndWrite(bytes, output);
            output[BytesPending++] = RdnConstants.Quote;
        }

        private void WriteRdnBinaryIndented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding < int.MaxValue - indent - encodedLength - 8 - _newLineLength);

            int maxRequired = indent + (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + encodedLength + 8 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.LetterB;
            output[BytesPending++] = RdnConstants.Quote;
            Base64EncodeAndWrite(bytes, output);
            output[BytesPending++] = RdnConstants.Quote;
        }

        private void WriteRdnBinaryIndented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> bytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - encodedLength - 8 - _newLineLength);

            int maxRequired = indent + escapedPropertyName.Length + encodedLength + 8 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.LetterB;
            output[BytesPending++] = RdnConstants.Quote;
            Base64EncodeAndWrite(bytes, output);
            output[BytesPending++] = RdnConstants.Quote;
        }
    }
}
