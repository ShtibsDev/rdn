package com.rdn.intellij.psi

import com.intellij.extapi.psi.PsiFileBase
import com.intellij.openapi.fileTypes.FileType
import com.intellij.psi.FileViewProvider
import com.rdn.intellij.RdnFileType
import com.rdn.intellij.RdnLanguage

class RdnFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, RdnLanguage) {
    override fun getFileType(): FileType = RdnFileType
    override fun toString(): String = "RDN File"
}
