package com.rdn.intellij.parser

import com.rdn.intellij.parser.model.*
import java.math.BigInteger
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset

/**
 * Full-fidelity recursive-descent RDN parser ported from the TypeScript reference implementation.
 *
 * Uses a 256-entry dispatch table for O(1) first-character branching.
 * Reports errors with character offset via [RdnSyntaxError].
 */
class RdnKotlinParser private constructor(private val source: String) {

    private var pos: Int = 0
    private val len: Int = source.length
    private var depth: Int = 0

    companion object {
        const val MAX_DEPTH = 128
        const val MAX_BINARY_SIZE = 100 * 1024 * 1024 // 100 MB

        // ── Token constants ─────────────────────────────────────────────
        private const val TOKEN_INVALID = 0
        private const val TOKEN_STRING = 1       // "
        private const val TOKEN_NUMBER = 2       // 0-9
        private const val TOKEN_MINUS = 3        // -
        private const val TOKEN_OPEN_BRACE = 4   // {
        private const val TOKEN_OPEN_BRACKET = 6 // [
        private const val TOKEN_OPEN_PAREN = 8   // (
        private const val TOKEN_TRUE = 12        // t
        private const val TOKEN_FALSE = 13       // f
        private const val TOKEN_NULL = 14        // n
        private const val TOKEN_AT = 15          // @
        private const val TOKEN_SLASH = 16       // /
        private const val TOKEN_B64 = 17         // b
        private const val TOKEN_HEX = 18         // x
        private const val TOKEN_INFINITY = 19    // I
        private const val TOKEN_NAN = 20         // N
        private const val TOKEN_MAP = 21         // M
        private const val TOKEN_SET = 22         // S

        // ── 256-entry dispatch table ────────────────────────────────────
        private val TOKEN_TABLE = IntArray(256).also { t ->
            t[0x22] = TOKEN_STRING       // "
            for (i in 0x30..0x39) t[i] = TOKEN_NUMBER // 0-9
            t[0x2D] = TOKEN_MINUS        // -
            t[0x7B] = TOKEN_OPEN_BRACE   // {
            t[0x5B] = TOKEN_OPEN_BRACKET // [
            t[0x28] = TOKEN_OPEN_PAREN   // (
            t[0x74] = TOKEN_TRUE         // t
            t[0x66] = TOKEN_FALSE        // f
            t[0x6E] = TOKEN_NULL         // n
            t[0x40] = TOKEN_AT           // @
            t[0x2F] = TOKEN_SLASH        // /
            t[0x62] = TOKEN_B64          // b
            t[0x78] = TOKEN_HEX          // x
            t[0x49] = TOKEN_INFINITY     // I
            t[0x4E] = TOKEN_NAN          // N
            t[0x4D] = TOKEN_MAP          // M
            t[0x53] = TOKEN_SET          // S
        }

        // ── Base64 decode table ─────────────────────────────────────────
        private const val B64_INVALID: Int = 0xFF
        private const val B64_PADDING: Int = 0xFE

        private val B64_DECODE = IntArray(256) { B64_INVALID }.also { t ->
            val chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
            for (i in chars.indices) t[chars[i].code] = i
            t[0x3D] = B64_PADDING // '='
        }

        // ── Hex decode table ────────────────────────────────────────────
        private const val HEX_INVALID: Int = 0xFF

        private val HEX_DECODE = IntArray(256) { HEX_INVALID }.also { t ->
            for (i in 0..9) t[0x30 + i] = i          // 0-9
            for (i in 0..5) t[0x41 + i] = 10 + i      // A-F
            for (i in 0..5) t[0x61 + i] = 10 + i      // a-f
        }

        /**
         * Parse an RDN string and return the parsed value.
         */
        fun parse(text: String): RdnValue {
            val parser = RdnKotlinParser(text)
            val result = parser.parseValue()
            parser.skipWs()
            if (parser.pos < parser.len) {
                parser.error("Unexpected data after value")
            }
            return result
        }
    }

    // ── Error reporting ─────────────────────────────────────────────────

