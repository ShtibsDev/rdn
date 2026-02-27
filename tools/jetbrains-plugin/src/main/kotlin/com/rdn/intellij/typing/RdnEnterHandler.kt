package com.rdn.intellij.typing

import com.intellij.codeInsight.editorActions.enter.EnterHandlerDelegate
import com.intellij.codeInsight.editorActions.enter.EnterHandlerDelegateAdapter
import com.intellij.openapi.actionSystem.DataContext
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.editor.actionSystem.EditorActionHandler
import com.intellij.openapi.util.Ref
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiFile
import com.intellij.application.options.CodeStyle
import com.rdn.intellij.RdnLanguage

class RdnEnterHandler : EnterHandlerDelegateAdapter() {
    override fun preprocessEnter(
        file: PsiFile,
        editor: Editor,
        caretOffset: Ref<Int>,
        caretAdvance: Ref<Int>,
        dataContext: DataContext,
        originalHandler: EditorActionHandler?
    ): EnterHandlerDelegate.Result {
        if (file.language != RdnLanguage) return EnterHandlerDelegate.Result.Continue

        val document = editor.document
        val offset = caretOffset.get()
        val lineNumber = document.getLineNumber(offset)
        val lineStart = document.getLineStartOffset(lineNumber)
        val textBeforeCaret = document.getText(TextRange(lineStart, offset)).trimEnd()

        val settings = CodeStyle.getSettings(file)
        val indentSize = settings.getIndentSize(file.fileType)
        val useTab = settings.useTabCharacter(file.fileType)
        val indentUnit = if (useTab) "\t" else " ".repeat(indentSize)

        val fullLine = document.getText(TextRange(lineStart, document.getLineEndOffset(lineNumber)))
        val currentIndent = fullLine.takeWhile { it == ' ' || it == '\t' }

        val lastChar = textBeforeCaret.lastOrNull()
        val isOpener = lastChar == '{' || lastChar == '[' || lastChar == '('

        if (isOpener) {
            val afterCaret = document.getText(TextRange(offset, document.getLineEndOffset(lineNumber))).trimStart()
            val firstCharAfter = afterCaret.firstOrNull()
            val isCloser = firstCharAfter == '}' || firstCharAfter == ']' || firstCharAfter == ')'

            if (isCloser) {
                val newIndent = currentIndent + indentUnit
                val replacement = "\n$newIndent\n$currentIndent"
                document.insertString(offset, replacement)
                editor.caretModel.moveToOffset(offset + 1 + newIndent.length)
                return EnterHandlerDelegate.Result.Stop
            } else {
                val newIndent = currentIndent + indentUnit
                document.insertString(offset, "\n$newIndent")
                editor.caretModel.moveToOffset(offset + 1 + newIndent.length)
                return EnterHandlerDelegate.Result.Stop
            }
        }

        return EnterHandlerDelegate.Result.Continue
    }
}
