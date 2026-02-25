package com.rdn.intellij.lexer

import com.intellij.psi.tree.IElementType

/**
 * Factory used by GrammarKit-generated code to create token types.
 * Returns the canonical instances from [RdnTokenTypes] so that the parser
 * and lexer share the exact same IElementType objects.
 */
object RdnTokenTypeFactory {
    private val tokensByName: Map<String, IElementType> = mapOf(
        // Structural — by name (used by lexer) and by text (used by GrammarKit-generated RdnTypes)
        "LBRACE" to RdnTokenTypes.LBRACE, "{" to RdnTokenTypes.LBRACE,
        "RBRACE" to RdnTokenTypes.RBRACE, "}" to RdnTokenTypes.RBRACE,
        "LBRACKET" to RdnTokenTypes.LBRACKET, "[" to RdnTokenTypes.LBRACKET,
        "RBRACKET" to RdnTokenTypes.RBRACKET, "]" to RdnTokenTypes.RBRACKET,
        "LPAREN" to RdnTokenTypes.LPAREN, "(" to RdnTokenTypes.LPAREN,
        "RPAREN" to RdnTokenTypes.RPAREN, ")" to RdnTokenTypes.RPAREN,
        "COLON" to RdnTokenTypes.COLON, ":" to RdnTokenTypes.COLON,
        "COMMA" to RdnTokenTypes.COMMA, "," to RdnTokenTypes.COMMA,
        "ARROW" to RdnTokenTypes.ARROW, "=>" to RdnTokenTypes.ARROW,
        // Literals — by name and by text
        "NULL" to RdnTokenTypes.NULL, "null" to RdnTokenTypes.NULL,
        "TRUE" to RdnTokenTypes.TRUE, "true" to RdnTokenTypes.TRUE,
        "FALSE" to RdnTokenTypes.FALSE, "false" to RdnTokenTypes.FALSE,
        "INTEGER" to RdnTokenTypes.INTEGER, "integer" to RdnTokenTypes.INTEGER,
        "FLOAT" to RdnTokenTypes.FLOAT, "float" to RdnTokenTypes.FLOAT,
        "BIGINT" to RdnTokenTypes.BIGINT, "bigint" to RdnTokenTypes.BIGINT,
        "NAN" to RdnTokenTypes.NAN, "NaN" to RdnTokenTypes.NAN,
        "INFINITY" to RdnTokenTypes.INFINITY, "Infinity" to RdnTokenTypes.INFINITY,
        "NEG_INFINITY" to RdnTokenTypes.NEG_INFINITY, "-Infinity" to RdnTokenTypes.NEG_INFINITY,
        // Strings
        "STRING_OPEN" to RdnTokenTypes.STRING_OPEN,
        "STRING_CONTENT" to RdnTokenTypes.STRING_CONTENT,
        "STRING_ESCAPE" to RdnTokenTypes.STRING_ESCAPE,
        "STRING_INVALID_ESCAPE" to RdnTokenTypes.STRING_INVALID_ESCAPE,
        "STRING_CLOSE" to RdnTokenTypes.STRING_CLOSE,
        // Object keys
        "KEY_OPEN" to RdnTokenTypes.KEY_OPEN,
        "KEY_CONTENT" to RdnTokenTypes.KEY_CONTENT,
        "KEY_ESCAPE" to RdnTokenTypes.KEY_ESCAPE,
        "KEY_CLOSE" to RdnTokenTypes.KEY_CLOSE,
        // Date/Time
        "AT_SIGN" to RdnTokenTypes.AT_SIGN, "@" to RdnTokenTypes.AT_SIGN,
        "DATE_PART" to RdnTokenTypes.DATE_PART,
        "TIME_SEPARATOR" to RdnTokenTypes.TIME_SEPARATOR,
        "TIME_PART" to RdnTokenTypes.TIME_PART,
        "MILLIS_PART" to RdnTokenTypes.MILLIS_PART,
        "TIMEZONE" to RdnTokenTypes.TIMEZONE,
        "UNIX_TIMESTAMP" to RdnTokenTypes.UNIX_TIMESTAMP,
        // Duration
        "DURATION_P" to RdnTokenTypes.DURATION_P,
        "DURATION_NUMBER" to RdnTokenTypes.DURATION_NUMBER,
        "DURATION_UNIT" to RdnTokenTypes.DURATION_UNIT,
        "DURATION_T" to RdnTokenTypes.DURATION_T,
        // Binary
        "BINARY_PREFIX" to RdnTokenTypes.BINARY_PREFIX,
        "BINARY_OPEN" to RdnTokenTypes.BINARY_OPEN,
        "BINARY_CONTENT" to RdnTokenTypes.BINARY_CONTENT,
        "BINARY_INVALID_CHAR" to RdnTokenTypes.BINARY_INVALID_CHAR,
        "BINARY_CLOSE" to RdnTokenTypes.BINARY_CLOSE,
        // Map/Set keywords
        "MAP_KEYWORD" to RdnTokenTypes.MAP_KEYWORD, "Map" to RdnTokenTypes.MAP_KEYWORD,
        "SET_KEYWORD" to RdnTokenTypes.SET_KEYWORD, "Set" to RdnTokenTypes.SET_KEYWORD,
        // RegExp
        "REGEXP_OPEN" to RdnTokenTypes.REGEXP_OPEN,
        "REGEXP_CLOSE" to RdnTokenTypes.REGEXP_CLOSE,
        "REGEXP_FLAGS" to RdnTokenTypes.REGEXP_FLAGS,
        "REGEXP_CONTENT" to RdnTokenTypes.REGEXP_CONTENT,
        "REGEXP_ESCAPE" to RdnTokenTypes.REGEXP_ESCAPE,
        "REGEXP_CHAR_CLASS_ESCAPE" to RdnTokenTypes.REGEXP_CHAR_CLASS_ESCAPE,
        "REGEXP_QUANTIFIER" to RdnTokenTypes.REGEXP_QUANTIFIER,
        "REGEXP_ANCHOR" to RdnTokenTypes.REGEXP_ANCHOR,
        "REGEXP_ALTERNATION" to RdnTokenTypes.REGEXP_ALTERNATION,
        "REGEXP_DOT" to RdnTokenTypes.REGEXP_DOT,
        "REGEXP_GROUP_OPEN" to RdnTokenTypes.REGEXP_GROUP_OPEN,
        "REGEXP_GROUP_CLOSE" to RdnTokenTypes.REGEXP_GROUP_CLOSE,
        "REGEXP_LOOKAROUND" to RdnTokenTypes.REGEXP_LOOKAROUND,
        "REGEXP_NAMED_GROUP" to RdnTokenTypes.REGEXP_NAMED_GROUP,
        "REGEXP_NON_CAPTURING" to RdnTokenTypes.REGEXP_NON_CAPTURING,
        "REGEXP_BACKREFERENCE" to RdnTokenTypes.REGEXP_BACKREFERENCE,
        "REGEXP_CHAR_CLASS_OPEN" to RdnTokenTypes.REGEXP_CHAR_CLASS_OPEN,
        "REGEXP_CHAR_CLASS_CLOSE" to RdnTokenTypes.REGEXP_CHAR_CLASS_CLOSE,
        "REGEXP_NEGATION" to RdnTokenTypes.REGEXP_NEGATION,
        "REGEXP_RANGE" to RdnTokenTypes.REGEXP_RANGE,
        // Special
        "WHITE_SPACE" to RdnTokenTypes.WHITE_SPACE,
        "BAD_CHARACTER" to RdnTokenTypes.BAD_CHARACTER,
    )

    /**
     * Called by GrammarKit-generated [com.rdn.intellij.psi.RdnTypes] to create token types.
     * Returns existing instances from [RdnTokenTypes] to ensure identity equality
     * between parser and lexer tokens.
     */
    @JvmStatic
    fun createTokenType(name: String): IElementType = tokensByName[name] ?: RdnTokenType(name)
}
