package com.rdn.intellij.parser

import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.lexer.Lexer
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.TokenSet
import com.rdn.intellij.lexer.RdnLexerAdapter
import com.rdn.intellij.lexer.RdnTokenTypes
import com.rdn.intellij.psi.RdnElementTypes
import com.rdn.intellij.psi.RdnFile
import com.rdn.intellij.psi.RdnTypes

class RdnParserDefinition : ParserDefinition {
    override fun createLexer(project: Project): Lexer = RdnLexerAdapter()

    override fun createParser(project: Project): PsiParser = RdnParser()

    override fun getFileNodeType(): IFileElementType = RdnElementTypes.FILE

    override fun getWhitespaceTokens(): TokenSet = TokenSet.create(RdnTokenTypes.WHITE_SPACE)

    override fun getCommentTokens(): TokenSet = TokenSet.EMPTY

    override fun getStringLiteralElements(): TokenSet = TokenSet.create(RdnTokenTypes.STRING_CONTENT, RdnTokenTypes.STRING_OPEN, RdnTokenTypes.STRING_CLOSE)

    override fun createElement(node: ASTNode): PsiElement = RdnTypes.Factory.createElement(node)

    override fun createFile(viewProvider: FileViewProvider): PsiFile = RdnFile(viewProvider)
}
