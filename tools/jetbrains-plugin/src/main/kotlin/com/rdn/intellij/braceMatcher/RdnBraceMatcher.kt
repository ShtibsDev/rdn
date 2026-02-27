package com.rdn.intellij.braceMatcher

import com.intellij.lang.BracePair
import com.intellij.lang.PairedBraceMatcher
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.lexer.RdnTokenTypes

class RdnBraceMatcher : PairedBraceMatcher {
    companion object {
        private val PAIRS = arrayOf(
            BracePair(RdnTokenTypes.LBRACE, RdnTokenTypes.RBRACE, true),
            BracePair(RdnTokenTypes.LBRACKET, RdnTokenTypes.RBRACKET, true),
            BracePair(RdnTokenTypes.LPAREN, RdnTokenTypes.RPAREN, true)
        )
    }

    override fun getPairs(): Array<BracePair> = PAIRS
    override fun isPairedBracesAllowedBeforeType(lbraceType: IElementType, tokenType: IElementType?): Boolean = true
    override fun getCodeConstructStart(file: PsiFile, openingBraceOffset: Int): Int = openingBraceOffset
}
