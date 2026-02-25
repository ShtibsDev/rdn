package com.rdn.intellij.parser

class RdnSyntaxError(message: String, val offset: Int) : Exception(message)
