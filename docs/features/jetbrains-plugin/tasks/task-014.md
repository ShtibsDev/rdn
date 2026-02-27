# Task 014: Write Scanner Tests

## References
- [Tech Design](../tech-design.md) — Section 10.7
- [Discovery](../discovery.md)

## Description
Create `RdnScannerTest.kt` with comprehensive tests for both `scanUnquotedKeys()` and `scanBinaryErrors()`. Tests verify correct brace disambiguation across object, map, set, and nested contexts, and correct identification of invalid base64 and hex characters.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/test/kotlin/com/rdn/intellij/annotator/RdnScannerTest.kt` — Scanner tests

## Implementation Details

### `RdnScannerTest.kt`

```kotlin
package com.rdn.intellij.annotator

import org.junit.jupiter.api.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class RdnScannerTest {

    // ===== Unquoted Key Tests =====

    @Test
    fun testUnquotedKeysSimple() {
        val result = RdnScanner.scanUnquotedKeys("{foo: 1}")
        assertEquals(1, result.size)
        assertEquals("foo", result[0].name)
        assertEquals(1, result[0].offset)
        assertEquals(3, result[0].length)
    }

    @Test
    fun testNoUnquotedKeysWhenQuoted() {
        val result = RdnScanner.scanUnquotedKeys("""{"foo": 1}""")
        assertTrue(result.isEmpty(), "Expected no errors for quoted key, got: $result")
    }

    @Test
    fun testMultipleUnquotedKeys() {
        val result = RdnScanner.scanUnquotedKeys("{foo: 1, bar: 2, baz: 3}")
        assertEquals(3, result.size)
        assertEquals(listOf("foo", "bar", "baz"), result.map { it.name })
    }

    @Test
    fun testMixedQuotedAndUnquoted() {
        val result = RdnScanner.scanUnquotedKeys("""{"ok": 1, bad: 2}""")
        assertEquals(1, result.size)
        assertEquals("bad", result[0].name)
    }

    @Test
    fun testUnquotedKeysNested() {
        val result = RdnScanner.scanUnquotedKeys("""{"outer": {inner: 1}}""")
        assertEquals(1, result.size)
        assertEquals("inner", result[0].name)
    }

    @Test
    fun testBraceDisambiguationObject() {
        // { "key": value } is an object — quoted key, no error
        val result = RdnScanner.scanUnquotedKeys("""{"a": 1}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBraceDisambiguationMap() {
        // { value => value } is a Map — bare value in key position is valid
        val result = RdnScanner.scanUnquotedKeys("""{"a" => 1, "b" => 2}""")
        assertTrue(result.isEmpty(), "Map keys should not trigger unquoted key errors")
    }

    @Test
    fun testBraceDisambiguationImplicitMap() {
        // Implicit map with string keys — no unquoted key errors
        val result = RdnScanner.scanUnquotedKeys("""{"a" => 1}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBraceDisambiguationSet() {
        // { value, ... } is a Set — not object key position
        val result = RdnScanner.scanUnquotedKeys("""{"a", "b", "c"}""")
        assertTrue(result.isEmpty(), "Set elements should not trigger unquoted key errors")
    }

    @Test
    fun testExplicitMapNoErrors() {
        // Map{ ... } — explicit map, no unquoted key errors
        val result = RdnScanner.scanUnquotedKeys("""Map{"key" => "value"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testExplicitSetNoErrors() {
        val result = RdnScanner.scanUnquotedKeys("""Set{"a", "b"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testArrayContextNoErrors() {
        // Array elements are never in key position
        val result = RdnScanner.scanUnquotedKeys("[foo, bar]")
        assertTrue(result.isEmpty(), "Array elements should not trigger unquoted key errors")
    }

    @Test
    fun testTupleContextNoErrors() {
        val result = RdnScanner.scanUnquotedKeys("(foo, bar)")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testKeywordsNotFlagged() {
        // null, true, false, NaN, Infinity are keywords, not unquoted keys
        val result = RdnScanner.scanUnquotedKeys("{\"x\": null, \"y\": true}")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testUnquotedKeyInsideRegex_NotFlagged() {
        // Identifiers inside regex patterns are not object keys
        val result = RdnScanner.scanUnquotedKeys("""{"re": /foo/}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testUnquotedKeyInsideString_NotFlagged() {
        // Identifiers inside strings are not object keys
        val result = RdnScanner.scanUnquotedKeys("""{"str": "foo: bar"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testEmptyObject() {
        assertTrue(RdnScanner.scanUnquotedKeys("{}").isEmpty())
    }

    @Test
    fun testEmptyInput() {
        assertTrue(RdnScanner.scanUnquotedKeys("").isEmpty())
    }

    @Test
    fun testSkipsRegexAndStrings() {
        // "key" in regex and strings must not be treated as key position
        val result = RdnScanner.scanUnquotedKeys("""{"re": /foo:bar/gi, "str": "baz:qux"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testOffsetCorrectness() {
        val input = "{ foo: 1 }"
        val result = RdnScanner.scanUnquotedKeys(input)
        assertEquals(1, result.size)
        assertEquals(2, result[0].offset)
        assertEquals(3, result[0].length)
        assertEquals("foo", input.substring(result[0].offset, result[0].offset + result[0].length))
    }

    // ===== Binary Error Tests =====

    @Test
    fun testBinaryErrorsBase64Valid() {
        val result = RdnScanner.scanBinaryErrors("""b"SGVsbG8="""")
        assertTrue(result.isEmpty(), "Expected no errors for valid base64")
    }

    @Test
    fun testBinaryErrorsBase64Invalid() {
        val result = RdnScanner.scanBinaryErrors("""b"SGVs!G8="""")
        assertEquals(1, result.size)
        assertEquals(BinaryEncoding.BASE64, result[0].kind)
        // Verify the offset points to '!'
        val input = """b"SGVs!G8=""""
        assertEquals('!', input[result[0].offset])
    }

    @Test
    fun testBinaryErrorsHexValid() {
        val result = RdnScanner.scanBinaryErrors("""x"48656C6C6F"""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBinaryErrorsHexInvalid() {
        val result = RdnScanner.scanBinaryErrors("""x"48g56"""")
        assertEquals(1, result.size)
        assertEquals(BinaryEncoding.HEX, result[0].kind)
    }

    @Test
    fun testBinaryErrorsMultipleInvalid() {
        val result = RdnScanner.scanBinaryErrors("""b"!@#"""")
        assertEquals(3, result.size)
    }

    @Test
    fun testBinaryErrorsInsideObject() {
        val result = RdnScanner.scanBinaryErrors("""{"data": b"SGVs!G8="}""")
        assertEquals(1, result.size)
    }

    @Test
    fun testBinaryErrorsNonBinaryStrings_NotFlagged() {
        // Regular strings with non-base64 chars should not be flagged
        val result = RdnScanner.scanBinaryErrors("""{"key": "hello!world"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBinaryErrorsEmptyBinary() {
        // Empty binary literals are valid
        val result = RdnScanner.scanBinaryErrors("""b"""""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBinaryErrorsEmptyInput() {
        assertTrue(RdnScanner.scanBinaryErrors("").isEmpty())
    }

    @Test
    fun testBinaryErrorsHexUpperAndLower() {
        // Both uppercase and lowercase hex digits are valid
        val result = RdnScanner.scanBinaryErrors("""x"abcdefABCDEF0123456789"""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testMultipleBinaryLiteralsInDocument() {
        val result = RdnScanner.scanBinaryErrors("""[b"SGVs!G8=", x"48g56"]""")
        assertEquals(2, result.size)
        assertEquals(BinaryEncoding.BASE64, result[0].kind)
        assertEquals(BinaryEncoding.HEX, result[1].kind)
    }
}
```

## Acceptance Criteria
- [ ] All tests pass with `./gradlew test --tests "*RdnScannerTest*"`
- [ ] `scanUnquotedKeys` correctly returns empty for map contexts (`{"k" => "v"}`)
- [ ] `scanUnquotedKeys` correctly returns empty for set contexts (`{"a", "b"}`)
- [ ] `scanUnquotedKeys` correctly flags bare identifiers in object position
- [ ] Offset values are correct: `input.substring(offset, offset + length)` equals the key name
- [ ] `scanBinaryErrors` distinguishes base64 and hex encodings correctly
- [ ] Empty input and empty binary literals are handled without exceptions
- [ ] Regular string contents are not scanned for binary errors

## Dependencies
- Depends on: task-013
- Blocks: None (tests are standalone)
