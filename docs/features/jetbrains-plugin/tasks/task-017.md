# Task 017: Implement Completion Contributor

## References
- [Tech Design](../tech-design.md) — Sections 3.5, 6.6
- [Discovery](../discovery.md)

## Description
Create `RdnCompletionContributor.kt` with three completion providers: (1) `$schema` at top-level object depth 1 in key position, (2) 11 keyword completions outside strings, and (3) 12 snippet completions with tab stops using `InsertHandler`. Implement a string-context guard that suppresses completions when the cursor is inside a string literal. Register in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/completion/RdnCompletionContributor.kt` — All three completion providers
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register extension

## Implementation Details

### `RdnCompletionContributor.kt`

```kotlin
package com.rdn.intellij.completion

import com.intellij.codeInsight.completion.*
import com.intellij.codeInsight.lookup.LookupElement
import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.intellij.openapi.editor.EditorModificationUtil
import com.intellij.patterns.PlatformPatterns
import com.intellij.util.ProcessingContext
import com.rdn.intellij.RdnLanguage

class RdnCompletionContributor : CompletionContributor() {
    init {
        // Register all three providers for any RDN file position
        val anyRdnPosition = PlatformPatterns.psiElement().withLanguage(RdnLanguage)

        extend(CompletionType.BASIC, anyRdnPosition, SchemaCompletionProvider())
        extend(CompletionType.BASIC, anyRdnPosition, KeywordCompletionProvider())
        extend(CompletionType.BASIC, anyRdnPosition, SnippetCompletionProvider())
    }

    /**
     * Guard: suppress all completions when cursor is inside a string literal.
     * Counts quote characters backwards on the current line; if odd, we're in a string.
     */
    private fun isInsideString(parameters: CompletionParameters): Boolean {
        val text = parameters.editor.document.text
        val offset = parameters.offset
        val lineStart = text.lastIndexOf('\n', offset - 1) + 1
        val lineText = text.substring(lineStart, offset)
        var quoteCount = 0
        var i = 0
        while (i < lineText.length) {
            when (lineText[i]) {
                '\\' -> i += 2 // skip escaped char
                '"' -> quoteCount++
                else -> i++
            }
            if (lineText[i - 1] != '\\') {} // already advanced
        }
        return quoteCount % 2 != 0
    }

    private inner class SchemaCompletionProvider : CompletionProvider<CompletionParameters>() {
        override fun addCompletions(parameters: CompletionParameters, context: ProcessingContext, result: CompletionResultSet) {
            if (isInsideString(parameters)) return

            val text = parameters.editor.document.text
            val offset = parameters.offset

            // Check we're at top-level object depth 1 (brace depth == 1)
            var depth = 0
            for (i in 0 until offset) {
                when (text[i]) {
                    '{', '[', '(' -> depth++
                    '}', ']', ')' -> depth--
                }
            }
            if (depth != 1) return

            // Check $schema doesn't already exist
            if (text.contains("\"\$schema\"")) return

            result.addElement(
                LookupElementBuilder.create("\"\$schema\"")
                    .withPresentableText("\$schema")
                    .withTailText(" — JSON Schema URL", true)
                    .withTypeText("string")
                    .withInsertHandler { ctx, _ ->
                        val editor = ctx.editor
                        val document = editor.document
                        val insertOffset = ctx.startOffset
                        document.replaceString(insertOffset, ctx.tailOffset, "\"\$schema\": \"\"")
                        editor.caretModel.moveToOffset(insertOffset + "\"\$schema\": \"".length)
                    }
            )
        }
    }

    private inner class KeywordCompletionProvider : CompletionProvider<CompletionParameters>() {
        private val keywords = listOf(
            Triple("true", "boolean", "Boolean true value"),
            Triple("false", "boolean", "Boolean false value"),
            Triple("null", "null", "Null value"),
            Triple("NaN", "number", "IEEE 754 Not-a-Number"),
            Triple("Infinity", "number", "IEEE 754 positive infinity"),
            Triple("-Infinity", "number", "IEEE 754 negative infinity"),
            Triple("Map", "collection", "Map collection keyword"),
            Triple("Set", "collection", "Set collection keyword"),
            Triple("@", "datetime", "Date/time/duration literal prefix"),
            Triple("b", "binary", "Base64 binary literal prefix"),
            Triple("x", "binary", "Hex binary literal prefix"),
        )

        override fun addCompletions(parameters: CompletionParameters, context: ProcessingContext, result: CompletionResultSet) {
            if (isInsideString(parameters)) return

            for ((keyword, type, doc) in keywords) {
                result.addElement(
                    LookupElementBuilder.create(keyword)
                        .withTypeText(type)
                        .withTailText(" — $doc", true)
                        .bold()
                )
            }
        }
    }

