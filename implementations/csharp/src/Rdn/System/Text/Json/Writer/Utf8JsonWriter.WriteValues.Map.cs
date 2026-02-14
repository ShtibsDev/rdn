// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class Utf8JsonWriter
    {
        private static ReadOnlySpan<byte> MapOpenBrace => "Map{"u8;
        private static ReadOnlySpan<byte> MapArrowMinimized => "=>"u8;
        private static ReadOnlySpan<byte> MapArrowIndented => " => "u8;

        /// <summary>
        /// Writes the beginning of an RDN Map: <c>Map{</c>.
        /// </summary>
        public void WriteStartMap()
        {
            if (CurrentDepth >= _options.MaxDepth)
            {
                ThrowInvalidOperationException_DepthTooLarge();
            }

            if (_options.IndentedOrNotSkipValidation)
            {
                WriteStartMapSlow();
            }
            else
            {
                WriteStartMapMinimized();
            }

            _currentDepth &= JsonConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = JsonTokenType.StartMap;
        }

        private void WriteStartMapMinimized()
        {
            // "Map{" = 4 bytes + optionally 1 list separator
            if (_memory.Length - BytesPending < 5)
            {
                Grow(5);
            }

            Span<byte> output = _memory.Span;
            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }
            MapOpenBrace.CopyTo(output.Slice(BytesPending));
            BytesPending += 4;
        }

        private void WriteStartMapSlow()
        {
            Debug.Assert(_options.Indented || !_options.SkipValidation);

            if (_options.Indented)
            {
                if (!_options.SkipValidation)
                {
                    ValidateStart();
                    UpdateBitStackOnStartMap();
                }
                WriteStartMapIndented();
            }
            else
            {
                Debug.Assert(!_options.SkipValidation);
                ValidateStart();
                UpdateBitStackOnStartMap();
                WriteStartMapMinimized();
            }
        }

        private void WriteStartMapIndented()
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int minRequired = indent + 4;   // "Map{" = 4 bytes
            int maxRequired = minRequired + 3; // Optionally, 1 list separator and 1-2 bytes for new line

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            if (_tokenType is not JsonTokenType.PropertyName and not JsonTokenType.None || _commentAfterNoneOrPropertyName)
            {
                WriteNewLine(output);
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            MapOpenBrace.CopyTo(output.Slice(BytesPending));
            BytesPending += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWritingPropertyForMap()
        {
            if (!_options.SkipValidation)
            {
                if (_enclosingContainer != EnclosingContainerType.Object || _tokenType == JsonTokenType.PropertyName)
                {
                    Debug.Assert(_tokenType != JsonTokenType.StartObject);
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
            WriteEnd(JsonConstants.CloseBrace);
            _tokenType = JsonTokenType.EndMap;
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
            _currentDepth &= JsonConstants.RemoveFlagsBitMask;
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

            _currentDepth &= JsonConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = JsonTokenType.StartMap;
        }

        /// <summary>
        /// Writes the beginning of an RDN Map with a UTF-8 property name as the key.
        /// </summary>
        public void WriteStartMap(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartMapEscape(utf8PropertyName);

            _currentDepth &= JsonConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = JsonTokenType.StartMap;
        }

        /// <summary>
        /// Writes the beginning of an RDN Map with a pre-encoded property name as the key.
        /// </summary>
        public void WriteStartMap(JsonEncodedText propertyName)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartMapByOptions(utf8PropertyName);

            _currentDepth &= JsonConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = JsonTokenType.StartMap;
        }

        private void WriteStartMapEscape(ReadOnlySpan<char> propertyName)
        {
            int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

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

            if (_options.Indented)
            {
                WritePropertyNameIndentedForMap(propertyName);
            }
            else
            {
                WritePropertyNameMinimizedForMap(propertyName);
            }
        }

        private void WriteStartMapEscapeProperty(ReadOnlySpan<char> propertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / JsonConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= JsonConstants.StackallocCharThreshold ?
                stackalloc char[JsonConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartMapByOptions(escapedPropertyName.Slice(0, written));

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteStartMapEscape(ReadOnlySpan<byte> utf8PropertyName)
        {
            int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

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

            if (_options.Indented)
            {
                WritePropertyNameIndentedForMap(utf8PropertyName);
            }
            else
            {
                WritePropertyNameMinimizedForMap(utf8PropertyName);
            }
        }

        private void WriteStartMapEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / JsonConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= JsonConstants.StackallocByteThreshold ?
                stackalloc byte[JsonConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartMapByOptions(escapedPropertyName.Slice(0, written));

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WritePropertyNameMinimizedForMap(ReadOnlySpan<byte> utf8PropertyName)
        {
            // "name":Map{ → quote(1) + name + quote(1) + colon(1) + Map{(4) + optional separator(1) = name.Length + 8
            int maxRequired = utf8PropertyName.Length + 8;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }
            output[BytesPending++] = JsonConstants.Quote;
            utf8PropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8PropertyName.Length;
            output[BytesPending++] = JsonConstants.Quote;
            output[BytesPending++] = JsonConstants.KeyValueSeparator;
            MapOpenBrace.CopyTo(output.Slice(BytesPending));
            BytesPending += 4;
        }

        private void WritePropertyNameIndentedForMap(ReadOnlySpan<byte> utf8PropertyName)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            // "name": Map{ → indent + name + quotes(2) + colon(1) + space(1) + Map{(4) + separator(1) + newline(2)
            int maxRequired = indent + utf8PropertyName.Length + 11 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);

            if (_tokenType != JsonTokenType.None)
            {
                WriteNewLine(output);
            }

            WriteIndentation(output.Slice(BytesPending), indent);
            BytesPending += indent;

            output[BytesPending++] = JsonConstants.Quote;
            utf8PropertyName.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8PropertyName.Length;
            output[BytesPending++] = JsonConstants.Quote;
            output[BytesPending++] = JsonConstants.KeyValueSeparator;
            output[BytesPending++] = JsonConstants.Space;
            MapOpenBrace.CopyTo(output.Slice(BytesPending));
            BytesPending += 4;
        }

        private void WritePropertyNameMinimizedForMap(ReadOnlySpan<char> propertyName)
        {
            int maxRequired = propertyName.Length * JsonConstants.MaxExpansionFactorWhileTranscoding + 8;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }
            output[BytesPending++] = JsonConstants.Quote;

            TranscodeAndWrite(propertyName, output);

            output[BytesPending++] = JsonConstants.Quote;
            output[BytesPending++] = JsonConstants.KeyValueSeparator;
            MapOpenBrace.CopyTo(output.Slice(BytesPending));
            BytesPending += 4;
        }

        private void WritePropertyNameIndentedForMap(ReadOnlySpan<char> propertyName)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + propertyName.Length * JsonConstants.MaxExpansionFactorWhileTranscoding + 11 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);

            if (_tokenType != JsonTokenType.None)
            {
                WriteNewLine(output);
            }

            WriteIndentation(output.Slice(BytesPending), indent);
            BytesPending += indent;

            output[BytesPending++] = JsonConstants.Quote;

            TranscodeAndWrite(propertyName, output);

            output[BytesPending++] = JsonConstants.Quote;
            output[BytesPending++] = JsonConstants.KeyValueSeparator;
            output[BytesPending++] = JsonConstants.Space;
            MapOpenBrace.CopyTo(output.Slice(BytesPending));
            BytesPending += 4;
        }
    }
}
