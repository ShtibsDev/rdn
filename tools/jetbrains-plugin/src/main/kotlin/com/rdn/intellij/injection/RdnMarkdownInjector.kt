package com.rdn.intellij.injection

import com.intellij.lang.injection.MultiHostInjector
import com.intellij.lang.injection.MultiHostRegistrar
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiLanguageInjectionHost
import com.rdn.intellij.RdnLanguage

class RdnMarkdownInjector : MultiHostInjector {
    override fun elementsToInjectIn(): List<Class<out PsiElement>> =
        listOf(PsiElement::class.java)

    override fun getLanguagesToInject(registrar: MultiHostRegistrar, context: PsiElement) {
        val file = context.containingFile ?: return
        val fileLanguage = file.language
        if (!fileLanguage.id.contains("Markdown", ignoreCase = true)) return

        val elementType = context.node?.elementType?.toString() ?: return
        if (!elementType.contains("CODE_FENCE", ignoreCase = true)) return

        val fullText = context.text
        val lines = fullText.split("\n")
        if (lines.size < 2) return

        val infoLine = lines[0].trimStart('`').trim()
        if (!infoLine.equals("rdn", ignoreCase = true)) return

        val openFenceLength = lines[0].length + 1
        val closeFenceLength = if (lines.last().startsWith("```")) lines.last().length else 0
        val contentStart = openFenceLength
        val contentEnd = fullText.length - closeFenceLength - (if (closeFenceLength > 0) 1 else 0)

        if (contentEnd <= contentStart) return

        val host = context as? PsiLanguageInjectionHost ?: return
        val contentRange = com.intellij.openapi.util.TextRange(contentStart, contentEnd)

        registrar
            .startInjecting(RdnLanguage)
            .addPlace(null, null, host, contentRange)
            .doneInjecting()
    }
}
