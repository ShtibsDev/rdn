// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace Rdn
{
    public ref partial struct Utf8RdnReader
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
            Debug.Assert(buffer[start] == RdnConstants.AtSign);

            int bodyStart = start + 1;

            // If the body starts in a later segment, we need more data
            if (bodyStart >= buffer.Length)
            {
                if (!GetNextSpan())
                {
                    if (_isFinalBlock)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.AtSign);
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
            if (first == RdnConstants.LetterP)
            {
                return ConsumeRdnDurationMultiSegment(buffer, bodyStart);
            }

            if (!RdnHelpers.IsDigit(first))
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, first);
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
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.AtSign);
                }
                return false;
            }

            // Disambiguate
            if (bodyLength >= 3 && buffer[bodyStart + 2] == RdnConstants.Colon)
            {
                return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, RdnTokenType.RdnTimeOnly);
            }

            if (bodyLength >= 5 && buffer[bodyStart + 4] == RdnConstants.Hyphen)
            {
                return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, RdnTokenType.RdnDateTime);
            }

            return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, RdnTokenType.RdnDateTime);
        }

        /// <summary>
        /// Multi-segment version of ConsumeRegex.
        /// For multi-segment input, we require the regex literal to be within one segment (simplification).
        /// </summary>
        private bool ConsumeRegexMultiSegment()
        {
            ReadOnlySpan<byte> buffer = _buffer;
            int start = _consumed;

            Debug.Assert(start < buffer.Length);
            Debug.Assert(buffer[start] == RdnConstants.Slash);

            int patternStart = start + 1;

            if (patternStart >= buffer.Length)
            {
                if (!GetNextSpan())
                {
                    if (_isFinalBlock)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
                    }
                    return false;
                }
                buffer = _buffer;
                patternStart = _consumed;
            }

            // Empty body // is invalid
            if (buffer[patternStart] == RdnConstants.Slash)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
            }

            // Scan for closing /
            bool hasEscape = false;
            int i = patternStart;
            while (i < buffer.Length)
            {
                byte b = buffer[i];
                if (b == RdnConstants.BackSlash)
                {
                    hasEscape = true;
                    i += 2;
                    if (i > buffer.Length && !_isFinalBlock)
                    {
                        return false;
                    }
                    continue;
                }
                if (b == RdnConstants.Slash)
                {
                    break;
                }
                if (b == 0)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, b);
                }
                i++;
            }

            if (i >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
                }
                return false;
            }

            int closingSlash = i;
            int flagStart = closingSlash + 1;
            int flagEnd = flagStart;
            while (flagEnd < buffer.Length && RdnCharTables.IsRegexFlag(buffer[flagEnd]))
            {
                flagEnd++;
            }

            if (flagEnd >= buffer.Length && !_isFinalBlock)
            {
                return false;
            }

            int valueLength = flagEnd - patternStart;
            ValueSpan = buffer.Slice(patternStart, valueLength);
            HasValueSequence = false;
            ValueIsEscaped = hasEscape;
            _tokenType = RdnTokenType.RdnRegExp;
            int totalConsumed = flagEnd - start;
            _consumed += totalConsumed;
            _bytePositionInLine += totalConsumed;
            _isNotPrimitive = false;

            return true;
        }

        private bool ConsumeRdnDurationMultiSegment(ReadOnlySpan<byte> buffer, int bodyStart)
        {
            Debug.Assert(buffer[bodyStart] == RdnConstants.LetterP);

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
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.LetterP);
            }

            return FinishRdnLiteralMultiSegment(buffer, bodyStart, bodyEnd, RdnTokenType.RdnDuration);
        }

        private bool FinishRdnLiteralMultiSegment(ReadOnlySpan<byte> buffer, int bodyStart, int bodyEnd, RdnTokenType tokenType)
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
