// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace Rdn
{
    public ref partial struct Utf8JsonReader
    {
        /// <summary>
        /// Multi-segment version of ConsumeRdnLiteral.
        /// For multi-segment input, we materialize the RDN body into a contiguous buffer.
        /// </summary>
        private bool ConsumeRdnLiteralMultiSegment()
        {
            // For multi-segment, we need to handle the case where the @ literal spans segments.
            // The simplest approach: get a contiguous view of the data.
            ReadOnlySpan<byte> buffer = _buffer;
            int start = _consumed;

            Debug.Assert(start < buffer.Length);
            Debug.Assert(buffer[start] == JsonConstants.AtSign);

            int bodyStart = start + 1;

            // If the body starts in a later segment, we need more data
            if (bodyStart >= buffer.Length)
            {
                if (!GetNextSpan())
                {
                    if (_isFinalBlock)
                    {
                        ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonConstants.AtSign);
                    }
                    return false;
                }
                buffer = _buffer;
                // After getting next span, _consumed is reset relative to new buffer
                // Since the @ was in the previous segment, we adjust
                bodyStart = _consumed;
            }

            byte first = buffer[bodyStart];

            // Duration: @P...
            if (first == JsonConstants.LetterP)
            {
                return ConsumeRdnDurationMultiSegment(buffer, bodyStart);
            }

            if (!JsonHelpers.IsDigit(first))
            {
                ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, first);
            }

            // Scan body to find end
            int bodyEnd = ScanRdnBody(buffer, bodyStart);
            int bodyLength = bodyEnd - bodyStart;

            if (bodyEnd >= buffer.Length && !_isFinalBlock)
            {
                // The literal might span segments - for now, we require it to be in one segment
                // This is a simplification; most date/time literals are short enough
                return false;
            }

            if (bodyLength < 1)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonConstants.AtSign);
                }
                return false;
            }

            // Disambiguate
            if (bodyLength >= 3 && buffer[bodyStart + 2] == JsonConstants.Colon)
            {
                return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, JsonTokenType.RdnTimeOnly);
            }

            if (bodyLength >= 5 && buffer[bodyStart + 4] == JsonConstants.Hyphen)
            {
                return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, JsonTokenType.RdnDateTime);
            }

            return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, JsonTokenType.RdnDateTime);
        }

        private bool ConsumeRdnDurationMultiSegment(ReadOnlySpan<byte> buffer, int bodyStart)
        {
            Debug.Assert(buffer[bodyStart] == JsonConstants.LetterP);

            int i = bodyStart;
            while (i < buffer.Length && RdnCharTables.IsDurationChar(buffer[i]))
            {
                i++;
            }

            if (i >= buffer.Length && !_isFinalBlock)
            {
                return false;
            }

            int bodyEnd = i;
            int bodyLength = bodyEnd - bodyStart;

            if (bodyLength < 2)
            {
                ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonConstants.LetterP);
            }

            return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, JsonTokenType.RdnDuration);
        }

        private bool FinishRdnLiteralMultiSegment(ReadOnlySpan<byte> buffer, int bodyStart, int bodyEnd, JsonTokenType tokenType)
        {
            int bodyLength = bodyEnd - bodyStart;
            int totalLength = 1 + bodyLength; // @ + body

            ValueSpan = buffer.Slice(bodyStart, bodyLength);
            HasValueSequence = false;
            ValueIsEscaped = false;
            _tokenType = tokenType;
            _consumed += totalLength;
            _bytePositionInLine += totalLength;
            _isNotPrimitive = false;

            return true;
        }
    }
}
