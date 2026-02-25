# Task 005: Write Lexer Tests

## References
- [Tech Design](../tech-design.md) — Section 10.2
- [Discovery](../discovery.md)

## Description
Create `RdnLexerTest.kt` with comprehensive token stream assertions covering all RDN token types. Tests verify that for a given input string, the lexer produces exactly the expected sequence of (`IElementType`, `text`) pairs. Use IntelliJ's `LexerTestCase` infrastructure.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/test/kotlin/com/rdn/intellij/lexer/RdnLexerTest.kt` — All lexer tests

## Implementation Details

### Test infrastructure

```kotlin
package com.rdn.intellij.lexer

import com.intellij.testFramework.LexerTestCase
import org.junit.Test

class RdnLexerTest : LexerTestCase() {
    override fun createLexer() = RdnLexerAdapter()
    override fun getDirPath() = "src/test/resources/lexer"

    private fun assertTokens(input: String, vararg expected: Pair<String, String>) {
        val lexer = createLexer()
        lexer.start(input)
        val actual = mutableListOf<Pair<String, String>>()
        while (lexer.tokenType != null) {
            actual.add(lexer.tokenType!!.toString() to lexer.tokenText)
            lexer.advance()
        }
        assertEquals(expected.toList(), actual)
    }
}
```

### Required test cases

