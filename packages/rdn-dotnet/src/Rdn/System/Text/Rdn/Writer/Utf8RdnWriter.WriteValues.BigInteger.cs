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
        /// Writes a <see cref="BigInteger"/> value as an RDN BigInteger literal (e.g. <c>42n</c>).
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        public void WriteBigIntegerValue(BigInteger value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteBigIntegerValueIndented(value);
            }
            else
            {
                WriteBigIntegerValueMinimized(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnBigInteger;
        }

        private void WriteBigIntegerValueMinimized(BigInteger value)
        {
            // Convert BigInteger to string representation
            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int utf8Length = System.Text.Encoding.UTF8.GetByteCount(digits);

            // digits + 'n' suffix + optional separator
            int maxRequired = utf8Length + 2;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            int bytesWritten = System.Text.Encoding.UTF8.GetBytes(digits.AsSpan(), output.Slice(BytesPending));
            BytesPending += bytesWritten;

            output[BytesPending++] = (byte)'n';
        }

        private void WriteBigIntegerValueIndented(BigInteger value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int utf8Length = System.Text.Encoding.UTF8.GetByteCount(digits);

            // indent + digits + 'n' suffix + optional separator + newline
            int maxRequired = indent + utf8Length + 2 + _newLineLength;

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

            int bytesWritten = System.Text.Encoding.UTF8.GetBytes(digits.AsSpan(), output.Slice(BytesPending));
            BytesPending += bytesWritten;

            output[BytesPending++] = (byte)'n';
        }
    }
}
