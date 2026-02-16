// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        /// <summary>
        /// Writes the pre-encoded property name (as a RDN string) as the first part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The RDN-encoded name of the property to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        public void WritePropertyName(RdnEncodedText propertyName)
            => WritePropertyNameHelper(propertyName.EncodedUtf8Bytes);

        internal void WritePropertyNameSection(ReadOnlySpan<byte> escapedPropertyNameSection)
        {
            if (_options.Indented)
            {
                ReadOnlySpan<byte> escapedPropertyName =
                    escapedPropertyNameSection.Slice(1, escapedPropertyNameSection.Length - 3);

                WritePropertyNameHelper(escapedPropertyName);
            }
            else
            {
                Debug.Assert(escapedPropertyNameSection.Length <= RdnConstants.MaxUnescapedTokenSize - 3);

                WriteStringPropertyNameSection(escapedPropertyNameSection);

                _currentDepth &= RdnConstants.RemoveFlagsBitMask;
                _tokenType = RdnTokenType.PropertyName;
                _commentAfterNoneOrPropertyName = false;
            }
        }

        private void WritePropertyNameHelper(ReadOnlySpan<byte> utf8PropertyName)
        {
            Debug.Assert(utf8PropertyName.Length <= RdnConstants.MaxUnescapedTokenSize);

            WriteStringByOptionsPropertyName(utf8PropertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _tokenType = RdnTokenType.PropertyName;
            _commentAfterNoneOrPropertyName = false;
        }

        /// <summary>
        /// Writes the property name (as a RDN string) as the first part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WritePropertyName(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WritePropertyName(propertyName.AsSpan());
        }

        /// <summary>
        /// Writes the property name (as a RDN string) as the first part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WritePropertyName(ReadOnlySpan<char> propertyName)
        {
            RdnWriterHelper.ValidateProperty(propertyName);

            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < int.MaxValue / 2);

            if (propertyIdx != -1)
            {
                WriteStringEscapeProperty(propertyName, propertyIdx);
            }
            else
            {
                WriteStringByOptionsPropertyName(propertyName);
            }
            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _tokenType = RdnTokenType.PropertyName;
            _commentAfterNoneOrPropertyName = false;
        }

        private void WriteStringEscapeProperty(scoped ReadOnlySpan<char> propertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);

            char[]? propertyArray = null;
            scoped Span<char> escapedPropertyName;

            if (firstEscapeIndexProp != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

                if (length > RdnConstants.StackallocCharThreshold)
                {
                    propertyArray = ArrayPool<char>.Shared.Rent(length);
                    escapedPropertyName = propertyArray;
                }
                else
                {
                    escapedPropertyName = stackalloc char[RdnConstants.StackallocCharThreshold];
                }

                RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);
                propertyName = escapedPropertyName.Slice(0, written);
            }

            WriteStringByOptionsPropertyName(propertyName);

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringByOptionsPropertyName(ReadOnlySpan<char> propertyName)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteStringIndentedPropertyName(propertyName);
            }
            else
            {
                WriteStringMinimizedPropertyName(propertyName);
            }
        }

        private void WriteStringMinimizedPropertyName(ReadOnlySpan<char> escapedPropertyName)
        {
            Debug.Assert(escapedPropertyName.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue - 4) / RdnConstants.MaxExpansionFactorWhileTranscoding);

            // All ASCII, 2 quotes for property name, and 1 colon => escapedPropertyName.Length + 3
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + 4;

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
        }

        private void WriteStringIndentedPropertyName(ReadOnlySpan<char> escapedPropertyName)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue - 5 - indent - _newLineLength) / RdnConstants.MaxExpansionFactorWhileTranscoding);

            // All ASCII, 2 quotes for property name, 1 colon, and 1 space => escapedPropertyName.Length + 4
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + 5 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

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
        }

        /// <summary>
        /// Writes the UTF-8 property name (as a RDN string) as the first part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
        {
            RdnWriterHelper.ValidateProperty(utf8PropertyName);

            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < int.MaxValue / 2);

            if (propertyIdx != -1)
            {
                WriteStringEscapeProperty(utf8PropertyName, propertyIdx);
            }
            else
            {
                WriteStringByOptionsPropertyName(utf8PropertyName);
            }
            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _tokenType = RdnTokenType.PropertyName;
            _commentAfterNoneOrPropertyName = false;
        }

        private void WritePropertyNameUnescaped(ReadOnlySpan<byte> utf8PropertyName)
        {
            RdnWriterHelper.ValidateProperty(utf8PropertyName);
            WriteStringByOptionsPropertyName(utf8PropertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _tokenType = RdnTokenType.PropertyName;
            _commentAfterNoneOrPropertyName = false;
        }

        private void WriteStringEscapeProperty(scoped ReadOnlySpan<byte> utf8PropertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);

            byte[]? propertyArray = null;
            scoped Span<byte> escapedPropertyName;

            if (firstEscapeIndexProp != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

                if (length > RdnConstants.StackallocByteThreshold)
                {
                    propertyArray = ArrayPool<byte>.Shared.Rent(length);
                    escapedPropertyName = propertyArray;
                }
                else
                {
                    escapedPropertyName = stackalloc byte[RdnConstants.StackallocByteThreshold];
                }

                RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);
                utf8PropertyName = escapedPropertyName.Slice(0, written);
            }

            WriteStringByOptionsPropertyName(utf8PropertyName);

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringByOptionsPropertyName(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteStringIndentedPropertyName(utf8PropertyName);
            }
            else
            {
                WriteStringMinimizedPropertyName(utf8PropertyName);
            }
        }

        // AggressiveInlining used since this is only called from one location.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringMinimizedPropertyName(ReadOnlySpan<byte> escapedPropertyName)
        {
            Debug.Assert(escapedPropertyName.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - 4);

            int minRequired = escapedPropertyName.Length + 3; // 2 quotes for property name, and 1 colon
            int maxRequired = minRequired + 1; // Optionally, 1 list separator

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
        }

        // AggressiveInlining used since this is only called from one location.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringPropertyNameSection(ReadOnlySpan<byte> escapedPropertyNameSection)
        {
            Debug.Assert(escapedPropertyNameSection.Length <= RdnConstants.MaxEscapedTokenSize - 3);
            Debug.Assert(escapedPropertyNameSection.Length < int.MaxValue - 4);

            int maxRequired = escapedPropertyNameSection.Length + 1; // Optionally, 1 list separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            escapedPropertyNameSection.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedPropertyNameSection.Length;
        }

        // AggressiveInlining used since this is only called from one location.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringIndentedPropertyName(ReadOnlySpan<byte> escapedPropertyName)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 5 - _newLineLength);

            int minRequired = indent + escapedPropertyName.Length + 4; // 2 quotes for property name, 1 colon, and 1 space
            int maxRequired = minRequired + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

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
        }

        /// <summary>
        /// Writes the pre-encoded property name and pre-encoded value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The RDN-encoded name of the property to write.</param>
        /// <param name="value">The RDN-encoded value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        public void WriteString(RdnEncodedText propertyName, RdnEncodedText value)
            => WriteStringHelper(propertyName.EncodedUtf8Bytes, value.EncodedUtf8Bytes);

        private void WriteStringHelper(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
        {
            Debug.Assert(utf8PropertyName.Length <= RdnConstants.MaxUnescapedTokenSize && utf8Value.Length <= RdnConstants.MaxUnescapedTokenSize);

            WriteStringByOptions(utf8PropertyName, utf8Value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the property name and pre-encoded value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The RDN-encoded name of the property to write.</param>
        /// <param name="value">The RDN-encoded value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteString(string propertyName, RdnEncodedText value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteString(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the property name and string text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// <para>
        /// The property name and value is escaped before writing.
        /// </para>
        /// <para>
        /// If <paramref name="value"/> is <see langword="null"/> the RDN null value is written,
        /// as if <see cref="WriteNull(System.ReadOnlySpan{byte})"/> were called.
        /// </para>
        /// </remarks>
        public void WriteString(string propertyName, string? value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (value == null)
            {
                WriteNull(propertyName.AsSpan());
            }
            else
            {
                WriteString(propertyName.AsSpan(), value.AsSpan());
            }
        }

        /// <summary>
        /// Writes the property name and text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name and value is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            RdnWriterHelper.ValidatePropertyAndValue(propertyName, value);

            WriteStringEscape(propertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the UTF-8 property name and UTF-8 text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <param name="utf8Value">The UTF-8 encoded value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name and value is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
        {
            RdnWriterHelper.ValidatePropertyAndValue(utf8PropertyName, utf8Value);

            WriteStringEscape(utf8PropertyName, utf8Value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the pre-encoded property name and string text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The RDN-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// <para>
        /// The value is escaped before writing.
        /// </para>
        /// <para>
        /// If <paramref name="value"/> is <see langword="null"/> the RDN null value is written,
        /// as if <see cref="WriteNull(Rdn.RdnEncodedText)"/> was called.
        /// </para>
        /// </remarks>
        public void WriteString(RdnEncodedText propertyName, string? value)
        {
            if (value == null)
            {
                WriteNull(propertyName);
            }
            else
            {
                WriteString(propertyName, value.AsSpan());
            }
        }

        /// <summary>
        /// Writes the pre-encoded property name and text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The RDN-encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The value is escaped before writing.
        /// </remarks>
        public void WriteString(RdnEncodedText propertyName, ReadOnlySpan<char> value)
            => WriteStringHelperEscapeValue(propertyName.EncodedUtf8Bytes, value);

        private void WriteStringHelperEscapeValue(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
        {
            Debug.Assert(utf8PropertyName.Length <= RdnConstants.MaxUnescapedTokenSize);

            RdnWriterHelper.ValidateValue(value);

            int valueIdx = RdnWriterHelper.NeedsEscaping(value, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < value.Length && valueIdx < int.MaxValue / 2);

            if (valueIdx != -1)
            {
                WriteStringEscapeValueOnly(utf8PropertyName, value, valueIdx);
            }
            else
            {
                WriteStringByOptions(utf8PropertyName, value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the property name and text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name and value is escaped before writing.
        /// </remarks>
        public void WriteString(string propertyName, ReadOnlySpan<char> value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteString(propertyName.AsSpan(), value);
        }

        /// <summary>
        /// Writes the UTF-8 property name and text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name and value is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
        {
            RdnWriterHelper.ValidatePropertyAndValue(utf8PropertyName, value);

            WriteStringEscape(utf8PropertyName, value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the pre-encoded property name and UTF-8 text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The RDN-encoded name of the property to write.</param>
        /// <param name="utf8Value">The UTF-8 encoded value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The value is escaped before writing.
        /// </remarks>
        public void WriteString(RdnEncodedText propertyName, ReadOnlySpan<byte> utf8Value)
            => WriteStringHelperEscapeValue(propertyName.EncodedUtf8Bytes, utf8Value);

        private void WriteStringHelperEscapeValue(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
        {
            Debug.Assert(utf8PropertyName.Length <= RdnConstants.MaxUnescapedTokenSize);

            RdnWriterHelper.ValidateValue(utf8Value);

            int valueIdx = RdnWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length && valueIdx < int.MaxValue / 2);

            if (valueIdx != -1)
            {
                WriteStringEscapeValueOnly(utf8PropertyName, utf8Value, valueIdx);
            }
            else
            {
                WriteStringByOptions(utf8PropertyName, utf8Value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the property name and UTF-8 text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="utf8Value">The UTF-8 encoded value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name and value is escaped before writing.
        /// </remarks>
        public void WriteString(string propertyName, ReadOnlySpan<byte> utf8Value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteString(propertyName.AsSpan(), utf8Value);
        }

        /// <summary>
        /// Writes the property name and UTF-8 text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="utf8Value">The UTF-8 encoded value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name and value is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
        {
            RdnWriterHelper.ValidatePropertyAndValue(propertyName, utf8Value);

            WriteStringEscape(propertyName, utf8Value);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the property name and pre-encoded value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The RDN-encoded value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<char> propertyName, RdnEncodedText value)
            => WriteStringHelperEscapeProperty(propertyName, value.EncodedUtf8Bytes);

        private void WriteStringHelperEscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
        {
            Debug.Assert(utf8Value.Length <= RdnConstants.MaxUnescapedTokenSize);

            RdnWriterHelper.ValidateProperty(propertyName);

            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < int.MaxValue / 2);

            if (propertyIdx != -1)
            {
                WriteStringEscapePropertyOnly(propertyName, utf8Value, propertyIdx);
            }
            else
            {
                WriteStringByOptions(propertyName, utf8Value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the property name and string text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// <para>
        /// The property name and value are escaped before writing.
        /// </para>
        /// <para>
        /// If <paramref name="value"/> is <see langword="null"/> the RDN null value is written,
        /// as if <see cref="WriteNull(System.ReadOnlySpan{char})"/> was called.
        /// </para>
        /// </remarks>
        public void WriteString(ReadOnlySpan<char> propertyName, string? value)
        {
            if (value == null)
            {
                WriteNull(propertyName);
            }
            else
            {
                WriteString(propertyName, value.AsSpan());
            }
        }

        /// <summary>
        /// Writes the UTF-8 property name and pre-encoded value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <param name="value">The RDN-encoded value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        public void WriteString(ReadOnlySpan<byte> utf8PropertyName, RdnEncodedText value)
            => WriteStringHelperEscapeProperty(utf8PropertyName, value.EncodedUtf8Bytes);

        private void WriteStringHelperEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
        {
            Debug.Assert(utf8Value.Length <= RdnConstants.MaxUnescapedTokenSize);

            RdnWriterHelper.ValidateProperty(utf8PropertyName);

            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < int.MaxValue / 2);

            if (propertyIdx != -1)
            {
                WriteStringEscapePropertyOnly(utf8PropertyName, utf8Value, propertyIdx);
            }
            else
            {
                WriteStringByOptions(utf8PropertyName, utf8Value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.String;
        }

        /// <summary>
        /// Writes the UTF-8 property name and string text value (as a RDN string) as part of a name/value pair of a RDN object.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded name of the property to write.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name or value is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid RDN being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// <para>
        /// The property name and value are escaped before writing.
        /// </para>
        /// <para>
        /// If <paramref name="value"/> is <see langword="null"/> the RDN null value is written,
        /// as if <see cref="WriteNull(System.ReadOnlySpan{byte})"/> was called.
        /// </para>
        /// </remarks>
        public void WriteString(ReadOnlySpan<byte> utf8PropertyName, string? value)
        {
            if (value == null)
            {
                WriteNull(utf8PropertyName);
            }
            else
            {
                WriteString(utf8PropertyName, value.AsSpan());
            }
        }

        private void WriteStringEscapeValueOnly(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> utf8Value, int firstEscapeIndex)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
            Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < utf8Value.Length);

            byte[]? valueArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndex);

            Span<byte> escapedValue = length <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (valueArray = ArrayPool<byte>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndex, _options.Encoder, out int written);

            WriteStringByOptions(escapedPropertyName, escapedValue.Slice(0, written));

            if (valueArray != null)
            {
                ArrayPool<byte>.Shared.Return(valueArray);
            }
        }

        private void WriteStringEscapeValueOnly(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> value, int firstEscapeIndex)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= value.Length);
            Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < value.Length);

            char[]? valueArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndex);

            Span<char> escapedValue = length <= RdnConstants.StackallocCharThreshold ?
                stackalloc char[RdnConstants.StackallocCharThreshold] :
                (valueArray = ArrayPool<char>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(value, escapedValue, firstEscapeIndex, _options.Encoder, out int written);

            WriteStringByOptions(escapedPropertyName, escapedValue.Slice(0, written));

            if (valueArray != null)
            {
                ArrayPool<char>.Shared.Return(valueArray);
            }
        }

        private void WriteStringEscapePropertyOnly(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> escapedValue, int firstEscapeIndex)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < propertyName.Length);

            char[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndex);

            Span<char> escapedPropertyName = length <= RdnConstants.StackallocCharThreshold ?
                stackalloc char[RdnConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndex, _options.Encoder, out int written);

            WriteStringByOptions(escapedPropertyName.Slice(0, written), escapedValue);

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringEscapePropertyOnly(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> escapedValue, int firstEscapeIndex)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndex);

            Span<byte> escapedPropertyName = length <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndex, _options.Encoder, out int written);

            WriteStringByOptions(escapedPropertyName.Slice(0, written), escapedValue);

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            int valueIdx = RdnWriterHelper.NeedsEscaping(value, _options.Encoder);
            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < value.Length && valueIdx < int.MaxValue / 2);
            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < int.MaxValue / 2);

            // Equivalent to: valueIdx != -1 || propertyIdx != -1
            if (valueIdx + propertyIdx != -2)
            {
                WriteStringEscapePropertyOrValue(propertyName, value, propertyIdx, valueIdx);
            }
            else
            {
                WriteStringByOptions(propertyName, value);
            }
        }

        private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
        {
            int valueIdx = RdnWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);
            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length && valueIdx < int.MaxValue / 2);
            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < int.MaxValue / 2);

            // Equivalent to: valueIdx != -1 || propertyIdx != -1
            if (valueIdx + propertyIdx != -2)
            {
                WriteStringEscapePropertyOrValue(utf8PropertyName, utf8Value, propertyIdx, valueIdx);
            }
            else
            {
                WriteStringByOptions(utf8PropertyName, utf8Value);
            }
        }

        private void WriteStringEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
        {
            int valueIdx = RdnWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);
            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length && valueIdx < int.MaxValue / 2);
            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < int.MaxValue / 2);

            // Equivalent to: valueIdx != -1 || propertyIdx != -1
            if (valueIdx + propertyIdx != -2)
            {
                WriteStringEscapePropertyOrValue(propertyName, utf8Value, propertyIdx, valueIdx);
            }
            else
            {
                WriteStringByOptions(propertyName, utf8Value);
            }
        }

        private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
        {
            int valueIdx = RdnWriterHelper.NeedsEscaping(value, _options.Encoder);
            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(valueIdx >= -1 && valueIdx < value.Length && valueIdx < int.MaxValue / 2);
            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < int.MaxValue / 2);

            // Equivalent to: valueIdx != -1 || propertyIdx != -1
            if (valueIdx + propertyIdx != -2)
            {
                WriteStringEscapePropertyOrValue(utf8PropertyName, value, propertyIdx, valueIdx);
            }
            else
            {
                WriteStringByOptions(utf8PropertyName, value);
            }
        }

        private void WriteStringEscapePropertyOrValue(scoped ReadOnlySpan<char> propertyName, scoped ReadOnlySpan<char> value, int firstEscapeIndexProp, int firstEscapeIndexVal)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= value.Length);
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);

            char[]? valueArray = null;
            char[]? propertyArray = null;
            scoped Span<char> escapedValue;

            if (firstEscapeIndexVal != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndexVal);

                if (length > RdnConstants.StackallocCharThreshold)
                {
                    valueArray = ArrayPool<char>.Shared.Rent(length);
                    escapedValue = valueArray;
                }
                else
                {
                    escapedValue = stackalloc char[RdnConstants.StackallocCharThreshold];
                }

                RdnWriterHelper.EscapeString(value, escapedValue, firstEscapeIndexVal, _options.Encoder, out int written);
                value = escapedValue.Slice(0, written);
            }

            scoped Span<char> escapedPropertyName;

            if (firstEscapeIndexProp != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

                if (length > RdnConstants.StackallocCharThreshold)
                {
                    propertyArray = ArrayPool<char>.Shared.Rent(length);
                    escapedPropertyName = propertyArray;
                }
                else
                {
                    escapedPropertyName = stackalloc char[RdnConstants.StackallocCharThreshold];
                }

                RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);
                propertyName = escapedPropertyName.Slice(0, written);
            }

            WriteStringByOptions(propertyName, value);

            if (valueArray != null)
            {
                ArrayPool<char>.Shared.Return(valueArray);
            }

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringEscapePropertyOrValue(scoped ReadOnlySpan<byte> utf8PropertyName, scoped ReadOnlySpan<byte> utf8Value, int firstEscapeIndexProp, int firstEscapeIndexVal)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);

            byte[]? valueArray = null;
            byte[]? propertyArray = null;
            scoped Span<byte> escapedValue;

            if (firstEscapeIndexVal != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);

                if (length > RdnConstants.StackallocByteThreshold)
                {
                    valueArray = ArrayPool<byte>.Shared.Rent(length);
                    escapedValue = valueArray;
                }
                else
                {
                    escapedValue = stackalloc byte[RdnConstants.StackallocByteThreshold];
                }

                RdnWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, _options.Encoder, out int written);
                utf8Value = escapedValue.Slice(0, written);
            }

            scoped Span<byte> escapedPropertyName;

            if (firstEscapeIndexProp != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

                if (length > RdnConstants.StackallocByteThreshold)
                {
                    propertyArray = ArrayPool<byte>.Shared.Rent(length);
                    escapedPropertyName = propertyArray;
                }
                else
                {
                    escapedPropertyName = stackalloc byte[RdnConstants.StackallocByteThreshold];
                }

                RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);
                utf8PropertyName = escapedPropertyName.Slice(0, written);
            }

            WriteStringByOptions(utf8PropertyName, utf8Value);

            if (valueArray != null)
            {
                ArrayPool<byte>.Shared.Return(valueArray);
            }

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringEscapePropertyOrValue(scoped ReadOnlySpan<char> propertyName, scoped ReadOnlySpan<byte> utf8Value, int firstEscapeIndexProp, int firstEscapeIndexVal)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);

            byte[]? valueArray = null;
            char[]? propertyArray = null;
            scoped Span<byte> escapedValue;

            if (firstEscapeIndexVal != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);

                if (length > RdnConstants.StackallocByteThreshold)
                {
                    valueArray = ArrayPool<byte>.Shared.Rent(length);
                    escapedValue = valueArray;
                }
                else
                {
                    escapedValue = stackalloc byte[RdnConstants.StackallocByteThreshold];
                }

                RdnWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, _options.Encoder, out int written);
                utf8Value = escapedValue.Slice(0, written);
            }

            scoped Span<char> escapedPropertyName;

            if (firstEscapeIndexProp != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

                if (length > RdnConstants.StackallocCharThreshold)
                {
                    propertyArray = ArrayPool<char>.Shared.Rent(length);
                    escapedPropertyName = propertyArray;
                }
                else
                {
                    escapedPropertyName = stackalloc char[RdnConstants.StackallocCharThreshold];
                }

                RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);
                propertyName = escapedPropertyName.Slice(0, written);
            }

            WriteStringByOptions(propertyName, utf8Value);

            if (valueArray != null)
            {
                ArrayPool<byte>.Shared.Return(valueArray);
            }

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringEscapePropertyOrValue(scoped ReadOnlySpan<byte> utf8PropertyName, scoped ReadOnlySpan<char> value, int firstEscapeIndexProp, int firstEscapeIndexVal)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= value.Length);
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);

            char[]? valueArray = null;
            byte[]? propertyArray = null;
            scoped Span<char> escapedValue;

            if (firstEscapeIndexVal != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndexVal);

                if (length > RdnConstants.StackallocCharThreshold)
                {
                    valueArray = ArrayPool<char>.Shared.Rent(length);
                    escapedValue = valueArray;
                }
                else
                {
                    escapedValue = stackalloc char[RdnConstants.StackallocCharThreshold];
                }

                RdnWriterHelper.EscapeString(value, escapedValue, firstEscapeIndexVal, _options.Encoder, out int written);
                value = escapedValue.Slice(0, written);
            }

            scoped Span<byte> escapedPropertyName;

            if (firstEscapeIndexProp != -1)
            {
                int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

                if (length > RdnConstants.StackallocByteThreshold)
                {
                    propertyArray = ArrayPool<byte>.Shared.Rent(length);
                    escapedPropertyName = propertyArray;
                }
                else
                {
                    escapedPropertyName = stackalloc byte[RdnConstants.StackallocByteThreshold];
                }

                RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);
                utf8PropertyName = escapedPropertyName.Slice(0, written);
            }

            WriteStringByOptions(utf8PropertyName, value);

            if (valueArray != null)
            {
                ArrayPool<char>.Shared.Return(valueArray);
            }

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteStringIndented(propertyName, value);
            }
            else
            {
                WriteStringMinimized(propertyName, value);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteStringIndented(utf8PropertyName, utf8Value);
            }
            else
            {
                WriteStringMinimized(utf8PropertyName, utf8Value);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteStringIndented(propertyName, utf8Value);
            }
            else
            {
                WriteStringMinimized(propertyName, utf8Value);
            }
        }

        private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
        {
            ValidateWritingProperty();
            if (_options.Indented)
            {
                WriteStringIndented(utf8PropertyName, value);
            }
            else
            {
                WriteStringMinimized(utf8PropertyName, value);
            }
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<char> escapedValue)
        {
            Debug.Assert(escapedValue.Length <= RdnConstants.MaxUnescapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < ((int.MaxValue - 6) / RdnConstants.MaxExpansionFactorWhileTranscoding) - escapedValue.Length);

            // All ASCII, 2 quotes for property name, 2 quotes for value, and 1 colon => escapedPropertyName.Length + escapedValue.Length + 5
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = ((escapedPropertyName.Length + escapedValue.Length) * RdnConstants.MaxExpansionFactorWhileTranscoding) + 6;

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

            output[BytesPending++] = RdnConstants.Quote;

            TranscodeAndWrite(escapedValue, output);

            output[BytesPending++] = RdnConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
        {
            Debug.Assert(escapedValue.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - escapedValue.Length - 6);

            int minRequired = escapedPropertyName.Length + escapedValue.Length + 5; // 2 quotes for property name, 2 quotes for value, and 1 colon
            int maxRequired = minRequired + 1; // Optionally, 1 list separator

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

            output[BytesPending++] = RdnConstants.Quote;

            escapedValue.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedValue.Length;

            output[BytesPending++] = RdnConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
        {
            Debug.Assert(escapedValue.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / RdnConstants.MaxExpansionFactorWhileTranscoding) - escapedValue.Length - 6);

            // All ASCII, 2 quotes for property name, 2 quotes for value, and 1 colon => escapedPropertyName.Length + escapedValue.Length + 5
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + escapedValue.Length + 6;

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

            output[BytesPending++] = RdnConstants.Quote;

            escapedValue.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedValue.Length;

            output[BytesPending++] = RdnConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> escapedValue)
        {
            Debug.Assert(escapedValue.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / RdnConstants.MaxExpansionFactorWhileTranscoding) - escapedValue.Length - 6);

            // All ASCII, 2 quotes for property name, 2 quotes for value, and 1 colon => escapedPropertyName.Length + escapedValue.Length + 5
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedValue.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + escapedPropertyName.Length + 6;

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

            output[BytesPending++] = RdnConstants.Quote;

            TranscodeAndWrite(escapedValue, output);

            output[BytesPending++] = RdnConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<char> escapedValue)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedValue.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < ((int.MaxValue - 7 - indent - _newLineLength) / RdnConstants.MaxExpansionFactorWhileTranscoding) - escapedValue.Length);

            // All ASCII, 2 quotes for property name, 2 quotes for value, 1 colon, and 1 space => escapedPropertyName.Length + escapedValue.Length + 6
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + ((escapedPropertyName.Length + escapedValue.Length) * RdnConstants.MaxExpansionFactorWhileTranscoding) + 7 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.Quote;

            TranscodeAndWrite(escapedValue, output);

            output[BytesPending++] = RdnConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedValue.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - escapedValue.Length - 7 - _newLineLength);

            int minRequired = indent + escapedPropertyName.Length + escapedValue.Length + 6; // 2 quotes for property name, 2 quotes for value, 1 colon, and 1 space
            int maxRequired = minRequired + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

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

            output[BytesPending++] = RdnConstants.Quote;

            escapedValue.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedValue.Length;

            output[BytesPending++] = RdnConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedValue.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / RdnConstants.MaxExpansionFactorWhileTranscoding) - escapedValue.Length - 7 - indent - _newLineLength);

            // All ASCII, 2 quotes for property name, 2 quotes for value, 1 colon, and 1 space => escapedPropertyName.Length + escapedValue.Length + 6
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + escapedValue.Length + 7 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.Quote;

            escapedValue.CopyTo(output.Slice(BytesPending));
            BytesPending += escapedValue.Length;

            output[BytesPending++] = RdnConstants.Quote;
        }

        // TODO: https://github.com/dotnet/runtime/issues/29293
        private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> escapedValue)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedValue.Length <= RdnConstants.MaxEscapedTokenSize);
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / RdnConstants.MaxExpansionFactorWhileTranscoding) - escapedValue.Length - 7 - indent - _newLineLength);

            // All ASCII, 2 quotes for property name, 2 quotes for value, 1 colon, and 1 space => escapedPropertyName.Length + escapedValue.Length + 6
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedValue.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + escapedPropertyName.Length + 7 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.Quote;

            TranscodeAndWrite(escapedValue, output);

            output[BytesPending++] = RdnConstants.Quote;
        }
    }
}
