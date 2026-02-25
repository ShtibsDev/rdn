package com.rdn.intellij.highlighting

import com.intellij.lexer.Lexer
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighterBase
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.lexer.RdnTokenTypes

class RdnSyntaxHighlighter : SyntaxHighlighterBase() {

    override fun getHighlightingLexer(): Lexer = RdnHighlightingLexer()

    override fun getTokenHighlights(tokenType: IElementType): Array<TextAttributesKey> = pack(ATTRIBUTES[tokenType])

    companion object {
        private val ATTRIBUTES: Map<IElementType, TextAttributesKey> = buildMap {
            // Keywords / literals
            put(RdnTokenTypes.NULL, RdnColors.KEYWORD)
            put(RdnTokenTypes.TRUE, RdnColors.KEYWORD)
            put(RdnTokenTypes.FALSE, RdnColors.KEYWORD)
            put(RdnTokenTypes.NAN, RdnColors.KEYWORD)
            put(RdnTokenTypes.INFINITY, RdnColors.KEYWORD)
            put(RdnTokenTypes.NEG_INFINITY, RdnColors.KEYWORD)

            // Numbers
            put(RdnTokenTypes.INTEGER, RdnColors.NUMBER)
            put(RdnTokenTypes.FLOAT, RdnColors.NUMBER)
            put(RdnTokenTypes.BIGINT, RdnColors.BIGINT)

            // Strings
            put(RdnTokenTypes.STRING_OPEN, RdnColors.STRING)
            put(RdnTokenTypes.STRING_CONTENT, RdnColors.STRING)
            put(RdnTokenTypes.STRING_ESCAPE, RdnColors.STRING_ESCAPE)
            put(RdnTokenTypes.STRING_INVALID_ESCAPE, RdnColors.STRING_INVALID_ESCAPE)
            put(RdnTokenTypes.STRING_CLOSE, RdnColors.STRING)

            // Object keys
            put(RdnTokenTypes.KEY_OPEN, RdnColors.OBJECT_KEY)
            put(RdnTokenTypes.KEY_CONTENT, RdnColors.OBJECT_KEY)
            put(RdnTokenTypes.KEY_ESCAPE, RdnColors.OBJECT_KEY)
            put(RdnTokenTypes.KEY_CLOSE, RdnColors.OBJECT_KEY)

            // Date/Time
            put(RdnTokenTypes.AT_SIGN, RdnColors.AT_SIGN)
            put(RdnTokenTypes.DATE_PART, RdnColors.DATE_PART)
            put(RdnTokenTypes.TIME_SEPARATOR, RdnColors.AT_SIGN)
            put(RdnTokenTypes.TIME_PART, RdnColors.TIME_PART)
            put(RdnTokenTypes.MILLIS_PART, RdnColors.MILLIS_PART)
            put(RdnTokenTypes.TIMEZONE, RdnColors.TIMEZONE)
            put(RdnTokenTypes.UNIX_TIMESTAMP, RdnColors.UNIX_TIMESTAMP)

            // Duration
            put(RdnTokenTypes.DURATION_P, RdnColors.DURATION_P)
            put(RdnTokenTypes.DURATION_NUMBER, RdnColors.DURATION_NUMBER)
            put(RdnTokenTypes.DURATION_UNIT, RdnColors.DURATION_UNIT)
            put(RdnTokenTypes.DURATION_T, RdnColors.DURATION_T)

            // Binary
            put(RdnTokenTypes.BINARY_PREFIX, RdnColors.BINARY_PREFIX)
            put(RdnTokenTypes.BINARY_OPEN, RdnColors.BINARY_PREFIX)
            put(RdnTokenTypes.BINARY_CONTENT, RdnColors.BINARY_CONTENT)
            put(RdnTokenTypes.BINARY_INVALID_CHAR, RdnColors.BINARY_INVALID_CHAR)
            put(RdnTokenTypes.BINARY_CLOSE, RdnColors.BINARY_PREFIX)

            // Map / Set keywords
            put(RdnTokenTypes.MAP_KEYWORD, RdnColors.MAP_KEYWORD)
            put(RdnTokenTypes.SET_KEYWORD, RdnColors.SET_KEYWORD)

            // RegExp
            put(RdnTokenTypes.REGEXP_OPEN, RdnColors.REGEXP_BODY)
            put(RdnTokenTypes.REGEXP_CONTENT, RdnColors.REGEXP_BODY)
            put(RdnTokenTypes.REGEXP_CLOSE, RdnColors.REGEXP_BODY)
            put(RdnTokenTypes.REGEXP_FLAGS, RdnColors.REGEXP_FLAGS)
            put(RdnTokenTypes.REGEXP_ESCAPE, RdnColors.REGEXP_ESCAPE)
            put(RdnTokenTypes.REGEXP_CHAR_CLASS_ESCAPE, RdnColors.REGEXP_CHAR_CLASS_ESCAPE)
            put(RdnTokenTypes.REGEXP_QUANTIFIER, RdnColors.REGEXP_QUANTIFIER)
            put(RdnTokenTypes.REGEXP_ANCHOR, RdnColors.REGEXP_ANCHOR)
            put(RdnTokenTypes.REGEXP_ALTERNATION, RdnColors.REGEXP_ALTERNATION)
            put(RdnTokenTypes.REGEXP_DOT, RdnColors.REGEXP_DOT)
            put(RdnTokenTypes.REGEXP_GROUP_OPEN, RdnColors.REGEXP_GROUP)
            put(RdnTokenTypes.REGEXP_GROUP_CLOSE, RdnColors.REGEXP_GROUP)
            put(RdnTokenTypes.REGEXP_LOOKAROUND, RdnColors.REGEXP_SPECIAL)
            put(RdnTokenTypes.REGEXP_NAMED_GROUP, RdnColors.REGEXP_SPECIAL)
            put(RdnTokenTypes.REGEXP_NON_CAPTURING, RdnColors.REGEXP_SPECIAL)
            put(RdnTokenTypes.REGEXP_BACKREFERENCE, RdnColors.REGEXP_SPECIAL)
            put(RdnTokenTypes.REGEXP_CHAR_CLASS_OPEN, RdnColors.REGEXP_CHAR_CLASS)
            put(RdnTokenTypes.REGEXP_CHAR_CLASS_CLOSE, RdnColors.REGEXP_CHAR_CLASS)
            put(RdnTokenTypes.REGEXP_NEGATION, RdnColors.REGEXP_SPECIAL)
            put(RdnTokenTypes.REGEXP_RANGE, RdnColors.REGEXP_SPECIAL)

            // Structural punctuation
            put(RdnTokenTypes.LBRACE, RdnColors.BRACES)
            put(RdnTokenTypes.RBRACE, RdnColors.BRACES)
            put(RdnTokenTypes.LBRACKET, RdnColors.BRACKETS)
            put(RdnTokenTypes.RBRACKET, RdnColors.BRACKETS)
            put(RdnTokenTypes.LPAREN, RdnColors.PARENS)
            put(RdnTokenTypes.RPAREN, RdnColors.PARENS)
            put(RdnTokenTypes.COMMA, RdnColors.COMMA)
            put(RdnTokenTypes.COLON, RdnColors.COLON)
            put(RdnTokenTypes.ARROW, RdnColors.ARROW)

            // Bad character
            put(RdnTokenTypes.BAD_CHARACTER, RdnColors.BAD_CHARACTER)
        }
    }
}
