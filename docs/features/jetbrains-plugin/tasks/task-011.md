# Task 011: Implement Kotlin Value Parser for Diagnostics

## References
- [Tech Design](../tech-design.md) — Sections 5.1, 6.4
- [Discovery](../discovery.md)

## Description
Write `RdnKotlinParser.kt`, a full-fidelity RDN parser in Kotlin used for diagnostic validation. This parser produces `RdnValue` types (the sealed hierarchy from Section 5.1) and throws `RdnSyntaxError` with position information on parse failure. It mirrors `packages/rdn-js/src/parser.ts` using a recursive-descent approach with a 256-entry dispatch table for O(1) character dispatch. Also create `RdnValue` model classes in a separate file.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/parser/model/RdnValue.kt` — Sealed value type hierarchy
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/parser/RdnKotlinParser.kt` — Full-fidelity recursive-descent parser

## Implementation Details

### `RdnValue.kt`

Copy the full sealed hierarchy from Section 5.1 of the tech design:

```kotlin
package com.rdn.intellij.parser.model

import java.math.BigInteger
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.Duration as JavaDuration

/**
 * Tagged interface for all RDN values produced by the parser.
 * Used for diagnostics validation (parse succeeds or throws).
 */
sealed interface RdnValue

data object RdnNull : RdnValue
data class RdnBoolean(val value: Boolean) : RdnValue
data class RdnNumber(val value: Double) : RdnValue
data class RdnBigInt(val value: BigInteger) : RdnValue
data class RdnString(val value: String) : RdnValue
data class RdnDateTime(val instant: Instant) : RdnValue
data class RdnDateOnly(val date: LocalDate) : RdnValue
data class RdnTimeOnly(val hours: Int, val minutes: Int, val seconds: Int, val milliseconds: Int) : RdnValue
data class RdnDuration(val iso: String) : RdnValue
data class RdnRegExp(val pattern: String, val flags: String) : RdnValue
data class RdnBinaryBase64(val data: ByteArray) : RdnValue {
    override fun equals(other: Any?) = other is RdnBinaryBase64 && data.contentEquals(other.data)
    override fun hashCode() = data.contentHashCode()
}
data class RdnBinaryHex(val data: ByteArray) : RdnValue {
    override fun equals(other: Any?) = other is RdnBinaryHex && data.contentEquals(other.data)
    override fun hashCode() = data.contentHashCode()
}
data class RdnNaN(val dummy: Unit = Unit) : RdnValue
data class RdnInfinity(val negative: Boolean) : RdnValue
data class RdnArray(val elements: List<RdnValue>) : RdnValue
data class RdnTuple(val elements: List<RdnValue>) : RdnValue
data class RdnObject(val properties: List<Pair<String, RdnValue>>) : RdnValue
data class RdnMap(val entries: List<Pair<RdnValue, RdnValue>>, val explicit: Boolean) : RdnValue
data class RdnSet(val elements: List<RdnValue>, val explicit: Boolean) : RdnValue
```

### `RdnSyntaxError.kt`

```kotlin
package com.rdn.intellij.parser

class RdnSyntaxError(
    message: String,
    val offset: Int,
    val line: Int = -1,
    val column: Int = -1
) : Exception(message)
```

### `RdnKotlinParser.kt` skeleton