    private fun error(msg: String): Nothing {
        throw RdnSyntaxError("$msg in RDN at position $pos", pos)
    }

    // ── Whitespace ──────────────────────────────────────────────────────

    private fun skipWs() {
        while (pos < len) {
            val c = source[pos].code
            if (c == 0x20 || c == 0x09 || c == 0x0A || c == 0x0D) pos++ else break
        }
    }

    // ── Expect a specific character ─────────────────────────────────────

    private fun expect(ch: Int) {
        if (pos >= len || source[pos].code != ch) {
            error("Expected '${ch.toChar()}'")
        }
        pos++
    }

    // ── Char code helper (returns -1 past end) ─────────────────────────

    private fun charAt(index: Int): Int {
        return if (index < len) source[index].code else -1
    }

    // ── Container depth tracking ────────────────────────────────────────

    private fun enterContainer() {
        if (++depth > MAX_DEPTH) throw RdnSyntaxError("Maximum nesting depth exceeded ($MAX_DEPTH)", pos)
    }

    // ════════════════════════════════════════════════════════════════════
    // String parsing with deferred materialization
    // ════════════════════════════════════════════════════════════════════

    private fun parseString(): String {
        pos++ // skip opening "
        val start = pos
        var hasEscape = false
        while (pos < len) {
            val c = source[pos].code
            if (c == 0x22) { // closing "
                if (!hasEscape) {
                    val result = source.substring(start, pos)
                    pos++ // skip closing "
                    return result
                }
                // Slow path: materialize with escapes
                val result = materializeString(start, pos)
                pos++ // skip closing "
                return result
            }
            if (c == 0x5C) { // backslash
                hasEscape = true
                pos++ // skip the backslash
                if (pos >= len) break
                if (source[pos].code == 0x75) { // \uXXXX
                    pos += 5 // u + 4 hex digits
                } else {
                    pos++
                }
                continue
            }
            if (c < 0x20) error("Unescaped control character in string")
            pos++
        }
        error("Unterminated string")
    }

    private fun materializeString(start: Int, end: Int): String {
        val sb = StringBuilder(end - start)
        var i = start
        while (i < end) {
            val c = source[i].code
            if (c == 0x5C) { // backslash
                i++
                when (source[i].code) {
                    0x22 -> { sb.append('"'); i++ }
                    0x5C -> { sb.append('\\'); i++ }
                    0x2F -> { sb.append('/'); i++ }
                    0x62 -> { sb.append('\b'); i++ }
                    0x66 -> { sb.append('\u000C'); i++ } // form feed
                    0x6E -> { sb.append('\n'); i++ }
                    0x72 -> { sb.append('\r'); i++ }
                    0x74 -> { sb.append('\t'); i++ }
                    0x75 -> { // \uXXXX
                        val hex = source.substring(i + 1, i + 5)
                        if (hex.length < 4) error("Invalid unicode escape")
                        val code = hex.toIntOrNull(16) ?: error("Invalid unicode escape")
                        sb.append(code.toChar())
                        i += 5
                    }
                    else -> error("Invalid escape sequence '\\${source[i]}'")
                }
            } else {
                // Bulk copy until the next backslash or end
                var j = i + 1
                while (j < end && source[j].code != 0x5C) j++
                sb.append(source, i, j)
                i = j
            }
        }
        return sb.toString()
    }

    // ════════════════════════════════════════════════════════════════════
    // Number parsing
    // ════════════════════════════════════════════════════════════════════

