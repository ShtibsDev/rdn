// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public ref partial struct Utf8JsonReader
    {
        /// <summary>
        /// Consumes an RDN @-prefixed literal (DateTime, TimeOnly, or Duration).
        /// ValueSpan is set to the body after the @ sign.
        /// </summary>
        private bool ConsumeRdnLiteral()
        {
            ReadOnlySpan<byte> buffer = _buffer;
            int start = _consumed;
            Debug.Assert(buffer[start] == JsonConstants.AtSign);

            int bodyStart = start + 1;

            if (bodyStart >= buffer.Length)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonConstants.AtSign);
                }
                return false;
            }

            byte first = buffer[bodyStart];

            // Duration: @P...
            if (first == JsonConstants.LetterP)
            {
                return ConsumeRdnDuration(buffer, bodyStart);
            }

            // Must start with a digit for DateTime, TimeOnly, or unix timestamp
            if (!JsonHelpers.IsDigit(first))
            {
                ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, first);
            }

            // Scan the body to find its end
            int bodyEnd = ScanRdnBody(buffer, bodyStart);
            int bodyLength = bodyEnd - bodyStart;

            if (bodyLength < 1)
            {
                if (_isFinalBlock)
                {
                    ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonConstants.AtSign);
                }
                return false;
            }

            // Disambiguate: check fixed positions
            // TimeOnly: HH:MM:SS — colon at position 2
            // DateTime: YYYY-MM-DD — hyphen at position 4
            // Unix timestamp: all digits
            if (bodyLength >= 3 && buffer[bodyStart + 2] == JsonConstants.Colon)
            {
                return FinishRdnLiteral(buffer, bodyStart, bodyEnd, JsonTokenType.RdnTimeOnly);
            }

            if (bodyLength >= 5 && buffer[bodyStart + 4] == JsonConstants.Hyphen)
            {
                return FinishRdnLiteral(buffer, bodyStart, bodyEnd, JsonTokenType.RdnDateTime);
            }

            // Unix timestamp (all digits)
            return FinishRdnLiteral(buffer, bodyStart, bodyEnd, JsonTokenType.RdnDateTime);
        }

        /// <summary>
        /// Consumes an RDN duration literal: @P[nY][nM][nD][T[nH][nM][nS]]
        /// </summary>
        private bool ConsumeRdnDuration(ReadOnlySpan<byte> buffer, int bodyStart)
        {
            Debug.Assert(buffer[bodyStart] == JsonConstants.LetterP);

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
                ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, JsonConstants.LetterP);
            }

            return FinishRdnLiteral(buffer, bodyStart, bodyEnd, JsonTokenType.RdnDuration);
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
        private bool FinishRdnLiteral(ReadOnlySpan<byte> buffer, int bodyStart, int bodyEnd, JsonTokenType tokenType)
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
                    JsonConstants.Delimiters.Contains(buffer[_consumed]) ||
                    buffer[_consumed] == JsonConstants.CloseBrace ||
                    buffer[_consumed] == JsonConstants.CloseBracket,
                    $"Expected delimiter after RDN literal but got '{(char)buffer[_consumed]}'");
            }
            else if (_isNotPrimitive && IsLastSpan)
            {
                ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, buffer[_consumed - 1]);
            }

            return true;
        }
    }
}
