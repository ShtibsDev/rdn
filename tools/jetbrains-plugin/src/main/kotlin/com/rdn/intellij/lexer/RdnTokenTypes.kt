package com.rdn.intellij.lexer

import com.intellij.psi.tree.IElementType
import com.rdn.intellij.RdnLanguage

class RdnTokenType(debugName: String) : IElementType(debugName, RdnLanguage)

object RdnTokenTypes {
    // Structural
    @JvmField val LBRACE = RdnTokenType("LBRACE")            // {
    @JvmField val RBRACE = RdnTokenType("RBRACE")            // }
    @JvmField val LBRACKET = RdnTokenType("LBRACKET")        // [
    @JvmField val RBRACKET = RdnTokenType("RBRACKET")        // ]
    @JvmField val LPAREN = RdnTokenType("LPAREN")            // (
    @JvmField val RPAREN = RdnTokenType("RPAREN")            // )
    @JvmField val COLON = RdnTokenType("COLON")              // :
    @JvmField val COMMA = RdnTokenType("COMMA")              // ,
    @JvmField val ARROW = RdnTokenType("ARROW")              // =>

    // Literals
    @JvmField val NULL = RdnTokenType("NULL")
    @JvmField val TRUE = RdnTokenType("TRUE")
    @JvmField val FALSE = RdnTokenType("FALSE")
    @JvmField val INTEGER = RdnTokenType("INTEGER")
    @JvmField val FLOAT = RdnTokenType("FLOAT")
    @JvmField val BIGINT = RdnTokenType("BIGINT")
    @JvmField val NAN = RdnTokenType("NAN")
    @JvmField val INFINITY = RdnTokenType("INFINITY")
    @JvmField val NEG_INFINITY = RdnTokenType("NEG_INFINITY")

    // Strings
    @JvmField val STRING_OPEN = RdnTokenType("STRING_OPEN")
    @JvmField val STRING_CONTENT = RdnTokenType("STRING_CONTENT")
    @JvmField val STRING_ESCAPE = RdnTokenType("STRING_ESCAPE")
    @JvmField val STRING_INVALID_ESCAPE = RdnTokenType("STRING_INVALID_ESCAPE")
    @JvmField val STRING_CLOSE = RdnTokenType("STRING_CLOSE")

    // Object keys (distinct from string tokens for separate highlighting)
    @JvmField val KEY_OPEN = RdnTokenType("KEY_OPEN")
    @JvmField val KEY_CONTENT = RdnTokenType("KEY_CONTENT")
    @JvmField val KEY_ESCAPE = RdnTokenType("KEY_ESCAPE")
    @JvmField val KEY_CLOSE = RdnTokenType("KEY_CLOSE")

    // Date/Time
    @JvmField val AT_SIGN = RdnTokenType("AT_SIGN")
    @JvmField val DATE_PART = RdnTokenType("DATE_PART")
    @JvmField val TIME_SEPARATOR = RdnTokenType("TIME_SEPARATOR")
    @JvmField val TIME_PART = RdnTokenType("TIME_PART")
    @JvmField val MILLIS_PART = RdnTokenType("MILLIS_PART")
    @JvmField val TIMEZONE = RdnTokenType("TIMEZONE")
    @JvmField val UNIX_TIMESTAMP = RdnTokenType("UNIX_TIMESTAMP")

    // Duration
    @JvmField val DURATION_P = RdnTokenType("DURATION_P")
    @JvmField val DURATION_NUMBER = RdnTokenType("DURATION_NUMBER")
    @JvmField val DURATION_UNIT = RdnTokenType("DURATION_UNIT")
    @JvmField val DURATION_T = RdnTokenType("DURATION_T")

    // Binary
    @JvmField val BINARY_PREFIX = RdnTokenType("BINARY_PREFIX")
    @JvmField val BINARY_OPEN = RdnTokenType("BINARY_OPEN")
    @JvmField val BINARY_CONTENT = RdnTokenType("BINARY_CONTENT")
    @JvmField val BINARY_INVALID_CHAR = RdnTokenType("BINARY_INVALID_CHAR")
    @JvmField val BINARY_CLOSE = RdnTokenType("BINARY_CLOSE")

    // Map/Set keywords
    @JvmField val MAP_KEYWORD = RdnTokenType("MAP_KEYWORD")
    @JvmField val SET_KEYWORD = RdnTokenType("SET_KEYWORD")

    // RegExp tokens (emitted in REGEXP lexer state)
    @JvmField val REGEXP_OPEN = RdnTokenType("REGEXP_OPEN")
    @JvmField val REGEXP_CLOSE = RdnTokenType("REGEXP_CLOSE")
    @JvmField val REGEXP_FLAGS = RdnTokenType("REGEXP_FLAGS")
    @JvmField val REGEXP_CONTENT = RdnTokenType("REGEXP_CONTENT")
    @JvmField val REGEXP_ESCAPE = RdnTokenType("REGEXP_ESCAPE")
    @JvmField val REGEXP_CHAR_CLASS_ESCAPE = RdnTokenType("REGEXP_CHAR_CLASS_ESCAPE")
    @JvmField val REGEXP_QUANTIFIER = RdnTokenType("REGEXP_QUANTIFIER")
    @JvmField val REGEXP_ANCHOR = RdnTokenType("REGEXP_ANCHOR")
    @JvmField val REGEXP_ALTERNATION = RdnTokenType("REGEXP_ALTERNATION")
    @JvmField val REGEXP_DOT = RdnTokenType("REGEXP_DOT")
    @JvmField val REGEXP_GROUP_OPEN = RdnTokenType("REGEXP_GROUP_OPEN")
    @JvmField val REGEXP_GROUP_CLOSE = RdnTokenType("REGEXP_GROUP_CLOSE")
    @JvmField val REGEXP_LOOKAROUND = RdnTokenType("REGEXP_LOOKAROUND")
    @JvmField val REGEXP_NAMED_GROUP = RdnTokenType("REGEXP_NAMED_GROUP")
    @JvmField val REGEXP_NON_CAPTURING = RdnTokenType("REGEXP_NON_CAPTURING")
    @JvmField val REGEXP_BACKREFERENCE = RdnTokenType("REGEXP_BACKREFERENCE")
    @JvmField val REGEXP_CHAR_CLASS_OPEN = RdnTokenType("REGEXP_CHAR_CLASS_OPEN")
    @JvmField val REGEXP_CHAR_CLASS_CLOSE = RdnTokenType("REGEXP_CHAR_CLASS_CLOSE")
    @JvmField val REGEXP_NEGATION = RdnTokenType("REGEXP_NEGATION")
    @JvmField val REGEXP_RANGE = RdnTokenType("REGEXP_RANGE")

    // Special
    @JvmField val WHITE_SPACE = com.intellij.psi.TokenType.WHITE_SPACE
    @JvmField val BAD_CHARACTER = com.intellij.psi.TokenType.BAD_CHARACTER
}
