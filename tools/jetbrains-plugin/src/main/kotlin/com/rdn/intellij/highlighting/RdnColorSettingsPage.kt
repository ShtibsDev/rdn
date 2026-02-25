package com.rdn.intellij.highlighting

import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.options.colors.AttributesDescriptor
import com.intellij.openapi.options.colors.ColorDescriptor
import com.intellij.openapi.options.colors.ColorSettingsPage
import com.rdn.intellij.RdnIcons
import javax.swing.Icon

class RdnColorSettingsPage : ColorSettingsPage {

    override fun getDisplayName(): String = "RDN"

    override fun getIcon(): Icon = RdnIcons.FILE

    override fun getHighlighter(): SyntaxHighlighter = RdnSyntaxHighlighter()

    override fun getAttributeDescriptors(): Array<AttributesDescriptor> = DESCRIPTORS

    override fun getColorDescriptors(): Array<ColorDescriptor> = ColorDescriptor.EMPTY_ARRAY

    override fun getAdditionalHighlightingTagToDescriptorMap(): Map<String, TextAttributesKey> = TAGS

    override fun getDemoText(): String = """
{
  "created": @2024-01-15T12:30:00.000Z,
  "epoch": @1705317000,
  "duration": @P1Y2M3DT4H5M6S,
  "timeOnly": @T14:30:00.000,
  "name": "Hello\nWorld",
  "invalid_escape": "\q",
  "count": 42,
  "pi": 3.14159,
  "big": 9007199254740993n,
  "flag": true,
  "nothing": null,
  "nan": NaN,
  "inf": Infinity,
  "neg_inf": -Infinity,
  "pattern": /^hello\s+\w+$/gim,
  "data": base64("SGVsbG8gV29ybGQ="),
  "tags": ["rdn", "json", "superset"],
  "nested": {
    "x": 1,
    "y": 2
  },
  "coords": Map{
    "lat" => 51.5,
    "lng" => -0.1
  },
  "primes": Set{2, 3, 5, 7, 11}
}
""".trimIndent()

