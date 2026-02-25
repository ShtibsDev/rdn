package com.rdn.intellij.completion

import com.intellij.codeInsight.completion.*
import com.intellij.codeInsight.lookup.LookupElementBuilder
import com.intellij.patterns.PlatformPatterns
import com.intellij.util.ProcessingContext
import com.rdn.intellij.psi.RdnFile

class RdnCompletionContributor : CompletionContributor() {
    init {
        // Use file-based pattern instead of element language check, because
        // BAD_CHARACTER tokens (used for dummy identifier) have Language.ANY
        val anyRdnPosition = PlatformPatterns.psiElement().inFile(PlatformPatterns.psiFile(RdnFile::class.java))

        extend(CompletionType.BASIC, anyRdnPosition, SchemaCompletionProvider())
        extend(CompletionType.BASIC, anyRdnPosition, KeywordCompletionProvider())
        extend(CompletionType.BASIC, anyRdnPosition, SnippetCompletionProvider())
    }

    /**
     * Guard: suppress completions when cursor is inside a string literal.
     * Counts unescaped quote characters backwards on the current line;
     * if odd, we're inside a string.
     */
    private fun isInsideString(parameters: CompletionParameters): Boolean {
        val text = parameters.editor.document.text
        val offset = parameters.offset
        val lineStart = text.lastIndexOf('\n', offset - 1) + 1
        val lineText = text.substring(lineStart, offset)
        return hasOddQuotes(lineText)
    }

    /**
     * Guard: suppress completions when cursor is inside a value string
     * (i.e., a string that appears after a colon). Key strings are allowed.
     */
    private fun isInsideValueString(parameters: CompletionParameters): Boolean {
        val text = parameters.editor.document.text
        val offset = parameters.offset
        val lineStart = text.lastIndexOf('\n', offset - 1) + 1
        val lineText = text.substring(lineStart, offset)
        if (!hasOddQuotes(lineText)) return false
        // Find the opening quote position, then check if preceded by ':'
        val beforeCursor = text.substring(0, offset)
        val lastQuote = beforeCursor.lastIndexOf('"')
        if (lastQuote <= 0) return false
        val beforeQuote = text.substring(0, lastQuote).trimEnd()
        return beforeQuote.endsWith(':')
    }

    private fun hasOddQuotes(lineText: String): Boolean {
        var quoteCount = 0
        var i = 0
        while (i < lineText.length) {
            if (lineText[i] == '\\') { i += 2; continue }
            if (lineText[i] == '"') quoteCount++
            i++
        }
        return quoteCount % 2 != 0
    }

    private inner class SchemaCompletionProvider : CompletionProvider<CompletionParameters>() {
        override fun addCompletions(parameters: CompletionParameters, context: ProcessingContext, result: CompletionResultSet) {
            if (isInsideValueString(parameters)) return

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
        private val snippets = listOf(
            SnippetDef("@date", "Date literal", "@2024-01-15", "@".length),
            SnippetDef("@datetime", "DateTime literal", "@2024-01-15T10:30:00.000Z", "@".length),
            SnippetDef("@time", "TimeOnly literal", "@14:30:00", "@".length),
            SnippetDef("@duration", "Duration literal", "@P1D", "@P".length),
            SnippetDef("@unix", "Unix timestamp", "@1705276800", "@".length),
            SnippetDef("Map{}", "Map collection", "Map{}", 4),
            SnippetDef("Set{}", "Set collection", "Set{}", 4),
            SnippetDef("tuple()", "Tuple", "()", 1),
            SnippetDef("b\"\"", "Base64 binary", "b\"\"", 2),
            SnippetDef("x\"\"", "Hex binary", "x\"\"", 2),
            SnippetDef("//", "RegExp literal", "//", 1),
            SnippetDef("0n", "BigInt literal", "0n", 1),
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

    private data class SnippetDef(val trigger: String, val display: String, val text: String, val cursorOffset: Int)
}
