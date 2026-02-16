// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        /// <summary>
        /// Writes the input as RDN content. It is expected that the input content is a single complete RDN value.
        /// </summary>
        /// <param name="rdn">The raw RDN content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is an RFC 8259-compliant RDN payload.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="rdn"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or greater than 715,827,882 (<see cref="int.MaxValue"/> / 3).</exception>
        /// <exception cref="RdnException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single RDN value according to the RDN RFC (https://tools.ietf.org/html/rfc8259)
        /// or the input RDN exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused RDN values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid RDN
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="RdnWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="RdnWriterOptions.Indented"/> and <see cref="RdnWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue([StringSyntax(StringSyntaxAttribute.Json)] string rdn, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            ArgumentNullException.ThrowIfNull(rdn);

            TranscodeAndWriteRawValue(rdn.AsSpan(), skipInputValidation);
        }

        /// <summary>
        /// Writes the input as RDN content. It is expected that the input content is a single complete RDN value.
        /// </summary>
        /// <param name="rdn">The raw RDN content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is an RFC 8259-compliant RDN payload.</param>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or greater than 715,827,882 (<see cref="int.MaxValue"/> / 3).</exception>
        /// <exception cref="RdnException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single RDN value according to the RDN RFC (https://tools.ietf.org/html/rfc8259)
        /// or the input RDN exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused RDN values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid RDN
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="RdnWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="RdnWriterOptions.Indented"/> and <see cref="RdnWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> rdn, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            TranscodeAndWriteRawValue(rdn, skipInputValidation);
        }

        /// <summary>
        /// Writes the input as RDN content. It is expected that the input content is a single complete RDN value.
        /// </summary>
        /// <param name="utf8Rdn">The raw RDN content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is an RFC 8259-compliant RDN payload.</param>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or greater than or equal to <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="RdnException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single RDN value according to the RDN RFC (https://tools.ietf.org/html/rfc8259)
        /// or the input RDN exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused RDN values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid RDN
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="RdnWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="RdnWriterOptions.Indented"/> and <see cref="RdnWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue(ReadOnlySpan<byte> utf8Rdn, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (utf8Rdn.Length == int.MaxValue)
            {
                ThrowHelper.ThrowArgumentException_ValueTooLarge(int.MaxValue);
            }

            WriteRawValueCore(utf8Rdn, skipInputValidation);
        }

        /// <summary>
        /// Writes the input as RDN content. It is expected that the input content is a single complete RDN value.
        /// </summary>
        /// <param name="utf8Rdn">The raw RDN content to write.</param>
        /// <param name="skipInputValidation">Whether to validate if the input is an RFC 8259-compliant RDN payload.</param>
        /// <exception cref="ArgumentException">Thrown if the length of the input is zero or equal to <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="RdnException">
        /// Thrown if <paramref name="skipInputValidation"/> is <see langword="false"/>, and the input
        /// is not a valid, complete, single RDN value according to the RDN RFC (https://tools.ietf.org/html/rfc8259)
        /// or the input RDN exceeds a recursive depth of 64.
        /// </exception>
        /// <remarks>
        /// When writing untrused RDN values, do not set <paramref name="skipInputValidation"/> to <see langword="true"/> as this can result in invalid RDN
        /// being written, and/or the overall payload being written to the writer instance being invalid.
        ///
        /// When using this method, the input content will be written to the writer destination as-is, unless validation fails (when it is enabled).
        ///
        /// The <see cref="RdnWriterOptions.SkipValidation"/> value for the writer instance is honored when using this method.
        ///
        /// The <see cref="RdnWriterOptions.Indented"/> and <see cref="RdnWriterOptions.Encoder"/> values for the writer instance are not applied when using this method.
        /// </remarks>
        public void WriteRawValue(ReadOnlySequence<byte> utf8Rdn, bool skipInputValidation = false)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            long utf8RdnLen = utf8Rdn.Length;

            if (utf8RdnLen == 0)
            {
                ThrowHelper.ThrowArgumentException(SR.ExpectedRdnTokens);
            }
            if (utf8RdnLen >= int.MaxValue)
            {
                ThrowHelper.ThrowArgumentException_ValueTooLarge(utf8RdnLen);
            }

            if (skipInputValidation)
            {
                // Treat all unvalidated raw RDN value writes as string. If the payload is valid, this approach does
                // not affect structural validation since a string token is equivalent to a complete object, array,
                // or other complete RDN tokens when considering structural validation on subsequent writer calls.
                // If the payload is not valid, then we make no guarantees about the structural validation of the final payload.
                _tokenType = RdnTokenType.String;
            }
            else
            {
                // Utilize reader validation.
                Utf8RdnReader reader = new(utf8Rdn);
                while (reader.Read());
                _tokenType = reader.TokenType;
            }

            Debug.Assert(utf8RdnLen < int.MaxValue);
            int len = (int)utf8RdnLen;

            // TODO (https://github.com/dotnet/runtime/issues/29293):
            // investigate writing this in chunks, rather than requesting one potentially long, contiguous buffer.
            int maxRequired = len + 1; // Optionally, 1 list separator. We've guarded against integer overflow earlier in the call stack.

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            utf8Rdn.CopyTo(output.Slice(BytesPending));
            BytesPending += len;

            SetFlagToAddListSeparatorBeforeNextItem();
        }

        private void TranscodeAndWriteRawValue(ReadOnlySpan<char> rdn, bool skipInputValidation)
        {
            if (rdn.Length > RdnConstants.MaxUtf16RawValueLength)
            {
                ThrowHelper.ThrowArgumentException_ValueTooLarge(rdn.Length);
            }

            byte[]? tempArray = null;

            // For performance, avoid obtaining actual byte count unless memory usage is higher than the threshold.
            Span<byte> utf8Rdn =
                // Use stack memory
                rdn.Length <= (RdnConstants.StackallocByteThreshold / RdnConstants.MaxExpansionFactorWhileTranscoding) ? stackalloc byte[RdnConstants.StackallocByteThreshold] :
                // Use a pooled array
                rdn.Length <= (RdnConstants.ArrayPoolMaxSizeBeforeUsingNormalAlloc / RdnConstants.MaxExpansionFactorWhileTranscoding) ? tempArray = ArrayPool<byte>.Shared.Rent(rdn.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) :
                // Use a normal alloc since the pool would create a normal alloc anyway based on the threshold (per current implementation)
                // and by using a normal alloc we can avoid the Clear().
                new byte[RdnReaderHelper.GetUtf8ByteCount(rdn)];

            try
            {
                int actualByteCount = RdnReaderHelper.GetUtf8FromText(rdn, utf8Rdn);
                utf8Rdn = utf8Rdn.Slice(0, actualByteCount);
                WriteRawValueCore(utf8Rdn, skipInputValidation);
            }
            finally
            {
                if (tempArray != null)
                {
                    utf8Rdn.Clear();
                    ArrayPool<byte>.Shared.Return(tempArray);
                }
            }
        }

        private void WriteRawValueCore(ReadOnlySpan<byte> utf8Rdn, bool skipInputValidation)
        {
            int len = utf8Rdn.Length;

            if (len == 0)
            {
                ThrowHelper.ThrowArgumentException(SR.ExpectedRdnTokens);
            }

            // In the UTF-16-based entry point methods above, we validate that the payload length <= int.MaxValue /3.
            // The result of this division will be rounded down, so even if every input character needs to be transcoded
            // (with expansion factor of 3), the resulting payload would be less than int.MaxValue,
            // as (int.MaxValue/3) * 3 is less than int.MaxValue.
            Debug.Assert(len < int.MaxValue);

            if (skipInputValidation)
            {
                // Treat all unvalidated raw RDN value writes as string. If the payload is valid, this approach does
                // not affect structural validation since a string token is equivalent to a complete object, array,
                // or other complete RDN tokens when considering structural validation on subsequent writer calls.
                // If the payload is not valid, then we make no guarantees about the structural validation of the final payload.
                _tokenType = RdnTokenType.String;
            }
            else
            {
                // Utilize reader validation.
                Utf8RdnReader reader = new(utf8Rdn);
                while (reader.Read());
                _tokenType = reader.TokenType;
            }

            // TODO (https://github.com/dotnet/runtime/issues/29293):
            // investigate writing this in chunks, rather than requesting one potentially long, contiguous buffer.
            int maxRequired = len + 1; // Optionally, 1 list separator. We've guarded against integer overflow earlier in the call stack.

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            utf8Rdn.CopyTo(output.Slice(BytesPending));
            BytesPending += len;

            SetFlagToAddListSeparatorBeforeNextItem();
        }
    }
}
