package com.rdn.intellij.highlighting

import com.intellij.lexer.Lexer
import com.intellij.lexer.LexerPosition
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.lexer.RdnLexerAdapter
import com.rdn.intellij.lexer.RdnTokenTypes

/**
 * Wraps [RdnLexerAdapter] and performs a single-lookahead remapping of STRING_* tokens
 * to KEY_* tokens when the string is immediately followed by a COLON (i.e., it is an
 * object key).
 *
 * Algorithm:
 *  1. When we encounter STRING_OPEN we speculatively collect the entire string sequence
 *     (STRING_OPEN, STRING_CONTENT*, STRING_ESCAPE*, STRING_INVALID_ESCAPE*, STRING_CLOSE)
 *     into a pending buffer.
 *  2. We then peek at the very next token from the underlying lexer.
 *  3. If that token is COLON, every buffered STRING_* token is remapped to the
 *     corresponding KEY_* token before being emitted.
 *  4. Buffered tokens are drained one-by-one on successive [advance] calls so that the
 *     normal token-stream contract (one token per advance) is preserved.
 */
class RdnHighlightingLexer : Lexer() {

    private val delegate = RdnLexerAdapter()

    // ---- buffered token state -----------------------------------------------
    private data class BufferedToken(val type: IElementType?, val start: Int, val end: Int, val state: Int)

    private val buffer = ArrayDeque<BufferedToken>()
    private var current: BufferedToken? = null

    // ---- Lexer state forwarding ---------------------------------------------

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer.clear()
        current = null
        delegate.start(buffer, startOffset, endOffset, initialState)
        advance()
    }

    override fun getState(): Int = current?.state ?: delegate.state

    override fun getTokenType(): IElementType? = current?.type

    override fun getTokenStart(): Int = current?.start ?: delegate.tokenStart

    override fun getTokenEnd(): Int = current?.end ?: delegate.tokenEnd

    override fun getBufferSequence(): CharSequence = delegate.bufferSequence

    override fun getBufferEnd(): Int = delegate.bufferEnd

    override fun advance() {
        if (buffer.isNotEmpty()) {
            current = buffer.removeFirst()
            return
        }

        val rawType = delegate.tokenType
        if (rawType == null) {
            current = null
            return
        }

        if (rawType == RdnTokenTypes.STRING_OPEN) {
            collectAndMaybeRemapString()
        } else {
            current = BufferedToken(rawType, delegate.tokenStart, delegate.tokenEnd, delegate.state)
            delegate.advance()
        }
    }

    // -------------------------------------------------------------------------

    private fun collectAndMaybeRemapString() {
        val collected = mutableListOf<BufferedToken>()

        // Collect STRING_OPEN through STRING_CLOSE inclusive
        while (delegate.tokenType != null) {
            val tok = BufferedToken(delegate.tokenType, delegate.tokenStart, delegate.tokenEnd, delegate.state)
            collected.add(tok)
            val wasClose = delegate.tokenType == RdnTokenTypes.STRING_CLOSE
            delegate.advance()
            if (wasClose) break
        }

        // Peek: is the very next non-null token a COLON?
        val isKey = delegate.tokenType == RdnTokenTypes.COLON

        // Remap if needed
        val remapped = if (isKey) collected.map { remapToKey(it) } else collected

        // The first remapped token becomes current; the rest go into the buffer
        current = remapped.first()
        for (i in 1 until remapped.size) {
            buffer.addLast(remapped[i])
        }
    }

    private fun remapToKey(tok: BufferedToken): BufferedToken {
        val mapped = when (tok.type) {
            RdnTokenTypes.STRING_OPEN -> RdnTokenTypes.KEY_OPEN
            RdnTokenTypes.STRING_CONTENT -> RdnTokenTypes.KEY_CONTENT
            RdnTokenTypes.STRING_ESCAPE -> RdnTokenTypes.KEY_ESCAPE
            RdnTokenTypes.STRING_INVALID_ESCAPE -> RdnTokenTypes.KEY_ESCAPE   // still highlight as key
            RdnTokenTypes.STRING_CLOSE -> RdnTokenTypes.KEY_CLOSE
            else -> tok.type
        }
        return tok.copy(type = mapped)
    }

    // LexerPosition snapshot (used by incremental highlighting infrastructure)
    override fun getCurrentPosition(): LexerPosition {
        val snap = current
        return object : LexerPosition {
            override fun getOffset(): Int = snap?.start ?: delegate.tokenStart
            override fun getState(): Int = snap?.state ?: delegate.state
        }
    }

    override fun restore(position: LexerPosition) {
        buffer.clear()
        current = null
        delegate.start(delegate.bufferSequence, position.offset, delegate.bufferEnd, position.state)
        advance()
    }
}
