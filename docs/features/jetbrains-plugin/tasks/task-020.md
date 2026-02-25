# Task 020: Implement CST Formatter

## References
- [Tech Design](../tech-design.md) — Sections 3.7, 6.7
- [Discovery](../discovery.md)

## Description
Port `formatter.ts` from the VSCode extension to `RdnCstFormatter.kt`. Implements `format()` and `formatSorted()` with compact/multi-line printing logic, 80-character line width threshold, configurable tab size and spaces/tabs preference, and `useExplicitMapKeyword`/`useExplicitSetKeyword` settings. Returns original text unchanged if parsing fails.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/formatter/RdnCstFormatter.kt` — CST-based formatter

## Implementation Details

### `RdnFormatOptions` data class

```kotlin
package com.rdn.intellij.formatter

data class RdnFormatOptions(
    val tabSize: Int = 2,
    val insertSpaces: Boolean = true,
    val printWidth: Int = 80,
    val useExplicitMapKeyword: Boolean = false,
    val useExplicitSetKeyword: Boolean = false
)
```

### `RdnCstFormatter.kt`

```kotlin
package com.rdn.intellij.formatter

import com.rdn.intellij.formatter.cst.*

object RdnCstFormatter {
    /**
     * Format RDN text. Returns original text if parsing fails.
     */
    fun format(text: String, opts: RdnFormatOptions = RdnFormatOptions()): String {
        return try {
            val doc = RdnCstParser.parse(text)
            val indent = if (opts.insertSpaces) " ".repeat(opts.tabSize) else "\t"
            printNode(doc.body, "", indent, opts) + "\n"
        } catch (e: Exception) {
            text // Return unchanged on parse failure
        }
    }

    /**
     * Format and sort all object keys alphabetically. Returns null if parsing fails.
     */
    fun formatSorted(text: String, opts: RdnFormatOptions = RdnFormatOptions()): String? {
        return try {
            val doc = RdnCstParser.parse(text)
            val sorted = sortNode(doc.body)
            val indent = if (opts.insertSpaces) " ".repeat(opts.tabSize) else "\t"
            printNode(sorted, "", indent, opts) + "\n"
        } catch (e: Exception) {
            null
        }
    }

    /**
     * Attempt compact (single-line) rendering. Returns null if exceeds printWidth.
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

    /**
     * Print a node with indentation. Tries compact first; falls back to multi-line.
     */
    private fun printNode(node: RdnCstNode, indent: String, indentUnit: String, opts: RdnFormatOptions): String {
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
            else -> printCompact(node, opts) ?: node.let { text.substring(it.start, it.end) }
        }
    }

    /**
     * Recursively sort all ObjectNode properties alphabetically by key.
     */
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
```

**Implementation note:** The `printNode` function for the fallback `else` branch references a `text` variable that doesn't exist at that scope. Fix by passing the original source text through the call chain, or by deriving text from `node.raw` where available. The cleanest approach is to make atomic literal nodes always return from `printCompact` (they always fit on one line), so the `else` branch is only reached for collection types.

## Acceptance Criteria
- [ ] `RdnCstFormatter.format("{\"a\":1,\"b\":2}")` produces `{"a": 1, "b": 2}\n`
- [ ] A deeply nested object that exceeds 80 characters is expanded to multi-line format
- [ ] `format("invalid!!!")` returns `"invalid!!!"` unchanged (parse failure fallback)
- [ ] `formatSorted("""{"z": 1, "a": 2}""")` produces a document with `"a"` before `"z"`
- [ ] `formatSorted("invalid")` returns `null`
- [ ] Tab indentation: `RdnFormatOptions(insertSpaces=false)` produces tab-indented output
- [ ] `useExplicitMapKeyword=true` emits `Map{...}` for implicit maps
- [ ] `useExplicitSetKeyword=true` emits `Set{...}` for implicit sets
- [ ] Trailing newline is always appended to formatted output
- [ ] Empty `{}` formats as `{}` (not `{\n}`)

## Dependencies
- Depends on: task-019
- Blocks: task-021, task-022, task-029
