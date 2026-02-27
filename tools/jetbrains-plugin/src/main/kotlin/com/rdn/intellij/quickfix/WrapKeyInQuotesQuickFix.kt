package com.rdn.intellij.quickfix

import com.intellij.codeInsight.intention.IntentionAction
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiDocumentManager
import com.intellij.psi.PsiFile

class WrapKeyInQuotesQuickFix(
    private val keyName: String,
    private val keyOffset: Int
) : IntentionAction {

    override fun getText(): String = "Wrap \"$keyName\" in quotes"

    override fun getFamilyName(): String = "Wrap key in quotes"

    override fun isAvailable(project: Project, editor: Editor?, file: PsiFile?): Boolean = true

    override fun invoke(project: Project, editor: Editor?, file: PsiFile?) {
        if (file == null) return
        val document = PsiDocumentManager.getInstance(project).getDocument(file) ?: return

        val range = TextRange(keyOffset, keyOffset + keyName.length)
        val currentText = document.getText(range)

        if (currentText == keyName) {
            document.replaceString(range.startOffset, range.endOffset, "\"$keyName\"")
            PsiDocumentManager.getInstance(project).commitDocument(document)
        }
    }

    override fun startInWriteAction(): Boolean = true
}