```kotlin
package com.rdn.intellij.parser

import com.rdn.intellij.parser.model.*
import java.math.BigInteger
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.util.Base64

class RdnKotlinParser(private val text: String) {
    private var pos = 0

    companion object {
        fun parse(text: String): RdnValue = RdnKotlinParser(text).parseDocument()

        // 256-entry dispatch table: maps first character to parse function
        // Populated in static initializer; null entries indicate invalid leading chars
        private val DISPATCH = arrayOfNulls<((RdnKotlinParser) -> RdnValue)>(256)

        init {
            DISPATCH['"'.code] = { it.parseString() }
            DISPATCH['{'.code] = { it.parseBrace() }
            DISPATCH['['.code] = { it.parseArray() }
            DISPATCH['('.code] = { it.parseTuple() }
            DISPATCH['@'.code] = { it.parseAt() }
            DISPATCH['/'.code] = { it.parseRegExp() }
            DISPATCH['b'.code] = { it.parseBinaryOrIdentifier('b') }
            DISPATCH['x'.code] = { it.parseBinaryOrIdentifier('x') }
            DISPATCH['M'.code] = { it.parseMapKeyword() }
            DISPATCH['S'.code] = { it.parseSetKeyword() }
            DISPATCH['N'.code] = { it.parseNaNOrNull() }
            DISPATCH['I'.code] = { it.parseInfinity(false) }
            DISPATCH['-'.code] = { it.parseNegative() }
            DISPATCH['t'.code] = { it.parseTrue() }
            DISPATCH['f'.code] = { it.parseFalse() }
            DISPATCH['n'.code] = { it.parseNull() }
            for (c in '0'..'9') DISPATCH[c.code] = { it.parseNumber() }
        }
    }

    private fun parseDocument(): RdnValue {
        skipWhitespace()
        val value = parseValue()
        skipWhitespace()
        if (pos < text.length) {
            throw RdnSyntaxError("Unexpected character '${text[pos]}'", pos)
        }
        return value
    }

    private fun parseValue(): RdnValue {
        skipWhitespace()
        if (pos >= text.length) throw RdnSyntaxError("Unexpected end of input", pos)
        val ch = text[pos]
        val fn = if (ch.code < 256) DISPATCH[ch.code] else null
        return fn?.invoke(this) ?: throw RdnSyntaxError("Unexpected character '$ch'", pos)
    }

    private fun skipWhitespace() {
        while (pos < text.length && text[pos].isWhitespace()) pos++
    }

    private fun expect(ch: Char) {
        if (pos >= text.length || text[pos] != ch) {
            throw RdnSyntaxError("Expected '$ch'", pos)
        }
        pos++
    }

    private fun expectString(s: String) {
        if (!text.startsWith(s, pos)) throw RdnSyntaxError("Expected '$s'", pos)
        pos += s.length
    }

    private fun parseString(): RdnString {
        expect('"')
        val sb = StringBuilder()
        while (pos < text.length && text[pos] != '"') {
            if (text[pos] == '\\') {
                pos++
                when (val esc = text.getOrNull(pos) ?: throw RdnSyntaxError("Unterminated string escape", pos)) {
                    '"', '\\', '/' -> { sb.append(esc); pos++ }
                    'b' -> { sb.append('\b'); pos++ }
                    'f' -> { sb.append('\u000C'); pos++ }
                    'n' -> { sb.append('\n'); pos++ }
                    'r' -> { sb.append('\r'); pos++ }
                    't' -> { sb.append('\t'); pos++ }
                    'u' -> {
                        pos++
                        val hex = text.substring(pos, minOf(pos + 4, text.length))
                        if (hex.length < 4 || !hex.all { it.isHexDigit() }) {
                            throw RdnSyntaxError("Invalid unicode escape", pos)
                        }
                        sb.append(hex.toInt(16).toChar())
                        pos += 4
                    }
                    else -> throw RdnSyntaxError("Invalid escape sequence '\\$esc'", pos)
                }
            } else {
                sb.append(text[pos++])
            }
        }
        expect('"')
        return RdnString(sb.toString())
    }

    private fun parseBrace(): RdnValue {
        expect('{')
        skipWhitespace()
        if (pos < text.length && text[pos] == '}') {
            pos++
            return RdnObject(emptyList())
        }
        // Parse first value, then peek at separator
        val firstValue = parseValue()
        skipWhitespace()
        return when {
            pos < text.length && text[pos] == ':' -> {
                // Object: firstValue must be a string key
                val key = (firstValue as? RdnString)?.value
                    ?: throw RdnSyntaxError("Object keys must be strings", pos)
                expect(':')
                skipWhitespace()
                val v = parseValue()
                val props = mutableListOf(key to v)
                skipWhitespace()
                while (pos < text.length && text[pos] == ',') {
                    pos++
                    skipWhitespace()
                    val k2 = parseString().value
                    skipWhitespace()
                    expect(':')
                    skipWhitespace()
                    val v2 = parseValue()
                    props.add(k2 to v2)
                    skipWhitespace()
                }
                expect('}')
                RdnObject(props)
            }
            pos < text.length && text.startsWith("=>", pos) -> {
                // Map
                pos += 2
                skipWhitespace()
                val v = parseValue()
                val entries = mutableListOf(firstValue to v)
                skipWhitespace()
                while (pos < text.length && text[pos] == ',') {
                    pos++
                    skipWhitespace()
                    val k2 = parseValue()
                    skipWhitespace()
                    expectString("=>")
                    skipWhitespace()
                    val v2 = parseValue()
                    entries.add(k2 to v2)
                    skipWhitespace()
                }
                expect('}')
                RdnMap(entries, explicit = false)
            }
            else -> {
                // Set: firstValue, then optional more values
                val elements = mutableListOf(firstValue)
                while (pos < text.length && text[pos] == ',') {
                    pos++
                    skipWhitespace()
                    elements.add(parseValue())
                    skipWhitespace()
                }
                expect('}')
                RdnSet(elements, explicit = false)
            }
        }
    }

    private fun parseArray(): RdnValue {
        expect('[')
        skipWhitespace()
        if (pos < text.length && text[pos] == ']') { pos++; return RdnArray(emptyList()) }
        val elements = mutableListOf(parseValue())
        skipWhitespace()
        while (pos < text.length && text[pos] == ',') {
            pos++; skipWhitespace()
            elements.add(parseValue())
            skipWhitespace()
        }
        expect(']')
        return RdnArray(elements)
    }

    private fun parseTuple(): RdnValue {
        expect('(')
        skipWhitespace()
        if (pos < text.length && text[pos] == ')') { pos++; return RdnTuple(emptyList()) }
        val elements = mutableListOf(parseValue())
        skipWhitespace()
        while (pos < text.length && text[pos] == ',') {
            pos++; skipWhitespace()
            elements.add(parseValue())
            skipWhitespace()
        }
        expect(')')
        return RdnTuple(elements)
    }

    private fun parseNumber(): RdnValue {
        val start = pos
        if (pos < text.length && text[pos] == '-') pos++
        while (pos < text.length && text[pos].isDigit()) pos++
        val hasDot = pos < text.length && text[pos] == '.'
        if (hasDot) { pos++; while (pos < text.length && text[pos].isDigit()) pos++ }
        val hasExp = pos < text.length && (text[pos] == 'e' || text[pos] == 'E')
        if (hasExp) {
            pos++
            if (pos < text.length && (text[pos] == '+' || text[pos] == '-')) pos++
            while (pos < text.length && text[pos].isDigit()) pos++
        }
        val isBigInt = pos < text.length && text[pos] == 'n'
        if (isBigInt) {
            if (hasDot || hasExp) throw RdnSyntaxError("BigInt cannot have decimal or exponent", pos)
            pos++
            val raw = text.substring(start, pos - 1)
            return RdnBigInt(BigInteger(raw))
        }
        return RdnNumber(text.substring(start, pos).toDouble())
    }

    // parseNegative, parseAt, parseRegExp, parseBinaryOrIdentifier, parseMapKeyword,
    // parseSetKeyword, parseNaNOrNull, parseInfinity, parseTrue, parseFalse, parseNull
    // — implement following the same pattern as parseNumber above.
    // Full implementation mirrors packages/rdn-js/src/parser.ts.

    private fun Char.isHexDigit() = this in '0'..'9' || this in 'a'..'f' || this in 'A'..'F'
}
```

