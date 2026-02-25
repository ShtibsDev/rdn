package com.rdn.intellij.actions

import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.command.WriteCommandAction
import com.intellij.psi.PsiDocumentManager
import com.rdn.intellij.RdnLanguage
import com.rdn.intellij.formatter.RdnCstFormatter
import com.rdn.intellij.formatter.RdnFormatOptions
import com.rdn.intellij.settings.RdnSettingsState

class SortDocumentKeysAction : AnAction("Sort Document Keys") {
    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val editor = e.getData(CommonDataKeys.EDITOR) ?: return
        val document = editor.document
        val psiFile = PsiDocumentManager.getInstance(project).getPsiFile(document) ?: return
        if (psiFile.language != RdnLanguage) return
        val settings = RdnSettingsState.getInstance(project)
        val opts = RdnFormatOptions(tabSize = editor.settings.getTabSize(project), insertSpaces = !editor.settings.isUseTabCharacter(project), useExplicitMapKeyword = settings.useExplicitMapKeyword, useExplicitSetKeyword = settings.useExplicitSetKeyword)
        val currentText = document.text
        val sorted = RdnCstFormatter.formatSorted(currentText, opts) ?: return
        if (sorted == currentText) return
        WriteCommandAction.runWriteCommandAction(project, "Sort RDN Document Keys", null, {
            document.setText(sorted)
            PsiDocumentManager.getInstance(project).commitDocument(document)
        })
    }

    override fun update(e: AnActionEvent) {
        val project = e.project
        val editor = e.getData(CommonDataKeys.EDITOR)
        val psiFile = if (project != null && editor != null) PsiDocumentManager.getInstance(project).getPsiFile(editor.document) else null
        e.presentation.isEnabledAndVisible = psiFile?.language == RdnLanguage
    }
}
