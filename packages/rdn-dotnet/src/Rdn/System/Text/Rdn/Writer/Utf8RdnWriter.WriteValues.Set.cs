// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        private static ReadOnlySpan<byte> SetOpenBrace => "Set{"u8;

        /// <summary>
        /// Writes the beginning of an RDN Set.
        /// When <paramref name="forceTypeName"/> is <see langword="true"/> or
        /// <see cref="RdnWriterOptions.AlwaysWriteSetTypeName"/> is set,
        /// writes <c>Set{</c>; otherwise writes just <c>{</c>.
        /// </summary>
        public void WriteStartSet(bool forceTypeName = false)
        {
            if (CurrentDepth >= _options.MaxDepth)
            {
                ThrowInvalidOperationException_DepthTooLarge();
            }

            bool writePrefix = forceTypeName || _options.AlwaysWriteSetTypeName;

            if (_options.IndentedOrNotSkipValidation)
            {
                WriteStartSetSlow(writePrefix);
            }
            else
            {
                WriteStartSetMinimized(writePrefix);
            }

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartSet;
        }

        private void WriteStartSetMinimized(bool writePrefix)
        {
            int prefixLen = writePrefix ? 4 : 1; // "Set{" = 4 bytes, "{" = 1 byte
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
                SetOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WriteStartSetSlow(bool writePrefix)
        {
            Debug.Assert(_options.Indented || !_options.SkipValidation);

            if (_options.Indented)
            {
                if (!_options.SkipValidation)
                {
                    ValidateStart();
                    UpdateBitStackOnStartSet();
                }
                WriteStartSetIndented(writePrefix);
            }
            else
            {
                Debug.Assert(!_options.SkipValidation);
                ValidateStart();
                UpdateBitStackOnStartSet();
                WriteStartSetMinimized(writePrefix);
            }
        }

        private void WriteStartSetIndented(bool writePrefix)
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
                SetOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWritingPropertyForSet()
        {
            if (!_options.SkipValidation)
            {
                if (_enclosingContainer != EnclosingContainerType.Object || _tokenType == RdnTokenType.PropertyName)
                {
                    Debug.Assert(_tokenType != RdnTokenType.StartObject);
                    ThrowInvalidOperationException(ExceptionResource.CannotWritePropertyWithinArray);
                }
                UpdateBitStackOnStartSet();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBitStackOnStartSet()
        {
            _bitStack.PushFalse();
            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _setDepthMask |= (1L << depth);
            }
            _enclosingContainer = EnclosingContainerType.Set;
        }

        /// <summary>
        /// Writes the end of an RDN Set: <c>}</c>.
        /// </summary>
        public void WriteEndSet()
        {
            WriteEnd(RdnConstants.CloseBrace);
            _tokenType = RdnTokenType.EndSet;
        }

        /// <summary>
        /// Writes the beginning of an RDN Set with a property name as the key.
        /// </summary>
        public void WriteStartSet(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteStartSet(propertyName.AsSpan());
        }

        /// <summary>
        /// Writes the beginning of an RDN Set with a property name as the key.
        /// </summary>
        public void WriteStartSet(ReadOnlySpan<char> propertyName)
        {
            ValidatePropertyNameAndDepth(propertyName);

            WriteStartSetEscape(propertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartSet;
        }

        /// <summary>
        /// Writes the beginning of an RDN Set with a UTF-8 property name as the key.
        /// </summary>
        public void WriteStartSet(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartSetEscape(utf8PropertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartSet;
        }

        /// <summary>
        /// Writes the beginning of an RDN Set with a pre-encoded property name as the key.
        /// </summary>
        public void WriteStartSet(RdnEncodedText propertyName)
        {
            ReadOnlySpan<byte> utf8PropertyName = propertyName.EncodedUtf8Bytes;
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartSetByOptions(utf8PropertyName);

            _currentDepth &= RdnConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = RdnTokenType.StartSet;
        }

        private void WriteStartSetEscape(ReadOnlySpan<char> propertyName)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStartSetEscapeProperty(propertyName, propertyIdx);
            }
            else
            {
                WriteStartSetByOptions(propertyName);
            }
        }

        private void WriteStartSetByOptions(ReadOnlySpan<char> propertyName)
        {
            ValidateWritingPropertyForSet();
            bool writePrefix = _options.AlwaysWriteSetTypeName;

            if (_options.Indented)
            {
                WritePropertyNameIndentedForSet(propertyName, writePrefix);
            }
            else
            {
                WritePropertyNameMinimizedForSet(propertyName, writePrefix);
            }
        }

        private void WriteStartSetEscapeProperty(ReadOnlySpan<char> propertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= RdnConstants.StackallocCharThreshold ?
                stackalloc char[RdnConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartSetByOptions(escapedPropertyName.Slice(0, written));

            if (propertyArray != null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        private void WriteStartSetEscape(ReadOnlySpan<byte> utf8PropertyName)
        {
            int propertyIdx = RdnWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStartSetEscapeProperty(utf8PropertyName, propertyIdx);
            }
            else
            {
                WriteStartSetByOptions(utf8PropertyName);
            }
        }

        private void WriteStartSetByOptions(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidateWritingPropertyForSet();
            bool writePrefix = _options.AlwaysWriteSetTypeName;

            if (_options.Indented)
            {
                WritePropertyNameIndentedForSet(utf8PropertyName, writePrefix);
            }
            else
            {
                WritePropertyNameMinimizedForSet(utf8PropertyName, writePrefix);
            }
        }

        private void WriteStartSetEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = RdnWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            RdnWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartSetByOptions(escapedPropertyName.Slice(0, written));

            if (propertyArray != null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        private void WritePropertyNameMinimizedForSet(ReadOnlySpan<byte> utf8PropertyName, bool writePrefix)
        {
            int prefixLen = writePrefix ? 4 : 1;
            // "name":{  or  "name":Set{  → quote(1) + name + quote(1) + colon(1) + prefix + optional separator(1)
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
                SetOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WritePropertyNameIndentedForSet(ReadOnlySpan<byte> utf8PropertyName, bool writePrefix)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int prefixLen = writePrefix ? 4 : 1;
            // "name": Set{  or  "name": {  → indent + name + quotes(2) + colon(1) + space(1) + prefix + separator(1) + newline(2)
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
                SetOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WritePropertyNameMinimizedForSet(ReadOnlySpan<char> propertyName, bool writePrefix)
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
                SetOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }

        private void WritePropertyNameIndentedForSet(ReadOnlySpan<char> propertyName, bool writePrefix)
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
                SetOpenBrace.CopyTo(output.Slice(BytesPending));
                BytesPending += 4;
            }
            else
            {
                output[BytesPending++] = RdnConstants.OpenBrace;
            }
        }
    }
}