    private fun parseNumber(negative: Boolean): RdnValue {
        val start = if (negative) pos - 1 else pos
        // Accumulate integer digits
        var intValue = 0L
        var digitCount = 0
        while (pos < len) {
            val d = source[pos].code - 0x30
            if (d < 0 || d > 9) break
            intValue = intValue * 10 + d
            digitCount++
            pos++
        }
        if (digitCount == 0) error("Expected digit")

        // Leading zero check: "01" is invalid, "0" alone is ok, "0." and "0e" are ok
        val firstDigitPos = start + (if (negative) 1 else 0)
        if (digitCount > 1 && source[firstDigitPos].code == 0x30) {
            error("Leading zeros not allowed")
        }

        // Check for bigint suffix 'n'
        if (pos < len && source[pos].code == 0x6E) { // 'n'
            pos++
            return RdnBigInt(BigInteger(source.substring(start, pos - 1)))
        }

        var isFloat = false

        // Fraction
        if (pos < len && source[pos].code == 0x2E) { // '.'
            isFloat = true
            pos++ // skip '.'
            var fracDigits = 0
            while (pos < len) {
                val d = source[pos].code - 0x30
                if (d < 0 || d > 9) break
                fracDigits++
                pos++
            }
            if (fracDigits == 0) error("Expected digit after decimal point")
        }

        // Exponent
        if (pos < len) {
            val e = source[pos].code
            if (e == 0x65 || e == 0x45) { // 'e' or 'E'
                isFloat = true
                pos++
                if (pos < len) {
                    val sign = source[pos].code
                    if (sign == 0x2B || sign == 0x2D) pos++ // + or -
                }
                var expDigits = 0
                while (pos < len) {
                    val d = source[pos].code - 0x30
                    if (d < 0 || d > 9) break
                    expDigits++
                    pos++
                }
                if (expDigits == 0) error("Expected digit in exponent")
            }
        }

        // Check for invalid bigint suffix after float
        if (pos < len && source[pos].code == 0x6E) { // 'n'
            if (isFloat) error("BigInt cannot have decimal point or exponent")
        }

        // Fast path: small integers (<=15 digits, no float)
        if (!isFloat && digitCount <= 15) {
            return RdnNumber(if (negative) -intValue.toDouble() else intValue.toDouble())
        }

        return RdnNumber(source.substring(start, pos).toDouble())
    }

    // ════════════════════════════════════════════════════════════════════
    // Date/Time parsing
    // ════════════════════════════════════════════════════════════════════

    private fun readDigits2(): Int {
        if (pos + 1 >= len) error("Unexpected end of input")
        val d1 = source[pos].code - 0x30
        val d2 = source[pos + 1].code - 0x30
        if (d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9) error("Expected 2-digit number")
        pos += 2
        return d1 * 10 + d2
    }

    private fun readDigits3(): Int {
        if (pos + 2 >= len) error("Unexpected end of input")
        val d1 = source[pos].code - 0x30
        val d2 = source[pos + 1].code - 0x30
        val d3 = source[pos + 2].code - 0x30
        if (d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 || d3 < 0 || d3 > 9) error("Expected 3-digit number")
        pos += 3
        return d1 * 100 + d2 * 10 + d3
    }

    private fun readDigits4(): Int {
        if (pos + 3 >= len) error("Unexpected end of input")
        val d1 = source[pos].code - 0x30
        val d2 = source[pos + 1].code - 0x30
        val d3 = source[pos + 2].code - 0x30
        val d4 = source[pos + 3].code - 0x30
        if (d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 || d3 < 0 || d3 > 9 || d4 < 0 || d4 > 9) error("Expected 4-digit year")
        pos += 4
        return d1 * 1000 + d2 * 100 + d3 * 10 + d4
    }

    private fun parseAt(): RdnValue {
        pos++ // skip @

        if (pos >= len) error("Unexpected end after @")

        val ch = source[pos].code

        // Duration: @P...
        if (ch == 0x50) { // 'P'
            return parseDuration()
        }

        // Check if this looks like a time (digit digit colon) vs date (digit digit digit digit dash)
        if (pos + 2 < len && ch in 0x30..0x39) {
            val ch2 = source[pos + 2].code

            if (ch2 == 0x3A) { // ':' at position 2 -> TimeOnly
                return parseTimeOnly()
            }

            if (pos + 4 < len && source[pos + 4].code == 0x2D) { // '-' at position 4 -> DateTime
                return parseDateTime()
            }

            // Must be unix timestamp (digits only)
            return parseUnixTimestamp()
        }

        error("Invalid @ literal")
    }

