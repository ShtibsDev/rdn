package com.rdn.intellij.formatter

import com.intellij.formatting.*
import com.intellij.lang.ASTNode
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiDocumentManager
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.codeStyle.CodeStyleSettings
import com.intellij.psi.impl.source.codeStyle.PostFormatProcessor
import com.rdn.intellij.RdnLanguage
import com.rdn.intellij.settings.RdnSettingsState

/**
 * Integrates the RDN CST formatter with IntelliJ's formatting infrastructure.
 *
 * Because the CST formatter operates on raw text (not the PSI tree), the
 * actual formatting is performed by [RdnPostFormatProcessor] which runs
 * after IntelliJ's block-based formatting pass. This builder provides a
 * minimal leaf block so the platform considers the file "formattable".
 */
class RdnFormattingModelBuilder : FormattingModelBuilder {
    override fun createModel(formattingContext: FormattingContext): FormattingModel {
        val file = formattingContext.containingFile
        val rootBlock = RdnRootBlock(file.node)
        return FormattingModelProvider.createFormattingModelForPsiFile(file, rootBlock, formattingContext.codeStyleSettings)
    }
}

/**
 * A no-op leaf block that satisfies IntelliJ's requirement for a root block.
 * All real formatting happens in [RdnPostFormatProcessor].
 */
class RdnRootBlock(private val node: ASTNode) : Block {
    override fun getTextRange(): TextRange = node.textRange
    override fun getSubBlocks(): List<Block> = emptyList()
    override fun getWrap(): Wrap? = null
    override fun getIndent(): Indent? = Indent.getNoneIndent()
    override fun getAlignment(): Alignment? = null
    override fun getSpacing(child1: Block?, child2: Block): Spacing? = null
    override fun getChildAttributes(newChildIndex: Int): ChildAttributes = ChildAttributes(Indent.getNoneIndent(), null)
    override fun isIncomplete(): Boolean = false
    override fun isLeaf(): Boolean = true
}

/**
 * Post-format processor that replaces the document text with the output of
 * [RdnCstFormatter] (or Prettier when available). Runs after the standard
 * block-based formatting pass, which for RDN files is intentionally a no-op.
 */
class RdnPostFormatProcessor : PostFormatProcessor {
    override fun processElement(source: PsiElement, settings: CodeStyleSettings): PsiElement = source

    override fun processText(source: PsiFile, rangeToReformat: TextRange, settings: CodeStyleSettings): TextRange {
        if (source.language != RdnLanguage) return rangeToReformat

        val project = source.project
        val rdnSettings = RdnSettingsState.getInstance(project)
        val tabSize = settings.getIndentSize(source.fileType)
        val insertSpaces = !settings.useTabCharacter(source.fileType)

        val opts = RdnFormatOptions(tabSize = tabSize, insertSpaces = insertSpaces, useExplicitMapKeyword = rdnSettings.useExplicitMapKeyword, useExplicitSetKeyword = rdnSettings.useExplicitSetKeyword)

        val text = source.text
        val formatted = if (RdnPrettierDetector.isPrettierAvailable(project)) {
            RdnPrettierDetector.runPrettier(text, project) ?: RdnCstFormatter.format(text, opts)
        } else {
            RdnCstFormatter.format(text, opts)
        }

        if (formatted == text) return rangeToReformat

        val document = PsiDocumentManager.getInstance(project).getDocument(source) ?: return rangeToReformat
        document.setText(formatted)
        PsiDocumentManager.getInstance(project).commitDocument(document)
        return TextRange(0, formatted.length)
    }
}
