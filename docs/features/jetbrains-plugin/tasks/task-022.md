# Task 022: Write Formatter Tests

## References
- [Tech Design](../tech-design.md) — Section 10.4
- [Discovery](../discovery.md)

## Description
Create `RdnFormatterTest.kt` testing the `RdnCstFormatter` directly (not through IntelliJ's formatting API). Tests verify compact formatting, multi-line expansion at the 80-char threshold, key sorting, tab vs. spaces indentation, explicit Map/Set keywords, and graceful handling of invalid input.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/test/kotlin/com/rdn/intellij/formatter/RdnFormatterTest.kt` — Formatter tests

## Implementation Details

### `RdnFormatterTest.kt`

```kotlin
package com.rdn.intellij.formatter

import org.junit.jupiter.api.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull
import kotlin.test.assertTrue

class RdnFormatterTest {
    private fun format(input: String, opts: RdnFormatOptions = RdnFormatOptions()) =
        RdnCstFormatter.format(input, opts)

    private fun formatSorted(input: String, opts: RdnFormatOptions = RdnFormatOptions()) =
        RdnCstFormatter.formatSorted(input, opts)

    // ===== Compact Formatting =====

    @Test
    fun testCompactObject() {
        val result = format("""{"a":1,"b":2}""")
        assertEquals("{\"a\": 1, \"b\": 2}\n", result)
    }

    @Test
    fun testCompactArray() {
        val result = format("[1,2,3]")
        assertEquals("[1, 2, 3]\n", result)
    }

    @Test
    fun testCompactTuple() {
        val result = format("(1,2,3)")
        assertEquals("(1, 2, 3)\n", result)
    }

    @Test
    fun testCompactEmptyObject() {
        val result = format("{}")
        assertEquals("{}\n", result)
    }

    @Test
    fun testCompactEmptyArray() {
        val result = format("[]")
        assertEquals("[]\n", result)
    }

    @Test
    fun testCompactAtomicLiterals() {
        assertEquals("null\n", format("null"))
        assertEquals("true\n", format("true"))
        assertEquals("false\n", format("false"))
        assertEquals("42\n", format("42"))
        assertEquals("3.14\n", format("3.14"))
        assertEquals("42n\n", format("42n"))
        assertEquals("NaN\n", format("NaN"))
        assertEquals("Infinity\n", format("Infinity"))
        assertEquals("-Infinity\n", format("-Infinity"))
    }

    @Test
    fun testCompactString() {
        assertEquals("\"hello\"\n", format("\"hello\""))
    }

    @Test
    fun testCompactDateTime() {
        assertEquals("@2024-01-15T10:30:00.000Z\n", format("@2024-01-15T10:30:00.000Z"))
    }

    @Test
    fun testCompactBinary() {
        assertEquals("b\"SGVsbG8=\"\n", format("b\"SGVsbG8=\""))
    }

    @Test
    fun testCompactRegExp() {
        assertEquals("/hello/gi\n", format("/hello/gi"))
    }

    // ===== Multi-line Expansion =====

    @Test
    fun testMultiLineExpansion() {
        // Object with properties that exceed 80 chars when on one line
        val input = """{"longPropertyName": "longStringValue1", "anotherLongProperty": "longStringValue2", "third": "longStringValue3"}"""
        val result = format(input)
        assertTrue(result.contains("\n"), "Expected multi-line output for long object")
    }

    @Test
    fun testMultiLineNestedObject() {
        val input = """{"a": {"b": {"c": "very long value that will push the line over the 80 character limit when nested"}}}"""
        val result = format(input)
        assertTrue(result.lines().size > 1, "Expected multi-line output")
    }

    @Test
    fun testShortObjectStaysCompact() {
        val input = """{"a": 1}"""
        val result = format(input)
        assertEquals("{\"a\": 1}\n", result)
    }

    // ===== Sort Keys =====

    @Test
    fun testSortKeys() {
        val input = """{"z": 3, "a": 1, "m": 2}"""
        val result = formatSorted(input)
        assertEquals("{\"a\": 1, \"m\": 2, \"z\": 3}\n", result)
    }

    @Test
    fun testSortKeysNested() {
        val input = """{"b": {"z": 1, "a": 2}, "a": {"y": 3, "x": 4}}"""
        val result = formatSorted(input)
        // Top-level sorted: "a" before "b"
        assertTrue(result!!.indexOf("\"a\"") < result.indexOf("\"b\""))
    }

    @Test
    fun testSortKeysArrayPreserved() {
        // Array element order should be preserved (arrays are not sorted)
        val input = """{"arr": [3, 1, 2]}"""
        val result = formatSorted(input)
        assertTrue(result!!.contains("[3, 1, 2]"), "Array order should be preserved")
    }

    // ===== Indentation =====

    @Test
    fun testSpacesIndentation() {
        val input = """{"a": {"b": "very long nested value that exceeds the line limit and forces multi-line output"}}"""
        val opts = RdnFormatOptions(tabSize = 4, insertSpaces = true)
        val result = format(input, opts)
        assertTrue(result.contains("    "), "Expected 4-space indentation")
    }

    @Test
    fun testTabIndentation() {
        val input = """{"a": {"b": "very long nested value that exceeds the line limit and forces multi-line output"}}"""
        val opts = RdnFormatOptions(insertSpaces = false)
        val result = format(input, opts)
        assertTrue(result.contains("\t"), "Expected tab indentation")
    }

    // ===== Explicit Map/Set Keywords =====

    @Test
    fun testExplicitMapKeyword() {
        val input = """{"key" => "val"}"""
        val opts = RdnFormatOptions(useExplicitMapKeyword = true)
        val result = format(input, opts)
        assertTrue(result.startsWith("Map{") || result.contains("Map{"), "Expected Map keyword")
    }

    @Test
    fun testExplicitSetKeyword() {
        val input = """{"a", "b"}"""
        val opts = RdnFormatOptions(useExplicitSetKeyword = true)
        val result = format(input, opts)
        assertTrue(result.startsWith("Set{") || result.contains("Set{"), "Expected Set keyword")
    }

    @Test
    fun testImplicitMapByDefault() {
        val input = """Map{"key" => "val"}"""
        val opts = RdnFormatOptions(useExplicitMapKeyword = false)
        val result = format(input, opts)
        // With useExplicitMapKeyword=false, explicit Map keyword may still be preserved
        // depending on implementation. The key assertion: no extra Map is added for implicit maps.
        assertTrue(result.contains("=>"), "Map entries must be preserved")
    }

    // ===== Error Handling =====

    @Test
    fun testPreservesInvalidInput() {
        val invalid = "this is not valid rdn !!!"
        val result = format(invalid)
        assertEquals(invalid, result, "Invalid input should be returned unchanged")
    }

    @Test
    fun testFormatSortedReturnsNullForInvalid() {
        val result = formatSorted("not valid rdn")
        assertNull(result, "formatSorted should return null for invalid input")
    }

    @Test
    fun testTrailingNewline() {
        val result = format("42")
        assertTrue(result.endsWith("\n"), "Formatted output must end with newline")
    }

    @Test
    fun testFormatSortedTrailingNewline() {
        val result = formatSorted("{\"a\": 1}")
        assertTrue(result?.endsWith("\n") ?: false, "formatSorted output must end with newline")
    }

    // ===== Collections =====

    @Test
    fun testImplicitSetFormatting() {
        val result = format("""{"a", "b", "c"}""")
        assertTrue(result.contains("\"a\"") && result.contains("\"b\"") && result.contains("\"c\""))
    }

    @Test
    fun testExplicitMapFormatting() {
        val result = format("""Map{"a" => 1}""")
        assertTrue(result.contains("Map{") || result.contains("\"a\" => 1"))
    }
}
```

## Acceptance Criteria
- [ ] All tests pass with `./gradlew test --tests "*RdnFormatterTest*"`
- [ ] Compact formatting adds a space after `:` in objects and after `,` in collections
- [ ] Multi-line output uses the correct indentation unit (spaces or tab)
- [ ] `formatSorted` sorts top-level keys and nested object keys alphabetically
- [ ] Array and tuple element order is preserved by `formatSorted`
- [ ] `format()` returns the original string (unchanged) when parsing fails
- [ ] `formatSorted()` returns `null` when parsing fails
- [ ] All formatted output ends with exactly one newline

## Dependencies
- Depends on: task-019, task-020
- Blocks: None (tests are standalone)
