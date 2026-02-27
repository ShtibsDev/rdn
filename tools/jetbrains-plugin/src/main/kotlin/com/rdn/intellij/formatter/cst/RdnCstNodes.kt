package com.rdn.intellij.formatter.cst

/**
 * Base for all CST nodes. Every node carries source positions.
 */
sealed interface RdnCstNode {
    val start: Int
    val end: Int
}

data class DocumentNode(val body: RdnCstNode, override val start: Int, override val end: Int) : RdnCstNode

data class StringLiteralNode(val value: String, val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class NumberLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class BigIntLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class BooleanLiteralNode(val value: Boolean, override val start: Int, override val end: Int) : RdnCstNode
data class NullLiteralNode(override val start: Int, override val end: Int) : RdnCstNode
data class NaNLiteralNode(override val start: Int, override val end: Int) : RdnCstNode
data class InfinityLiteralNode(val negative: Boolean, override val start: Int, override val end: Int) : RdnCstNode
data class DateTimeLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class TimeOnlyLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class DurationLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class BinaryLiteralNode(val encoding: BinaryEncoding, val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class RegExpLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode

data class ArrayNode(val elements: List<RdnCstNode>, override val start: Int, override val end: Int) : RdnCstNode
data class TupleNode(val elements: List<RdnCstNode>, override val start: Int, override val end: Int) : RdnCstNode

data class ObjectPropertyNode(val key: StringLiteralNode, val value: RdnCstNode, override val start: Int, override val end: Int) : RdnCstNode
data class ObjectNode(val properties: List<ObjectPropertyNode>, override val start: Int, override val end: Int) : RdnCstNode

data class MapEntryNode(val key: RdnCstNode, val value: RdnCstNode, override val start: Int, override val end: Int) : RdnCstNode
data class MapNode(val entries: List<MapEntryNode>, val explicit: Boolean, override val start: Int, override val end: Int) : RdnCstNode

data class SetNode(val elements: List<RdnCstNode>, val explicit: Boolean, override val start: Int, override val end: Int) : RdnCstNode

enum class BinaryEncoding { BASE64, HEX }
