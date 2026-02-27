package com.rdn.intellij.lexer

import com.intellij.psi.tree.IElementType
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/**
 * Unit tests for the RDN JFlex lexer ([RdnFlexLexer]).
 *
 * Each test calls [assertTokens], which drives the raw [RdnFlexLexer] (the generated class that
 * lives in `src/main/gen/`) directly — no IntelliJ platform test infrastructure required.
 *
 * Every expected token is expressed as a `Pair<text, tokenTypeName>`, e.g.:
 *   `"{" to "LBRACE"`.
 *
 * The helper collects the full token stream (text + type debug-name pairs) and compares it
 * against the expected list with a single assertEquals so failures show a clean diff.
 */
class RdnLexerTest {

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    /**
     * Tokenizes [input] with a fresh [RdnFlexLexer] and asserts that the resulting token
     * stream matches [expected].
     *
     * Each element of [expected] is `Pair(tokenText, tokenTypeName)`.
     * WHITE_SPACE tokens are included so tests can be explicit about spacing.
     *
     * Token text is obtained via [RdnFlexLexer.yytext], which is the standard JFlex method
     * that returns the matched text as a [CharSequence].
     */
    private fun assertTokens(input: String, vararg expected: Pair<String, String>) {
        val lexer = RdnFlexLexer(null)
        lexer.reset(input, 0, input.length, RdnFlexLexer.YYINITIAL)

        val actual = mutableListOf<Pair<String, String>>()
        while (true) {
            val tokenType: IElementType = lexer.advance() ?: break
            val tokenText = lexer.yytext().toString()
            actual.add(tokenText to tokenType.toString())
        }

        assertEquals(expected.toList(), actual)
    }

    // -------------------------------------------------------------------------
    // 1. Basic object: {"key": 42}
    // -------------------------------------------------------------------------

    @Test
    fun `basic object with string key and integer value`() {
        assertTokens(
            """{"key": 42}""",
            "{" to "LBRACE",
            "\"" to "STRING_OPEN",
            "key" to "STRING_CONTENT",
            "\"" to "STRING_CLOSE",
            ":" to "COLON",
            " " to "WHITE_SPACE",
            "42" to "INTEGER",
            "}" to "RBRACE",
        )
    }

    // -------------------------------------------------------------------------
    // 2. BigInt and plain integers
    // -------------------------------------------------------------------------

    @Test
    fun `bigint positive`() {
        assertTokens("42n", "42n" to "BIGINT")
    }

    @Test
    fun `bigint negative`() {
        assertTokens("-999n", "-999n" to "BIGINT")
    }

    @Test
    fun `plain integer`() {
        assertTokens("42", "42" to "INTEGER")
    }

    @Test
    fun `integer zero`() {
        assertTokens("0", "0" to "INTEGER")
    }

    @Test
    fun `negative integer`() {
        assertTokens("-1", "-1" to "INTEGER")
    }

    // -------------------------------------------------------------------------
    // 3. Floats
    // -------------------------------------------------------------------------

    @Test
    fun `float with fraction`() {
        assertTokens("3.14", "3.14" to "FLOAT")
    }

    @Test
    fun `float with exponent only`() {
        assertTokens("1e10", "1e10" to "FLOAT")
    }

    @Test
    fun `float negative with fraction and negative exponent`() {
        assertTokens("-2.5e-3", "-2.5e-3" to "FLOAT")
    }

    @Test
    fun `float fraction and uppercase exponent`() {
        assertTokens("6.022E23", "6.022E23" to "FLOAT")
    }

    // -------------------------------------------------------------------------
    // 4. Special numbers
    // -------------------------------------------------------------------------

    @Test
    fun `NaN keyword`() {
        assertTokens("NaN", "NaN" to "NAN")
    }

    @Test
    fun `Infinity keyword`() {
        assertTokens("Infinity", "Infinity" to "INFINITY")
    }

    @Test
    fun `negative Infinity keyword`() {
        assertTokens("-Infinity", "-Infinity" to "NEG_INFINITY")
    }

    // -------------------------------------------------------------------------
    // 5. Keywords: null, true, false
    // -------------------------------------------------------------------------

