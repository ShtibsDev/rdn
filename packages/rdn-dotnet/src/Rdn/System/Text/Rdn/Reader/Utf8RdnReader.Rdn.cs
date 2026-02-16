// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public ref partial struct Utf8RdnReader
    {
        /// <summary>
        /// Consumes an RDN @-prefixed literal (DateTime, TimeOnly, or Duration).
        /// ValueSpan is set to the body after the @ sign.
        /// </summary>
        private bool ConsumeRdnLiteral()
        {
            ReadOnlySpan<byte> buffer = _buffer;
            int start = _consumed;
            Debug.Assert(buffer[start] == RdnConstants.AtSign);

            int bodyStart = start + 1;

            if (bodyStart >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.AtSign);
                }
                return false;
            }

            byte first = buffer[bodyStart];

            // Duration: @P...
            if (first == RdnConstants.LetterP)
            {
                return ConsumeRdnDuration(buffer, bodyStart);
            }

            // Must start with a digit for DateTime, TimeOnly, or unix timestamp
            if (!RdnHelpers.IsDigit(first))
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, first);
            }

            // Scan the body to find its end
            int bodyEnd = ScanRdnBody(buffer, bodyStart);
            int bodyLength = bodyEnd - bodyStart;

            if (bodyLength < 1)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.AtSign);
                }
                return false;
            }

            // Disambiguate: check fixed positions
            // TimeOnly: HH:MM:SS — colon at position 2
            // DateTime: YYYY-MM-DD — hyphen at position 4
            // Unix timestamp: all digits
            if (bodyLength >= 3 && buffer[bodyStart + 2] == RdnConstants.Colon)
            {
                return FinishRdnLiteral(buffer, bodyStart, bodyEnd, RdnTokenType.RdnTimeOnly);
            }

            if (bodyLength >= 5 && buffer[bodyStart + 4] == RdnConstants.Hyphen)
            {
                return FinishRdnLiteral(buffer, bodyStart, bodyEnd, RdnTokenType.RdnDateTime);
            }

            // Unix timestamp (all digits)
            return FinishRdnLiteral(buffer, bodyStart, bodyEnd, RdnTokenType.RdnDateTime);
        }

        /// <summary>
        /// Consumes an RDN duration literal: @P[nY][nM][nD][T[nH][nM][nS]]
        /// </summary>
        private bool ConsumeRdnDuration(ReadOnlySpan<byte> buffer, int bodyStart)
        {
            Debug.Assert(buffer[bodyStart] == RdnConstants.LetterP);

            int i = bodyStart;
            while (i < buffer.Length && RdnCharTables.IsDurationChar(buffer[i]))
            {
                i++;
            }

            // Check if we need more data
            if (i >= buffer.Length && !_isFinalBlock)
            {
                return false;
            }

            int bodyEnd = i;
            int bodyLength = bodyEnd - bodyStart;

            if (bodyLength < 2)
            {
                // Must be at least "P" + one designator
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.LetterP);
            }

            return FinishRdnLiteral(buffer, bodyStart, bodyEnd, RdnTokenType.RdnDuration);
        }

        /// <summary>
        /// Consumes an RDN regex literal: /pattern/flags
        /// ValueSpan is set to "pattern/flags" (between opening / and end of flags).
        /// </summary>
        private bool ConsumeRegex()
        {
            ReadOnlySpan<byte> buffer = _buffer;
            int start = _consumed;
            Debug.Assert(buffer[start] == RdnConstants.Slash);

            int patternStart = start + 1;

            if (patternStart >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
                }
                return false;
            }

            // Empty body // is invalid per grammar (requires 1+ chars)
            if (buffer[patternStart] == RdnConstants.Slash)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
            }

            // Phase 1: Scan for closing /
            bool hasEscape = false;
            int i = patternStart;
            while (i < buffer.Length)
            {
                byte b = buffer[i];
                if (b == RdnConstants.BackSlash)
                {
                    hasEscape = true;
                    i += 2; // Skip escaped char
                    if (i > buffer.Length)
                    {
                        if (_isFinalBlock)
                        {
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
                        }
                        return false;
                    }
                    continue;
                }
                if (b == RdnConstants.Slash)
                {
                    break;
                }
                if (b == 0) // NUL not allowed
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

            // i now points at the closing /
            int closingSlash = i;

            // Phase 2: Consume flags after closing /
            int flagStart = closingSlash + 1;
            int flagEnd = flagStart;
            while (flagEnd < buffer.Length && RdnCharTables.IsRegexFlag(buffer[flagEnd]))
            {
                flagEnd++;
            }

            // If we're at end of buffer and not final, we may need more data for flags
            if (flagEnd >= buffer.Length && !_isFinalBlock)
            {
                return false;
            }

            // Phase 3: Set state
            // ValueSpan = "pattern/flags" (from patternStart to flagEnd, includes the middle /)
            int valueLength = flagEnd - patternStart;
            ValueSpan = buffer.Slice(patternStart, valueLength);
            ValueIsEscaped = hasEscape;
            _tokenType = RdnTokenType.RdnRegExp;
            int totalConsumed = flagEnd - start; // opening / + pattern + closing / + flags
            _consumed += totalConsumed;
            _bytePositionInLine += totalConsumed;
            _isNotPrimitive = false;

            return true;
        }

        /// <summary>
        /// Consumes an RDN base64 binary literal: b"..."
        /// ValueSpan is set to the content between quotes (the base64 chars).
        /// ValueIsEscaped is set to false to indicate base64 encoding.
        /// </summary>
        private bool ConsumeBinaryB64()
        {
            ReadOnlySpan<byte> buffer = _buffer;
            int start = _consumed;
            Debug.Assert(buffer[start] == RdnConstants.LetterB);

            int quotePos = start + 1;
            if (quotePos >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.LetterB);
                }
                return false;
            }

            if (buffer[quotePos] != RdnConstants.Quote)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, buffer[quotePos]);
            }

            int contentStart = quotePos + 1;

            // Scan for closing quote — binary content has no escape sequences
            int i = contentStart;
            while (i < buffer.Length && buffer[i] != RdnConstants.Quote)
            {
                i++;
            }

            if (i >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Quote);
                }
                return false;
            }

            int contentLength = i - contentStart;

            // Validate base64 content
            ReadOnlySpan<byte> content = buffer.Slice(contentStart, contentLength);
            for (int j = 0; j < content.Length; j++)
            {
                byte c = content[j];
                bool isValid = (c >= (byte)'A' && c <= (byte)'Z') ||
                               (c >= (byte)'a' && c <= (byte)'z') ||
                               (c >= (byte)'0' && c <= (byte)'9') ||
                               c == (byte)'+' || c == (byte)'/';
                if (!isValid)
                {
                    // Allow trailing padding
                    if (c == (byte)'=')
                    {
                        // Padding must only appear at end
                        for (int k = j; k < content.Length; k++)
                        {
                            if (content[k] != (byte)'=')
                            {
                                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, content[k]);
                            }
                        }
                        break;
                    }
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, c);
                }
            }

            // Validate: total length (including padding) must be multiple of 4 (if non-empty)
            if (contentLength > 0 && contentLength % 4 != 0)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.LetterB);
            }

            int closingQuote = i;
            int totalConsumed = closingQuote + 1 - start; // b + " + content + "

            ValueSpan = buffer.Slice(contentStart, contentLength);
            ValueIsEscaped = false; // false = base64
            _tokenType = RdnTokenType.RdnBinary;
            _consumed += totalConsumed;
            _bytePositionInLine += totalConsumed;
            _isNotPrimitive = false;

            return true;
        }

        /// <summary>
        /// Consumes an RDN hex binary literal: x"..."
        /// ValueSpan is set to the content between quotes (the hex chars).
        /// ValueIsEscaped is set to true to indicate hex encoding.
        /// </summary>
        private bool ConsumeBinaryHex()
        {
            ReadOnlySpan<byte> buffer = _buffer;
            int start = _consumed;
            Debug.Assert(buffer[start] == RdnConstants.LetterX);

            int quotePos = start + 1;
            if (quotePos >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.LetterX);
                }
                return false;
            }

            if (buffer[quotePos] != RdnConstants.Quote)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, buffer[quotePos]);
            }

            int contentStart = quotePos + 1;

            // Scan for closing quote — binary content has no escape sequences
            int i = contentStart;
            while (i < buffer.Length && buffer[i] != RdnConstants.Quote)
            {
                i++;
            }

            if (i >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Quote);
                }
                return false;
            }

            int contentLength = i - contentStart;

            // Validate hex content: all chars must be hex digits
            ReadOnlySpan<byte> content = buffer.Slice(contentStart, contentLength);
            for (int j = 0; j < content.Length; j++)
            {
                if (!RdnReaderHelper.IsHexDigit(content[j]))
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, content[j]);
                }
            }

            // Validate: hex must have even length
            if (contentLength % 2 != 0)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.LetterX);
            }

            int closingQuote = i;
            int totalConsumed = closingQuote + 1 - start; // x + " + content + "

            ValueSpan = buffer.Slice(contentStart, contentLength);
            ValueIsEscaped = true; // true = hex encoding
            _tokenType = RdnTokenType.RdnBinary;
            _consumed += totalConsumed;
            _bytePositionInLine += totalConsumed;
            _isNotPrimitive = false;

            return true;
        }

        /// <summary>
        /// Scans forward from bodyStart to find the end of an RDN literal body using the terminator lookup table.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ScanRdnBodyCheck(ReadOnlySpan<byte> buffer, int bodyStart, out int bodyEnd)
        {
            int i = bodyStart;
            while (i < buffer.Length)
            {
                if (RdnCharTables.IsTerminator(buffer[i]))
                {
                    bodyEnd = i;
                    return true;
                }
                i++;
            }

            // Reached end of buffer
            if (_isFinalBlock)
            {
                // At end of input, the literal ends here
                bodyEnd = i;
                return true;
            }

            bodyEnd = i;
            return false; // Need more data
        }

        /// <summary>
        /// Scans forward from bodyStart to find the end of an RDN literal body.
        /// Returns the index of the first terminator (or buffer length if at end of final block).
        /// </summary>
        private int ScanRdnBody(ReadOnlySpan<byte> buffer, int bodyStart)
        {
            int i = bodyStart;
            while (i < buffer.Length)
            {
                if (RdnCharTables.IsTerminator(buffer[i]))
                {
                    return i;
                }
                i++;
            }

            // At end of buffer
            return i;
        }

        /// <summary>
        /// Finishes consuming an RDN literal by setting the token state.
        /// </summary>
        private bool FinishRdnLiteral(ReadOnlySpan<byte> buffer, int bodyStart, int bodyEnd, RdnTokenType tokenType)
        {
            int bodyLength = bodyEnd - bodyStart;
            int totalLength = 1 + bodyLength; // @ + body

            ValueSpan = buffer.Slice(bodyStart, bodyLength);
            ValueIsEscaped = false;
            _tokenType = tokenType;
            _consumed += totalLength;
            _bytePositionInLine += totalLength;
            _isNotPrimitive = false;

            // Validate delimiter after the literal (if not at end of input)
            if (_consumed < (uint)buffer.Length)
            {
                // OK - there's data after the literal
                Debug.Assert(
                    RdnConstants.Delimiters.Contains(buffer[_consumed]) ||
                    buffer[_consumed] == RdnConstants.CloseBrace ||
                    buffer[_consumed] == RdnConstants.CloseBracket,
                    $"Expected delimiter after RDN literal but got '{(char)buffer[_consumed]}'");
            }
            else if (_isNotPrimitive && IsLastSpan)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, buffer[_consumed - 1]);
            }

            return true;
        }
    }
}
