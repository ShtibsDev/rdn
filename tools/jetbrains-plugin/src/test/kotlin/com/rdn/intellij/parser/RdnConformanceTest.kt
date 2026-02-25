package com.rdn.intellij.parser

import com.rdn.intellij.parser.model.*
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.TestFactory
import org.junit.jupiter.api.assertThrows
import java.io.File
import java.util.Base64

/**
 * Conformance tests for [RdnKotlinParser] driven by the shared language-agnostic test suite
 * located at `test-suite/` relative to the repository root.
 *
 * The directory is passed in via the `testSuiteDir` system property, configured in build.gradle.kts:
 *
 *   systemProperty("testSuiteDir", rootProject.projectDir.resolve("../../test-suite").absolutePath)
 *
 * Three test categories are exercised:
 * - `valid/`   – parse .rdn, serialize to tagged JSON, compare with .expected.json
 * - `invalid/` – parse .rdn, assert [RdnSyntaxError] is thrown
 * - `roundtrip/` – parse .rdn → stringify back to RDN → parse again, assert structural equality
 */
class RdnConformanceTest {

    private val testSuiteDir: File by lazy {
        val path = System.getProperty("testSuiteDir")
            ?: error("System property 'testSuiteDir' is not set. Run tests via Gradle.")
        File(path).also { require(it.isDirectory) { "testSuiteDir does not exist: $path" } }
    }

    // ── Valid tests ──────────────────────────────────────────────────────────

    @TestFactory
    fun validTests(): List<DynamicTest> {
        val validDir = testSuiteDir.resolve("valid")
        val rdnFiles = validDir.listFiles { f -> f.extension == "rdn" } ?: return emptyList()
        return rdnFiles.sortedBy { it.name }.map { rdnFile ->
            DynamicTest.dynamicTest("valid/${rdnFile.nameWithoutExtension}") {
                val expectedFile = rdnFile.resolveSibling("${rdnFile.nameWithoutExtension}.expected.json")
                require(expectedFile.exists()) { "Missing expected file: ${expectedFile.path}" }

                val source = rdnFile.readText()
                val parsed = RdnKotlinParser.parse(source)
                val actual = toTaggedJson(parsed)

                val expected = expectedFile.readText().trim()
                assertEqual(expected, actual, rdnFile.name)
            }
        }
    }

    // ── Invalid tests ────────────────────────────────────────────────────────

    @TestFactory
    fun invalidTests(): List<DynamicTest> {
        val invalidDir = testSuiteDir.resolve("invalid")
        val rdnFiles = invalidDir.listFiles { f -> f.extension == "rdn" } ?: return emptyList()
        return rdnFiles.sortedBy { it.name }.map { rdnFile ->
            DynamicTest.dynamicTest("invalid/${rdnFile.nameWithoutExtension}") {
                val source = rdnFile.readText()
                assertThrows<RdnSyntaxError>("Expected RdnSyntaxError for ${rdnFile.name}") {
                    RdnKotlinParser.parse(source)
                }
            }
        }
    }

    // ── Roundtrip tests ──────────────────────────────────────────────────────

