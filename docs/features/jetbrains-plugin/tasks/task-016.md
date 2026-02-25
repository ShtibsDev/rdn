# Task 016: Implement Quick Fixes

## References
- [Tech Design](../tech-design.md) — Sections 3.4, 6.5
- [Discovery](../discovery.md)

## Description
Create two `LocalQuickFix` implementations: `WrapKeyInQuotesQuickFix` for single-key fixes and `WrapAllKeysInQuotesQuickFix` for bulk fixes. Both are attached to unquoted key annotations by the `ExternalAnnotator`. The bulk fix only appears when 2 or more unquoted keys exist. Fixes apply replacements in reverse offset order to preserve text positions during multi-edit operations.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/quickfix/WrapKeyInQuotesQuickFix.kt` — Single-key quick fix
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/quickfix/WrapAllKeysInQuotesQuickFix.kt` — Bulk quick fix

## Implementation Details

### `WrapKeyInQuotesQuickFix.kt`

```kotlin
package com.rdn.intellij.quickfix

import com.intellij.codeInspection.LocalQuickFix
import com.intellij.codeInspection.ProblemDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiDocumentManager

class WrapKeyInQuotesQuickFix(
    private val keyName: String,
    private val keyOffset: Int
) : LocalQuickFix {

    override fun getName(): String = "Wrap \"$keyName\" in quotes"

    override fun getFamilyName(): String = "Wrap key in quotes"

    override fun applyFix(project: Project, descriptor: ProblemDescriptor) {
        val element = descriptor.psiElement ?: return
        val file = element.containingFile ?: return
        val document = PsiDocumentManager.getInstance(project).getDocument(file) ?: return

        val range = TextRange(keyOffset, keyOffset + keyName.length)
        val currentText = document.getText(range)

        if (currentText == keyName) {
            document.replaceString(range.startOffset, range.endOffset, "\"$keyName\"")
            PsiDocumentManager.getInstance(project).commitDocument(document)
        }
    }
}
```

### `WrapAllKeysInQuotesQuickFix.kt`

```kotlin
package com.rdn.intellij.quickfix

import com.intellij.codeInspection.LocalQuickFix
import com.intellij.codeInspection.ProblemDescriptor
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiDocumentManager

/**
 * Wraps all unquoted keys in the document with double quotes.
 * Applies replacements in REVERSE offset order to preserve text positions.
 *
 * @param keys List of (keyName, startOffset, length) triples for all unquoted keys.
 */
class WrapAllKeysInQuotesQuickFix(
    private val keys: List<Triple<String, Int, Int>>
) : LocalQuickFix {

    override fun getName(): String = "Wrap all unquoted keys in quotes (${keys.size} keys)"

    override fun getFamilyName(): String = "Wrap all keys in quotes"

    override fun applyFix(project: Project, descriptor: ProblemDescriptor) {
        val element = descriptor.psiElement ?: return
        val file = element.containingFile ?: return
        val document = PsiDocumentManager.getInstance(project).getDocument(file) ?: return

        // Apply fixes in reverse offset order to avoid invalidating positions
        val sortedKeys = keys.sortedByDescending { it.second }

        for ((keyName, startOffset, length) in sortedKeys) {
            val range = document.getText().let { text ->
                if (startOffset + length <= text.length && text.substring(startOffset, startOffset + length) == keyName) {
                    startOffset to startOffset + length
                } else null
            } ?: continue

            document.replaceString(range.first, range.second, "\"$keyName\"")
        }

        PsiDocumentManager.getInstance(project).commitDocument(document)
    }
}
```

### Integration with ExternalAnnotator

In `RdnExternalAnnotator.apply()` (task-015), the quick fixes are attached:

```kotlin
// Single fix — always offered
builder.withFix(WrapKeyInQuotesQuickFix(keyName, item.startOffset))

// Bulk fix — only when 2+ unquoted keys exist
if (unquotedKeyAnnotations.size >= 2) {
    builder.withFix(WrapAllKeysInQuotesQuickFix(
        unquotedKeyAnnotations.map { Triple(it.unquotedKeyName!!, it.startOffset, it.endOffset - it.startOffset) }
    ))
}
```

### Design notes

- **Reverse-order application:** When multiple replacements are applied to a document, applying them from last to first ensures earlier offsets remain valid. For example, replacing `bar` at offset 20 before `foo` at offset 5 avoids shifting offset 5 when offset 20 changes.
- **Text verification:** Before replacing, verify the text at `[startOffset, startOffset+length)` still equals `keyName`. If the document was modified between `doAnnotate` and `applyFix`, skip stale replacements gracefully.
- **`commitDocument`:** Call `PsiDocumentManager.commitDocument()` after all replacements to resynchronize the PSI tree with the new document state.

## Acceptance Criteria
- [ ] Alt+Enter on `foo` in `{foo: 1}` offers "Wrap \"foo\" in quotes"
- [ ] Applying single fix changes `{foo: 1}` to `{"foo": 1}`
- [ ] When 2+ unquoted keys exist, Alt+Enter also offers "Wrap all unquoted keys in quotes (N keys)"
- [ ] Applying bulk fix on `{foo: 1, bar: 2}` produces `{"foo": 1, "bar": 2}`
- [ ] After applying a single fix, the remaining unquoted key diagnostic is still visible
- [ ] After applying the bulk fix, all diagnostics disappear
- [ ] Fixes appear in the gutter light bulb as well as Alt+Enter menu
- [ ] `getFamilyName()` returns a constant string (required for fix grouping)

## Dependencies
- Depends on: task-015
- Blocks: None
