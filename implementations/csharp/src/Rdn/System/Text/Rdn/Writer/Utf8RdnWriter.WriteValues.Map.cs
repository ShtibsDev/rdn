// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        private static ReadOnlySpan<byte> MapOpenBrace => "Map{"u8;
        private static ReadOnlySpan<byte> MapArrowMinimized => "=>"u8;
        private static ReadOnlySpan<byte> MapArrowIndented => " => "u8;

        /// <summary>
        /// Writes the beginning of an RDN Map.
        /// When <paramref name="forceTypeName"/> is <see langword="true"/> or
        /// <see cref="RdnWriterOptions.AlwaysWriteCollectionTypeNames"/> is set,
        /// writes <c>Map{</c>; otherwise writes just <c>{</c>.
        /// </summary>
        public void WriteStartMap(bool forceTypeName = false)
        {
            if (CurrentDepth >= _options.MaxDepth)
            {
                ThrowInvalidOperationException_DepthTooLarge();
            }

            bool writePrefix = forceTypeName || _options.AlwaysWriteCollectionTypeNames;

            if (_options.IndentedOrNotSkipValidation)
            {
                WriteStartMapSlow(writePrefix);
            }
            else
            {
                WriteStartMapMinimized(writePrefix);
            }

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartMap;
        }

        private void WriteStartMapMinimized(bool writePrefix)
        {
            int prefixLen = writePrefix ? 4 : 1; // "Map{" = 4 bytes, "{" = 1 byte
            int maxRequired = prefixLen + 1; // + optionally 1 list separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;
            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }
            if (writePrefix)
            {
                MapOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WriteStartMapSlow(bool writePrefix)
        {
            Debug.Assert(_options.Indented || !_options.SkipValidation);

            if (_options.Indented)
            {
                if (!_options.SkipValidation)
                {
                    ValidateStart();
                    UpdateBitStackOnStartMap();
                }
                WriteStartMapIndented(writePrefix);
            }
            else
            {
                Debug.Assert(!_options.SkipValidation);
                ValidateStart();
                UpdateBitStackOnStartMap();
                WriteStartMapMinimized(writePrefix);
            }
        }

        private void WriteStartMapIndented(bool writePrefix)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int prefixLen = writePrefix ? 4 : 1;
            int minRequired = indent + prefixLen;
            int maxRequired = minRequired + 3; // Optionally, 1 list separator and 1-2 bytes for new line

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            if (_tokenType is not RdnTokenType.PropertyName and not RdnTokenType.None || _commentAfterNoneOrPropertyName)
            {
                WriteNewLine(output);
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            if (writePrefix)
            {
                MapOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWritingPropertyForMap()
        {
            if (!_options.SkipValidation)
            {
                if (_enclosingContainer != EnclosingContainerType.Object || _tokenType == RdnTokenType.PropertyName)
                {
                    Debug.Assert(_tokenType != RdnTokenType.StartObject);
                    ThrowInvalidOperationException(ExceptionResource.CannotWritePropertyWithinArray);
                }
                UpdateBitStackOnStartMap();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBitStackOnStartMap()
        {
            _bitStack.PushFalse();
            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _mapDepthMask |= (1L << depth);
            }
            _enclosingContainer = EnclosingContainerType.Map;
        }

        /// <summary>
        /// Writes the end of an RDN Map: <c>}</c>.
        /// </summary>
        public void WriteEndMap()
        {
            WriteEnd(RdnConstants.CloseBrace);
            _tokenType = RdnTokenType.EndMap;
        }

        /// <summary>
        /// Writes the map arrow separator <c>=&gt;</c> between a key and value in an RDN Map.
        /// In indented mode, writes <c> =&gt; </c> (with surrounding spaces).
        /// </summary>
        public void WriteMapArrow()
        {
            if (_options.Indented)
            {
                WriteMapArrowIndented();
            }
            else
            {
                WriteMapArrowMinimized();
            }

            // Clear the list separator flag so the value after the arrow
            // does not get a comma prefix. The arrow replaces the comma
            // between a key and its value.
            _currentDepth &= RdnConstants.RemoveFlagsBitMask;

            // Set token type to PropertyName so the value after => is written
            // inline (no newline+indent), just like values after : in objects.
            _tokenType = RdnTokenType.PropertyName;
        }

        private void WriteMapArrowMinimized()
        {
            if (_memory.Length - BytesPending < 2)
            {
                Grow(2);
            }

            Span<byte> output = _memory.Span;
            MapArrowMinimized.CopyTo(output.Slice(BytesPending));
            BytesPending += 2;
        }

        private void WriteMapArrowIndented()
        {
            if (_memory.Length - BytesPending < 4)
            {
                Grow(4);
            }

            Span<byte> output = _memory.Span;
            MapArrowIndented.CopyTo(output.Slice(BytesPending));
            BytesPending += 4;
        }

        /// <summary>
        /// Writes the beginning of an RDN Map with a property name as the key.
        /// </summary>
        public void WriteStartMap(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteStartMap(propertyName.AsSpan());
        }

        /// <summary>
        /// Writes the beginning of an RDN Map with a property name as the key.
        /// </summary>
        public void WriteStartMap(ReadOnlySpan<char> propertyName)
        {
            ValidatePropertyNameAndDepth(propertyName);

            WriteStartMapEscape(propertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartMap;
        }

        /// <summary>
        /// Writes the beginning of an RDN Map with a UTF-8 property name as the key.
        /// </summary>
        public void WriteStartMap(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartMapEscape(utf8PropertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartMap;
        }

        /// <summary>
        /// Writes the beginning of an RDN Map with a pre-encoded property name as the key.
        /// </summary>
        public void WriteStartMap(RdnEncodedText propertyName)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartMapByOptions(utf8PropertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartMap;
        }

        private void WriteStartMapEscape(ReadOnlySpan<char> propertyName)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStartMapEscapeProperty(propertyName, propertyIdx);
            }
            else
            {
                WriteStartMapByOptions(propertyName);
            }
        }

        private void WriteStartMapByOptions(ReadOnlySpan<char> propertyName)
        {
            ValidateWritingPropertyForMap();
            bool writePrefix = _options.AlwaysWriteCollectionTypeNames;

            if (_options.Indented)
            {
                WritePropertyNameIndentedForMap(propertyName, writePrefix);
            }
            else
            {
                WritePropertyNameMinimizedForMap(propertyName, writePrefix);
            }
        }

        private void WriteStartMapEscapeProperty(ReadOnlySpan<char> propertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= RdnConstants.StackallocCharThreshold ?
                stackalloc char[RdnConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartMapByOptions(escapedPropertyName.Slice(0, written));

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteStartMapEscape(ReadOnlySpan<byte> utf8PropertyName)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStartMapEscapeProperty(utf8PropertyName, propertyIdx);
            }
            else
            {
                WriteStartMapByOptions(utf8PropertyName);
            }
        }

        private void WriteStartMapByOptions(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidateWritingPropertyForMap();
            bool writePrefix = _options.AlwaysWriteCollectionTypeNames;

            if (_options.Indented)
            {
                WritePropertyNameIndentedForMap(utf8PropertyName, writePrefix);
            }
            else
            {
                WritePropertyNameMinimizedForMap(utf8PropertyName, writePrefix);
            }
        }

        private void WriteStartMapEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartMapByOptions(escapedPropertyName.Slice(0, written));

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WritePropertyNameMinimizedForMap(ReadOnlySpan<byte> utf8PropertyName, bool writePrefix)
        {
            int prefixLen = writePrefix ? 4 : 1; // "Map{" = 4 bytes, "{" = 1 byte
            // "name":{  or  "name":Map{  → quote(1) + name + quote(1) + colon(1) + prefix + optional separator(1)
            int maxRequired = utf8PropertyName.Length + 4 + prefixLen;

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
            utf8PropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8PropertyName.Length;
            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;
            if (writePrefix)
            {
                MapOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WritePropertyNameIndentedForMap(ReadOnlySpan<byte> utf8PropertyName, bool writePrefix)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int prefixLen = writePrefix ? 4 : 1;
            // "name": Map{  or  "name": {  → indent + name + quotes(2) + colon(1) + space(1) + prefix + separator(1) + newline(2)
            int maxRequired = indent + utf8PropertyName.Length + 7 + prefixLen + _newLineLength;

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
            utf8PropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8PropertyName.Length;
            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;
            output[BytesPending++] = RdnConstants.Space;
            if (writePrefix)
            {
                MapOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WritePropertyNameMinimizedForMap(ReadOnlySpan<char> propertyName, bool writePrefix)
        {
            int prefixLen = writePrefix ? 4 : 1;
            int maxRequired = propertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding + 4 + prefixLen;

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

            TranscodeAndWrite(propertyName, output);

            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;
            if (writePrefix)
            {
                MapOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WritePropertyNameIndentedForMap(ReadOnlySpan<char> propertyName, bool writePrefix)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int prefixLen = writePrefix ? 4 : 1;
            int maxRequired = indent + propertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding + 7 + prefixLen + _newLineLength;

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

            TranscodeAndWrite(propertyName, output);

            output[BytesPending++] = RdnConstants.Quote;
            output[BytesPending++] = RdnConstants.KeyValueSeparator;
            output[BytesPending++] = RdnConstants.Space;
            if (writePrefix)
            {
                MapOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }
    }
}
