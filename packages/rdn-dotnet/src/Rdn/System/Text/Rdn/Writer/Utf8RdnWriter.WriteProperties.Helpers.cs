// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidatePropertyNameAndDepth(ReadOnlySpan<char> propertyName)
        {
            if (propertyName.Length > RdnConstants.MaxCharacterTokenSize || CurrentDepth >= _options.MaxDepth)
                ThrowHelper.ThrowInvalidOperationOrArgumentException(propertyName, _currentDepth, _options.MaxDepth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidatePropertyNameAndDepth(ReadOnlySpan<byte> utf8PropertyName)
        {
            if (utf8PropertyName.Length > RdnConstants.MaxUnescapedTokenSize || CurrentDepth >= _options.MaxDepth)
                ThrowHelper.ThrowInvalidOperationOrArgumentException(utf8PropertyName, _currentDepth, _options.MaxDepth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateDepth()
        {
            if (CurrentDepth >= _options.MaxDepth)
                ThrowHelper.ThrowInvalidOperationException(_currentDepth, _options.MaxDepth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWritingProperty()
        {
            if (!_options.SkipValidation)
            {
                if (_enclosingContainer != EnclosingContainerType.Object || _tokenType == RdnTokenType.PropertyName)
                {
                    Debug.Assert(_tokenType != RdnTokenType.StartObject);
                    OnValidateWritingPropertyFailed();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWritingProperty(byte token)
        {
            if (!_options.SkipValidation)
            {
                if (_enclosingContainer != EnclosingContainerType.Object || _tokenType == RdnTokenType.PropertyName)
                {
                    Debug.Assert(_tokenType != RdnTokenType.StartObject);
                    OnValidateWritingPropertyFailed();
                }
                UpdateBitStackOnStart(token);
            }
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnValidateWritingPropertyFailed()
        {
            if (IsWritingPartialString)
            {
                ThrowInvalidOperationException(ExceptionResource.CannotWriteWithinString);
            }

            Debug.Assert(_enclosingContainer != EnclosingContainerType.Object || _tokenType == RdnTokenType.PropertyName);
            ThrowInvalidOperationException(ExceptionResource.CannotWritePropertyWithinArray);
        }

        private void WritePropertyNameMinimized(ReadOnlySpan<byte> escapedPropertyName, byte token)
        {
            Debug.Assert(escapedPropertyName.Length < int.MaxValue - 5);

            int minRequired = escapedPropertyName.Length + 4; // 2 quotes, 1 colon, and 1 start token
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
            output[BytesPending++] = token;
        }

        private void WritePropertyNameIndented(ReadOnlySpan<byte> escapedPropertyName, byte token)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 6 - _newLineLength);

            int minRequired = indent + escapedPropertyName.Length + 5; // 2 quotes, 1 colon, 1 space, and 1 start token
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
            output[BytesPending++] = token;
        }

        private void WritePropertyNameMinimized(ReadOnlySpan<char> escapedPropertyName, byte token)
        {
            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / RdnConstants.MaxExpansionFactorWhileTranscoding) - 5);

            // All ASCII, 2 quotes, 1 colon, and 1 start token => escapedPropertyName.Length + 4
            // Optionally, 1 list separator, and up to 3x growth when transcoding
            int maxRequired = (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + 5;

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
            output[BytesPending++] = token;
        }

        private void WritePropertyNameIndented(ReadOnlySpan<char> escapedPropertyName, byte token)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            Debug.Assert(escapedPropertyName.Length < (int.MaxValue / RdnConstants.MaxExpansionFactorWhileTranscoding) - indent - 6 - _newLineLength);

            // All ASCII, 2 quotes, 1 colon, 1 space, and 1 start token => indent + escapedPropertyName.Length + 5
            // Optionally, 1 list separator, 1-2 bytes for new line, and up to 3x growth when transcoding
            int maxRequired = indent + (escapedPropertyName.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) + 6 + _newLineLength;

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
            output[BytesPending++] = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TranscodeAndWrite(ReadOnlySpan<char> escapedPropertyName, Span<byte> output)
        {
            OperationStatus status = RdnWriterHelper.ToUtf8(escapedPropertyName, output.Slice(BytesPending), out int written);
            Debug.Assert(status == OperationStatus.Done);
            BytesPending += written;
        }
    }
}
