package com.rdn.intellij.formatter

import com.rdn.intellij.formatter.cst.*

/**
 * Formatting options for the RDN CST formatter.
 */
data class RdnFormatOptions(
    val tabSize: Int = 2,
    val insertSpaces: Boolean = true,
    val printWidth: Int = 80,
    val useExplicitMapKeyword: Boolean = false,
    val useExplicitSetKeyword: Boolean = false
)

/**
 * CST-based formatter for RDN documents.
 *
 * Parses text into CST nodes, then pretty-prints them with configurable
 * indentation and line-width. Falls back to compact (single-line) rendering
 * when the result fits within [RdnFormatOptions.printWidth].
 */
object RdnCstFormatter {

    /**
     * Format the given RDN [text]. Returns the original text unchanged
     * if parsing fails.
     */
    fun format(text: String, opts: RdnFormatOptions = RdnFormatOptions()): String {
        return try {
            val doc = RdnCstParser.parse(text)
            val indent = if (opts.insertSpaces) " ".repeat(opts.tabSize) else "\t"
            printNode(doc.body, "", indent, opts) + "\n"
        } catch (_: Exception) {
            text
        }
    }

    /**
     * Format with object keys sorted alphabetically. Returns `null` if
     * parsing fails.
     */
    fun formatSorted(text: String, opts: RdnFormatOptions = RdnFormatOptions()): String? {
        return try {
            val doc = RdnCstParser.parse(text)
            val sorted = sortNode(doc.body)
            val indent = if (opts.insertSpaces) " ".repeat(opts.tabSize) else "\t"
            printNode(sorted, "", indent, opts) + "\n"
        } catch (_: Exception) {
            null
        }
    }

    // ------------------------------------------------------------------
    // Compact (single-line) rendering
    // ------------------------------------------------------------------

    /**
     * Try to render [node] on a single line. Returns `null` when the
     * result would exceed [RdnFormatOptions.printWidth].
     */
    private fun printCompact(node: RdnCstNode, opts: RdnFormatOptions): String? {
        val compact = when (node) {
            is DocumentNode -> printCompact(node.body, opts)
            is NullLiteralNode -> "null"
            is BooleanLiteralNode -> if (node.value) "true" else "false"
            is NaNLiteralNode -> "NaN"
            is InfinityLiteralNode -> if (node.negative) "-Infinity" else "Infinity"
            is NumberLiteralNode -> node.raw
            is BigIntLiteralNode -> node.raw
            is StringLiteralNode -> node.raw
            is DateTimeLiteralNode -> node.raw
            is TimeOnlyLiteralNode -> node.raw
            is DurationLiteralNode -> node.raw
            is BinaryLiteralNode -> node.raw
            is RegExpLiteralNode -> node.raw
            is ArrayNode -> {
                if (node.elements.isEmpty()) return "[]"
                val elements = node.elements.map { printCompact(it, opts) ?: return null }
                "[${elements.joinToString(", ")}]"
            }
            is TupleNode -> {
                if (node.elements.isEmpty()) return "()"
                val elements = node.elements.map { printCompact(it, opts) ?: return null }
                "(${elements.joinToString(", ")})"
            }
            is ObjectNode -> {
                if (node.properties.isEmpty()) return "{}"
                val props = node.properties.map { prop ->
                    "${prop.key.raw}: ${printCompact(prop.value, opts) ?: return null}"
                }
                "{${props.joinToString(", ")}}"
            }
            is MapNode -> {
                val prefix = if (node.explicit || opts.useExplicitMapKeyword) "Map" else ""
                if (node.entries.isEmpty()) return "${prefix}{}"
                val entries = node.entries.map { entry ->
                    "${printCompact(entry.key, opts) ?: return null} => ${printCompact(entry.value, opts) ?: return null}"
                }
                "${prefix}{${entries.joinToString(", ")}}"
            }
            is SetNode -> {
                val prefix = if (node.explicit || opts.useExplicitSetKeyword) "Set" else ""
                if (node.elements.isEmpty()) return "${prefix}{}"
                val elements = node.elements.map { printCompact(it, opts) ?: return null }
                "${prefix}{${elements.joinToString(", ")}}"
            }
            is ObjectPropertyNode -> "${node.key.raw}: ${printCompact(node.value, opts) ?: return null}"
            is MapEntryNode -> "${printCompact(node.key, opts) ?: return null} => ${printCompact(node.value, opts) ?: return null}"
        } ?: return null

        return if (compact.length <= opts.printWidth) compact else null
    }

    // ------------------------------------------------------------------
    // Multi-line (expanded) rendering
    // ------------------------------------------------------------------

    private fun printNode(node: RdnCstNode, indent: String, indentUnit: String, opts: RdnFormatOptions): String {
        // Try compact first — if it fits, use it
        val compact = printCompact(node, opts)
        if (compact != null) return compact

        val nextIndent = indent + indentUnit
        return when (node) {
            is ArrayNode -> {
                val elements = node.elements.joinToString(",\n$nextIndent") { printNode(it, nextIndent, indentUnit, opts) }
                "[\n$nextIndent$elements\n$indent]"
            }
            is TupleNode -> {
                val elements = node.elements.joinToString(",\n$nextIndent") { printNode(it, nextIndent, indentUnit, opts) }
                "(\n$nextIndent$elements\n$indent)"
            }
            is ObjectNode -> {
                val props = node.properties.joinToString(",\n$nextIndent") { prop ->
                    "${prop.key.raw}: ${printNode(prop.value, nextIndent, indentUnit, opts)}"
                }
                "{\n$nextIndent$props\n$indent}"
            }
            is MapNode -> {
                val prefix = if (node.explicit || opts.useExplicitMapKeyword) "Map" else ""
                val entries = node.entries.joinToString(",\n$nextIndent") { entry ->
                    "${printNode(entry.key, nextIndent, indentUnit, opts)} => ${printNode(entry.value, nextIndent, indentUnit, opts)}"
                }
                "${prefix}{\n$nextIndent$entries\n$indent}"
            }
            is SetNode -> {
                val prefix = if (node.explicit || opts.useExplicitSetKeyword) "Set" else ""
                val elements = node.elements.joinToString(",\n$nextIndent") { printNode(it, nextIndent, indentUnit, opts) }
                "${prefix}{\n$nextIndent$elements\n$indent}"
            }
            is DocumentNode -> printNode(node.body, indent, indentUnit, opts)
            // Atomic literal nodes always fit in printCompact, so this
            // branch should never be reached. Guard with printCompact fallback.
            else -> printCompact(node, opts) ?: ""
        }
    }

    // ------------------------------------------------------------------
    // Key sorting (recursive, objects only)
    // ------------------------------------------------------------------

    private fun sortNode(node: RdnCstNode): RdnCstNode = when (node) {
        is ObjectNode -> {
            val sortedProps = node.properties
                .map { it.copy(value = sortNode(it.value) as RdnCstNode, key = it.key) }
                .sortedBy { it.key.value }
            node.copy(properties = sortedProps)
        }
        is ArrayNode -> node.copy(elements = node.elements.map { sortNode(it) })
        is TupleNode -> node.copy(elements = node.elements.map { sortNode(it) })
        is MapNode -> node.copy(entries = node.entries.map { it.copy(key = sortNode(it.key), value = sortNode(it.value)) })
        is SetNode -> node.copy(elements = node.elements.map { sortNode(it) })
        is DocumentNode -> node.copy(body = sortNode(node.body))
        is ObjectPropertyNode -> node.copy(value = sortNode(node.value))
        is MapEntryNode -> node.copy(key = sortNode(node.key), value = sortNode(node.value))
        else -> node
    }
}
