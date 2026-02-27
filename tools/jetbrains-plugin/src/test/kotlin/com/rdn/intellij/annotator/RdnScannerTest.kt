package com.rdn.intellij.annotator

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class RdnScannerTest {

    // ===== Unquoted Key Tests =====

    @Test
    fun testUnquotedKeysSimple() {
        val result = scanUnquotedKeys("{foo: 1}")
        assertEquals(1, result.size)
        assertEquals("foo", result[0].name)
        assertEquals(1, result[0].offset)
        assertEquals(3, result[0].length)
    }

    @Test
    fun testNoUnquotedKeysWhenQuoted() {
        val result = scanUnquotedKeys("""{"foo": 1}""")
        assertTrue(result.isEmpty(), "Expected no errors for quoted key, got: $result")
    }

    @Test
    fun testMultipleUnquotedKeys() {
        val result = scanUnquotedKeys("{foo: 1, bar: 2, baz: 3}")
        assertEquals(3, result.size)
        assertEquals(listOf("foo", "bar", "baz"), result.map { it.name })
    }

    @Test
    fun testMixedQuotedAndUnquoted() {
        val result = scanUnquotedKeys("""{"ok": 1, bad: 2}""")
        assertEquals(1, result.size)
        assertEquals("bad", result[0].name)
    }

    @Test
    fun testUnquotedKeysNested() {
        val result = scanUnquotedKeys("""{"outer": {inner: 1}}""")
        assertEquals(1, result.size)
        assertEquals("inner", result[0].name)
    }

    @Test
    fun testBraceDisambiguationObject() {
        val result = scanUnquotedKeys("""{"a": 1}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBraceDisambiguationMap() {
        val result = scanUnquotedKeys("""{"a" => 1, "b" => 2}""")
        assertTrue(result.isEmpty(), "Map keys should not trigger unquoted key errors")
    }

    @Test
    fun testBraceDisambiguationImplicitMap() {
        val result = scanUnquotedKeys("""{"a" => 1}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBraceDisambiguationSet() {
        val result = scanUnquotedKeys("""{"a", "b", "c"}""")
        assertTrue(result.isEmpty(), "Set elements should not trigger unquoted key errors")
    }

    @Test
    fun testExplicitMapNoErrors() {
        val result = scanUnquotedKeys("""Map{"key" => "value"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testExplicitSetNoErrors() {
        val result = scanUnquotedKeys("""Set{"a", "b"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testArrayContextNoErrors() {
        val result = scanUnquotedKeys("[foo, bar]")
        assertTrue(result.isEmpty(), "Array elements should not trigger unquoted key errors")
    }

    @Test
    fun testTupleContextNoErrors() {
        val result = scanUnquotedKeys("(foo, bar)")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testKeywordsNotFlagged() {
        val result = scanUnquotedKeys("""{"x": null, "y": true}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testUnquotedKeyInsideRegex_NotFlagged() {
        val result = scanUnquotedKeys("""{"re": /foo/}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testUnquotedKeyInsideString_NotFlagged() {
        val result = scanUnquotedKeys("""{"str": "foo: bar"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testEmptyObject() {
        assertTrue(scanUnquotedKeys("{}").isEmpty())
    }

    @Test
    fun testEmptyInput() {
        assertTrue(scanUnquotedKeys("").isEmpty())
    }

    @Test
    fun testSkipsRegexAndStrings() {
        val result = scanUnquotedKeys("""{"re": /foo:bar/gi, "str": "baz:qux"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testOffsetCorrectness() {
        val input = "{ foo: 1 }"
        val result = scanUnquotedKeys(input)
        assertEquals(1, result.size)
        assertEquals(2, result[0].offset)
        assertEquals(3, result[0].length)
        assertEquals("foo", input.substring(result[0].offset, result[0].offset + result[0].length))
    }

    // ===== Binary Error Tests =====

    @Test
    fun testBinaryErrorsBase64Valid() {
        val result = scanBinaryErrors("""b"SGVsbG8="""")
        assertTrue(result.isEmpty(), "Expected no errors for valid base64")
    }

    @Test
    fun testBinaryErrorsBase64Invalid() {
        val input = """b"SGVs!G8=""""
        val result = scanBinaryErrors(input)
        assertEquals(1, result.size)
        assertEquals(BinaryEncoding.BASE64, result[0].kind)
        assertEquals('!', input[result[0].offset])
    }

    @Test
    fun testBinaryErrorsHexValid() {
        val result = scanBinaryErrors("""x"48656C6C6F"""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBinaryErrorsHexInvalid() {
        val result = scanBinaryErrors("""x"48g56"""")
        assertEquals(1, result.size)
        assertEquals(BinaryEncoding.HEX, result[0].kind)
    }

    @Test
    fun testBinaryErrorsMultipleInvalid() {
        val result = scanBinaryErrors("""b"!@#"""")
        assertEquals(3, result.size)
    }

    @Test
    fun testBinaryErrorsInsideObject() {
        val result = scanBinaryErrors("""{"data": b"SGVs!G8="}""")
        assertEquals(1, result.size)
    }

    @Test
    fun testBinaryErrorsNonBinaryStrings_NotFlagged() {
        val result = scanBinaryErrors("""{"key": "hello!world"}""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBinaryErrorsEmptyBinary() {
        val result = scanBinaryErrors("""b"" """)
        assertTrue(result.isEmpty())
    }

    @Test
    fun testBinaryErrorsEmptyInput() {
        assertTrue(scanBinaryErrors("").isEmpty())
    }

    @Test
    fun testBinaryErrorsHexUpperAndLower() {
        val result = scanBinaryErrors("""x"abcdefABCDEF0123456789"""")
        assertTrue(result.isEmpty())
    }

    @Test
    fun testMultipleBinaryLiteralsInDocument() {
        val result = scanBinaryErrors("""[b"SGVs!G8=", x"48g56"]""")
        assertEquals(2, result.size)
        assertEquals(BinaryEncoding.BASE64, result[0].kind)
        assertEquals(BinaryEncoding.HEX, result[1].kind)
    }
}
