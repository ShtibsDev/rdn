package com.rdn.intellij

import com.intellij.openapi.fileTypes.LanguageFileType
import javax.swing.Icon

object RdnFileType : LanguageFileType(RdnLanguage) {
    override fun getName(): String = "RDN File"
    override fun getDescription(): String = "Rich Data Notation file"
    override fun getDefaultExtension(): String = "rdn"
    override fun getIcon(): Icon = RdnIcons.FILE
}
