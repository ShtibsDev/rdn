package com.rdn.intellij.parser.model

import java.math.BigInteger
import java.time.Instant
import java.time.LocalDate

sealed interface RdnValue

data object RdnNull : RdnValue
data class RdnBoolean(val value: Boolean) : RdnValue
data class RdnNumber(val value: Double) : RdnValue
data class RdnBigInt(val value: BigInteger) : RdnValue
data class RdnString(val value: String) : RdnValue
data class RdnDateTime(val instant: Instant) : RdnValue
data class RdnDateOnly(val date: LocalDate) : RdnValue
data class RdnTimeOnly(val hours: Int, val minutes: Int, val seconds: Int, val milliseconds: Int) : RdnValue
data class RdnDuration(val iso: String) : RdnValue
data class RdnRegExp(val pattern: String, val flags: String) : RdnValue
data class RdnBinaryBase64(val data: ByteArray) : RdnValue {
    override fun equals(other: Any?) = other is RdnBinaryBase64 && data.contentEquals(other.data)
    override fun hashCode() = data.contentHashCode()
}
data class RdnBinaryHex(val data: ByteArray) : RdnValue {
    override fun equals(other: Any?) = other is RdnBinaryHex && data.contentEquals(other.data)
    override fun hashCode() = data.contentHashCode()
}
data class RdnNaN(val dummy: Unit = Unit) : RdnValue
data class RdnInfinity(val negative: Boolean) : RdnValue
data class RdnArray(val elements: List<RdnValue>) : RdnValue
data class RdnTuple(val elements: List<RdnValue>) : RdnValue
data class RdnObject(val properties: List<Pair<String, RdnValue>>) : RdnValue
data class RdnMap(val entries: List<Pair<RdnValue, RdnValue>>, val explicit: Boolean) : RdnValue
data class RdnSet(val elements: List<RdnValue>, val explicit: Boolean) : RdnValue
