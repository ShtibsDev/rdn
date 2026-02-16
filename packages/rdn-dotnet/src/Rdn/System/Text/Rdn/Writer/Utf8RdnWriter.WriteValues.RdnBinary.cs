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
        /// Writes a binary value as an RDN b"..." literal (base64-encoded).
        /// </summary>
        public void WriteRdnBinaryValue(ReadOnlySpan<byte> bytes)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnBinaryValueIndented(bytes);
            }
            else
            {
                WriteRdnBinaryValueMinimized(bytes);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBinary;
        }

        private void WriteRdnBinaryValueMinimized(ReadOnlySpan<byte> bytes)
        {
            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
            // b (1) + " (1) + base64 + " (1) + optional separator (1) = encodedLength + 4
            int maxRequired = encodedLength + 4;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.LetterB;
            output[BytesPending++] = RdnConstants.Quote;
            Base64EncodeAndWrite(bytes, output);
            output[BytesPending++] = RdnConstants.Quote;
        }

        private void WriteRdnBinaryValueIndented(ReadOnlySpan<byte> bytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
            int maxRequired = indent + encodedLength + 4 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            if (_tokenType != RdnTokenType.PropertyName)
            {
                if (_tokenType != RdnTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = RdnConstants.LetterB;
            output[BytesPending++] = RdnConstants.Quote;
            Base64EncodeAndWrite(bytes, output);
            output[BytesPending++] = RdnConstants.Quote;
        }

        /// <summary>
        /// Writes a binary value as an RDN x"..." literal (hex-encoded).
        /// </summary>
        public void WriteRdnBinaryHexValue(ReadOnlySpan<byte> bytes)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnBinaryHexValueIndented(bytes);
            }
            else
            {
                WriteRdnBinaryHexValueMinimized(bytes);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBinary;
        }

        private void WriteRdnBinaryHexValueMinimized(ReadOnlySpan<byte> bytes)
        {
            int hexLength = bytes.Length * 2;
            // x (1) + " (1) + hex + " (1) + optional separator (1) = hexLength + 4
            int maxRequired = hexLength + 4;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.LetterX;
            output[BytesPending++] = RdnConstants.Quote;
            HexConverter.EncodeToUtf8(bytes, output.Slice(BytesPending), HexConverter.Casing.Upper);
            BytesPending += hexLength;
            output[BytesPending++] = RdnConstants.Quote;
        }

        private void WriteRdnBinaryHexValueIndented(ReadOnlySpan<byte> bytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int hexLength = bytes.Length * 2;
            int maxRequired = indent + hexLength + 4 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            if (_tokenType != RdnTokenType.PropertyName)
            {
                if (_tokenType != RdnTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = RdnConstants.LetterX;
            output[BytesPending++] = RdnConstants.Quote;
            HexConverter.EncodeToUtf8(bytes, output.Slice(BytesPending), HexConverter.Casing.Upper);
            BytesPending += hexLength;
            output[BytesPending++] = RdnConstants.Quote;
        }
    }
}
