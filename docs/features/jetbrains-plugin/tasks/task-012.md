# Task 012: Run Conformance Tests Against test-suite

## References
- [Tech Design](../tech-design.md) — Sections 4 (decision #9), 10.1
- [Discovery](../discovery.md)

## Description
Create `RdnConformanceTest.kt` that validates `RdnKotlinParser` against the shared `test-suite/` directory. Tests read all `test-suite/valid/*.rdn` files, parse them, serialize results to JSON using the `$type` convention, and assert equality with `*.expected.json`. Tests read all `test-suite/invalid/*.rdn` files and assert each throws `RdnSyntaxError`. Tests run roundtrip verification for `test-suite/roundtrip/*.rdn`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/test/kotlin/com/rdn/intellij/parser/RdnConformanceTest.kt` — Conformance tests

## Implementation Details

### `RdnConformanceTest.kt`

```kotlin
package com.rdn.intellij.parser

import com.rdn.intellij.parser.model.*
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.TestFactory
import org.junit.jupiter.api.assertThrows
import java.io.File
import java.math.BigInteger
import kotlin.test.assertEquals

class RdnConformanceTest {
    private val testSuiteDir: File = run {
        val path = System.getProperty("testSuiteDir")
            ?: error("testSuiteDir system property not set. Run via Gradle.")
        File(path)
    }

    @TestFactory
    fun validTests(): List<DynamicTest> {
        val validDir = testSuiteDir.resolve("valid")
        return validDir.listFiles { f -> f.extension == "rdn" }!!
            .sortedBy { it.name }
            .map { rdnFile ->
                DynamicTest.dynamicTest("valid/${rdnFile.nameWithoutExtension}") {
                    val expectedFile = rdnFile.resolveSibling("${rdnFile.nameWithoutExtension}.expected.json")
                    val rdnText = rdnFile.readText()
                    val expectedJson = expectedFile.readText()

                    val parsed = RdnKotlinParser.parse(rdnText)
                    val actualJson = toTaggedJson(parsed)

                    assertEquals(
                        normalizeJson(expectedJson),
                        normalizeJson(actualJson),
                        "Mismatch for ${rdnFile.name}"
                    )
                }
            }
    }

    @TestFactory
    fun invalidTests(): List<DynamicTest> {
        val invalidDir = testSuiteDir.resolve("invalid")
        return invalidDir.listFiles { f -> f.extension == "rdn" }!!
            .sortedBy { it.name }
            .map { rdnFile ->
                DynamicTest.dynamicTest("invalid/${rdnFile.nameWithoutExtension}") {
                    val rdnText = rdnFile.readText()
                    assertThrows<RdnSyntaxError> {
                        RdnKotlinParser.parse(rdnText)
                    }
                }
            }
    }

    @TestFactory
    fun roundtripTests(): List<DynamicTest> {
        val roundtripDir = testSuiteDir.resolve("roundtrip")
        return roundtripDir.listFiles { f -> f.extension == "rdn" }!!
            .sortedBy { it.name }
            .map { rdnFile ->
                DynamicTest.dynamicTest("roundtrip/${rdnFile.nameWithoutExtension}") {
                    val rdnText = rdnFile.readText()
                    val firstParse = RdnKotlinParser.parse(rdnText)
                    val stringified = stringify(firstParse)
                    val secondParse = RdnKotlinParser.parse(stringified)
                    assertEquals(firstParse, secondParse, "Roundtrip failed for ${rdnFile.name}")
                }
            }
    }

    /**
     * Converts an RdnValue to the tagged JSON format used by test-suite expected files.
     * Tagged convention: {"$type": "TypeName", "value": ...}
     */
    private fun toTaggedJson(value: RdnValue): String = when (value) {
        is RdnNull -> "null"
        is RdnBoolean -> if (value.value) "true" else "false"
        is RdnNumber -> {
            val d = value.value
            if (d.isNaN()) """{"${"\$"}type":"NaN"}"""
            else if (d.isInfinite()) """{"${"\$"}type":"Infinity","negative":${d < 0}}"""
            else if (d == d.toLong().toDouble()) d.toLong().toString()
            else d.toString()
        }
        is RdnBigInt -> """{"${"\$"}type":"BigInt","value":"${value.value}"}"""
        is RdnString -> "\"${escapeJson(value.value)}\""
        is RdnNaN -> """{"${"\$"}type":"NaN"}"""
        is RdnInfinity -> """{"${"\$"}type":"Infinity","negative":${value.negative}}"""
        is RdnDateTime -> """{"${"\$"}type":"Date","value":"${value.instant}"}"""
        is RdnDateOnly -> """{"${"\$"}type":"Date","value":"${value.date}T00:00:00.000Z"}"""
        is RdnTimeOnly -> """{"${"\$"}type":"TimeOnly","hours":${value.hours},"minutes":${value.minutes},"seconds":${value.seconds},"milliseconds":${value.milliseconds}}"""
        is RdnDuration -> """{"${"\$"}type":"Duration","value":"${value.iso}"}"""
        is RdnRegExp -> """{"${"\$"}type":"RegExp","pattern":"${escapeJson(value.pattern)}","flags":"${value.flags}"}"""
        is RdnBinaryBase64 -> """{"${"\$"}type":"BinaryBase64","value":"${java.util.Base64.getEncoder().encodeToString(value.data)}"}"""
        is RdnBinaryHex -> """{"${"\$"}type":"BinaryHex","value":"${value.data.joinToString("") { "%02x".format(it) }}"}"""
        is RdnArray -> "[${value.elements.joinToString(",") { toTaggedJson(it) }}]"
        is RdnTuple -> """{"${"\$"}type":"Tuple","elements":[${value.elements.joinToString(",") { toTaggedJson(it) }}]}"""
        is RdnObject -> "{${value.properties.joinToString(",") { (k, v) -> "\"${escapeJson(k)}\":${toTaggedJson(v)}" }}}"
        is RdnMap -> """{"${"\$"}type":"Map","entries":[${value.entries.joinToString(",") { (k, v) -> "[${toTaggedJson(k)},${toTaggedJson(v)}]" }}],"explicit":${value.explicit}}"""
        is RdnSet -> """{"${"\$"}type":"Set","elements":[${value.elements.joinToString(",") { toTaggedJson(it) }}],"explicit":${value.explicit}}"""
    }

    private fun escapeJson(s: String): String = s
        .replace("\\", "\\\\")
        .replace("\"", "\\\"")
        .replace("\n", "\\n")
        .replace("\r", "\\r")
        .replace("\t", "\\t")

    /** Stringify an RdnValue back to RDN text for roundtrip testing. */
    private fun stringify(value: RdnValue): String = when (value) {
        is RdnNull -> "null"
        is RdnBoolean -> value.value.toString()
        is RdnNumber -> value.value.toString()
        is RdnNaN -> "NaN"
        is RdnInfinity -> if (value.negative) "-Infinity" else "Infinity"
        is RdnBigInt -> "${value.value}n"
        is RdnString -> "\"${escapeJson(value.value)}\""
        is RdnDateTime -> "@${value.instant}"
        is RdnDateOnly -> "@${value.date}"
        is RdnTimeOnly -> "@%02d:%02d:%02d".format(value.hours, value.minutes, value.seconds)
        is RdnDuration -> "@${value.iso}"
        is RdnRegExp -> "/${value.pattern}/${value.flags}"
        is RdnBinaryBase64 -> "b\"${java.util.Base64.getEncoder().encodeToString(value.data)}\""
        is RdnBinaryHex -> "x\"${value.data.joinToString("") { "%02x".format(it) }}\""
        is RdnArray -> "[${value.elements.joinToString(", ") { stringify(it) }}]"
        is RdnTuple -> "(${value.elements.joinToString(", ") { stringify(it) }})"
        is RdnObject -> "{${value.properties.joinToString(", ") { (k, v) -> "\"${escapeJson(k)}\": ${stringify(v)}" }}}"
        is RdnMap -> if (value.explicit) "Map{${value.entries.joinToString(", ") { (k, v) -> "${stringify(k)} => ${stringify(v)}" }}}" else "{${value.entries.joinToString(", ") { (k, v) -> "${stringify(k)} => ${stringify(v)}" }}}"
        is RdnSet -> if (value.explicit) "Set{${value.elements.joinToString(", ") { stringify(it) }}}" else "{${value.elements.joinToString(", ") { stringify(it) }}}"
    }

    private fun normalizeJson(json: String): String {
        // Simple normalization: remove whitespace outside strings
        // A robust implementation uses a JSON parser
        return json.trim()
    }
}
```

### Expected test-suite coverage

**Valid tests (11 files):** `primitives`, `bigint`, `binary`, `datetime`, `map`, `nested`, `regexp`, `set`, `special-numbers`, `time-and-duration`, `tuple`

**Invalid tests (10 files):** `bigint-decimal`, `bigint-exponent`, `invalid-binary`, `invalid-date`, `invalid-hex`, `invalid-regexp`, `single-quotes`, `trailing-comma`, `unclosed-map`, `unquoted-key`

**Roundtrip tests (2 files):** `all-types`, `empty-containers`

### Gradle configuration

The `testSuiteDir` system property is set in `build.gradle.kts`:

```kotlin
tasks {
    test {
        useJUnitPlatform()
        systemProperty(
            "testSuiteDir",
            rootProject.projectDir.resolve("../../test-suite").absolutePath
        )
    }
}
```

## Acceptance Criteria
- [ ] All 11 valid tests pass (parser produces output matching expected JSON)
- [ ] All 10 invalid tests pass (parser throws `RdnSyntaxError`)
- [ ] Both roundtrip tests pass (parse → stringify → parse produces equal structure)
- [ ] Tests locate `test-suite/` automatically via `testSuiteDir` system property
- [ ] Running `./gradlew test --tests "*Conformance*"` reports 23 passing tests
- [ ] If a new `.rdn` file is added to `test-suite/`, it is automatically picked up (no hardcoded file lists)

## Dependencies
- Depends on: task-011
- Blocks: None (conformance tests are standalone validation)
