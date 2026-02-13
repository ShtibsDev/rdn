// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Rdn
{
    /// <summary>
    /// V8-inspired lookup tables for O(1) character classification in RDN date/time parsing.
    /// </summary>
    internal static class RdnCharTables
    {
        // Terminator characters that end an RDN literal: whitespace, structural chars, delimiters
        private static ReadOnlySpan<byte> TerminatorTable => new byte[256]
        {
            // 0x00-0x0F: control chars are terminators (especially \t=0x09, \n=0x0A, \r=0x0D)
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            // 0x10-0x1F: control chars
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            // 0x20-0x2F: space(20) !"#$%&'()*+,-./
            // space=1, !=0, "=0, #=0, $=0, %=0, &=0, '=0, (=0, )=1, *=0, +=0, ,=1, -=0, .=0, /=1
            1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
            // 0x30-0x3F: 0-9:;<=>?
            // digits=0, :=0, ;=0, <=0, ==1, >=0, ?=0
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
            // 0x40-0x4F: @A-O  all 0
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            // 0x50-0x5F: P-Z[\]^_  ]=1, rest 0
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
            // 0x60-0x6F: `a-o  all 0
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            // 0x70-0x7F: p-z{|}~DEL  }=1, rest 0
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0,
            // 0x80-0xFF: high bytes, all 0
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };

        // Valid duration characters: digits, P, Y, M, D, T, H, S, W, .
        private static ReadOnlySpan<byte> DurationCharTable => new byte[256]
        {
            // 0x00-0x0F
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            // 0x10-0x1F
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            // 0x20-0x2F: .=1 (0x2E)
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0,
            // 0x30-0x3F: 0-9=1
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
            // 0x40-0x4F: D=1(44) H=1(48) M=1(4D)
            0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0,
            // 0x50-0x5F: P=1(50) S=1(53) T=1(54) W=1(57) Y=1(59)
            1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0,
            // 0x60-0x6F
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            // 0x70-0x7F
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            // 0x80-0xFF
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTerminator(byte b) => TerminatorTable[b] != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDurationChar(byte b) => DurationCharTable[b] != 0;

        /// <summary>
        /// Extracts a two-digit decimal value from consecutive ASCII bytes.
        /// Port of V8's digit pair extraction: (p[0]-'0')*10 + (p[1]-'0').
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadTwoDigits(ReadOnlySpan<byte> span)
        {
            return (span[0] - '0') * 10 + (span[1] - '0');
        }

        /// <summary>
        /// Extracts a four-digit decimal value from consecutive ASCII bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadFourDigits(ReadOnlySpan<byte> span)
        {
            return (span[0] - '0') * 1000 + (span[1] - '0') * 100 + (span[2] - '0') * 10 + (span[3] - '0');
        }

        /// <summary>
        /// Extracts a three-digit decimal value from consecutive ASCII bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadThreeDigits(ReadOnlySpan<byte> span)
        {
            return (span[0] - '0') * 100 + (span[1] - '0') * 10 + (span[2] - '0');
        }
    }
}