    private inner class SnippetCompletionProvider : CompletionProvider<CompletionParameters>() {
        // Each entry: (trigger text, display text, inserted text, cursor offset after insert)
        // cursor offset -1 means end of inserted text
        private val snippets = listOf(
            SnippetDef("@date", "Date literal", "@2024-01-15", "@".length),
            SnippetDef("@datetime", "DateTime literal", "@2024-01-15T10:30:00.000Z", "@".length),
            SnippetDef("@time", "TimeOnly literal", "@14:30:00", "@".length),
            SnippetDef("@duration", "Duration literal", "@P1D", "@P".length),
            SnippetDef("@unix", "Unix timestamp", "@1705276800", "@".length),
            SnippetDef("Map{}", "Map collection", "Map{}", 4),       // cursor after {
            SnippetDef("Set{}", "Set collection", "Set{}", 4),       // cursor after {
            SnippetDef("tuple()", "Tuple", "()", 1),                  // cursor inside ()
            SnippetDef("b\"\"", "Base64 binary", "b\"\"", 2),         // cursor inside ""
            SnippetDef("x\"\"", "Hex binary", "x\"\"", 2),            // cursor inside ""
            SnippetDef("//", "RegExp literal", "//", 1),               // cursor after first /
            SnippetDef("0n", "BigInt literal", "0n", 1),               // cursor on digit
        )

        override fun addCompletions(parameters: CompletionParameters, context: ProcessingContext, result: CompletionResultSet) {
            if (isInsideString(parameters)) return

            for (snippet in snippets) {
                result.addElement(
                    LookupElementBuilder.create(snippet.trigger)
                        .withPresentableText(snippet.display)
                        .withTypeText("snippet")
                        .withInsertHandler { ctx, _ ->
                            val editor = ctx.editor
                            val document = editor.document
                            val start = ctx.startOffset
                            document.replaceString(start, ctx.tailOffset, snippet.text)
                            editor.caretModel.moveToOffset(start + snippet.cursorOffset)
                        }
                )
            }
        }
    }

    private data class SnippetDef(
        val trigger: String,
        val display: String,
        val text: String,
        val cursorOffset: Int
    )
}
```

### `plugin.xml` additions

```xml
<completion.contributor
    language="RDN"
    implementationClass="com.rdn.intellij.completion.RdnCompletionContributor"
    order="first"/>
```

### Notes on `$schema` detection

The `$schema` check uses a simple `contains("\"\$schema\"")` scan of the full document text. A more robust implementation would check the PSI tree for an `RdnObjectProperty` with key `$schema` at the root level. The simpler approach is acceptable for the initial implementation.

### Notes on brace depth calculation

The depth calculation iterates over all characters before the cursor. This is O(n) on document size. For large files, optimize by using the PSI tree to find the containing `RdnObject` node and check its depth, or cache the result. The naive O(n) approach is acceptable for initial implementation.

## Acceptance Criteria
- [ ] Pressing Ctrl+Space in an empty top-level object offers `$schema`
- [ ] `$schema` completion inserts `"$schema": ""` with cursor inside the URL quotes
- [ ] `$schema` is NOT offered when `"$schema"` already exists in the document
- [ ] `$schema` is NOT offered inside a nested object (brace depth > 1)
- [ ] Pressing Ctrl+Space in value position offers `true`, `false`, `null`, `NaN`, `Infinity`, `-Infinity`
- [ ] Pressing Ctrl+Space offers `Map`, `Set`, `@`, `b`, `x` keywords
- [ ] Pressing Ctrl+Space offers all 12 snippets
- [ ] No completions are offered when the cursor is inside a string literal `"..."`
- [ ] Snippet `Map{}` positions cursor after the `{`
- [ ] Snippet `b""` positions cursor between the quotes

## Dependencies
- Depends on: task-002
- Blocks: task-018