    @Test
    fun `null keyword`() {
        assertTokens("null", "null" to "NULL")
    }

    @Test
    fun `true keyword`() {
        assertTokens("true", "true" to "TRUE")
    }

    @Test
    fun `false keyword`() {
        assertTokens("false", "false" to "FALSE")
    }

    // -------------------------------------------------------------------------
    // 6. String content and valid escapes
    // -------------------------------------------------------------------------

    @Test
    fun `string with newline escape`() {
        // "hello\nworld" — the \n is a two-character escape sequence in the source
        assertTokens(
            "\"hello\\nworld\"",
            "\"" to "STRING_OPEN",
            "hello" to "STRING_CONTENT",
            "\\n" to "STRING_ESCAPE",
            "world" to "STRING_CONTENT",
            "\"" to "STRING_CLOSE",
        )
    }

    @Test
    fun `string with unicode escape`() {
        assertTokens(
            "\"\\u0041\"",
            "\"" to "STRING_OPEN",
            "\\u0041" to "STRING_ESCAPE",
            "\"" to "STRING_CLOSE",
        )
    }

    @Test
    fun `string with all single-char escapes`() {
        // \" \\ \/ \b \f \n \r \t
        val input = "\"\\\"\\\\\\/\\b\\f\\n\\r\\t\""
        assertTokens(
            input,
            "\"" to "STRING_OPEN",
            "\\\"" to "STRING_ESCAPE",
            "\\\\" to "STRING_ESCAPE",
            "\\/" to "STRING_ESCAPE",
            "\\b" to "STRING_ESCAPE",
            "\\f" to "STRING_ESCAPE",
            "\\n" to "STRING_ESCAPE",
            "\\r" to "STRING_ESCAPE",
            "\\t" to "STRING_ESCAPE",
            "\"" to "STRING_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 7. Invalid string escapes
    // -------------------------------------------------------------------------

    @Test
    fun `invalid string escape backslash-q`() {
        assertTokens(
            "\"\\q\"",
            "\"" to "STRING_OPEN",
            "\\q" to "STRING_INVALID_ESCAPE",
            "\"" to "STRING_CLOSE",
        )
    }

    @Test
    fun `invalid string escape backslash-j`() {
        assertTokens(
            "\"\\j\"",
            "\"" to "STRING_OPEN",
            "\\j" to "STRING_INVALID_ESCAPE",
            "\"" to "STRING_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 8. DateTime
    // -------------------------------------------------------------------------

    @Test
    fun `date only`() {
        assertTokens(
            "@2024-01-15",
            "@" to "AT_SIGN",
            "2024-01-15" to "DATE_PART",
        )
    }

    @Test
    fun `datetime with millis and timezone`() {
        assertTokens(
            "@2024-01-15T10:30:00.000Z",
            "@" to "AT_SIGN",
            "2024-01-15" to "DATE_PART",
            "T" to "TIME_SEPARATOR",
            "10:30:00" to "TIME_PART",
            ".000" to "MILLIS_PART",
            "Z" to "TIMEZONE",
        )
    }

    @Test
    fun `datetime without millis with timezone`() {
        assertTokens(
            "@2024-06-01T00:00:00Z",
            "@" to "AT_SIGN",
            "2024-06-01" to "DATE_PART",
            "T" to "TIME_SEPARATOR",
            "00:00:00" to "TIME_PART",
            "Z" to "TIMEZONE",
        )
    }

    // -------------------------------------------------------------------------
    // 9. Unix timestamp
    // -------------------------------------------------------------------------

    @Test
    fun `unix timestamp`() {
        assertTokens(
            "@1705276800",
            "@" to "AT_SIGN",
            "1705276800" to "UNIX_TIMESTAMP",
        )
    }

    @Test
    fun `unix timestamp zero`() {
        assertTokens(
            "@0",
            "@" to "AT_SIGN",
            "0" to "UNIX_TIMESTAMP",
        )
    }

    // -------------------------------------------------------------------------
    // 10. TimeOnly: @HH:MM:SS
    // -------------------------------------------------------------------------

    @Test
    fun `time only`() {
        assertTokens(
            "@14:30:00",
            "@" to "AT_SIGN",
            "14:30:00" to "TIME_PART",
        )
    }

    @Test
    fun `time only midnight`() {
        assertTokens(
            "@00:00:00",
            "@" to "AT_SIGN",
            "00:00:00" to "TIME_PART",
        )
    }

    // -------------------------------------------------------------------------
    // 11. Duration
    // -------------------------------------------------------------------------

    @Test
    fun `duration with years months days`() {
        assertTokens(
            "@P1Y2M3D",
            "@" to "AT_SIGN",
            "P" to "DURATION_P",
            "1" to "DURATION_NUMBER",
            "Y" to "DURATION_UNIT",
            "2" to "DURATION_NUMBER",
            "M" to "DURATION_UNIT",
            "3" to "DURATION_NUMBER",
            "D" to "DURATION_UNIT",
        )
    }

    @Test
    fun `duration with days and time components`() {
        assertTokens(
            "@P1DT2H30M",
            "@" to "AT_SIGN",
            "P" to "DURATION_P",
            "1" to "DURATION_NUMBER",
            "D" to "DURATION_UNIT",
            "T" to "DURATION_T",
            "2" to "DURATION_NUMBER",
            "H" to "DURATION_UNIT",
            "30" to "DURATION_NUMBER",
            "M" to "DURATION_UNIT",
        )
    }

    @Test
    fun `duration with decimal fraction`() {
        assertTokens(
            "@P1.5Y",
            "@" to "AT_SIGN",
            "P" to "DURATION_P",
            "1.5" to "DURATION_NUMBER",
            "Y" to "DURATION_UNIT",
        )
    }

    @Test
    fun `duration time only`() {
        assertTokens(
            "@PT30S",
            "@" to "AT_SIGN",
            "P" to "DURATION_P",
            "T" to "DURATION_T",
            "30" to "DURATION_NUMBER",
            "S" to "DURATION_UNIT",
        )
    }

    // -------------------------------------------------------------------------
    // 12. Binary base64
    // -------------------------------------------------------------------------

    @Test
    fun `binary base64`() {
        assertTokens(
            "b\"SGVsbG8=\"",
            "b" to "BINARY_PREFIX",
            "\"" to "BINARY_OPEN",
            "SGVsbG8=" to "BINARY_CONTENT",
            "\"" to "BINARY_CLOSE",
        )
    }

    @Test
    fun `binary base64 empty`() {
        assertTokens(
            "b\"\"",
            "b" to "BINARY_PREFIX",
            "\"" to "BINARY_OPEN",
            "\"" to "BINARY_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 13. Binary hex
    // -------------------------------------------------------------------------

    @Test
    fun `binary hex`() {
        assertTokens(
            "x\"48656C6C6F\"",
            "x" to "BINARY_PREFIX",
            "\"" to "BINARY_OPEN",
            "48656C6C6F" to "BINARY_CONTENT",
            "\"" to "BINARY_CLOSE",
        )
    }

    @Test
    fun `binary hex lowercase`() {
        assertTokens(
            "x\"deadbeef\"",
            "x" to "BINARY_PREFIX",
            "\"" to "BINARY_OPEN",
            "deadbeef" to "BINARY_CONTENT",
            "\"" to "BINARY_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 14. Binary invalid chars
    // -------------------------------------------------------------------------

    @Test
    fun `binary base64 invalid characters`() {
        // '!' is not a valid base64 character — lexer emits BINARY_INVALID_CHAR for the bad run,
        // then BINARY_CONTENT for the trailing valid chars.
        assertTokens(
            "b\"!SGVs\"",
            "b" to "BINARY_PREFIX",
            "\"" to "BINARY_OPEN",
            "!" to "BINARY_INVALID_CHAR",
            "SGVs" to "BINARY_CONTENT",
            "\"" to "BINARY_CLOSE",
        )
    }

    @Test
    fun `binary hex invalid characters`() {
        // 'G' is not a valid hex digit
        assertTokens(
            "x\"GG48\"",
            "x" to "BINARY_PREFIX",
            "\"" to "BINARY_OPEN",
            "GG" to "BINARY_INVALID_CHAR",
            "48" to "BINARY_CONTENT",
            "\"" to "BINARY_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 15. Map keyword
    // -------------------------------------------------------------------------

    @Test
    fun `map keyword followed by brace`() {
        assertTokens(
            "Map{",
            "Map" to "MAP_KEYWORD",
            "{" to "LBRACE",
        )
    }

    @Test
    fun `map keyword with content`() {
        assertTokens(
            "Map{}",
            "Map" to "MAP_KEYWORD",
            "{" to "LBRACE",
            "}" to "RBRACE",
        )
    }

    // -------------------------------------------------------------------------
    // 16. Set keyword
    // -------------------------------------------------------------------------

    @Test
    fun `set keyword followed by brace`() {
        assertTokens(
            "Set{",
            "Set" to "SET_KEYWORD",
            "{" to "LBRACE",
        )
    }

    @Test
    fun `set keyword with content`() {
        assertTokens(
            "Set{}",
            "Set" to "SET_KEYWORD",
            "{" to "LBRACE",
            "}" to "RBRACE",
        )
    }

    // -------------------------------------------------------------------------
    // 17. RegExp basic
    // -------------------------------------------------------------------------

    @Test
    fun `regexp basic`() {
        assertTokens(
            "/hello/",
            "/" to "REGEXP_OPEN",
            "hello" to "REGEXP_CONTENT",
            "/" to "REGEXP_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 18. RegExp with flags
    // -------------------------------------------------------------------------

    @Test
    fun `regexp with flags gi`() {
        assertTokens(
            "/hello/gi",
            "/" to "REGEXP_OPEN",
            "hello" to "REGEXP_CONTENT",
            "/" to "REGEXP_CLOSE",
            "gi" to "REGEXP_FLAGS",
        )
    }

    @Test
    fun `regexp with all common flags`() {
        assertTokens(
            "/pattern/gimsuy",
            "/" to "REGEXP_OPEN",
            "pattern" to "REGEXP_CONTENT",
            "/" to "REGEXP_CLOSE",
            "gimsuy" to "REGEXP_FLAGS",
        )
    }

    // -------------------------------------------------------------------------
    // 19. RegExp quantifiers
    // -------------------------------------------------------------------------

    @Test
    fun `regexp quantifiers plus star question`() {
        assertTokens(
            "/a+b*c?/",
            "/" to "REGEXP_OPEN",
            "a" to "REGEXP_CONTENT",
            "+" to "REGEXP_QUANTIFIER",
            "b" to "REGEXP_CONTENT",
            "*" to "REGEXP_QUANTIFIER",
            "c" to "REGEXP_CONTENT",
            "?" to "REGEXP_QUANTIFIER",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp numeric quantifier`() {
        assertTokens(
            "/a{2,4}/",
            "/" to "REGEXP_OPEN",
            "a" to "REGEXP_CONTENT",
            "{2,4}" to "REGEXP_QUANTIFIER",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp lazy quantifier`() {
        assertTokens(
            "/a+?/",
            "/" to "REGEXP_OPEN",
            "a" to "REGEXP_CONTENT",
            "+?" to "REGEXP_QUANTIFIER",
            "/" to "REGEXP_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 20. RegExp char class
    // -------------------------------------------------------------------------

    @Test
    fun `regexp character class range`() {
        assertTokens(
            "/[a-z]+/",
            "/" to "REGEXP_OPEN",
            "[" to "REGEXP_CHAR_CLASS_OPEN",
            "a" to "REGEXP_CONTENT",
            "-" to "REGEXP_RANGE",
            "z" to "REGEXP_CONTENT",
            "]" to "REGEXP_CHAR_CLASS_CLOSE",
            "+" to "REGEXP_QUANTIFIER",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp character class letters`() {
        assertTokens(
            "/[abc]/",
            "/" to "REGEXP_OPEN",
            "[" to "REGEXP_CHAR_CLASS_OPEN",
            "abc" to "REGEXP_CONTENT",
            "]" to "REGEXP_CHAR_CLASS_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 21. RegExp negated char class
    // -------------------------------------------------------------------------

    @Test
    fun `regexp negated character class`() {
        assertTokens(
            "/[^0-9]/",
            "/" to "REGEXP_OPEN",
            "[" to "REGEXP_CHAR_CLASS_OPEN",
            "^" to "REGEXP_NEGATION",
            "0" to "REGEXP_CONTENT",
            "-" to "REGEXP_RANGE",
            "9" to "REGEXP_CONTENT",
            "]" to "REGEXP_CHAR_CLASS_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 22. RegExp groups
    // -------------------------------------------------------------------------

    @Test
    fun `regexp capturing group`() {
        assertTokens(
            "/(abc)/",
            "/" to "REGEXP_OPEN",
            "(" to "REGEXP_GROUP_OPEN",
            "abc" to "REGEXP_CONTENT",
            ")" to "REGEXP_GROUP_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp non-capturing group`() {
        assertTokens(
            "/(?:abc)/",
            "/" to "REGEXP_OPEN",
            "(?:" to "REGEXP_NON_CAPTURING",
            "abc" to "REGEXP_CONTENT",
            ")" to "REGEXP_GROUP_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp named group`() {
        assertTokens(
            "/(?<name>abc)/",
            "/" to "REGEXP_OPEN",
            "(?<name>" to "REGEXP_NAMED_GROUP",
            "abc" to "REGEXP_CONTENT",
            ")" to "REGEXP_GROUP_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp lookahead`() {
        assertTokens(
            "/a(?=b)/",
            "/" to "REGEXP_OPEN",
            "a" to "REGEXP_CONTENT",
            "(?=" to "REGEXP_LOOKAROUND",
            "b" to "REGEXP_CONTENT",
            ")" to "REGEXP_GROUP_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp negative lookahead`() {
        assertTokens(
            "/a(?!b)/",
            "/" to "REGEXP_OPEN",
            "a" to "REGEXP_CONTENT",
            "(?!" to "REGEXP_LOOKAROUND",
            "b" to "REGEXP_CONTENT",
            ")" to "REGEXP_GROUP_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp lookbehind`() {
        assertTokens(
            "/(?<=a)b/",
            "/" to "REGEXP_OPEN",
            "(?<=" to "REGEXP_LOOKAROUND",
            "a" to "REGEXP_CONTENT",
            ")" to "REGEXP_GROUP_CLOSE",
            "b" to "REGEXP_CONTENT",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp negative lookbehind`() {
        assertTokens(
            "/(?<!a)b/",
            "/" to "REGEXP_OPEN",
            "(?<!" to "REGEXP_LOOKAROUND",
            "a" to "REGEXP_CONTENT",
            ")" to "REGEXP_GROUP_CLOSE",
            "b" to "REGEXP_CONTENT",
            "/" to "REGEXP_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 23. RegExp backreference
    // -------------------------------------------------------------------------

    @Test
    fun `regexp numeric backreference`() {
        // /(.)\\1/ — the raw source string has a literal backslash-1
        assertTokens(
            "/(.)\\1/",
            "/" to "REGEXP_OPEN",
            "(" to "REGEXP_GROUP_OPEN",
            "." to "REGEXP_DOT",
            ")" to "REGEXP_GROUP_CLOSE",
            "\\1" to "REGEXP_BACKREFERENCE",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp named backreference`() {
        assertTokens(
            "/(?<word>\\w+)\\k<word>/",
            "/" to "REGEXP_OPEN",
            "(?<word>" to "REGEXP_NAMED_GROUP",
            "\\w" to "REGEXP_CHAR_CLASS_ESCAPE",
            "+" to "REGEXP_QUANTIFIER",
            ")" to "REGEXP_GROUP_CLOSE",
            "\\k<word>" to "REGEXP_BACKREFERENCE",
            "/" to "REGEXP_CLOSE",
        )
    }

    // -------------------------------------------------------------------------
    // 24. Structural tokens
    // -------------------------------------------------------------------------

    @Test
    fun `empty array`() {
        assertTokens(
            "[]",
            "[" to "LBRACKET",
            "]" to "RBRACKET",
        )
    }

    @Test
    fun `empty tuple`() {
        assertTokens(
            "()",
            "(" to "LPAREN",
            ")" to "RPAREN",
        )
    }

    @Test
    fun `comma separator`() {
        assertTokens(
            "1,2",
            "1" to "INTEGER",
            "," to "COMMA",
            "2" to "INTEGER",
        )
    }

    @Test
    fun `arrow separator`() {
        assertTokens(
            "=>",
            "=>" to "ARROW",
        )
    }

    @Test
    fun `map entry with arrow`() {
        assertTokens(
            "\"a\"=>1",
            "\"" to "STRING_OPEN",
            "a" to "STRING_CONTENT",
            "\"" to "STRING_CLOSE",
            "=>" to "ARROW",
            "1" to "INTEGER",
        )
    }

    // -------------------------------------------------------------------------
    // Additional edge-case tests
    // -------------------------------------------------------------------------

    @Test
    fun `whitespace is preserved`() {
        assertTokens(
            "1 2",
            "1" to "INTEGER",
            " " to "WHITE_SPACE",
            "2" to "INTEGER",
        )
    }

    @Test
    fun `multiple whitespace characters`() {
        assertTokens(
            "null\t true",
            "null" to "NULL",
            "\t " to "WHITE_SPACE",
            "true" to "TRUE",
        )
    }

    @Test
    fun `regexp anchors`() {
        assertTokens(
            "/^abc$/",
            "/" to "REGEXP_OPEN",
            "^" to "REGEXP_ANCHOR",
            "abc" to "REGEXP_CONTENT",
            "$" to "REGEXP_ANCHOR",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp alternation`() {
        assertTokens(
            "/a|b/",
            "/" to "REGEXP_OPEN",
            "a" to "REGEXP_CONTENT",
            "|" to "REGEXP_ALTERNATION",
            "b" to "REGEXP_CONTENT",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp dot`() {
        assertTokens(
            "/a.b/",
            "/" to "REGEXP_OPEN",
            "a" to "REGEXP_CONTENT",
            "." to "REGEXP_DOT",
            "b" to "REGEXP_CONTENT",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp char class escape in pattern`() {
        assertTokens(
            "/\\d+/",
            "/" to "REGEXP_OPEN",
            "\\d" to "REGEXP_CHAR_CLASS_ESCAPE",
            "+" to "REGEXP_QUANTIFIER",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `regexp char class escape inside char class`() {
        assertTokens(
            "/[\\d]/",
            "/" to "REGEXP_OPEN",
            "[" to "REGEXP_CHAR_CLASS_OPEN",
            "\\d" to "REGEXP_CHAR_CLASS_ESCAPE",
            "]" to "REGEXP_CHAR_CLASS_CLOSE",
            "/" to "REGEXP_CLOSE",
        )
    }

    @Test
    fun `bad character in top level`() {
        // An unrecognised character at the top level should produce BAD_CHARACTER.
        // We use a backtick which matches none of the YYINITIAL patterns.
        assertTokens(
            "`",
            "`" to "BAD_CHARACTER",
        )
    }

    @Test
    fun `datetime with single-digit millis`() {
        assertTokens(
            "@2024-03-10T12:00:00.5Z",
            "@" to "AT_SIGN",
            "2024-03-10" to "DATE_PART",
            "T" to "TIME_SEPARATOR",
            "12:00:00" to "TIME_PART",
            ".5" to "MILLIS_PART",
            "Z" to "TIMEZONE",
        )
    }

    @Test
    fun `full array literal`() {
        assertTokens(
            "[1, true, null]",
            "[" to "LBRACKET",
            "1" to "INTEGER",
            "," to "COMMA",
            " " to "WHITE_SPACE",
            "true" to "TRUE",
            "," to "COMMA",
            " " to "WHITE_SPACE",
            "null" to "NULL",
            "]" to "RBRACKET",
        )
    }

    @Test
    fun `nested object`() {
        assertTokens(
            """{"a":{"b":1}}""",
            "{" to "LBRACE",
            "\"" to "STRING_OPEN",
            "a" to "STRING_CONTENT",
            "\"" to "STRING_CLOSE",
            ":" to "COLON",
            "{" to "LBRACE",
            "\"" to "STRING_OPEN",
            "b" to "STRING_CONTENT",
            "\"" to "STRING_CLOSE",
            ":" to "COLON",
            "1" to "INTEGER",
            "}" to "RBRACE",
            "}" to "RBRACE",
        )
    }
}