    private fun parseDateTime(): RdnValue {
        val year = readDigits4()
        expect(0x2D) // -
        val month = readDigits2()
        expect(0x2D) // -
        val day = readDigits2()

        // Date only: @YYYY-MM-DD (no 'T' follows)
        if (pos >= len || source[pos].code != 0x54) { // not 'T'
            return RdnDateOnly(LocalDate.of(year, month, day))
        }

        pos++ // skip 'T'
        val hours = readDigits2()
        expect(0x3A) // :
        val minutes = readDigits2()
        expect(0x3A) // :
        val seconds = readDigits2()

        var ms = 0
        if (pos < len && source[pos].code == 0x2E) { // '.'
            pos++ // skip '.'
            ms = readDigits3()
        }

        expect(0x5A) // 'Z'

        val instant = LocalDate.of(year, month, day)
            .atTime(hours, minutes, seconds, ms * 1_000_000)
            .toInstant(ZoneOffset.UTC)
        return RdnDateTime(instant)
    }

    private fun parseTimeOnly(): RdnValue {
        val hours = readDigits2()
        expect(0x3A) // :
        val minutes = readDigits2()
        expect(0x3A) // :
        val seconds = readDigits2()

        var ms = 0
        if (pos < len && source[pos].code == 0x2E) { // '.'
            pos++ // skip '.'
            ms = readDigits3()
        }

        return RdnTimeOnly(hours, minutes, seconds, ms)
    }

    private fun parseDuration(): RdnValue {
        val start = pos
        pos++ // skip 'P'
        // Scan until we hit a non-duration character
        while (pos < len) {
            val c = source[pos].code
            if ((c in 0x30..0x39) || c == 0x59 || c == 0x4D || c == 0x44 || c == 0x54 || c == 0x48 || c == 0x53 || c == 0x2E) {
                // 0-9, Y, M, D, T, H, S, .
                pos++
            } else {
                break
            }
        }
        val iso = source.substring(start, pos)
        if (iso.length < 2) error("Invalid duration")
        return RdnDuration(iso)
    }

    private fun parseUnixTimestamp(): RdnValue {
        val start = pos
        while (pos < len) {
            val d = source[pos].code - 0x30
            if (d < 0 || d > 9) break
            pos++
        }
        val digits = source.substring(start, pos)
        val num = digits.toLong()
        // <=10 digits = seconds, >10 = milliseconds
        val epochMillis = if (digits.length <= 10) num * 1000 else num
        return RdnDateTime(Instant.ofEpochMilli(epochMillis))
    }

    // ════════════════════════════════════════════════════════════════════
    // RegExp parsing
    // ════════════════════════════════════════════════════════════════════

    private fun parseRegExp(): RdnRegExp {
        pos++ // skip opening /
        val patternStart = pos
        var escaped = false

        while (pos < len) {
            val c = source[pos].code
            if (escaped) {
                escaped = false
                pos++
                continue
            }
            if (c == 0x5C) { // backslash
                escaped = true
                pos++
                continue
            }
            if (c == 0x2F) { // closing /
                break
            }
            pos++
        }

        if (pos >= len) error("Unterminated regular expression")
        val pattern = source.substring(patternStart, pos)
        pos++ // skip closing /

        // Read flags
        val flagStart = pos
        while (pos < len) {
            val c = source[pos].code
            // Valid flags: d g i m s u v y
            if (c == 0x64 || c == 0x67 || c == 0x69 || c == 0x6D || c == 0x73 || c == 0x75 || c == 0x76 || c == 0x79) {
                pos++
            } else {
                break
            }
        }
        val flags = source.substring(flagStart, pos)
        return RdnRegExp(pattern, flags)
    }

    // ════════════════════════════════════════════════════════════════════
    // Binary parsing
    // ════════════════════════════════════════════════════════════════════

