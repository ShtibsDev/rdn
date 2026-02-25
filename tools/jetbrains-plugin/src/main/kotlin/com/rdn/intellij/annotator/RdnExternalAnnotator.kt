package com.rdn.intellij.annotator

import com.intellij.lang.annotation.AnnotationHolder
import com.intellij.lang.annotation.ExternalAnnotator
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.openapi.editor.Document
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiFile
import com.rdn.intellij.parser.RdnKotlinParser
import com.rdn.intellij.parser.RdnSyntaxError
import com.rdn.intellij.quickfix.WrapAllKeysInQuotesQuickFix
import com.rdn.intellij.quickfix.WrapKeyInQuotesQuickFix

// ─── Data classes for annotator pipeline ─────────────────────────────────────

data class RdnAnnotatorInput(val text: String, val document: Document)

data class RdnAnnotationItem(
    val startOffset: Int,
    val endOffset: Int,
    val message: String,
    val kind: AnnotationKind,
    val unquotedKeyName: String? = null,
)

enum class AnnotationKind { UNQUOTED_KEY, BINARY_CHAR, PARSE_ERROR }

data class RdnAnnotatorResult(val annotations: List<RdnAnnotationItem>)

// ─── 3-pass ExternalAnnotator ────────────────────────────────────────────────

class RdnExternalAnnotator : ExternalAnnotator<RdnAnnotatorInput, RdnAnnotatorResult>() {

    override fun collectInformation(file: PsiFile, editor: Editor, hasErrors: Boolean): RdnAnnotatorInput {
        val document = editor.document
        return RdnAnnotatorInput(text = document.text, document = document)
    }

    override fun doAnnotate(collectedInfo: RdnAnnotatorInput): RdnAnnotatorResult {
        val text = collectedInfo.text
        val annotations = mutableListOf<RdnAnnotationItem>()

        // Pass 1: Unquoted keys
        val unquotedKeys = scanUnquotedKeys(text)
        for (key in unquotedKeys) {
            annotations.add(RdnAnnotationItem(
                startOffset = key.offset,
                endOffset = key.offset + key.length,
                message = "Unquoted key \"${key.name}\" \u2014 RDN requires all object keys to be quoted strings",
                kind = AnnotationKind.UNQUOTED_KEY,
                unquotedKeyName = key.name,
            ))
        }

        // Pass 2: Binary character validation
        val binaryErrors = scanBinaryErrors(text)
        for (err in binaryErrors) {
            annotations.add(RdnAnnotationItem(
                startOffset = err.offset,
                endOffset = err.offset + err.length,
                message = err.message,
                kind = AnnotationKind.BINARY_CHAR,
            ))
        }

        // Pass 3: Full parse validation
        // Collect lines covered by unquoted key errors to suppress duplicate parse errors
        val unquotedKeyLines = unquotedKeys.map { key ->
            collectedInfo.document.getLineNumber(key.offset)
        }.toSet()

        if (unquotedKeys.isEmpty()) {
            // Only run full parse if no unquoted keys (they would cause parse errors anyway)
            try {
                RdnKotlinParser.parse(text)
            } catch (e: RdnSyntaxError) {
                val errorLine = collectedInfo.document.getLineNumber(e.offset.coerceIn(0, text.length - 1))
                if (errorLine !in unquotedKeyLines) {
                    val start = e.offset.coerceIn(0, maxOf(0, text.length - 1))
                    val end = minOf(start + 1, text.length)
                    annotations.add(RdnAnnotationItem(
                        startOffset = start,
                        endOffset = end,
                        message = e.message ?: "Syntax error",
                        kind = AnnotationKind.PARSE_ERROR,
                    ))
                }
            }
        }

        return RdnAnnotatorResult(annotations)
    }

    override fun apply(file: PsiFile, annotationResult: RdnAnnotatorResult, holder: AnnotationHolder) {
        val unquotedKeyAnnotations = annotationResult.annotations.filter { it.kind == AnnotationKind.UNQUOTED_KEY }

        for (item in annotationResult.annotations) {
            val range = TextRange(item.startOffset, item.endOffset)
            val builder = holder.newAnnotation(HighlightSeverity.ERROR, item.message).range(range)

            when (item.kind) {
                AnnotationKind.UNQUOTED_KEY -> {
                    val keyName = item.unquotedKeyName!!
                    builder.withFix(WrapKeyInQuotesQuickFix(keyName, item.startOffset))
                    if (unquotedKeyAnnotations.size >= 2) {
                        builder.withFix(WrapAllKeysInQuotesQuickFix(
                            unquotedKeyAnnotations.map { Triple(it.unquotedKeyName!!, it.startOffset, it.endOffset - it.startOffset) }
                        ))
                    }
                }
                AnnotationKind.BINARY_CHAR -> { /* no quick fix for binary chars */ }
                AnnotationKind.PARSE_ERROR -> { /* no quick fix for generic parse errors */ }
            }

            builder.create()
        }
    }
}
