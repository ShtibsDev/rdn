# Task 006: Implement Syntax Highlighter

## References
- [Tech Design](../tech-design.md) — Sections 3.2, 6.2
- [Discovery](../discovery.md)

## Description
Create `RdnSyntaxHighlighter.kt` that maps every `RdnTokenType` to one or more `TextAttributesKey` color keys using IntelliJ's standard semantic attribute keys. Create `RdnSyntaxHighlighterFactory.kt` as the registered factory. Implement `RdnHighlightingLexer.kt`, a lookahead wrapper that re-maps `STRING_*` tokens to `KEY_*` tokens when the string is followed by `:`. Register both in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/highlighting/RdnSyntaxHighlighter.kt` — Token-to-color mapping
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/highlighting/RdnSyntaxHighlighterFactory.kt` — Factory
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/highlighting/RdnHighlightingLexer.kt` — Key token re-mapper
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/highlighting/RdnColors.kt` — `TextAttributesKey` constants
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register factory

## Implementation Details

### `RdnColors.kt`

```kotlin
package com.rdn.intellij.highlighting

import com.intellij.openapi.editor.DefaultLanguageHighlighterColors as Default
import com.intellij.openapi.editor.colors.TextAttributesKey

object RdnColors {
    val KEYWORD = TextAttributesKey.createTextAttributesKey("RDN_KEYWORD", Default.KEYWORD)
    val NUMBER = TextAttributesKey.createTextAttributesKey("RDN_NUMBER", Default.NUMBER)
    val BIGINT = TextAttributesKey.createTextAttributesKey("RDN_BIGINT", Default.NUMBER)
    val STRING = TextAttributesKey.createTextAttributesKey("RDN_STRING", Default.STRING)
    val STRING_ESCAPE = TextAttributesKey.createTextAttributesKey("RDN_STRING_ESCAPE", Default.VALID_STRING_ESCAPE)
    val STRING_INVALID_ESCAPE = TextAttributesKey.createTextAttributesKey("RDN_STRING_INVALID_ESCAPE", Default.INVALID_STRING_ESCAPE)
    val OBJECT_KEY = TextAttributesKey.createTextAttributesKey("RDN_OBJECT_KEY", Default.INSTANCE_FIELD)
    val AT_SIGN = TextAttributesKey.createTextAttributesKey("RDN_AT_SIGN", Default.METADATA)
    val DATE_PART = TextAttributesKey.createTextAttributesKey("RDN_DATE_PART", Default.METADATA)
    val TIME_PART = TextAttributesKey.createTextAttributesKey("RDN_TIME_PART", Default.NUMBER)
    val MILLIS_PART = TextAttributesKey.createTextAttributesKey("RDN_MILLIS_PART", Default.NUMBER)
    val TIMEZONE = TextAttributesKey.createTextAttributesKey("RDN_TIMEZONE", Default.KEYWORD)
    val UNIX_TIMESTAMP = TextAttributesKey.createTextAttributesKey("RDN_UNIX_TIMESTAMP", Default.NUMBER)
    val DURATION_P = TextAttributesKey.createTextAttributesKey("RDN_DURATION_P", Default.METADATA)
    val DURATION_NUMBER = TextAttributesKey.createTextAttributesKey("RDN_DURATION_NUMBER", Default.NUMBER)
    val DURATION_UNIT = TextAttributesKey.createTextAttributesKey("RDN_DURATION_UNIT", Default.KEYWORD)
    val DURATION_T = TextAttributesKey.createTextAttributesKey("RDN_DURATION_T", Default.METADATA)
    val BINARY_PREFIX = TextAttributesKey.createTextAttributesKey("RDN_BINARY_PREFIX", Default.KEYWORD)
    val BINARY_CONTENT = TextAttributesKey.createTextAttributesKey("RDN_BINARY_CONTENT", Default.STRING)
    val BINARY_INVALID_CHAR = TextAttributesKey.createTextAttributesKey("RDN_BINARY_INVALID_CHAR", Default.INVALID_STRING_ESCAPE)
    val MAP_KEYWORD = TextAttributesKey.createTextAttributesKey("RDN_MAP_KEYWORD", Default.KEYWORD)
    val SET_KEYWORD = TextAttributesKey.createTextAttributesKey("RDN_SET_KEYWORD", Default.KEYWORD)
    val REGEXP_BODY = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_BODY", Default.STRING)
    val REGEXP_ESCAPE = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_ESCAPE", Default.VALID_STRING_ESCAPE)
    val REGEXP_CHAR_CLASS_ESCAPE = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_CHAR_CLASS_ESCAPE", Default.VALID_STRING_ESCAPE)
    val REGEXP_QUANTIFIER = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_QUANTIFIER", Default.KEYWORD)
    val REGEXP_ANCHOR = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_ANCHOR", Default.KEYWORD)
    val REGEXP_ALTERNATION = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_ALTERNATION", Default.KEYWORD)
    val REGEXP_DOT = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_DOT", Default.KEYWORD)
    val REGEXP_GROUP = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_GROUP", Default.PARENTHESES)
    val REGEXP_SPECIAL = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_SPECIAL", Default.METADATA)
    val REGEXP_CHAR_CLASS = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_CHAR_CLASS", Default.BRACKETS)
    val REGEXP_FLAGS = TextAttributesKey.createTextAttributesKey("RDN_REGEXP_FLAGS", Default.METADATA)
    val BRACES = TextAttributesKey.createTextAttributesKey("RDN_BRACES", Default.BRACES)
    val BRACKETS = TextAttributesKey.createTextAttributesKey("RDN_BRACKETS", Default.BRACKETS)
    val PARENS = TextAttributesKey.createTextAttributesKey("RDN_PARENS", Default.PARENTHESES)
    val COMMA = TextAttributesKey.createTextAttributesKey("RDN_COMMA", Default.COMMA)
    val COLON = TextAttributesKey.createTextAttributesKey("RDN_COLON", Default.SEMICOLON)
    val ARROW = TextAttributesKey.createTextAttributesKey("RDN_ARROW", Default.OPERATION_SIGN)
    val BAD_CHARACTER = TextAttributesKey.createTextAttributesKey("RDN_BAD_CHARACTER", Default.INVALID_STRING_ESCAPE)
}
```

### `RdnSyntaxHighlighter.kt`

```kotlin
package com.rdn.intellij.highlighting

