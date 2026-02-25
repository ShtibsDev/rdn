package com.rdn.intellij.parser

import com.intellij.testFramework.ParsingTestCase

class RdnParserTest : ParsingTestCase("parser", "rdn", RdnParserDefinition()) {
    override fun getTestDataPath(): String = "src/test/resources"

    fun testObject() = doTest(true)
    fun testArray() = doTest(true)
    fun testTuple() = doTest(true)
    fun testMapExplicit() = doTest(true)
    fun testMapImplicit() = doTest(true)
    fun testSetExplicit() = doTest(true)
    fun testSetImplicit() = doTest(true)
    fun testSetSingleElement() = doTest(true)
    fun testEmptyBrace() = doTest(true)
    fun testBraceDisambiguation() = doTest(true)
    fun testStringLiteral() = doTest(true)
    fun testNumberLiteral() = doTest(true)
    fun testBigIntLiteral() = doTest(true)
    fun testBooleanLiteral() = doTest(true)
    fun testNullLiteral() = doTest(true)
    fun testNanLiteral() = doTest(true)
    fun testInfinityLiteral() = doTest(true)
    fun testDateTimeLiteral() = doTest(true)
    fun testTimeOnlyLiteral() = doTest(true)
    fun testDurationLiteral() = doTest(true)
    fun testBinaryLiteral() = doTest(true)
    fun testRegExpLiteral() = doTest(true)
    fun testNestedObject() = doTest(true)
    fun testAllTypes() = doTest(true)
    fun testErrorRecovery() = doTest(true)
}
