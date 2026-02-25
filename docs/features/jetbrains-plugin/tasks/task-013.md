# Task 013: Implement Scanner (Unquoted Keys + Binary Validation)

## References
- [Tech Design](../tech-design.md) — Sections 3.3, 6.4
- [Discovery](../discovery.md)

## Description
Port `scanner.ts` from the VSCode extension to `RdnScanner.kt`. The scanner is a lightweight, fast-path tool that scans document text for two categories of errors: (1) unquoted keys in object position, and (2) invalid characters inside `b"..."` and `x"..."` binary literals. It uses brace context tracking with brace disambiguation (Unknown, Object, Map, Set, ExplicitMap, ExplicitSet, Array, Tuple) to determine whether a bare identifier is in key position.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/annotator/RdnScanner.kt` — Scanner with unquoted key and binary error detection

## Implementation Details

### `RdnScanner.kt`

```kotlin
package com.rdn.intellij.annotator

data class UnquotedKey(val name: String, val offset: Int, val length: Int)
data class BinaryCharError(val offset: Int, val length: Int, val message: String, val kind: BinaryEncoding)

enum class BinaryEncoding { BASE64, HEX }

private enum class BraceContext {
    UNKNOWN,       // Just opened, haven't seen first token yet
    OBJECT,        // { "key": value, ... }
    MAP,           // { key => value, ... } or Map{ ... }
    SET,           // { value, ... } or Set{ ... }
    EXPLICIT_MAP,  // Map{ ... }
    EXPLICIT_SET,  // Set{ ... }
    ARRAY,         // [ ... ]
    TUPLE,         // ( ... )
}

object RdnScanner {
    /**
     * Scans text for unquoted object keys.
     * Returns list of (name, offset, length) for each bare identifier found in key position.
     *
     * Algorithm: maintain a stack of BraceContext values.
     * - When UNKNOWN and a bare identifier is seen followed by ':', classify brace as OBJECT and emit error.
     * - When UNKNOWN and '=>' is seen, reclassify as MAP.
     * - When UNKNOWN and a value is followed by ',' or '}', reclassify as SET.
     */
    fun scanUnquotedKeys(text: String): List<UnquotedKey> {
        val result = mutableListOf<UnquotedKey>()
        val contextStack = ArrayDeque<BraceContext>()
        var pos = 0

        fun skipWhitespace() {
            while (pos < text.length && text[pos].isWhitespace()) pos++
        }

        fun skipString() {
            pos++ // skip opening "
            while (pos < text.length) {
                when (text[pos]) {
                    '\\' -> pos += 2 // skip escape
                    '"' -> { pos++; return }
                    else -> pos++
                }
            }
        }

        fun skipLineComment() {
            // RDN has no comments, but skip if encountered to be resilient
            while (pos < text.length && text[pos] != '\n') pos++
        }

        fun peekIdentifier(): String? {
            val start = pos
            if (pos >= text.length) return null
            val ch = text[pos]
            if (!ch.isLetter() && ch != '_' && ch != '$') return null
            while (pos < text.length && (text[pos].isLetterOrDigit() || text[pos] == '_' || text[pos] == '$')) pos++
            return text.substring(start, pos).also { pos = start }
        }

        fun readIdentifier(): String {
            val start = pos
            while (pos < text.length && (text[pos].isLetterOrDigit() || text[pos] == '_' || text[pos] == '$')) pos++
            return text.substring(start, pos)
        }

        fun isInKeyPosition(): Boolean {
            val ctx = contextStack.lastOrNull() ?: return false
            return ctx == BraceContext.UNKNOWN || ctx == BraceContext.OBJECT
        }

        while (pos < text.length) {
            skipWhitespace()
            if (pos >= text.length) break

            when (text[pos]) {
                '"' -> skipString()
                '{' -> {
                    pos++
                    // Check for explicit Map/Set by looking at what preceded (done by checking context)
                    contextStack.addLast(BraceContext.UNKNOWN)
                }
                '}' -> { pos++; if (contextStack.isNotEmpty()) contextStack.removeLast() }
                '[' -> { pos++; contextStack.addLast(BraceContext.ARRAY) }
                ']' -> { pos++; if (contextStack.isNotEmpty()) contextStack.removeLast() }
                '(' -> { pos++; contextStack.addLast(BraceContext.TUPLE) }
                ')' -> { pos++; if (contextStack.isNotEmpty()) contextStack.removeLast() }
                '/' -> {
                    // Skip regex
                    pos++
                    while (pos < text.length && text[pos] != '/') {
                        if (text[pos] == '\\') pos++
                        if (text[pos] == '[') {
                            pos++
                            while (pos < text.length && text[pos] != ']') { if (text[pos] == '\\') pos++; pos++ }
                        }
                        pos++
                    }
                    if (pos < text.length) pos++ // skip closing /
                    // skip flags
                    while (pos < text.length && text[pos].isLetter()) pos++
                }
                'b', 'x' -> {
                    val prefix = text[pos]
                    if (pos + 1 < text.length && text[pos + 1] == '"') {
                        pos++ // skip b/x
                        skipString() // skip binary string body (scanner doesn't validate here)
                    } else {
                        // Possibly an identifier
                        val identStart = pos
                        val ident = readIdentifier()
                        if (isInKeyPosition()) {
                            skipWhitespace()
                            if (pos < text.length && text[pos] == ':') {
                                result.add(UnquotedKey(ident, identStart, ident.length))
                                // Reclassify UNKNOWN -> OBJECT
                                if (contextStack.lastOrNull() == BraceContext.UNKNOWN) {
                                    contextStack[contextStack.size - 1] = BraceContext.OBJECT
                                }
                            }
                        }
                    }
                }
                '=' -> {
                    if (pos + 1 < text.length && text[pos + 1] == '>') {
                        // Arrow: reclassify UNKNOWN -> MAP
                        if (contextStack.lastOrNull() == BraceContext.UNKNOWN) {
                            contextStack[contextStack.size - 1] = BraceContext.MAP
                        }
                        pos += 2
                    } else pos++
                }
                else -> {
                    val ch = text[pos]
                    if (ch.isLetter() || ch == '_' || ch == '$') {
                        val identStart = pos
                        val ident = readIdentifier()
                        // Check if it's a known keyword (not an unquoted key)
                        val isKeyword = ident in setOf("null", "true", "false", "NaN", "Infinity", "Map", "Set")
                        if (!isKeyword && isInKeyPosition()) {
                            skipWhitespace()
                            if (pos < text.length && text[pos] == ':') {
                                result.add(UnquotedKey(ident, identStart, ident.length))
                                if (contextStack.lastOrNull() == BraceContext.UNKNOWN) {
                                    contextStack[contextStack.size - 1] = BraceContext.OBJECT
                                }
                            }
                        }
                    } else {
                        pos++
                    }
                }
            }
        }

        return result
    }