import com.intellij.lexer.Lexer
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighterBase
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.lexer.RdnTokenTypes.*

class RdnSyntaxHighlighter : SyntaxHighlighterBase() {
    override fun getHighlightingLexer(): Lexer = RdnHighlightingLexer()

    override fun getTokenHighlights(tokenType: IElementType): Array<TextAttributesKey> =
        when (tokenType) {
            NULL, TRUE, FALSE, NAN, INFINITY, NEG_INFINITY -> pack(RdnColors.KEYWORD)
            INTEGER, FLOAT, UNIX_TIMESTAMP, TIME_PART, MILLIS_PART -> pack(RdnColors.NUMBER)
            BIGINT -> pack(RdnColors.BIGINT)
            STRING_OPEN, STRING_CONTENT, STRING_CLOSE -> pack(RdnColors.STRING)
            STRING_ESCAPE -> pack(RdnColors.STRING_ESCAPE)
            STRING_INVALID_ESCAPE -> pack(RdnColors.STRING_INVALID_ESCAPE)
            KEY_OPEN, KEY_CONTENT, KEY_CLOSE -> pack(RdnColors.OBJECT_KEY)
            KEY_ESCAPE -> pack(RdnColors.STRING_ESCAPE)
            AT_SIGN, DATE_PART, TIME_SEPARATOR -> pack(RdnColors.AT_SIGN)
            TIMEZONE -> pack(RdnColors.TIMEZONE)
            DURATION_P, DURATION_T -> pack(RdnColors.DURATION_P)
            DURATION_NUMBER -> pack(RdnColors.DURATION_NUMBER)
            DURATION_UNIT -> pack(RdnColors.DURATION_UNIT)
            BINARY_PREFIX -> pack(RdnColors.BINARY_PREFIX)
            BINARY_OPEN, BINARY_CONTENT, BINARY_CLOSE -> pack(RdnColors.BINARY_CONTENT)
            BINARY_INVALID_CHAR -> pack(RdnColors.BINARY_INVALID_CHAR)
            MAP_KEYWORD -> pack(RdnColors.MAP_KEYWORD)
            SET_KEYWORD -> pack(RdnColors.SET_KEYWORD)
            REGEXP_OPEN, REGEXP_CONTENT, REGEXP_CLOSE -> pack(RdnColors.REGEXP_BODY)
            REGEXP_FLAGS -> pack(RdnColors.REGEXP_FLAGS)
            REGEXP_ESCAPE -> pack(RdnColors.REGEXP_ESCAPE)
            REGEXP_CHAR_CLASS_ESCAPE -> pack(RdnColors.REGEXP_CHAR_CLASS_ESCAPE)
            REGEXP_QUANTIFIER -> pack(RdnColors.REGEXP_QUANTIFIER)
            REGEXP_ANCHOR -> pack(RdnColors.REGEXP_ANCHOR)
            REGEXP_ALTERNATION -> pack(RdnColors.REGEXP_ALTERNATION)
            REGEXP_DOT -> pack(RdnColors.REGEXP_DOT)
            REGEXP_GROUP_OPEN, REGEXP_GROUP_CLOSE -> pack(RdnColors.REGEXP_GROUP)
            REGEXP_LOOKAROUND, REGEXP_NAMED_GROUP, REGEXP_NON_CAPTURING, REGEXP_BACKREFERENCE -> pack(RdnColors.REGEXP_SPECIAL)
            REGEXP_CHAR_CLASS_OPEN, REGEXP_CHAR_CLASS_CLOSE, REGEXP_NEGATION, REGEXP_RANGE -> pack(RdnColors.REGEXP_CHAR_CLASS)
            LBRACE, RBRACE -> pack(RdnColors.BRACES)
            LBRACKET, RBRACKET -> pack(RdnColors.BRACKETS)
            LPAREN, RPAREN -> pack(RdnColors.PARENS)
            COMMA -> pack(RdnColors.COMMA)
            COLON -> pack(RdnColors.COLON)
            ARROW -> pack(RdnColors.ARROW)
            BAD_CHARACTER -> pack(RdnColors.BAD_CHARACTER)
            else -> emptyArray()
        }
}
```

### `RdnHighlightingLexer.kt`

The highlighting lexer wraps `RdnLexerAdapter` and performs lookahead to re-map string tokens to key tokens when the string is followed by `:`.

```kotlin
package com.rdn.intellij.highlighting