    private fun parseBinaryB64(): RdnBinaryBase64 {
        pos++ // skip 'b'
        if (pos >= len || source[pos].code != 0x22) error("Expected '\"' after 'b'")
        pos++ // skip opening "

        val start = pos
        // Scan to closing "
        while (pos < len && source[pos].code != 0x22) pos++
        if (pos >= len) error("Unterminated binary literal")
        val content = source.substring(start, pos)
        pos++ // skip closing "

        if (content.isEmpty()) return RdnBinaryBase64(ByteArray(0))

        // Validate and decode base64
        if (content.length % 4 != 0) error("Invalid base64: length must be a multiple of 4")

        // Count padding
        var padding = 0
        if (content[content.length - 1].code == 0x3D) padding++
        if (content.length > 1 && content[content.length - 2].code == 0x3D) padding++

        val outLen = (content.length / 4) * 3 - padding
        if (outLen > MAX_BINARY_SIZE) error("Binary data too large")
        val out = ByteArray(outLen)

        var outPos = 0
        var i = 0
        while (i < content.length) {
            val a = B64_DECODE[content[i].code]
            val b = B64_DECODE[content[i + 1].code]
            val c = B64_DECODE[content[i + 2].code]
            val d = B64_DECODE[content[i + 3].code]

            // Validate: 0xFF = invalid char, 0xFE = padding
            if (a == B64_INVALID || a == B64_PADDING || b == B64_INVALID || b == B64_PADDING) error("Invalid base64 character")
            if (c == B64_INVALID || d == B64_INVALID) error("Invalid base64 character")

            // Padding can only appear at end
            if (c == B64_PADDING) {
                // c and d must both be padding
                if (d != B64_PADDING) error("Invalid base64 padding")
                // Check for non-zero padding bits
                if (b and 0x0F != 0) error("Invalid base64: non-zero padding bits")
                out[outPos++] = ((a shl 2) or (b shr 4)).toByte()
            } else if (d == B64_PADDING) {
                // Only d is padding
                // Check for non-zero padding bits
                if (c and 0x03 != 0) error("Invalid base64: non-zero padding bits")
                out[outPos++] = ((a shl 2) or (b shr 4)).toByte()
                out[outPos++] = (((b and 0x0F) shl 4) or (c shr 2)).toByte()
            } else {
                out[outPos++] = ((a shl 2) or (b shr 4)).toByte()
                out[outPos++] = (((b and 0x0F) shl 4) or (c shr 2)).toByte()
                out[outPos++] = (((c and 0x03) shl 6) or d).toByte()
            }
            i += 4
        }

        return RdnBinaryBase64(out)
    }

    private fun parseBinaryHex(): RdnBinaryHex {
        pos++ // skip 'x'
        if (pos >= len || source[pos].code != 0x22) error("Expected '\"' after 'x'")
        pos++ // skip opening "

        val start = pos
        while (pos < len && source[pos].code != 0x22) pos++
        if (pos >= len) error("Unterminated hex literal")
        val content = source.substring(start, pos)
        pos++ // skip closing "

        if (content.isEmpty()) return RdnBinaryHex(ByteArray(0))
        if (content.length % 2 != 0) error("Invalid hex: odd length")
        if (content.length / 2 > MAX_BINARY_SIZE) error("Binary data too large")

        val out = ByteArray(content.length / 2)
        var i = 0
        while (i < content.length) {
            val hi = HEX_DECODE[content[i].code]
            val lo = HEX_DECODE[content[i + 1].code]
            if (hi == HEX_INVALID || lo == HEX_INVALID) error("Invalid hex character")
            out[i / 2] = ((hi shl 4) or lo).toByte()
            i += 2
        }
        return RdnBinaryHex(out)
    }

    // ════════════════════════════════════════════════════════════════════
    // Collection parsing
    // ════════════════════════════════════════════════════════════════════

    private fun parseArray(): RdnArray {
        enterContainer()
        pos++ // skip [
        skipWs()
        if (pos < len && source[pos].code == 0x5D) { // ]
            pos++
            depth--
            return RdnArray(emptyList())
        }
        val elements = mutableListOf<RdnValue>()
        elements.add(parseValue())
        skipWs()
        while (pos < len && source[pos].code == 0x2C) { // ,
            pos++
            skipWs()
            elements.add(parseValue())
            skipWs()
        }
        expect(0x5D) // ]
        depth--
        return RdnArray(elements)
    }

