package com.rdn.intellij.documentation

import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset

object RdnFormatUtils {
    fun formatDate(instant: Instant, formatStr: String): String {
        val dt = instant.atZone(ZoneOffset.UTC)
        val sb = StringBuilder()
        var i = 0
        while (i < formatStr.length) {
            when {
                formatStr[i] == '[' -> {
                    val end = formatStr.indexOf(']', i + 1)
                    if (end == -1) { sb.append(formatStr[i]); i++ }
                    else { sb.append(formatStr.substring(i + 1, end)); i = end + 1 }
                }
                formatStr.startsWith("YYYY", i) -> { sb.append("%04d".format(dt.year)); i += 4 }
                formatStr.startsWith("MMMM", i) -> { sb.append(dt.month.name.lowercase().replaceFirstChar { it.uppercase() }); i += 4 }
                formatStr.startsWith("MMM", i) -> { sb.append(dt.month.name.lowercase().replaceFirstChar { it.uppercase() }.take(3)); i += 3 }
                formatStr.startsWith("MM", i) -> { sb.append("%02d".format(dt.monthValue)); i += 2 }
                formatStr.startsWith("DD", i) -> { sb.append("%02d".format(dt.dayOfMonth)); i += 2 }
                formatStr.startsWith("D", i) -> { sb.append(dt.dayOfMonth); i += 1 }
                formatStr.startsWith("HH", i) -> { sb.append("%02d".format(dt.hour)); i += 2 }
                formatStr.startsWith("mm", i) -> { sb.append("%02d".format(dt.minute)); i += 2 }
                formatStr.startsWith("ss", i) -> { sb.append("%02d".format(dt.second)); i += 2 }
                formatStr.startsWith("SSS", i) -> { sb.append("%03d".format(dt.nano / 1_000_000)); i += 3 }
                else -> { sb.append(formatStr[i]); i++ }
            }
        }
        return sb.toString()
    }

    fun formatLocalDate(date: LocalDate, formatStr: String): String =
        formatDate(date.atStartOfDay().toInstant(ZoneOffset.UTC), formatStr)

    fun formatTimeOnly(hours: Int, minutes: Int, seconds: Int, milliseconds: Int, formatStr: String): String {
        val sb = StringBuilder()
        var i = 0
        while (i < formatStr.length) {
            when {
                formatStr.startsWith("HH", i) -> { sb.append("%02d".format(hours)); i += 2 }
                formatStr.startsWith("mm", i) -> { sb.append("%02d".format(minutes)); i += 2 }
                formatStr.startsWith("ss", i) -> { sb.append("%02d".format(seconds)); i += 2 }
                formatStr.startsWith("SSS", i) -> { sb.append("%03d".format(milliseconds)); i += 3 }
                else -> { sb.append(formatStr[i]); i++ }
            }
        }
        return sb.toString()
    }

    fun expandDuration(iso: String): String {
        val regex = Regex("P(?:(\\d+)Y)?(?:(\\d+)M)?(?:(\\d+)D)?(?:T(?:(\\d+)H)?(?:(\\d+)M)?(?:(\\d+(?:\\.\\d+)?)S)?)?")
        val match = regex.matchEntire(iso) ?: return iso

        val parts = mutableListOf<String>()
        match.groupValues[1].takeIf { it.isNotEmpty() }?.toInt()?.let { n -> parts.add("$n ${if (n == 1) "year" else "years"}") }
        match.groupValues[2].takeIf { it.isNotEmpty() }?.toInt()?.let { n -> parts.add("$n ${if (n == 1) "month" else "months"}") }
        match.groupValues[3].takeIf { it.isNotEmpty() }?.toInt()?.let { n -> parts.add("$n ${if (n == 1) "day" else "days"}") }
        match.groupValues[4].takeIf { it.isNotEmpty() }?.toInt()?.let { n -> parts.add("$n ${if (n == 1) "hour" else "hours"}") }
        match.groupValues[5].takeIf { it.isNotEmpty() }?.toInt()?.let { n -> parts.add("$n ${if (n == 1) "minute" else "minutes"}") }
        match.groupValues[6].takeIf { it.isNotEmpty() }?.let { s ->
            val n = s.toDouble()
            parts.add("$n ${if (n == 1.0) "second" else "seconds"}")
        }

        return if (parts.isEmpty()) "0 seconds" else parts.joinToString(", ")
    }

    fun groupDigits(digits: String): String {
        val isNeg = digits.startsWith("-")
        val abs = if (isNeg) digits.substring(1) else digits
        val grouped = abs.reversed().chunked(3).joinToString(",").reversed()
        return if (isNeg) "-$grouped" else grouped
    }

    fun formatByteSize(bytes: Int): String = when {
        bytes == 1 -> "1 byte"
        bytes < 1024 -> "$bytes bytes"
        bytes < 1024 * 1024 -> "${"%.1f".format(bytes / 1024.0)} KB"
        else -> "${"%.1f".format(bytes / (1024.0 * 1024.0))} MB"
    }

    fun expandRegExpFlags(flags: String): String {
        val names = mapOf(
            'd' to "generate indices",
            'g' to "global",
            'i' to "case-insensitive",
            'm' to "multiline",
            's' to "dotAll",
            'u' to "unicode",
            'v' to "unicode sets",
            'y' to "sticky"
        )
        val expanded = flags.mapNotNull { names[it] }
        return if (expanded.isEmpty()) "no flags" else expanded.joinToString(", ")
    }

    fun classifyUnixTimestamp(digits: String): String {
        val n = digits.toLongOrNull() ?: return "unknown"
        return when (digits.length) {
            10 -> "seconds (ambiguous — could also be milliseconds if before 1970-01-01)"
            13 -> "milliseconds"
            else -> if (n > 0) "seconds" else "unknown"
        }
    }
}