    companion object {
        private val DESCRIPTORS: Array<AttributesDescriptor> = arrayOf(
            // Keywords / literals
            AttributesDescriptor("Keywords//null, true, false, NaN, Infinity", RdnColors.KEYWORD),
            // Numbers
            AttributesDescriptor("Numbers//Integer and float", RdnColors.NUMBER),
            AttributesDescriptor("Numbers//BigInt (n suffix)", RdnColors.BIGINT),
            // Strings
            AttributesDescriptor("Strings//String text", RdnColors.STRING),
            AttributesDescriptor("Strings//Valid escape sequence", RdnColors.STRING_ESCAPE),
            AttributesDescriptor("Strings//Invalid escape sequence", RdnColors.STRING_INVALID_ESCAPE),
            // Object keys
            AttributesDescriptor("Object//Key", RdnColors.OBJECT_KEY),
            // Date/Time
            AttributesDescriptor("Date and Time//@ sign", RdnColors.AT_SIGN),
            AttributesDescriptor("Date and Time//Date part (YYYY-MM-DD)", RdnColors.DATE_PART),
            AttributesDescriptor("Date and Time//Time part (HH:MM:SS)", RdnColors.TIME_PART),
            AttributesDescriptor("Date and Time//Milliseconds part (.mmm)", RdnColors.MILLIS_PART),
            AttributesDescriptor("Date and Time//Timezone offset", RdnColors.TIMEZONE),
            AttributesDescriptor("Date and Time//Unix timestamp", RdnColors.UNIX_TIMESTAMP),
            // Duration
            AttributesDescriptor("Duration//P designator", RdnColors.DURATION_P),
            AttributesDescriptor("Duration//Number", RdnColors.DURATION_NUMBER),
            AttributesDescriptor("Duration//Unit designator (Y, M, D, H, S)", RdnColors.DURATION_UNIT),
            AttributesDescriptor("Duration//T designator", RdnColors.DURATION_T),
            // Binary
            AttributesDescriptor("Binary//Prefix (base64, hex)", RdnColors.BINARY_PREFIX),
            AttributesDescriptor("Binary//Content", RdnColors.BINARY_CONTENT),
            AttributesDescriptor("Binary//Invalid character", RdnColors.BINARY_INVALID_CHAR),
            // Map / Set
            AttributesDescriptor("Collections//Map keyword", RdnColors.MAP_KEYWORD),
            AttributesDescriptor("Collections//Set keyword", RdnColors.SET_KEYWORD),
            // RegExp
            AttributesDescriptor("RegExp//Body text", RdnColors.REGEXP_BODY),
            AttributesDescriptor("RegExp//Escape sequence", RdnColors.REGEXP_ESCAPE),
            AttributesDescriptor("RegExp//Character class escape (\\d, \\w, \\s)", RdnColors.REGEXP_CHAR_CLASS_ESCAPE),
            AttributesDescriptor("RegExp//Quantifier (+, *, ?, {n,m})", RdnColors.REGEXP_QUANTIFIER),
            AttributesDescriptor("RegExp//Anchor (^, $)", RdnColors.REGEXP_ANCHOR),
            AttributesDescriptor("RegExp//Alternation (|)", RdnColors.REGEXP_ALTERNATION),
            AttributesDescriptor("RegExp//Dot (.)", RdnColors.REGEXP_DOT),
            AttributesDescriptor("RegExp//Group parentheses", RdnColors.REGEXP_GROUP),
            AttributesDescriptor("RegExp//Special group (lookahead, named, non-capturing)", RdnColors.REGEXP_SPECIAL),
            AttributesDescriptor("RegExp//Character class brackets [...]", RdnColors.REGEXP_CHAR_CLASS),
            AttributesDescriptor("RegExp//Flags (g, i, m, s, u, y)", RdnColors.REGEXP_FLAGS),
            // Structural punctuation
            AttributesDescriptor("Punctuation//Braces { }", RdnColors.BRACES),
            AttributesDescriptor("Punctuation//Brackets [ ]", RdnColors.BRACKETS),
            AttributesDescriptor("Punctuation//Parentheses ( )", RdnColors.PARENS),
            AttributesDescriptor("Punctuation//Comma", RdnColors.COMMA),
            AttributesDescriptor("Punctuation//Colon", RdnColors.COLON),
            AttributesDescriptor("Punctuation//Arrow (=>)", RdnColors.ARROW),
            // Bad character
            AttributesDescriptor("Bad character", RdnColors.BAD_CHARACTER),
        )

        private val TAGS: Map<String, TextAttributesKey> = mapOf(
            "keyword" to RdnColors.KEYWORD,
            "number" to RdnColors.NUMBER,
            "bigint" to RdnColors.BIGINT,
            "string" to RdnColors.STRING,
            "string_escape" to RdnColors.STRING_ESCAPE,
            "string_invalid_escape" to RdnColors.STRING_INVALID_ESCAPE,
            "object_key" to RdnColors.OBJECT_KEY,
            "at_sign" to RdnColors.AT_SIGN,
            "date_part" to RdnColors.DATE_PART,
            "time_part" to RdnColors.TIME_PART,
            "millis_part" to RdnColors.MILLIS_PART,
            "timezone" to RdnColors.TIMEZONE,
            "unix_timestamp" to RdnColors.UNIX_TIMESTAMP,
            "duration_p" to RdnColors.DURATION_P,
            "duration_number" to RdnColors.DURATION_NUMBER,
            "duration_unit" to RdnColors.DURATION_UNIT,
            "duration_t" to RdnColors.DURATION_T,
            "binary_prefix" to RdnColors.BINARY_PREFIX,
            "binary_content" to RdnColors.BINARY_CONTENT,
            "binary_invalid" to RdnColors.BINARY_INVALID_CHAR,
            "map_keyword" to RdnColors.MAP_KEYWORD,
            "set_keyword" to RdnColors.SET_KEYWORD,
            "regexp_body" to RdnColors.REGEXP_BODY,
            "regexp_escape" to RdnColors.REGEXP_ESCAPE,
            "regexp_cc_escape" to RdnColors.REGEXP_CHAR_CLASS_ESCAPE,
            "regexp_quantifier" to RdnColors.REGEXP_QUANTIFIER,
            "regexp_anchor" to RdnColors.REGEXP_ANCHOR,
            "regexp_alternation" to RdnColors.REGEXP_ALTERNATION,
            "regexp_dot" to RdnColors.REGEXP_DOT,
            "regexp_group" to RdnColors.REGEXP_GROUP,
            "regexp_special" to RdnColors.REGEXP_SPECIAL,
            "regexp_char_class" to RdnColors.REGEXP_CHAR_CLASS,
            "regexp_flags" to RdnColors.REGEXP_FLAGS,
            "braces" to RdnColors.BRACES,
            "brackets" to RdnColors.BRACKETS,
            "parens" to RdnColors.PARENS,
            "comma" to RdnColors.COMMA,
            "colon" to RdnColors.COLON,
            "arrow" to RdnColors.ARROW,
            "bad_char" to RdnColors.BAD_CHARACTER,
        )
    }
}
