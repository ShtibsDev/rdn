package com.rdn.intellij.formatter

import com.rdn.intellij.formatter.cst.*

/**
 * Parses RDN text into a CST (Concrete Syntax Tree) with source positions.
 * Used by RdnCstFormatter for document formatting.
 * Ported from tools/prettier-plugin-rdn/src/parser.ts.
 */
class RdnCstParser(private val text: String) {
    private var pos = 0

    companion object {
        fun parse(text: String): DocumentNode {
            val parser = RdnCstParser(text)
            parser.skipWhitespace()
            val start = parser.pos
            val body = parser.parseValue()
            parser.skipWhitespace()
            return DocumentNode(body, start, parser.pos)
        }
    }

    private fun skipWhitespace() {
        while (pos < text.length && text[pos].isWhitespace()) pos++
    }

    private fun expect(ch: Char) {
        if (pos >= text.length || text[pos] != ch) {
            throw IllegalArgumentException("Expected '$ch' at position $pos, got '${text.getOrNull(pos)}'")
        }
        pos++
    }

    private fun parseValue(): RdnCstNode {
        skipWhitespace()
        if (pos >= text.length) throw IllegalArgumentException("Unexpected end of input at position $pos")
        return when (text[pos]) {
            '"' -> parseString()
            '{' -> parseBrace()
            '[' -> parseArray()
            '(' -> parseTuple()
            '@' -> parseAt()
            '/' -> parseRegExp()
            't' -> parseKeyword("true") { BooleanLiteralNode(true, it, it + 4) }
            'f' -> parseKeyword("false") { BooleanLiteralNode(false, it, it + 5) }
            'n' -> parseKeyword("null") { NullLiteralNode(it, it + 4) }
            'N' -> parseKeyword("NaN") { NaNLiteralNode(it, it + 3) }
            'I' -> parseKeyword("Infinity") { InfinityLiteralNode(false, it, it + 8) }
            '-' -> {
                if (text.startsWith("-Infinity", pos)) {
                    val start = pos; pos += 9
                    InfinityLiteralNode(true, start, pos)
                } else {
                    parseNumber()
                }
            }
            'b' -> if (pos + 1 < text.length && text[pos + 1] == '"') parseBinary(BinaryEncoding.BASE64) else parseNumber()
            'x' -> if (pos + 1 < text.length && text[pos + 1] == '"') parseBinary(BinaryEncoding.HEX) else parseNumber()
            'M' -> parseMapKeyword()
            'S' -> parseSetKeyword()
            else -> if (text[pos].isDigit() || text[pos] == '-') parseNumber()
                    else throw IllegalArgumentException("Unexpected character '${text[pos]}' at position $pos")
        }
    }

    private fun parseString(): StringLiteralNode {
        val start = pos
        expect('"')
        val sb = StringBuilder()
        while (pos < text.length && text[pos] != '"') {
            if (text[pos] == '\\') {
                sb.append(text[pos]); pos++
                if (pos < text.length) { sb.append(text[pos]); pos++ }
            } else {
                sb.append(text[pos]); pos++
            }
        }
        expect('"')
        val raw = text.substring(start, pos)
        val value = raw.removeSurrounding("\"")
            .replace("\\\"", "\"")
            .replace("\\\\", "\\")
            .replace("\\n", "\n")
            .replace("\\r", "\r")
            .replace("\\t", "\t")
        return StringLiteralNode(value = value, raw = raw, start = start, end = pos)
    }

    private fun parseNumber(): RdnCstNode {
        val start = pos
        if (pos < text.length && text[pos] == '-') pos++
        while (pos < text.length && text[pos].isDigit()) pos++
        val hasDot = pos < text.length && text[pos] == '.'
        if (hasDot) { pos++; while (pos < text.length && text[pos].isDigit()) pos++ }
        val hasExp = pos < text.length && (text[pos] == 'e' || text[pos] == 'E')
        if (hasExp) {
            pos++
            if (pos < text.length && (text[pos] == '+' || text[pos] == '-')) pos++
            while (pos < text.length && text[pos].isDigit()) pos++
        }
        val isBigInt = pos < text.length && text[pos] == 'n'
        if (isBigInt) {
            pos++
            return BigIntLiteralNode(raw = text.substring(start, pos), start = start, end = pos)
        }
        return NumberLiteralNode(raw = text.substring(start, pos), start = start, end = pos)
    }

    private fun parseBrace(): RdnCstNode {
        val start = pos
        expect('{')
        skipWhitespace()
        if (pos < text.length && text[pos] == '}') {
            pos++
            return ObjectNode(emptyList(), start, pos)
        }
        val firstValueStart = pos
        val firstValue = parseValue()
        skipWhitespace()
        return when {
            pos < text.length && text[pos] == ':' -> parseObjectRest(start, firstValue as? StringLiteralNode
                ?: throw IllegalArgumentException("Object key must be string at $firstValueStart"))
            pos < text.length && text.startsWith("=>", pos) -> parseMapRest(start, firstValue, explicit = false)
            else -> parseSetRest(start, firstValue, explicit = false)
        }
    }

    private fun parseObjectRest(start: Int, firstKey: StringLiteralNode): ObjectNode {
        expect(':')
        skipWhitespace()
        val firstValue = parseValue()
        val props = mutableListOf(ObjectPropertyNode(firstKey, firstValue, firstKey.start, firstValue.end))
        skipWhitespace()
        while (pos < text.length && text[pos] == ',') {
            pos++; skipWhitespace()
            val k = parseString(); skipWhitespace()
            expect(':'); skipWhitespace()
            val v = parseValue()
            props.add(ObjectPropertyNode(k, v, k.start, v.end))
            skipWhitespace()
        }
        expect('}')
        return ObjectNode(props, start, pos)
    }

