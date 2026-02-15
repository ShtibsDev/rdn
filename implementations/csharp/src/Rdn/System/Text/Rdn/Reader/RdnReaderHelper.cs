// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Rdn
{
    internal static partial class RdnReaderHelper
    {
        // Characters that require the bracketed property name syntax in RDN Path.
        private static readonly SearchValues<char> s_specialCharacters = SearchValues.Create("$. '/\"[]()\t\n\r\f\b\\\u0085\u2028\u2029");

        // Characters that need to be escaped in the single-quoted bracket notation.
        private static readonly SearchValues<char> s_charactersToEscape = SearchValues.Create("'\\");

        public static bool ContainsSpecialCharacters(this ReadOnlySpan<char> text) =>
            text.ContainsAny(s_specialCharacters);

        /// <summary>
        /// Appends a property name escaped for use in RDN Path single-quoted bracket notation.
        /// Escapes single quotes as \' and backslashes as \\.
        /// </summary>
        public static void AppendEscapedPropertyName(this ref ValueStringBuilder builder, string propertyName)
        {
            ReadOnlySpan<char> span = propertyName.AsSpan();

            int i = span.IndexOfAny(s_charactersToEscape);

            // Fast path: if no characters need escaping, append directly.
            if (i < 0)
            {
                builder.Append(propertyName);
                return;
            }

            // Append the portion before the first character needing escaping.
            if (i > 0)
            {
                builder.Append(span.Slice(0, i));
            }

            // Escape characters from position i onward.
            for (; i < span.Length; i++)
            {
                char c = span[i];
                if (c is '\\' or '\'')
                {
                    builder.Append('\\');
                }

                builder.Append(c);
            }
        }

        /// <summary>
        /// Appends a property name escaped for use in RDN Path single-quoted bracket notation.
        /// Escapes single quotes as \' and backslashes as \\.
        /// </summary>
        public static void AppendEscapedPropertyName(this StringBuilder builder, string propertyName)
        {
            ReadOnlySpan<char> span = propertyName.AsSpan();

            int i = span.IndexOfAny(s_charactersToEscape);

            // Fast path: if no characters need escaping, append directly.
            if (i < 0)
            {
                builder.Append(propertyName);
                return;
            }

            // Append the portion before the first character needing escaping.
            if (i > 0)
            {
                builder.Append(span.Slice(0, i));
            }

            // Escape characters from position i onward.
            for (; i < span.Length; i++)
            {
                char c = span[i];
                if (c is '\\' or '\'')
                {
                    builder.Append('\\');
                }

                builder.Append(c);
            }
        }

        public static (int, int) CountNewLines(ReadOnlySpan<byte> data)
        {
            int lastLineFeedIndex = data.LastIndexOf(RdnConstants.LineFeed);
            int newLines = 0;

            if (lastLineFeedIndex >= 0)
            {
                newLines = 1;
                data = data.Slice(0, lastLineFeedIndex);
#if NET
                newLines += data.Count(RdnConstants.LineFeed);
#else
                int pos;
                while ((pos = data.IndexOf(RdnConstants.LineFeed)) >= 0)
                {
                    newLines++;
                    data = data.Slice(pos + 1);
                }
#endif
            }

            return (newLines, lastLineFeedIndex);
        }

        internal static RdnValueKind ToValueKind(this RdnTokenType tokenType)
        {
            switch (tokenType)
            {
                case RdnTokenType.None:
                    return RdnValueKind.Undefined;
                case RdnTokenType.StartArray:
                    return RdnValueKind.Array;
                case RdnTokenType.StartObject:
                    return RdnValueKind.Object;
                case RdnTokenType.StartSet:
                    return RdnValueKind.Set;
                case RdnTokenType.StartMap:
                    return RdnValueKind.Map;
                case RdnTokenType.String:
                case RdnTokenType.Number:
                case RdnTokenType.True:
                case RdnTokenType.False:
                case RdnTokenType.Null:
                case RdnTokenType.RdnDateTime:
                case RdnTokenType.RdnTimeOnly:
                case RdnTokenType.RdnDuration:
                case RdnTokenType.RdnRegExp:
                    // This is the offset between the set of literals within RdnValueType and RdnTokenType
                    // Essentially: RdnTokenType.Null - RdnValueType.Null (and RDN types follow the same offset)
                    return (RdnValueKind)((byte)tokenType - 4);
                case RdnTokenType.RdnBinary:
                    return RdnValueKind.RdnBinary;
                default:
                    Debug.Fail($"No mapping for token type {tokenType}");
                    return RdnValueKind.Undefined;
            }
        }

        // Returns true if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
        // Otherwise, return false.
        public static bool IsTokenTypePrimitive(RdnTokenType tokenType) =>
            (tokenType - RdnTokenType.String) <= (RdnTokenType.RdnRegExp - RdnTokenType.String) || tokenType == RdnTokenType.RdnBinary;

        // A hex digit is valid if it is in the range: [0..9] | [A..F] | [a..f]
        // Otherwise, return false.
        public static bool IsHexDigit(byte nextByte) => HexConverter.IsHexChar(nextByte);

        public static bool TryGetValue(ReadOnlySpan<byte> segment, bool isEscaped, out DateTime value)
        {
            if (!RdnHelpers.IsValidDateTimeOffsetParseLength(segment.Length))
            {
                value = default;
                return false;
            }

            // Segment needs to be unescaped
            if (isEscaped)
            {
                return TryGetEscapedDateTime(segment, out value);
            }

            Debug.Assert(segment.IndexOf(RdnConstants.BackSlash) == -1);

            if (RdnHelpers.TryParseAsISO(segment, out DateTime tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetEscapedDateTime(ReadOnlySpan<byte> source, out DateTime value)
        {
            Debug.Assert(source.Length <= RdnConstants.MaximumEscapedDateTimeOffsetParseLength);
            Span<byte> sourceUnescaped = stackalloc byte[RdnConstants.MaximumEscapedDateTimeOffsetParseLength];

            Unescape(source, sourceUnescaped, out int written);
            Debug.Assert(written > 0);

            sourceUnescaped = sourceUnescaped.Slice(0, written);
            Debug.Assert(!sourceUnescaped.IsEmpty);

            if (RdnHelpers.IsValidUnescapedDateTimeOffsetParseLength(sourceUnescaped.Length)
                && RdnHelpers.TryParseAsISO(sourceUnescaped, out DateTime tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetValue(ReadOnlySpan<byte> segment, bool isEscaped, out DateTimeOffset value)
        {
            if (!RdnHelpers.IsValidDateTimeOffsetParseLength(segment.Length))
            {
                value = default;
                return false;
            }

            // Segment needs to be unescaped
            if (isEscaped)
            {
                return TryGetEscapedDateTimeOffset(segment, out value);
            }

            Debug.Assert(segment.IndexOf(RdnConstants.BackSlash) == -1);

            if (RdnHelpers.TryParseAsISO(segment, out DateTimeOffset tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetEscapedDateTimeOffset(ReadOnlySpan<byte> source, out DateTimeOffset value)
        {
            Debug.Assert(source.Length <= RdnConstants.MaximumEscapedDateTimeOffsetParseLength);
            Span<byte> sourceUnescaped = stackalloc byte[RdnConstants.MaximumEscapedDateTimeOffsetParseLength];

            Unescape(source, sourceUnescaped, out int written);
            Debug.Assert(written > 0);

            sourceUnescaped = sourceUnescaped.Slice(0, written);
            Debug.Assert(!sourceUnescaped.IsEmpty);

            if (RdnHelpers.IsValidUnescapedDateTimeOffsetParseLength(sourceUnescaped.Length)
                && RdnHelpers.TryParseAsISO(sourceUnescaped, out DateTimeOffset tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetValue(ReadOnlySpan<byte> segment, bool isEscaped, out Guid value)
        {
            if (segment.Length > RdnConstants.MaximumEscapedGuidLength)
            {
                value = default;
                return false;
            }

            // Segment needs to be unescaped
            if (isEscaped)
            {
                return TryGetEscapedGuid(segment, out value);
            }

            Debug.Assert(segment.IndexOf(RdnConstants.BackSlash) == -1);

            if (segment.Length == RdnConstants.MaximumFormatGuidLength
                && Utf8Parser.TryParse(segment, out Guid tmp, out _, 'D'))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetEscapedGuid(ReadOnlySpan<byte> source, out Guid value)
        {
            Debug.Assert(source.Length <= RdnConstants.MaximumEscapedGuidLength);

            Span<byte> utf8Unescaped = stackalloc byte[RdnConstants.MaximumEscapedGuidLength];
            Unescape(source, utf8Unescaped, out int written);
            Debug.Assert(written > 0);

            utf8Unescaped = utf8Unescaped.Slice(0, written);
            Debug.Assert(!utf8Unescaped.IsEmpty);

            if (utf8Unescaped.Length == RdnConstants.MaximumFormatGuidLength
                && Utf8Parser.TryParse(utf8Unescaped, out Guid tmp, out _, 'D'))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

#if NET
        public static bool TryGetFloatingPointConstant(ReadOnlySpan<byte> span, out Half value)
        {
            if (span.Length == 3)
            {
                if (span.SequenceEqual(RdnConstants.NaNValue))
                {
                    value = Half.NaN;
                    return true;
                }
            }
            else if (span.Length == 8)
            {
                if (span.SequenceEqual(RdnConstants.PositiveInfinityValue))
                {
                    value = Half.PositiveInfinity;
                    return true;
                }
            }
            else if (span.Length == 9)
            {
                if (span.SequenceEqual(RdnConstants.NegativeInfinityValue))
                {
                    value = Half.NegativeInfinity;
                    return true;
                }
            }

            value = default;
            return false;
        }
#endif

        public static bool TryGetFloatingPointConstant(ReadOnlySpan<byte> span, out float value)
        {
            if (span.Length == 3)
            {
                if (span.SequenceEqual(RdnConstants.NaNValue))
                {
                    value = float.NaN;
                    return true;
                }
            }
            else if (span.Length == 8)
            {
                if (span.SequenceEqual(RdnConstants.PositiveInfinityValue))
                {
                    value = float.PositiveInfinity;
                    return true;
                }
            }
            else if (span.Length == 9)
            {
                if (span.SequenceEqual(RdnConstants.NegativeInfinityValue))
                {
                    value = float.NegativeInfinity;
                    return true;
                }
            }

            value = 0;
            return false;
        }

        public static bool TryGetFloatingPointConstant(ReadOnlySpan<byte> span, out double value)
        {
            if (span.Length == 3)
            {
                if (span.SequenceEqual(RdnConstants.NaNValue))
                {
                    value = double.NaN;
                    return true;
                }
            }
            else if (span.Length == 8)
            {
                if (span.SequenceEqual(RdnConstants.PositiveInfinityValue))
                {
                    value = double.PositiveInfinity;
                    return true;
                }
            }
            else if (span.Length == 9)
            {
                if (span.SequenceEqual(RdnConstants.NegativeInfinityValue))
                {
                    value = double.NegativeInfinity;
                    return true;
                }
            }

            value = 0;
            return false;
        }
    }
}