import com.intellij.lexer.LexerBase
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.lexer.RdnLexerAdapter
import com.rdn.intellij.lexer.RdnTokenTypes

class RdnHighlightingLexer : LexerBase() {
    private val delegate = RdnLexerAdapter()
    // Buffer of tokens from lookahead
    private val buffer = ArrayDeque<Triple<IElementType, Int, Int>>()
    private var currentType: IElementType? = null
    private var currentStart = 0
    private var currentEnd = 0
    private lateinit var text: CharSequence

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        text = buffer
        this.buffer.clear()
        delegate.start(buffer, startOffset, endOffset, initialState)
        advance()
    }

    override fun advance() {
        if (this.buffer.isNotEmpty()) {
            val next = this.buffer.removeFirst()
            currentType = next.first
            currentStart = next.second
            currentEnd = next.third
        } else {
            currentType = delegate.tokenType
            if (currentType == null) return
            currentStart = delegate.tokenStart
            currentEnd = delegate.tokenEnd
            delegate.advance()
        }
        // If this is a STRING_OPEN, peek ahead to see if : follows after STRING_CLOSE
        if (currentType == RdnTokenTypes.STRING_OPEN) {
            lookaheadKeyCheck()
        }
    }

    private fun lookaheadKeyCheck() {
        // Collect all tokens through STRING_CLOSE
        val collected = mutableListOf(Triple(currentType!!, currentStart, currentEnd))
        var inner = delegate.tokenType
        while (inner != null && inner != RdnTokenTypes.STRING_CLOSE) {
            collected.add(Triple(inner, delegate.tokenStart, delegate.tokenEnd))
            delegate.advance()
            inner = delegate.tokenType
        }
        if (inner == RdnTokenTypes.STRING_CLOSE) {
            collected.add(Triple(inner, delegate.tokenStart, delegate.tokenEnd))
            delegate.advance()
        }
        // Peek at next non-whitespace
        val peekType = delegate.tokenType
        val isKey = peekType == RdnTokenTypes.COLON
        if (isKey) {
            // Re-map STRING_* to KEY_*
            val remapped = collected.map { (type, s, e) ->
                val newType = when (type) {
                    RdnTokenTypes.STRING_OPEN -> RdnTokenTypes.KEY_OPEN
                    RdnTokenTypes.STRING_CONTENT -> RdnTokenTypes.KEY_CONTENT
                    RdnTokenTypes.STRING_ESCAPE -> RdnTokenTypes.KEY_ESCAPE
                    RdnTokenTypes.STRING_CLOSE -> RdnTokenTypes.KEY_CLOSE
                    else -> type
                }
                Triple(newType, s, e)
            }
            buffer.addAll(remapped.drop(1))
            currentType = remapped.first().first
            currentStart = remapped.first().second
            currentEnd = remapped.first().third
        } else {
            buffer.addAll(collected.drop(1))
            currentType = collected.first().first
            currentStart = collected.first().second
            currentEnd = collected.first().third
        }
    }

    override fun getTokenType(): IElementType? = currentType
    override fun getTokenStart(): Int = currentStart
    override fun getTokenEnd(): Int = currentEnd
    override fun getBufferSequence(): CharSequence = text
    override fun getBufferEnd(): Int = delegate.bufferEnd
    override fun getState(): Int = delegate.state
}
```

### `plugin.xml` additions

```xml
<lang.syntaxHighlighterFactory
    language="RDN"
    implementationClass="com.rdn.intellij.highlighting.RdnSyntaxHighlighterFactory"/>
```

### `RdnSyntaxHighlighterFactory.kt`

```kotlin
package com.rdn.intellij.highlighting

import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile

class RdnSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter =
        RdnSyntaxHighlighter()
}
```

## Acceptance Criteria
- [ ] Opening a `.rdn` file shows colored syntax immediately
- [ ] `"key": "value"` — key text uses `OBJECT_KEY` color, value text uses `STRING` color
- [ ] `42n` is highlighted with `BIGINT` color (can be italic)
- [ ] `NaN`, `Infinity`, `-Infinity` use `KEYWORD` color
- [ ] Date parts `@2024-01-15` use `METADATA`-derived color for `@` and date
- [ ] RegExp quantifiers `+`, `*`, `?` use `KEYWORD` color
- [ ] RegExp character class `[a-z]` uses `BRACKETS`-derived color
- [ ] Binary invalid characters use the error/invalid color
- [ ] All `RdnColors` constants have unique names prefixed with `RDN_`

## Dependencies
- Depends on: task-002, task-003, task-004
- Blocks: task-007
