# Task 015: Implement ExternalAnnotator for Diagnostics

## References
- [Tech Design](../tech-design.md) — Sections 3.3, 4 (decision #12), 6.4
- [Discovery](../discovery.md)

## Description
Create `RdnExternalAnnotator.kt` implementing a 3-pass diagnostic pipeline on a background thread. Pass 1: unquoted keys via `RdnScanner.scanUnquotedKeys()`. Pass 2: binary character errors via `RdnScanner.scanBinaryErrors()`. Pass 3: full parse via `RdnKotlinParser`. Parse errors on lines already covered by unquoted key diagnostics are suppressed. Attach quick fixes from task-016. Register in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/annotator/RdnExternalAnnotator.kt` — 3-pass diagnostic annotator
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register annotator

## Implementation Details

### Data classes for annotator pipeline

```kotlin
package com.rdn.intellij.annotator

import com.intellij.openapi.editor.Document

data class RdnAnnotatorInput(
    val text: String,
    val document: Document
)

data class RdnAnnotationItem(
    val startOffset: Int,
    val endOffset: Int,
    val message: String,
    val kind: AnnotationKind,
    val unquotedKeyName: String? = null  // non-null for unquoted key errors
)

enum class AnnotationKind { UNQUOTED_KEY, BINARY_CHAR, PARSE_ERROR }

data class RdnAnnotatorResult(
    val annotations: List<RdnAnnotationItem>
)
```

### `RdnExternalAnnotator.kt`

```kotlin
package com.rdn.intellij.annotator

import com.intellij.lang.annotation.AnnotationHolder
import com.intellij.lang.annotation.ExternalAnnotator
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiFile
import com.rdn.intellij.parser.RdnKotlinParser
import com.rdn.intellij.parser.RdnSyntaxError
import com.rdn.intellij.quickfix.WrapAllKeysInQuotesQuickFix
import com.rdn.intellij.quickfix.WrapKeyInQuotesQuickFix

class RdnExternalAnnotator : ExternalAnnotator<RdnAnnotatorInput, RdnAnnotatorResult>() {

    override fun collectInformation(file: PsiFile, editor: Editor, hasErrors: Boolean): RdnAnnotatorInput {
        val document = editor.document
        return RdnAnnotatorInput(text = document.text, document = document)
    }

    override fun doAnnotate(collectedInfo: RdnAnnotatorInput): RdnAnnotatorResult {
        val text = collectedInfo.text
        val annotations = mutableListOf<RdnAnnotationItem>()

        // Pass 1: Unquoted keys
        val unquotedKeys = RdnScanner.scanUnquotedKeys(text)
        for (key in unquotedKeys) {
            annotations.add(RdnAnnotationItem(
                startOffset = key.offset,
                endOffset = key.offset + key.length,
                message = "Unquoted key \"${key.name}\" — RDN requires all object keys to be quoted strings",
                kind = AnnotationKind.UNQUOTED_KEY,
                unquotedKeyName = key.name
            ))
        }

        // Pass 2: Binary character validation
        val binaryErrors = RdnScanner.scanBinaryErrors(text)
        for (err in binaryErrors) {
            annotations.add(RdnAnnotationItem(
                startOffset = err.offset,
                endOffset = err.offset + err.length,
                message = err.message,
                kind = AnnotationKind.BINARY_CHAR
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
                        kind = AnnotationKind.PARSE_ERROR
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
            val builder = holder.newAnnotation(HighlightSeverity.ERROR, item.message)
                .range(range)

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
```

### `plugin.xml` additions

```xml
<externalAnnotator
    language="RDN"
    implementationClass="com.rdn.intellij.annotator.RdnExternalAnnotator"/>
```

### Debounce behavior

IntelliJ's `ExternalAnnotator` framework handles debouncing automatically. The `collectInformation` method is called by the IDE framework on the EDT after a brief delay following user typing. The `doAnnotate` method is called on a background thread. No manual debounce timer is needed.

### Parse error suppression logic

When unquoted keys are present, the full parse (Pass 3) is skipped entirely. This is because unquoted keys will cause the Kotlin parser to fail with a syntax error (it expects quoted strings for object keys), and reporting both an unquoted key error AND a parse error for the same position would be redundant. Once the user fixes the unquoted keys, Pass 3 becomes active.

## Acceptance Criteria
- [ ] Opening a `.rdn` file with `{foo: 1}` shows a red squiggly under `foo` within 300ms of opening
- [ ] The error message reads: `Unquoted key "foo" — RDN requires all object keys to be quoted strings`
- [ ] Opening `b"SGVs!G8="` shows a red squiggly under `!`
- [ ] Opening `{trailing: 1,}` shows a parse error squiggly (trailing comma is invalid in RDN)
- [ ] A file with both unquoted keys and structural errors does NOT show duplicate parse errors on the same line as an unquoted key
- [ ] Diagnostics disappear immediately when the error is corrected and the IDE re-analyzes
- [ ] Alt+Enter on an unquoted key offers "Wrap in quotes" quick fix
- [ ] The annotator runs on a background thread (verify via `ApplicationManager.getApplication().isDispatchThread()` returning false inside `doAnnotate`)

## Dependencies
- Depends on: task-011, task-013
- Blocks: task-016