    @TestFactory
    fun roundtripTests(): List<DynamicTest> {
        val roundtripDir = testSuiteDir.resolve("roundtrip")
        val rdnFiles = roundtripDir.listFiles { f -> f.extension == "rdn" } ?: return emptyList()
        return rdnFiles.sortedBy { it.name }.map { rdnFile ->
            DynamicTest.dynamicTest("roundtrip/${rdnFile.nameWithoutExtension}") {
                val source = rdnFile.readText()
                val first = RdnKotlinParser.parse(source)
                val serialized = stringify(first)
                val second = RdnKotlinParser.parse(serialized)
                check(rdnValuesEqual(first, second)) {
                    "Roundtrip failed for ${rdnFile.name}:\n" +
                    "  original value : $first\n" +
                    "  serialized RDN : $serialized\n" +
                    "  re-parsed value: $second"
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Tagged-JSON serializer
    //
    // Mirrors the convention used in test-suite/valid/*.expected.json:
    //   native JSON types   → plain JSON (null, bool, number, string, array, object)
    //   extended RDN types  → {"$type": "TypeName", "value": ...}
    // ════════════════════════════════════════════════════════════════════════

    private fun toTaggedJson(value: RdnValue): String = when (value) {
        is RdnNull -> "null"
        is RdnBoolean -> if (value.value) "true" else "false"
        is RdnNumber -> {
            val d = value.value
            when {
                d.isNaN() -> "null"          // plain NaN has no JSON representation; goes via RdnNaN
                d.isInfinite() -> "null"     // same – goes via RdnInfinity
                d == d.toLong().toDouble() && !d.isInfinite() && d >= Long.MIN_VALUE && d <= Long.MAX_VALUE ->
                    d.toLong().toString()
                else -> d.toBigDecimal().stripTrailingZeros().toPlainString()
            }
        }
        is RdnString -> jsonString(value.value)
        is RdnBigInt -> """{"${'$'}type":"BigInt","value":${jsonString(value.value.toString())}}"""
        is RdnDateTime -> {
            val iso = value.instant.toString() // produces "2024-01-15T10:30:00.123Z" or "2024-01-15T00:00:00Z"
            // Normalise to always include milliseconds (the spec expects ".000Z" not "Z")
            val normalised = normaliseDateIso(iso)
            """{"${'$'}type":"Date","value":${jsonString(normalised)}}"""
        }
        is RdnDateOnly -> {
            // DateOnly maps to midnight UTC in the tagged format
            val iso = "${value.date}T00:00:00.000Z"
            """{"${'$'}type":"Date","value":${jsonString(iso)}}"""
        }
        is RdnTimeOnly -> {
            val inner = """{"hours":${value.hours},"minutes":${value.minutes},"seconds":${value.seconds},"milliseconds":${value.milliseconds}}"""
            """{"${'$'}type":"TimeOnly","value":$inner}"""
        }
        is RdnDuration -> """{"${'$'}type":"Duration","value":${jsonString(value.iso)}}"""
        is RdnRegExp -> {
            val inner = """{"source":${jsonString(value.pattern)},"flags":${jsonString(value.flags)}}"""
            """{"${'$'}type":"RegExp","value":$inner}"""
        }
        is RdnBinaryBase64 -> {
            val b64 = if (value.data.isEmpty()) "" else Base64.getEncoder().encodeToString(value.data)
            """{"${'$'}type":"Binary","value":${jsonString(b64)}}"""
        }
        is RdnBinaryHex -> {
            // Both base64 and hex binary encode to base64 in the expected JSON
            val b64 = if (value.data.isEmpty()) "" else Base64.getEncoder().encodeToString(value.data)
            """{"${'$'}type":"Binary","value":${jsonString(b64)}}"""
        }
        is RdnNaN -> """{"${'$'}type":"Number","value":"NaN"}"""
        is RdnInfinity -> {
            val v = if (value.negative) "-Infinity" else "Infinity"
            """{"${'$'}type":"Number","value":"$v"}"""
        }
        is RdnArray -> {
            val items = value.elements.joinToString(",") { toTaggedJson(it) }
            "[$items]"
        }
        is RdnTuple -> {
            // Tuples round-trip as plain arrays in the tagged JSON (see tuple.expected.json)
            val items = value.elements.joinToString(",") { toTaggedJson(it) }
            "[$items]"
        }
        is RdnObject -> {
            val entries = value.properties.joinToString(",") { (k, v) -> "${jsonString(k)}:${toTaggedJson(v)}" }
            "{$entries}"
        }
        is RdnMap -> {
            val pairs = value.entries.joinToString(",") { (k, v) -> "[${toTaggedJson(k)},${toTaggedJson(v)}]" }
            """{"${'$'}type":"Map","value":[$pairs]}"""
        }
        is RdnSet -> {
            val items = value.elements.joinToString(",") { toTaggedJson(it) }
            """{"${'$'}type":"Set","value":[$items]}"""
        }
    }

    /** Ensure the ISO string always has milliseconds: "...T10:30:00Z" → "...T10:30:00.000Z". */
    private fun normaliseDateIso(iso: String): String {
        // java.time.Instant.toString() omits fractional seconds when zero
        return if (iso.endsWith("Z") && !iso.contains('.')) {
            iso.dropLast(1) + ".000Z"
        } else {
            // May have nano precision beyond ms; truncate to ms
            val dotIdx = iso.indexOf('.')
            if (dotIdx >= 0) {
                val fracAndZ = iso.substring(dotIdx + 1)
                val fracDigits = fracAndZ.dropLast(1) // remove trailing 'Z'
                val ms = fracDigits.padEnd(3, '0').take(3)
                iso.substring(0, dotIdx) + "." + ms + "Z"
            } else {
                iso
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // RDN serializer (for roundtrip tests)
    // ════════════════════════════════════════════════════════════════════════

    private fun stringify(value: RdnValue): String = when (value) {
        is RdnNull -> "null"
        is RdnBoolean -> if (value.value) "true" else "false"
        is RdnNumber -> {
            val d = value.value
            if (d == d.toLong().toDouble() && !d.isInfinite()) d.toLong().toString()
            else d.toString()
        }
        is RdnString -> jsonString(value.value)
        is RdnBigInt -> "${value.value}n"
        is RdnDateTime -> {
            val iso = normaliseDateIso(value.instant.toString())
            "@$iso"
        }
        is RdnDateOnly -> "@${value.date}"
        is RdnTimeOnly -> {
            val ms = if (value.milliseconds != 0) ".${value.milliseconds.toString().padStart(3, '0')}" else ""
            "@${value.hours.toString().padStart(2, '0')}:${value.minutes.toString().padStart(2, '0')}:${value.seconds.toString().padStart(2, '0')}$ms"
        }
        is RdnDuration -> "@${value.iso}"
        is RdnRegExp -> "/${value.pattern}/${value.flags}"
        is RdnBinaryBase64 -> {
            val b64 = if (value.data.isEmpty()) "" else Base64.getEncoder().encodeToString(value.data)
            "b\"$b64\""
        }
        is RdnBinaryHex -> {
            val hex = value.data.joinToString("") { "%02X".format(it) }
            "x\"$hex\""
        }
        is RdnNaN -> "NaN"
        is RdnInfinity -> if (value.negative) "-Infinity" else "Infinity"
        is RdnArray -> {
            val items = value.elements.joinToString(", ") { stringify(it) }
            "[$items]"
        }
        is RdnTuple -> {
            val items = value.elements.joinToString(", ") { stringify(it) }
            "($items)"
        }
        is RdnObject -> {
            val entries = value.properties.joinToString(", ") { (k, v) -> "${jsonString(k)}: ${stringify(v)}" }
            "{$entries}"
        }
        is RdnMap -> {
            val entries = value.entries.joinToString(", ") { (k, v) -> "${stringify(k)} => ${stringify(v)}" }
            "Map{$entries}"
        }
        is RdnSet -> {
            val items = value.elements.joinToString(", ") { stringify(it) }
            "Set{$items}"
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Structural equality for roundtrip comparison
    // ════════════════════════════════════════════════════════════════════════

    private fun rdnValuesEqual(a: RdnValue, b: RdnValue): Boolean {
        if (a::class != b::class) return false
        return when {
            a is RdnNull && b is RdnNull -> true
            a is RdnBoolean && b is RdnBoolean -> a.value == b.value
            a is RdnNumber && b is RdnNumber -> a.value == b.value || (a.value.isNaN() && b.value.isNaN())
            a is RdnString && b is RdnString -> a.value == b.value
            a is RdnBigInt && b is RdnBigInt -> a.value == b.value
            a is RdnDateTime && b is RdnDateTime -> a.instant == b.instant
            a is RdnDateOnly && b is RdnDateOnly -> a.date == b.date
            a is RdnTimeOnly && b is RdnTimeOnly ->
                a.hours == b.hours && a.minutes == b.minutes && a.seconds == b.seconds && a.milliseconds == b.milliseconds
            a is RdnDuration && b is RdnDuration -> a.iso == b.iso
            a is RdnRegExp && b is RdnRegExp -> a.pattern == b.pattern && a.flags == b.flags
            a is RdnBinaryBase64 && b is RdnBinaryBase64 -> a.data.contentEquals(b.data)
            a is RdnBinaryHex && b is RdnBinaryHex -> a.data.contentEquals(b.data)
            a is RdnNaN && b is RdnNaN -> true
            a is RdnInfinity && b is RdnInfinity -> a.negative == b.negative
            a is RdnArray && b is RdnArray ->
                a.elements.size == b.elements.size && a.elements.zip(b.elements).all { (x, y) -> rdnValuesEqual(x, y) }
            a is RdnTuple && b is RdnTuple ->
                a.elements.size == b.elements.size && a.elements.zip(b.elements).all { (x, y) -> rdnValuesEqual(x, y) }
            a is RdnObject && b is RdnObject ->
                a.properties.size == b.properties.size &&
                a.properties.zip(b.properties).all { (p, q) -> p.first == q.first && rdnValuesEqual(p.second, q.second) }
            a is RdnMap && b is RdnMap ->
                a.entries.size == b.entries.size &&
                a.entries.zip(b.entries).all { (p, q) -> rdnValuesEqual(p.first, q.first) && rdnValuesEqual(p.second, q.second) }
            a is RdnSet && b is RdnSet ->
                a.elements.size == b.elements.size && a.elements.zip(b.elements).all { (x, y) -> rdnValuesEqual(x, y) }
            else -> false
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // JSON string escaping
    // ════════════════════════════════════════════════════════════════════════

    private fun jsonString(s: String): String {
        val sb = StringBuilder(s.length + 2)
        sb.append('"')
        for (ch in s) {
            when (ch) {
                '"' -> sb.append("\\\"")
                '\\' -> sb.append("\\\\")
                '\b' -> sb.append("\\b")
                '\u000C' -> sb.append("\\f")
                '\n' -> sb.append("\\n")
                '\r' -> sb.append("\\r")
                '\t' -> sb.append("\\t")
                else -> if (ch.code < 0x20) sb.append("\\u%04x".format(ch.code)) else sb.append(ch)
            }
        }
        sb.append('"')
        return sb.toString()
    }

    // ════════════════════════════════════════════════════════════════════════
    // Comparison helper (normalise whitespace in expected JSON before compare)
    // ════════════════════════════════════════════════════════════════════════

    /**
     * Compares two JSON strings for semantic equality by stripping insignificant whitespace
     * (spaces and newlines outside of string values) from both sides before comparing.
     */
    private fun assertEqual(expected: String, actual: String, fileName: String) {
        val normExpected = normaliseJson(expected)
        val normActual = normaliseJson(actual)
        check(normExpected == normActual) {
            "Conformance mismatch for $fileName\n" +
            "  expected (normalised): $normExpected\n" +
            "  actual   (normalised): $normActual"
        }
    }

    /**
     * Strips whitespace that is outside quoted strings so that pretty-printed and compact
     * JSON can be compared directly.
     */
    private fun normaliseJson(json: String): String {
        val sb = StringBuilder(json.length)
        var inString = false
        var i = 0
        while (i < json.length) {
            val ch = json[i]
            when {
                inString -> {
                    sb.append(ch)
                    if (ch == '\\' && i + 1 < json.length) {
                        sb.append(json[i + 1])
                        i += 2
                        continue
                    }
                    if (ch == '"') inString = false
                }
                ch == '"' -> {
                    inString = true
                    sb.append(ch)
                }
                ch == ' ' || ch == '\n' || ch == '\r' || ch == '\t' -> { /* skip */ }
                else -> sb.append(ch)
            }
            i++
        }
        return sb.toString()
    }
}