    private fun parseMapRest(start: Int, firstKey: RdnCstNode, explicit: Boolean): MapNode {
        pos += 2 // skip =>
        skipWhitespace()
        val firstValue = parseValue()
        val entries = mutableListOf(MapEntryNode(firstKey, firstValue, firstKey.start, firstValue.end))
        skipWhitespace()
        while (pos < text.length && text[pos] == ',') {
            pos++; skipWhitespace()
            val k = parseValue(); skipWhitespace()
            if (!text.startsWith("=>", pos)) throw IllegalArgumentException("Expected => at $pos")
            pos += 2; skipWhitespace()
            val v = parseValue()
            entries.add(MapEntryNode(k, v, k.start, v.end))
            skipWhitespace()
        }
        expect('}')
        return MapNode(entries, explicit, start, pos)
    }

    private fun parseSetRest(start: Int, firstElement: RdnCstNode, explicit: Boolean): SetNode {
        val elements = mutableListOf(firstElement)
        while (pos < text.length && text[pos] == ',') {
            pos++; skipWhitespace()
            elements.add(parseValue()); skipWhitespace()
        }
        expect('}')
        return SetNode(elements, explicit, start, pos)
    }

    private fun parseArray(): ArrayNode {
        val start = pos; expect('['); skipWhitespace()
        if (pos < text.length && text[pos] == ']') { pos++; return ArrayNode(emptyList(), start, pos) }
        val elements = mutableListOf(parseValue()); skipWhitespace()
        while (pos < text.length && text[pos] == ',') { pos++; skipWhitespace(); elements.add(parseValue()); skipWhitespace() }
        expect(']')
        return ArrayNode(elements, start, pos)
    }

    private fun parseTuple(): TupleNode {
        val start = pos; expect('('); skipWhitespace()
        if (pos < text.length && text[pos] == ')') { pos++; return TupleNode(emptyList(), start, pos) }
        val elements = mutableListOf(parseValue()); skipWhitespace()
        while (pos < text.length && text[pos] == ',') { pos++; skipWhitespace(); elements.add(parseValue()); skipWhitespace() }
        expect(')')
        return TupleNode(elements, start, pos)
    }

    private fun parseAt(): RdnCstNode {
        val start = pos; pos++ // skip @
        val rest = text.substring(pos)
        return when {
            rest.startsWith("P") -> {
                while (pos < text.length && !text[pos].isWhitespace() && text[pos] != ',' && text[pos] != '}' && text[pos] != ']' && text[pos] != ')') pos++
                DurationLiteralNode(text.substring(start, pos), start, pos)
            }
            rest.matches(Regex("\\d{4}-.*")) -> {
                while (pos < text.length && !text[pos].isWhitespace() && text[pos] != ',' && text[pos] != '}' && text[pos] != ']' && text[pos] != ')') pos++
                DateTimeLiteralNode(text.substring(start, pos), start, pos)
            }
            rest.matches(Regex("\\d{2}:.*")) -> {
                while (pos < text.length && !text[pos].isWhitespace() && text[pos] != ',' && text[pos] != '}' && text[pos] != ']' && text[pos] != ')') pos++
                TimeOnlyLiteralNode(text.substring(start, pos), start, pos)
            }
            else -> {
                while (pos < text.length && text[pos].isDigit()) pos++
                DateTimeLiteralNode(text.substring(start, pos), start, pos)
            }
        }
    }

    private fun parseRegExp(): RegExpLiteralNode {
        val start = pos; pos++ // skip /
        while (pos < text.length) {
            when (text[pos]) {
                '\\' -> pos += 2
                '[' -> { pos++; while (pos < text.length && text[pos] != ']') { if (text[pos] == '\\') pos++; pos++ }; pos++ }
                '/' -> { pos++; break }
                else -> pos++
            }
        }
        while (pos < text.length && text[pos] in "dgimsuvy") pos++
        return RegExpLiteralNode(text.substring(start, pos), start, pos)
    }

    private fun parseBinary(encoding: BinaryEncoding): BinaryLiteralNode {
        val start = pos; pos++ // skip b/x
        expect('"')
        while (pos < text.length && text[pos] != '"') pos++
        expect('"')
        return BinaryLiteralNode(encoding, text.substring(start, pos), start, pos)
    }

    private fun parseMapKeyword(): MapNode {
        val start = pos
        pos += 3 // skip "Map"
        skipWhitespace(); expect('{'); skipWhitespace()
        if (pos < text.length && text[pos] == '}') { pos++; return MapNode(emptyList(), explicit = true, start = start, end = pos) }
        val firstKey = parseValue(); skipWhitespace()
        return parseMapRest(start, firstKey, explicit = true)
    }

    private fun parseSetKeyword(): SetNode {
        val start = pos
        pos += 3 // skip "Set"
        skipWhitespace(); expect('{'); skipWhitespace()
        if (pos < text.length && text[pos] == '}') { pos++; return SetNode(emptyList(), explicit = true, start = start, end = pos) }
        val firstElement = parseValue(); skipWhitespace()
        return parseSetRest(start, firstElement, explicit = true)
    }

    private fun <T : RdnCstNode> parseKeyword(keyword: String, make: (Int) -> T): T {
        val start = pos
        if (!text.startsWith(keyword, pos)) throw IllegalArgumentException("Expected '$keyword' at $pos")
        pos += keyword.length
        return make(start)
    }
}