    private fun parseTuple(): RdnTuple {
        enterContainer()
        pos++ // skip (
        skipWs()
        if (pos < len && source[pos].code == 0x29) { // )
            pos++
            depth--
            return RdnTuple(emptyList())
        }
        val elements = mutableListOf<RdnValue>()
        elements.add(parseValue())
        skipWs()
        while (pos < len && source[pos].code == 0x2C) { // ,
            pos++
            skipWs()
            elements.add(parseValue())
            skipWs()
        }
        expect(0x29) // )
        depth--
        return RdnTuple(elements)
    }

    private fun parseBrace(): RdnValue {
        enterContainer()
        pos++ // skip {
        skipWs()
        // Empty braces -> Object
        if (pos < len && source[pos].code == 0x7D) { // }
            pos++
            depth--
            return RdnObject(emptyList())
        }

        // Parse first value
        val firstValue = parseValue()
        skipWs()

        if (pos >= len) error("Unterminated brace expression")

        val sep = source[pos].code

        // : -> Object
        if (sep == 0x3A) {
            if (firstValue !is RdnString) error("Object key must be a string")
            return finishObject(firstValue.value)
        }

        // = -> check for => (Map)
        if (sep == 0x3D) {
            if (pos + 1 < len && source[pos + 1].code == 0x3E) { // =>
                return finishMap(firstValue)
            }
            error("Expected '=>'")
        }

        // , -> Set
        if (sep == 0x2C) {
            return finishSet(firstValue)
        }

        // } -> single-element Set
        if (sep == 0x7D) {
            pos++
            depth--
            return RdnSet(listOf(firstValue), explicit = false)
        }

        error("Expected ':', '=>', ',' or '}' after value in brace expression")
    }

    private fun finishObject(firstKey: String): RdnObject {
        val properties = mutableListOf<Pair<String, RdnValue>>()
        pos++ // skip :
        skipWs()
        properties.add(firstKey to parseValue())
        skipWs()
        while (pos < len && source[pos].code == 0x2C) { // ,
            pos++
            skipWs()
            val key = parseString()
            skipWs()
            expect(0x3A) // :
            skipWs()
            properties.add(key to parseValue())
            skipWs()
        }
        expect(0x7D) // }
        depth--
        return RdnObject(properties)
    }

    private fun finishMap(firstKey: RdnValue): RdnMap {
        val entries = mutableListOf<Pair<RdnValue, RdnValue>>()
        pos += 2 // skip =>
        skipWs()
        entries.add(firstKey to parseValue())
        skipWs()
        while (pos < len && source[pos].code == 0x2C) { // ,
            pos++
            skipWs()
            val key = parseValue()
            skipWs()
            if (pos + 1 >= len || source[pos].code != 0x3D || source[pos + 1].code != 0x3E) {
                error("Expected '=>' in map entry")
            }
            pos += 2 // skip =>
            skipWs()
            entries.add(key to parseValue())
            skipWs()
        }
        expect(0x7D) // }
        depth--
        return RdnMap(entries, explicit = false)
    }

    private fun finishSet(firstValue: RdnValue): RdnSet {
        val elements = mutableListOf<RdnValue>()
        elements.add(firstValue)
        pos++ // skip ,
        skipWs()
        elements.add(parseValue())
        skipWs()
        while (pos < len && source[pos].code == 0x2C) { // ,
            pos++
            skipWs()
            elements.add(parseValue())
            skipWs()
        }
        expect(0x7D) // }
        depth--
        return RdnSet(elements, explicit = false)
    }