**Implementation note:** Implement all remaining private parse methods following the same pattern. The TypeScript parser at `packages/rdn-js/src/parser.ts` is the definitive reference. Pay special attention to:
- DateTime disambiguation: `@` followed by 4 digits and `-` → DateTime; `@` followed by 2 digits and `:` → TimeOnly; `@P` → Duration; `@` followed by digits only → Unix timestamp.
- BigInt: disallow decimal points and exponents.
- Binary: `b"..."` decodes as base64, `x"..."` decodes as hex.
- RegExp: scan for closing `/` that is not escaped; read `[dgimsuvy]` flags immediately after.

## Acceptance Criteria
- [ ] `RdnKotlinParser.parse("{}")` returns `RdnObject(emptyList())`
- [ ] `RdnKotlinParser.parse("42n")` returns `RdnBigInt(BigInteger.valueOf(42))`
- [ ] `RdnKotlinParser.parse("@2024-01-15")` returns `RdnDateOnly(LocalDate.of(2024, 1, 15))`
- [ ] `RdnKotlinParser.parse("@14:30:00")` returns `RdnTimeOnly(14, 30, 0, 0)`
- [ ] `RdnKotlinParser.parse("@P1Y")` returns `RdnDuration("P1Y")`
- [ ] `RdnKotlinParser.parse("b\"SGVsbG8=\"")` returns `RdnBinaryBase64` with decoded bytes for "Hello"
- [ ] `RdnKotlinParser.parse("{\"k\" => \"v\"}")` returns `RdnMap` with one entry
- [ ] `RdnKotlinParser.parse("{\"a\", \"b\"}")` returns `RdnSet` with two elements
- [ ] `RdnKotlinParser.parse("3.14n")` throws `RdnSyntaxError` (BigInt with decimal)
- [ ] Parser throws `RdnSyntaxError` with a non-negative `offset` for all invalid inputs

## Dependencies
- Depends on: task-001
- Blocks: task-012, task-015
