package com.rdn.intellij.quickfix

import com.intellij.codeInsight.intention.IntentionAction
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiDocumentManager
import com.intellij.psi.PsiFile

/**
 * Wraps all unquoted keys in the document with double quotes.
 * Applies replacements in REVERSE offset order to preserve text positions.
 *
 * @param keys List of (keyName, startOffset, length) triples for all unquoted keys.
 */
class WrapAllKeysInQuotesQuickFix(
    private val keys: List<Triple<String, Int, Int>>
) : IntentionAction {

    override fun getText(): String = "Wrap all unquoted keys in quotes (${keys.size} keys)"

    override fun getFamilyName(): String = "Wrap all keys in quotes"

    override fun isAvailable(project: Project, editor: Editor?, file: PsiFile?): Boolean = true

    override fun invoke(project: Project, editor: Editor?, file: PsiFile?) {
        if (file == null) return
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

    override fun startInWriteAction(): Boolean = true
}