    /**
     * Scans text for invalid characters inside b"..." and x"..." binary literals.
     * Returns list of (offset, length, message, kind) for each invalid character.
     */
    fun scanBinaryErrors(text: String): List<BinaryCharError> {
        val result = mutableListOf<BinaryCharError>()
        var pos = 0

        val base64Valid = Regex("[A-Za-z0-9+/=]")
        val hexValid = Regex("[0-9A-Fa-f]")

        while (pos < text.length) {
            val ch = text[pos]
            if ((ch == 'b' || ch == 'x') && pos + 1 < text.length && text[pos + 1] == '"') {
                val kind = if (ch == 'b') BinaryEncoding.BASE64 else BinaryEncoding.HEX
                pos += 2 // skip prefix and opening quote
                while (pos < text.length && text[pos] != '"') {
                    val c = text[pos]
                    val valid = when (kind) {
                        BinaryEncoding.BASE64 -> base64Valid.matches(c.toString())
                        BinaryEncoding.HEX -> hexValid.matches(c.toString())
                    }
                    if (!valid) {
                        val msg = when (kind) {
                            BinaryEncoding.BASE64 -> "Invalid base64 character '$c'"
                            BinaryEncoding.HEX -> "Invalid hex character '$c'"
                        }
                        result.add(BinaryCharError(pos, 1, msg, kind))
                    }
                    pos++
                }
                if (pos < text.length) pos++ // skip closing quote
            } else if (ch == '"') {
                // Skip regular string
                pos++
                while (pos < text.length && text[pos] != '"') {
                    if (text[pos] == '\\') pos++
                    pos++
                }
                if (pos < text.length) pos++
            } else {
                pos++
            }
        }

        return result
    }
}
```

### Handling `Map{` and `Set{` prefixes

When the scanner sees the identifier `Map` or `Set` followed by `{`, the next `{` should be pushed as `EXPLICIT_MAP` or `EXPLICIT_SET` context respectively, not `UNKNOWN`. This prevents false positives for content inside explicit maps/sets. Update the scanner to track this:

```kotlin
// Before pushing UNKNOWN on '{':
val prevIdent = lastNonWhitespaceIdent // track as you scan
val ctx = when (prevIdent) {
    "Map" -> BraceContext.EXPLICIT_MAP
    "Set" -> BraceContext.EXPLICIT_SET
    else -> BraceContext.UNKNOWN
}
contextStack.addLast(ctx)
```

In `EXPLICIT_MAP` context, bare identifiers in key position are not flagged (Map keys can be any value type).

## Acceptance Criteria
- [ ] `scanUnquotedKeys("{foo: 1}")` returns `[UnquotedKey("foo", 1, 3)]`
- [ ] `scanUnquotedKeys("""{"foo": 1}""")` returns empty list (quoted key is valid)
- [ ] `scanUnquotedKeys("""{"k" => "v"}""")` returns empty list (map entry, not object key)
- [ ] `scanUnquotedKeys("Map{foo: 1}")` returns empty list (inside explicit Map, not object)
- [ ] `scanUnquotedKeys("[foo]")` returns empty list (array context, not object)
- [ ] `scanUnquotedKeys("{foo: 1, bar: 2}")` returns two entries
- [ ] `scanBinaryErrors("b\"SGVs!G8=\"")` returns one error at offset of `!`
- [ ] `scanBinaryErrors("x\"48g56\"")` returns one error at offset of `g`
- [ ] `scanBinaryErrors("b\"SGVsbG8=\"")` returns empty list (valid base64)
- [ ] Both functions handle empty string input without error

## Dependencies
- Depends on: task-001
- Blocks: task-014, task-015
