package com.rdn.intellij.lexer

import com.intellij.lexer.FlexAdapter

class RdnLexerAdapter : FlexAdapter(RdnFlexLexer(null))