```kotlin
@Test
fun testBasicObject() {
    assertTokens(
        """{"key": 42}""",
        "LBRACE" to "{",
        "STRING_OPEN" to "\"",
        "STRING_CONTENT" to "key",
        "STRING_CLOSE" to "\"",
        "COLON" to ":",
        "WHITE_SPACE" to " ",
        "INTEGER" to "42",
        "RBRACE" to "}"
    )
}

@Test
fun testBigInt() {
    assertTokens("42n", "BIGINT" to "42n")
    assertTokens("-999n", "BIGINT" to "-999n")
    // Ensure 42 without n is INTEGER not BIGINT
    assertTokens("42", "INTEGER" to "42")
}

@Test
fun testFloat() {
    assertTokens("3.14", "FLOAT" to "3.14")
    assertTokens("1e10", "FLOAT" to "1e10")
    assertTokens("-2.5e-3", "FLOAT" to "-2.5e-3")
}

@Test
fun testSpecialNumbers() {
    assertTokens("NaN", "NAN" to "NaN")
    assertTokens("Infinity", "INFINITY" to "Infinity")
    assertTokens("-Infinity", "NEG_INFINITY" to "-Infinity")
}

@Test
fun testKeywords() {
    assertTokens("null", "NULL" to "null")
    assertTokens("true", "TRUE" to "true")
    assertTokens("false", "FALSE" to "false")
}

@Test
fun testStringEscapes() {
    assertTokens(
        "\"hello\\nworld\"",
        "STRING_OPEN" to "\"",
        "STRING_CONTENT" to "hello",
        "STRING_ESCAPE" to "\\n",
        "STRING_CONTENT" to "world",
        "STRING_CLOSE" to "\""
    )
    assertTokens(
        "\"\\u0041\"",
        "STRING_OPEN" to "\"",
        "STRING_ESCAPE" to "\\u0041",
        "STRING_CLOSE" to "\""
    )
}

@Test
fun testInvalidEscapes() {
    assertTokens(
        "\"\\q\"",
        "STRING_OPEN" to "\"",
        "STRING_INVALID_ESCAPE" to "\\q",
        "STRING_CLOSE" to "\""
    )
}

@Test
fun testDateTime() {
    assertTokens(
        "@2024-01-15",
        "AT_SIGN" to "@",
        "DATE_PART" to "2024-01-15"
    )
    assertTokens(
        "@2024-01-15T10:30:00.000Z",
        "AT_SIGN" to "@",
        "DATE_PART" to "2024-01-15",
        "TIME_SEPARATOR" to "T",
        "TIME_PART" to "10:30:00",
        "MILLIS_PART" to ".000",
        "TIMEZONE" to "Z"
    )
}

@Test
fun testUnixTimestamp() {
    assertTokens(
        "@1705276800",
        "AT_SIGN" to "@",
        "UNIX_TIMESTAMP" to "1705276800"
    )
}

@Test
fun testTimeOnly() {
    assertTokens(
        "@14:30:00",
        "AT_SIGN" to "@",
        "TIME_PART" to "14:30:00"
    )
}

@Test
fun testDuration() {
    assertTokens(
        "@P1Y2M3D",
        "AT_SIGN" to "@",
        "DURATION_P" to "P",
        "DURATION_NUMBER" to "1",
        "DURATION_UNIT" to "Y",
        "DURATION_NUMBER" to "2",
        "DURATION_UNIT" to "M",
        "DURATION_NUMBER" to "3",
        "DURATION_UNIT" to "D"
    )
    assertTokens(
        "@P1DT2H30M",
        "AT_SIGN" to "@",
        "DURATION_P" to "P",
        "DURATION_NUMBER" to "1",
        "DURATION_UNIT" to "D",
        "DURATION_T" to "T",
        "DURATION_NUMBER" to "2",
        "DURATION_UNIT" to "H",
        "DURATION_NUMBER" to "30",
        "DURATION_UNIT" to "M"
    )
}

@Test
fun testBinaryBase64() {
    assertTokens(
        "b\"SGVsbG8=\"",
        "BINARY_PREFIX" to "b",
        "BINARY_OPEN" to "\"",
        "BINARY_CONTENT" to "SGVsbG8=",
        "BINARY_CLOSE" to "\""
    )
}

@Test
fun testBinaryHex() {
    assertTokens(
        "x\"48656C6C6F\"",
        "BINARY_PREFIX" to "x",
        "BINARY_OPEN" to "\"",
        "BINARY_CONTENT" to "48656C6C6F",
        "BINARY_CLOSE" to "\""
    )
}

@Test
fun testBinaryInvalidChars() {
    // Invalid chars in base64
    assertTokens(
        "b\"SGVs!G8=\"",
        "BINARY_PREFIX" to "b",
        "BINARY_OPEN" to "\"",
        "BINARY_CONTENT" to "SGVs",
        "BINARY_INVALID_CHAR" to "!",
        "BINARY_CONTENT" to "G8=",
        "BINARY_CLOSE" to "\""
    )
    // Invalid chars in hex (g is not a hex digit)
    assertTokens(
        "x\"48656g6C\"",
        "BINARY_PREFIX" to "x",
        "BINARY_OPEN" to "\"",
        "BINARY_CONTENT" to "48656",
        "BINARY_INVALID_CHAR" to "g",
        "BINARY_CONTENT" to "6C",
        "BINARY_CLOSE" to "\""
    )
}

@Test
fun testMapKeyword() {
    assertTokens(
        "Map{",
        "MAP_KEYWORD" to "Map",
        "LBRACE" to "{"
    )
    // Without brace, "Map" is not a MAP_KEYWORD
    assertTokens("Map", "BAD_CHARACTER" to "M", "BAD_CHARACTER" to "a", "BAD_CHARACTER" to "p")
}

@Test
fun testSetKeyword() {
    assertTokens(
        "Set{",
        "SET_KEYWORD" to "Set",
        "LBRACE" to "{"
    )
}

@Test
fun testRegExpBasic() {
    assertTokens(
        "/hello/",
        "REGEXP_OPEN" to "/",
        "REGEXP_CONTENT" to "hello",
        "REGEXP_CLOSE" to "/"
    )
}

@Test
fun testRegExpWithFlags() {
    assertTokens(
        "/hello/gi",
        "REGEXP_OPEN" to "/",
        "REGEXP_CONTENT" to "hello",
        "REGEXP_CLOSE" to "/",
        "REGEXP_FLAGS" to "gi"
    )
}

@Test
fun testRegExpQuantifiers() {
    assertTokens(
        "/a+b*c?/",
        "REGEXP_OPEN" to "/",
        "REGEXP_CONTENT" to "a",
        "REGEXP_QUANTIFIER" to "+",
        "REGEXP_CONTENT" to "b",
        "REGEXP_QUANTIFIER" to "*",
        "REGEXP_CONTENT" to "c",
        "REGEXP_QUANTIFIER" to "?",
        "REGEXP_CLOSE" to "/"
    )
}

@Test
fun testRegExpCharClass() {
    assertTokens(
        "/[a-z]+/",
        "REGEXP_OPEN" to "/",
        "REGEXP_CHAR_CLASS_OPEN" to "[",
        "REGEXP_CONTENT" to "a",
        "REGEXP_RANGE" to "-",
        "REGEXP_CONTENT" to "z",
        "REGEXP_CHAR_CLASS_CLOSE" to "]",
        "REGEXP_QUANTIFIER" to "+",
        "REGEXP_CLOSE" to "/"
    )
}

@Test
fun testRegExpNegatedCharClass() {
    assertTokens(
        "/[^0-9]/",
        "REGEXP_OPEN" to "/",
        "REGEXP_CHAR_CLASS_OPEN" to "[",
        "REGEXP_NEGATION" to "^",
        "REGEXP_CONTENT" to "0",
        "REGEXP_RANGE" to "-",
        "REGEXP_CONTENT" to "9",
        "REGEXP_CHAR_CLASS_CLOSE" to "]",
        "REGEXP_CLOSE" to "/"
    )
}

@Test
fun testRegExpGroups() {
    assertTokens(
        "/(abc)/",
        "REGEXP_OPEN" to "/",
        "REGEXP_GROUP_OPEN" to "(",
        "REGEXP_CONTENT" to "abc",
        "REGEXP_GROUP_CLOSE" to ")",
        "REGEXP_CLOSE" to "/"
    )
}

@Test
fun testRegExpLookahead() {
    assertTokens(
        "/foo(?=bar)/",
        "REGEXP_OPEN" to "/",
        "REGEXP_CONTENT" to "foo",
        "REGEXP_LOOKAROUND" to "(?=",
        "REGEXP_GROUP_OPEN" to "(",  // Note: implementation may vary
        "REGEXP_CONTENT" to "bar",
        "REGEXP_GROUP_CLOSE" to ")",
        "REGEXP_CLOSE" to "/"
    )
}

@Test
fun testRegExpBackreference() {
    assertTokens(
        "/(.)\\1/",
        "REGEXP_OPEN" to "/",
        "REGEXP_GROUP_OPEN" to "(",
        "REGEXP_DOT" to ".",
        "REGEXP_GROUP_CLOSE" to ")",
        "REGEXP_BACKREFERENCE" to "\\1",
        "REGEXP_CLOSE" to "/"
    )
}

@Test
fun testStructural() {
    assertTokens("[]", "LBRACKET" to "[", "RBRACKET" to "]")
    assertTokens("()", "LPAREN" to "(", "RPAREN" to ")")
    assertTokens(",", "COMMA" to ",")
    assertTokens("=>", "ARROW" to "=>")
}
```

## Acceptance Criteria
- [ ] All tests pass with `./gradlew test --tests "*RdnLexerTest*"`
- [ ] BIGINT rule correctly takes priority over INTEGER for inputs like `42n`
- [ ] Float rule correctly takes priority over INTEGER for inputs like `3.14`
- [ ] String escape sequences produce `STRING_ESCAPE`, invalid ones produce `STRING_INVALID_ESCAPE`
- [ ] RegExp character class negation (`^`) only produces `REGEXP_NEGATION` when inside `[...]`
- [ ] Binary base64 and hex invalid characters produce `BINARY_INVALID_CHAR` tokens
- [ ] `Map` without following `{` does not produce `MAP_KEYWORD`
- [ ] DateTime, TimeOnly, and Duration produce correct token sequences
- [ ] Named group `(?<name>...)` produces `REGEXP_NAMED_GROUP`

## Dependencies
- Depends on: task-003, task-004
- Blocks: None (tests are standalone)
