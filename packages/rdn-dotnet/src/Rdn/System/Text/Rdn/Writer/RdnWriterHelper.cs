// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Rdn.Encodings.Web;
using System.Text.Unicode;

namespace Rdn
{
    internal static partial class RdnWriterHelper
    {
        public static void WriteIndentation(Span<byte> buffer, int indent, byte indentByte)
        {
            Debug.Assert(buffer.Length >= indent);

            // Based on perf tests, the break-even point where vectorized Fill is faster
            // than explicitly writing the space in a loop is 8.
            if (indent < 8)
            {
                int i = 0;
                while (i + 1 < indent)
                {
                    buffer[i++] = indentByte;
                    buffer[i++] = indentByte;
                }

                if (i < indent)
                {
                    buffer[i] = indentByte;
                }
            }
            else
            {
                buffer.Slice(0, indent).Fill(indentByte);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateNewLine(string value)
        {
            if (value is null)
                ThrowHelper.ThrowArgumentNullException(nameof(value));

            if (value is not RdnConstants.NewLineLineFeed and not RdnConstants.NewLineCarriageReturnLineFeed)
                ThrowHelper.ThrowArgumentOutOfRangeException_NewLine(nameof(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateIndentCharacter(char value)
        {
            if (value is not RdnConstants.DefaultIndentCharacter and not RdnConstants.TabIndentCharacter)
                ThrowHelper.ThrowArgumentOutOfRangeException_IndentCharacter(nameof(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateIndentSize(int value)
        {
            if (value is < RdnConstants.MinimumIndentSize or > RdnConstants.MaximumIndentSize)
                ThrowHelper.ThrowArgumentOutOfRangeException_IndentSize(nameof(value), RdnConstants.MinimumIndentSize, RdnConstants.MaximumIndentSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateProperty(ReadOnlySpan<byte> propertyName)
        {
            if (propertyName.Length > RdnConstants.MaxUnescapedTokenSize)
                ThrowHelper.ThrowArgumentException_PropertyNameTooLarge(propertyName.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateValue(ReadOnlySpan<byte> value)
        {
            if (value.Length > RdnConstants.MaxUnescapedTokenSize)
                ThrowHelper.ThrowArgumentException_ValueTooLarge(value.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateDouble(double value)
        {
            // RDN supports NaN, Infinity, and -Infinity as bare number literals.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateSingle(float value)
        {
            // RDN supports NaN, Infinity, and -Infinity as bare number literals.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateProperty(ReadOnlySpan<char> propertyName)
        {
            if (propertyName.Length > RdnConstants.MaxCharacterTokenSize)
                ThrowHelper.ThrowArgumentException_PropertyNameTooLarge(propertyName.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidateValue(ReadOnlySpan<char> value)
        {
            if (value.Length > RdnConstants.MaxCharacterTokenSize)
                ThrowHelper.ThrowArgumentException_ValueTooLarge(value.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
        {
            if (propertyName.Length > RdnConstants.MaxCharacterTokenSize || value.Length > RdnConstants.MaxUnescapedTokenSize)
                ThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<char> value)
        {
            if (propertyName.Length > RdnConstants.MaxUnescapedTokenSize || value.Length > RdnConstants.MaxCharacterTokenSize)
                ThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value)
        {
            if (propertyName.Length > RdnConstants.MaxUnescapedTokenSize || value.Length > RdnConstants.MaxUnescapedTokenSize)
                ThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyAndValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            if (propertyName.Length > RdnConstants.MaxCharacterTokenSize || value.Length > RdnConstants.MaxCharacterTokenSize)
                ThrowHelper.ThrowArgumentException(propertyName, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyNameLength(ReadOnlySpan<char> propertyName)
        {
            if (propertyName.Length > RdnConstants.MaxCharacterTokenSize)
                ThrowHelper.ThrowPropertyNameTooLargeArgumentException(propertyName.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ValidatePropertyNameLength(ReadOnlySpan<byte> propertyName)
        {
            if (propertyName.Length > RdnConstants.MaxUnescapedTokenSize)
                ThrowHelper.ThrowPropertyNameTooLargeArgumentException(propertyName.Length);
        }

        internal static void ValidateNumber(ReadOnlySpan<byte> utf8FormattedNumber)
        {
            // This is a simplified version of the number reader from Utf8RdnReader.TryGetNumber,
            // because it doesn't need to deal with "NeedsMoreData", or remembering the format.
            //
            // The Debug.Asserts in this method should change to validated ArgumentExceptions if/when
            // writing a formatted number becomes public API.
            Debug.Assert(!utf8FormattedNumber.IsEmpty);

            // RDN supports NaN, Infinity, and -Infinity as bare number literals.
            if (utf8FormattedNumber.SequenceEqual(RdnConstants.NaNValue) ||
                utf8FormattedNumber.SequenceEqual(RdnConstants.PositiveInfinityValue) ||
                utf8FormattedNumber.SequenceEqual(RdnConstants.NegativeInfinityValue))
            {
                return;
            }

            int i = 0;

            if (utf8FormattedNumber[i] == '-')
            {
                i++;

                if (utf8FormattedNumber.Length <= i)
                {
                    throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
                }
            }

            if (utf8FormattedNumber[i] == '0')
            {
                i++;
            }
            else
            {
                while (i < utf8FormattedNumber.Length && RdnHelpers.IsDigit(utf8FormattedNumber[i]))
                {
                    i++;
                }
            }

            if (i == utf8FormattedNumber.Length)
            {
                return;
            }

            // The non digit character inside the number
            byte val = utf8FormattedNumber[i];

            if (val == '.')
            {
                i++;

                if (utf8FormattedNumber.Length <= i)
                {
                    throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
                }

                while (i < utf8FormattedNumber.Length && RdnHelpers.IsDigit(utf8FormattedNumber[i]))
                {
                    i++;
                }

                if (i == utf8FormattedNumber.Length)
                {
                    return;
                }

                Debug.Assert(i < utf8FormattedNumber.Length);
                val = utf8FormattedNumber[i];
            }

            if (val == 'e' || val == 'E')
            {
                i++;

                if (utf8FormattedNumber.Length <= i)
                {
                    throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
                }

                val = utf8FormattedNumber[i];

                if (val == '+' || val == '-')
                {
                    i++;
                }
            }
            else
            {
                throw new ArgumentException(
                    SR.Format(SR.ExpectedEndOfDigitNotFound, ThrowHelper.GetPrintableString(val)),
                    nameof(utf8FormattedNumber));
            }

            if (utf8FormattedNumber.Length <= i)
            {
                throw new ArgumentException(SR.RequiredDigitNotFoundEndOfData, nameof(utf8FormattedNumber));
            }

            while (i < utf8FormattedNumber.Length && RdnHelpers.IsDigit(utf8FormattedNumber[i]))
            {
                i++;
            }

            if (i != utf8FormattedNumber.Length)
            {
                throw new ArgumentException(
                    SR.Format(SR.ExpectedEndOfDigitNotFound, ThrowHelper.GetPrintableString(utf8FormattedNumber[i])),
                    nameof(utf8FormattedNumber));
            }
        }

#if !NET
        private static readonly UTF8Encoding s_utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
#endif

        public static bool IsValidUtf8String(ReadOnlySpan<byte> bytes)
        {
#if NET
            return Utf8.IsValid(bytes);
#else
            try
            {
                _ = s_utf8Encoding.GetCharCount(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
#endif
        }

        internal static OperationStatus ToUtf8(ReadOnlySpan<char> source, Span<byte> destination, out int written)
        {
#if NET
            OperationStatus status = Utf8.FromUtf16(source, destination, out int charsRead, out written, replaceInvalidSequences: false, isFinalBlock: true);
            Debug.Assert(status is OperationStatus.Done or OperationStatus.DestinationTooSmall or OperationStatus.InvalidData);
            Debug.Assert(charsRead == source.Length || status is not OperationStatus.Done);
            return status;
#else
            written = 0;
            try
            {
                written = s_utf8Encoding.GetBytes(source, destination);
                return OperationStatus.Done;
            }
            catch (EncoderFallbackException)
            {
                return OperationStatus.InvalidData;
            }
            catch (ArgumentException)
            {
                return OperationStatus.DestinationTooSmall;
            }
#endif
        }

        internal delegate T WriteCallback<T>(ReadOnlySpan<byte> serializedValue);

        internal static T WriteString<T>(ReadOnlySpan<byte> utf8Value, WriteCallback<T> writeCallback)
        {
            int firstByteToEscape = RdnWriterHelper.NeedsEscaping(utf8Value, JavaScriptEncoder.Default);

            if (firstByteToEscape == -1)
            {
                int quotedLength = utf8Value.Length + 2;
                byte[]? rented = null;

                try
                {
                    Span<byte> quotedValue = quotedLength > RdnConstants.StackallocByteThreshold
                        ? (rented = ArrayPool<byte>.Shared.Rent(quotedLength)).AsSpan(0, quotedLength)
                        : stackalloc byte[RdnConstants.StackallocByteThreshold].Slice(0, quotedLength);

                    quotedValue[0] = RdnConstants.Quote;
                    utf8Value.CopyTo(quotedValue.Slice(1));
                    quotedValue[quotedValue.Length - 1] = RdnConstants.Quote;

                    return writeCallback(quotedValue);
                }
                finally
                {
                    if (rented != null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }
            else
            {
                Debug.Assert(int.MaxValue / RdnConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);

                int length = checked(2 + RdnWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstByteToEscape));
                byte[]? rented = null;

                try
                {
                    scoped Span<byte> escapedValue;

                    if (length > RdnConstants.StackallocByteThreshold)
                    {
                        rented = ArrayPool<byte>.Shared.Rent(length);
                        escapedValue = rented;
                    }
                    else
                    {
                        escapedValue = stackalloc byte[RdnConstants.StackallocByteThreshold];
                    }

                    escapedValue[0] = RdnConstants.Quote;
                    RdnWriterHelper.EscapeString(utf8Value, escapedValue.Slice(1), firstByteToEscape, JavaScriptEncoder.Default, out int written);
                    escapedValue[1 + written] = RdnConstants.Quote;

                    return writeCallback(escapedValue.Slice(0, written + 2));
                }
                finally
                {
                    if (rented != null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }
        }
    }
}
