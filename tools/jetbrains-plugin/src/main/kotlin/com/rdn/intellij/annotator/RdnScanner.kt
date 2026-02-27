package com.rdn.intellij.annotator

/**
 * Lightweight scanner that detects unquoted object keys and invalid binary characters in RDN text.
 *
 * Faithfully ported from the TypeScript reference implementation at
 * `tools/vscode-extension/src/scanner.ts`.
 *
 * Does NOT fully parse RDN — it only tracks enough context to identify:
 * 1. Bare identifiers in object-key position (e.g. `{foo: 1}` instead of `{"foo": 1}`)
 * 2. Invalid characters inside `b"..."` (base64) and `x"..."` (hex) binary literals
 */

// ─── Data classes ────────────────────────────────────────────────────────────

data class UnquotedKey(val name: String, val offset: Int, val length: Int)

data class BinaryCharError(val offset: Int, val length: Int, val message: String, val kind: BinaryEncoding)

enum class BinaryEncoding { BASE64, HEX }

// ─── Brace context stack ─────────────────────────────────────────────────────

private enum class Ctx {
    /** `{` seen but not yet disambiguated */
    UnknownBrace,
    Object,
    Map,
    Set,
    ExplicitMap,
    ExplicitSet,
    Array,
    Tuple,
}

// ─── Identifier / keyword helpers ────────────────────────────────────────────

private val RDN_KEYWORDS = setOf("true", "false", "null", "NaN", "Infinity", "Map", "Set")

private fun isIdentStart(ch: Char): Boolean =
    ch in 'a'..'z' || ch in 'A'..'Z' || ch == '_' || ch == '$'

private fun isIdentChar(ch: Char): Boolean =
    isIdentStart(ch) || ch in '0'..'9'

private fun isWhitespace(ch: Char): Boolean =
    ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r'

// ─── Regex flag set (d g i m s u v y) ───────────────────────────────────────

private val REGEX_FLAGS = charArrayOf('d', 'g', 'i', 'm', 's', 'u', 'v', 'y')

private fun isRegexFlag(ch: Char): Boolean = ch in REGEX_FLAGS

// ═════════════════════════════════════════════════════════════════════════════
// scanUnquotedKeys
// ═════════════════════════════════════════════════════════════════════════════

/**
 * Scan [text] and return all unquoted keys found in object contexts.
 *
 * Maintains a stack of brace contexts to track whether we are inside an Object,
 * Map, Set, Array, or Tuple. Bare identifiers followed by `:` inside an Object
 * context (or an as-yet-unknown brace context that resolves to Object) are
 * reported — **unless** the identifier is an RDN keyword.
 */
