package com.rdn.intellij

import com.intellij.lang.Language

object RdnLanguage : Language("RDN") {
    private fun readResolve(): Any = RdnLanguage
}