    private fun parseExplicitMap(): RdnMap {
        enterContainer()
        // pos is at 'M', check for 'Map{'
        if (pos + 3 >= len || source[pos + 1].code != 0x61 || source[pos + 2].code != 0x70 || source[pos + 3].code != 0x7B) {
            error("Expected 'Map{'")
        }
        pos += 4 // skip 'Map{'
        skipWs()
        if (pos < len && source[pos].code == 0x7D) { // }
            pos++
            depth--
            return RdnMap(emptyList(), explicit = true)
        }
        val entries = mutableListOf<Pair<RdnValue, RdnValue>>()
        // Parse first entry
        val key = parseValue()
        skipWs()
        if (pos + 1 >= len || source[pos].code != 0x3D || source[pos + 1].code != 0x3E) {
            error("Expected '=>' in map entry")
        }
        pos += 2 // skip =>
        skipWs()
        entries.add(key to parseValue())
        skipWs()
        while (pos < len && source[pos].code == 0x2C) { // ,
            pos++
            skipWs()
            val k = parseValue()
            skipWs()
            if (pos + 1 >= len || source[pos].code != 0x3D || source[pos + 1].code != 0x3E) {
                error("Expected '=>' in map entry")
            }
            pos += 2
            skipWs()
            entries.add(k to parseValue())
            skipWs()
        }
        expect(0x7D) // }
        depth--
        return RdnMap(entries, explicit = true)
    }

    private fun parseExplicitSet(): RdnSet {
        enterContainer()
        // pos is at 'S', check for 'Set{'
        if (pos + 3 >= len || source[pos + 1].code != 0x65 || source[pos + 2].code != 0x74 || source[pos + 3].code != 0x7B) {
            error("Expected 'Set{'")
        }
        pos += 4 // skip 'Set{'
        skipWs()
        if (pos < len && source[pos].code == 0x7D) { // }
            pos++
            depth--
            return RdnSet(emptyList(), explicit = true)
        }
        val elements = mutableListOf<RdnValue>()
        elements.add(parseValue())
        skipWs()
        while (pos < len && source[pos].code == 0x2C) { // ,
            pos++
            skipWs()
            elements.add(parseValue())
            skipWs()
        }
        expect(0x7D) // }
        depth--
        return RdnSet(elements, explicit = true)
    }

    // ════════════════════════════════════════════════════════════════════
    // Literal parsing
    // ════════════════════════════════════════════════════════════════════

    private fun parseLiteral(expected: String) {
        for (i in expected.indices) {
            if (pos >= len || source[pos].code != expected[i].code) {
                error("Expected '$expected'")
            }
            pos++
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Main value dispatch
    // ════════════════════════════════════════════════════════════════════

    private fun parseValue(): RdnValue {
        skipWs()
        if (pos >= len) error("Unexpected end of input")

        val ch = source[pos].code
        val token = if (ch < 256) TOKEN_TABLE[ch] else TOKEN_INVALID

        return when (token) {
            TOKEN_STRING -> RdnString(parseString())
            TOKEN_NUMBER -> parseNumber(false)
            TOKEN_MINUS -> {
                pos++ // skip -
                // -Infinity
                if (pos < len && source[pos].code == 0x49) { // 'I'
                    parseLiteral("Infinity")
                    return RdnInfinity(negative = true)
                }
                parseNumber(true)
            }
            TOKEN_OPEN_BRACE -> parseBrace()
            TOKEN_OPEN_BRACKET -> parseArray()
            TOKEN_OPEN_PAREN -> parseTuple()
            TOKEN_TRUE -> { parseLiteral("true"); RdnBoolean(true) }
            TOKEN_FALSE -> { parseLiteral("false"); RdnBoolean(false) }
            TOKEN_NULL -> { parseLiteral("null"); RdnNull }
            TOKEN_AT -> parseAt()
            TOKEN_SLASH -> parseRegExp()
            TOKEN_B64 -> parseBinaryB64()
            TOKEN_HEX -> parseBinaryHex()
            TOKEN_INFINITY -> { parseLiteral("Infinity"); RdnInfinity(negative = false) }
            TOKEN_NAN -> { parseLiteral("NaN"); RdnNaN() }
            TOKEN_MAP -> parseExplicitMap()
            TOKEN_SET -> parseExplicitSet()
            else -> error("Unexpected character '${ch.toChar()}'")
        }
    }
}