fun scanUnquotedKeys(text: String): List<UnquotedKey> {
    val results = mutableListOf<UnquotedKey>()
    val stack = mutableListOf<Ctx>()
    var i = 0
    val len = text.length

    fun peek(): Char = if (i < len) text[i] else '\u0000'

    fun skipWhitespace() {
        while (i < len && isWhitespace(text[i])) i++
    }

    fun skipString() {
        // i is on the opening quote
        i++ // skip opening "
        while (i < len) {
            when (text[i]) {
                '\\' -> i += 2 // skip escape + next char
                '"' -> { i++; return } // skip closing "
                else -> i++
            }
        }
    }

    fun skipRegex() {
        // i is on the opening /
        i++ // skip opening /
        while (i < len) {
            when (text[i]) {
                '\\' -> i += 2
                '/' -> {
                    i++ // skip closing /
                    // skip flags
                    while (i < len && isRegexFlag(text[i])) i++
                    return
                }
                else -> i++
            }
        }
    }

    fun readIdent(): String {
        val start = i
        while (i < len && isIdentChar(text[i])) i++
        return text.substring(start, i)
    }

    fun currentCtx(): Ctx? = stack.lastOrNull()

    fun resolveUnknownBrace(to: Ctx) {
        val idx = stack.lastIndex
        if (idx >= 0 && stack[idx] == Ctx.UnknownBrace) {
            stack[idx] = to
        }
    }

    while (i < len) {
        skipWhitespace()
        if (i >= len) break

        val ch = text[i]
        val ctx = currentCtx()

        // ── Opening brackets ─────────────────────────────────────────────
        if (ch == '{') {
            // Check for Map{ or Set{ is handled below when we read an identifier
            stack.add(Ctx.UnknownBrace)
            i++
            continue
        }

        if (ch == '[') {
            stack.add(Ctx.Array)
            i++
            continue
        }

        if (ch == '(') {
            stack.add(Ctx.Tuple)
            i++
            continue
        }

        // ── Closing brackets ─────────────────────────────────────────────
        if (ch == '}' || ch == ']' || ch == ')') {
            if (ch == '}' && ctx == Ctx.UnknownBrace) {
                // Empty {} -> Object (no diagnostics needed for empty)
                resolveUnknownBrace(Ctx.Object)
            }
            if (stack.isNotEmpty()) stack.removeAt(stack.lastIndex)
            i++
            continue
        }

        // ── String ───────────────────────────────────────────────────────
        if (ch == '"') {
            skipString()

            // After a string in an unknown-brace context, look at what follows
            if (ctx == Ctx.UnknownBrace) {
                skipWhitespace()
                val next = peek()
                if (next == ':') {
                    resolveUnknownBrace(Ctx.Object)
                } else if (next == '=' && i + 1 < len && text[i + 1] == '>') {
                    resolveUnknownBrace(Ctx.Map)
                } else if (next == ',' || next == '}') {
                    resolveUnknownBrace(Ctx.Set)
                }
            }
            continue
        }

        // ── @ prefix — skip date/time/duration literals ──────────────────
        if (ch == '@') {
            i++ // skip @
            // Consume until whitespace or structural char
            while (i < len && !isWhitespace(text[i]) && text[i] !in charArrayOf(',', '}', ']', ')', '\r', '\n')) {
                i++
            }
            continue
        }

        // ── Binary: b" or x" ─────────────────────────────────────────────
        if ((ch == 'b' || ch == 'x') && i + 1 < len && text[i + 1] == '"') {
            i++ // skip prefix
            skipString()
            continue
        }

        // ── Regex: / ─────────────────────────────────────────────────────
        // Simple heuristic: / at start of value position is regex
        if (ch == '/') {
            skipRegex()
            continue
        }

        // ── Arrow => ─────────────────────────────────────────────────────
        if (ch == '=' && i + 1 < len && text[i + 1] == '>') {
            if (ctx == Ctx.UnknownBrace) {
                resolveUnknownBrace(Ctx.Map)
            }
            i += 2
            continue
        }

        // ── Colon : (object key-value separator) ─────────────────────────
        if (ch == ':') {
            if (ctx == Ctx.UnknownBrace) {
                resolveUnknownBrace(Ctx.Object)
            }
            i++
            continue
        }

        // ── Comma ────────────────────────────────────────────────────────
        if (ch == ',') {
            if (ctx == Ctx.UnknownBrace) {
                resolveUnknownBrace(Ctx.Set)
            }
            i++
            continue
        }

        // ── Numbers (including negative, BigInt, special) ────────────────
        if (ch == '-' || ch in '0'..'9') {
            if (ch == '-') i++
            while (i < len && (text[i] in '0'..'9' || text[i] == '.' || text[i] == 'e' || text[i] == 'E' || text[i] == '+' || text[i] == '-' || text[i] == 'n')) {
                i++
            }
            continue
        }

        // ── Identifiers (keywords, potential unquoted keys, Map/Set prefixes) ──
        if (isIdentStart(ch)) {
            val identStart = i
            val ident = readIdent()

            // Map{ or Set{ — push explicit context
            if ((ident == "Map" || ident == "Set") && i < len && text[i] == '{') {
                stack.add(if (ident == "Map") Ctx.ExplicitMap else Ctx.ExplicitSet)
                i++ // skip {
                continue
            }

            // -Infinity (the "Infinity" part after a minus sign was already consumed)
            if (ident == "Infinity") {
                continue
            }

            // RDN keywords — never unquoted keys
            if (ident in RDN_KEYWORDS) {
                // In unknown-brace, check what follows to disambiguate
                if (ctx == Ctx.UnknownBrace) {
                    skipWhitespace()
                    val next = peek()
                    if (next == ',' || next == '}') {
                        resolveUnknownBrace(Ctx.Set)
                    }
                }
                continue
            }

            // If we're in an object or unknown-brace context, and the next non-ws
            // char is :, this is an unquoted key
            skipWhitespace()
            val next = peek()

            if (next == ':') {
                // This identifier is used as a key
                if (ctx == Ctx.UnknownBrace) {
                    resolveUnknownBrace(Ctx.Object)
                }
                val resolvedCtx = currentCtx()
                if (resolvedCtx == Ctx.Object) {
                    results.add(UnquotedKey(name = ident, offset = identStart, length = ident.length))
                }
            } else if (ctx == Ctx.UnknownBrace) {
                if (next == '=' && i + 1 < len && text[i + 1] == '>') {
                    resolveUnknownBrace(Ctx.Map)
                } else if (next == ',' || next == '}') {
                    resolveUnknownBrace(Ctx.Set)
                }
            }
            continue
        }

        // ── Skip any other character ─────────────────────────────────────
        i++
    }

    return results
}

