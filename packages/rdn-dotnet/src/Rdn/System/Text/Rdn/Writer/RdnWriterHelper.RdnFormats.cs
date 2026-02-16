// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    internal static partial class RdnWriterHelper
    {
        // Pre-computed ASCII digit pairs "00" through "99" (200 bytes)
        private static ReadOnlySpan<byte> DigitPairs => "00010203040506070809101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899"u8;

        /// <summary>
        /// Writes a two-digit value (00-99) to the output span. Returns 2.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTwoDigit(Span<byte> output, int value)
        {
            Debug.Assert(value >= 0 && value <= 99);
            int idx = value * 2;
            output[0] = DigitPairs[idx];
            output[1] = DigitPairs[idx + 1];
        }

        /// <summary>
        /// Writes a four-digit value (0000-9999) to the output span. Returns 4.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteFourDigit(Span<byte> output, int value)
        {
            Debug.Assert(value >= 0 && value <= 9999);
            WriteTwoDigit(output, value / 100);
            WriteTwoDigit(output.Slice(2), value % 100);
        }

        /// <summary>
        /// Writes a three-digit value (000-999) to the output span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteThreeDigit(Span<byte> output, int value)
        {
            Debug.Assert(value >= 0 && value <= 999);
            output[0] = (byte)('0' + value / 100);
            WriteTwoDigit(output.Slice(1), value % 100);
        }

        /// <summary>
        /// Formats a DateTime as YYYY-MM-DDTHH:mm:ss.sssZ (24 bytes) directly into the buffer.
        /// The DateTime is converted to UTC before formatting.
        /// </summary>
        public static int FormatRdnDateTime(Span<byte> output, DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

            // YYYY-MM-DDTHH:mm:ss.sssZ = 24 chars
            Debug.Assert(output.Length >= 24);

            WriteFourDigit(output, utc.Year);
            output[4] = (byte)'-';
            WriteTwoDigit(output.Slice(5), utc.Month);
            output[7] = (byte)'-';
            WriteTwoDigit(output.Slice(8), utc.Day);
            output[10] = (byte)'T';
            WriteTwoDigit(output.Slice(11), utc.Hour);
            output[13] = (byte)':';
            WriteTwoDigit(output.Slice(14), utc.Minute);
            output[16] = (byte)':';
            WriteTwoDigit(output.Slice(17), utc.Second);
            output[19] = (byte)'.';
            WriteThreeDigit(output.Slice(20), utc.Millisecond);
            output[23] = (byte)'Z';

            return 24;
        }

        /// <summary>
        /// Formats a DateTimeOffset as YYYY-MM-DDTHH:mm:ss.sssZ (24 bytes) directly into the buffer.
        /// The value is converted to UTC before formatting.
        /// </summary>
        public static int FormatRdnDateTimeOffset(Span<byte> output, DateTimeOffset value)
        {
            return FormatRdnDateTime(output, value.UtcDateTime);
        }

        /// <summary>
        /// Formats a DateOnly as YYYY-MM-DD (10 bytes) directly into the buffer.
        /// </summary>
        public static int FormatRdnDateOnly(Span<byte> output, DateOnly value)
        {
            // YYYY-MM-DD = 10 chars
            Debug.Assert(output.Length >= 10);

            WriteFourDigit(output, value.Year);
            output[4] = (byte)'-';
            WriteTwoDigit(output.Slice(5), value.Month);
            output[7] = (byte)'-';
            WriteTwoDigit(output.Slice(8), value.Day);

            return 10;
        }

        /// <summary>
        /// Formats a TimeOnly as HH:MM:SS[.mmm] into the buffer.
        /// Returns the number of bytes written (8 or 12).
        /// </summary>
        public static int FormatRdnTimeOnly(Span<byte> output, TimeOnly value)
        {
            Debug.Assert(output.Length >= 12);

            WriteTwoDigit(output, value.Hour);
            output[2] = (byte)':';
            WriteTwoDigit(output.Slice(3), value.Minute);
            output[5] = (byte)':';
            WriteTwoDigit(output.Slice(6), value.Second);

            int ms = value.Millisecond;
            if (ms > 0)
            {
                output[8] = (byte)'.';
                WriteThreeDigit(output.Slice(9), ms);
                return 12;
            }

            return 8;
        }

        /// <summary>
        /// Writes a variable-length non-negative integer directly to the output span.
        /// Returns the number of bytes written.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteInt(Span<byte> output, int value)
        {
            Debug.Assert(value >= 0);

            if (value < 10)
            {
                output[0] = (byte)('0' + value);
                return 1;
            }

            if (value < 100)
            {
                WriteTwoDigit(output, value);
                return 2;
            }

            // For larger values, write digits right-to-left, then reverse
            int start = 0;
            int pos = 0;
            int v = value;
            while (v > 0)
            {
                output[pos++] = (byte)('0' + v % 10);
                v /= 10;
            }
            // Reverse the digits
            int left = start, right = pos - 1;
            while (left < right)
            {
                (output[left], output[right]) = (output[right], output[left]);
                left++;
                right--;
            }
            return pos;
        }

        /// <summary>
        /// Formats a TimeSpan as an ISO 8601 duration directly into a Span&lt;byte&gt; buffer.
        /// Zero-allocation hot path for the writer.
        /// Max output: "-P99999999DT23H59M59.999S" = ~26 bytes.
        /// Returns the number of bytes written.
        /// </summary>
        public static int FormatTimeSpanAsIsoDuration(Span<byte> output, TimeSpan value)
        {
            Debug.Assert(output.Length >= 26);

            bool negative = value < TimeSpan.Zero;
            if (negative) value = value.Negate();

            int days = value.Days;
            int hours = value.Hours;
            int minutes = value.Minutes;
            int seconds = value.Seconds;
            int milliseconds = value.Milliseconds;

            int pos = 0;

            if (negative)
                output[pos++] = (byte)'-';

            output[pos++] = (byte)'P';

            if (days > 0)
            {
                pos += WriteInt(output.Slice(pos), days);
                output[pos++] = (byte)'D';
            }

            if (hours > 0 || minutes > 0 || seconds > 0 || milliseconds > 0)
            {
                output[pos++] = (byte)'T';
                if (hours > 0)
                {
                    pos += WriteInt(output.Slice(pos), hours);
                    output[pos++] = (byte)'H';
                }
                if (minutes > 0)
                {
                    pos += WriteInt(output.Slice(pos), minutes);
                    output[pos++] = (byte)'M';
                }
                if (milliseconds > 0)
                {
                    // Write seconds.milliseconds, trimming trailing zeros from the fraction
                    pos += WriteInt(output.Slice(pos), seconds);
                    output[pos++] = (byte)'.';
                    // Write up to 3 fractional digits, trimming trailing zeros
                    if (milliseconds % 100 == 0)
                    {
                        // Only 1 digit needed (e.g. 500 -> .5)
                        output[pos++] = (byte)('0' + milliseconds / 100);
                    }
                    else if (milliseconds % 10 == 0)
                    {
                        // 2 digits needed (e.g. 120 -> .12)
                        WriteTwoDigit(output.Slice(pos), milliseconds / 10);
                        pos += 2;
                    }
                    else
                    {
                        // All 3 digits needed (e.g. 123 -> .123)
                        WriteThreeDigit(output.Slice(pos), milliseconds);
                        pos += 3;
                    }
                    output[pos++] = (byte)'S';
                }
                else if (seconds > 0)
                {
                    pos += WriteInt(output.Slice(pos), seconds);
                    output[pos++] = (byte)'S';
                }
            }

            // P with no components = zero duration
            int minLen = negative ? 2 : 1;
            if (pos == minLen)
            {
                output[pos++] = (byte)'0';
                output[pos++] = (byte)'D';
            }

            return pos;
        }

        /// <summary>
        /// Converts a TimeSpan to an ISO 8601 duration string (e.g. "P1DT2H3M4S", "PT30M", "P0D").
        /// </summary>
        public static string FormatTimeSpanAsIsoDuration(TimeSpan value)
        {
            bool negative = value < TimeSpan.Zero;
            if (negative) value = value.Negate();

            int days = value.Days;
            int hours = value.Hours;
            int minutes = value.Minutes;
            int seconds = value.Seconds;
            int milliseconds = value.Milliseconds;

            var sb = new System.Text.StringBuilder(16);
            if (negative) sb.Append('-');
            sb.Append('P');

            if (days > 0)
                sb.Append(days).Append('D');

            if (hours > 0 || minutes > 0 || seconds > 0 || milliseconds > 0)
            {
                sb.Append('T');
                if (hours > 0)
                    sb.Append(hours).Append('H');
                if (minutes > 0)
                    sb.Append(minutes).Append('M');
                if (milliseconds > 0)
                {
                    double totalSeconds = seconds + milliseconds / 1000.0;
                    sb.Append(totalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append('S');
                }
                else if (seconds > 0)
                {
                    sb.Append(seconds).Append('S');
                }
            }

            // P with no components = zero duration
            if (sb.Length == (negative ? 2 : 1))
                sb.Append("0D");

            return sb.ToString();
        }
    }
}