// ═════════════════════════════════════════════════════════════════════════════
// scanBinaryErrors
// ═════════════════════════════════════════════════════════════════════════════

private fun isBase64Char(ch: Char): Boolean =
    ch in 'A'..'Z' || ch in 'a'..'z' || ch in '0'..'9' || ch == '+' || ch == '/'

private fun isHexChar(ch: Char): Boolean =
    ch in '0'..'9' || ch in 'A'..'F' || ch in 'a'..'f'

/**
 * Scan [text] and return all invalid characters found inside `b"..."` and `x"..."` literals.
 *
 * Valid base64 content: `A-Z a-z 0-9 + /` (padding `=` only at the end).
 * Valid hex content: `0-9 A-F a-f`.
 */
fun scanBinaryErrors(text: String): List<BinaryCharError> {
    val results = mutableListOf<BinaryCharError>()
    var i = 0
    val len = text.length

    while (i < len) {
        val ch = text[i]

        // ── Binary literal: b" or x" ─────────────────────────────────────
        if ((ch == 'b' || ch == 'x') && i + 1 < len && text[i + 1] == '"') {
            val kind = if (ch == 'b') BinaryEncoding.BASE64 else BinaryEncoding.HEX
            val contentStart = i + 2 // after prefix + opening "

            // Find closing quote (handle escape sequences to find correct end)
            var j = contentStart
            while (j < len) {
                if (text[j] == '\\') { j += 2; continue }
                if (text[j] == '"') break
                j++
            }
            val contentEnd = j

            // Validate every character in the content
            if (kind == BinaryEncoding.HEX) {
                for (k in contentStart until contentEnd) {
                    val c = text[k]
                    if (!isHexChar(c)) {
                        results.add(BinaryCharError(offset = k, length = 1, message = "Invalid hex character '$c'", kind = BinaryEncoding.HEX))
                    }
                }
            } else {
                var foundPadding = false
                for (k in contentStart until contentEnd) {
                    val c = text[k]
                    if (c == '=') {
                        foundPadding = true
                    } else if (foundPadding) {
                        results.add(BinaryCharError(offset = k, length = 1, message = "Invalid base64: data after padding '='", kind = BinaryEncoding.BASE64))
                    } else if (!isBase64Char(c)) {
                        results.add(BinaryCharError(offset = k, length = 1, message = "Invalid base64 character '$c'", kind = BinaryEncoding.BASE64))
                    }
                }
            }

            i = if (contentEnd < len) contentEnd + 1 else contentEnd // past closing "
            continue
        }

        // ── Regular string — skip entirely to avoid false positives ──────
        if (ch == '"') {
            i++
            while (i < len) {
                if (text[i] == '\\') { i += 2; continue }
                if (text[i] == '"') { i++; break }
                i++
            }
            continue
        }

        // ── @ literal — skip ─────────────────────────────────────────────
        if (ch == '@') {
            i++
            while (i < len && text[i].let { it.isLetterOrDigit() || it in charArrayOf('.', ':', '-', '+', 'T', 'Z', 'P') }) i++
            continue
        }

        // ── Regex — skip ─────────────────────────────────────────────────
        if (ch == '/') {
            i++
            while (i < len) {
                if (text[i] == '\\') { i += 2; continue }
                if (text[i] == '/') {
                    i++
                    while (i < len && isRegexFlag(text[i])) i++
                    break
                }
                i++
            }
            continue
        }

        i++
    }

    return results
}
