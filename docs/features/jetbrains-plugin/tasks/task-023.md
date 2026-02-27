# Task 023: Implement Format Utilities

## References
- [Tech Design](../tech-design.md) — Sections 3.6, 6.8
- [Discovery](../discovery.md)

## Description
Port `format.ts` from the VSCode extension to `RdnFormatUtils.kt`. This utility provides date/time formatting with token-based format strings (using `[literal]` for literal parts), duration expansion from ISO 8601 strings to English, digit grouping, and byte size formatting. Used by `RdnDocumentationProvider` (task-025) to render hover content.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/documentation/RdnFormatUtils.kt` — Formatting utilities for hover content

## Implementation Details

### `RdnFormatUtils.kt`

```kotlin
package com.rdn.intellij.documentation

import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter

object RdnFormatUtils {
    /**
     * Format a date/time Instant using a Moment.js-style format string.
     * Supported tokens: YYYY, MM, DD, HH, mm, ss, SSS
     * Literal text can be escaped with [brackets]: [UTC] → "UTC"
     *
     * Examples:
     *   "YYYY-MM-DD HH:mm:ss.SSS [UTC]" → "2024-01-15 10:30:00.000 UTC"
     *   "MMMM D, YYYY" → "January 15, 2024"
     */
    fun formatDate(instant: Instant, formatStr: String): String {
        val dt = instant.atZone(ZoneOffset.UTC)
        val sb = StringBuilder()
        var i = 0
        while (i < formatStr.length) {
            when {
                formatStr[i] == '[' -> {
                    // Literal: read until closing ]
                    val end = formatStr.indexOf(']', i + 1)
                    if (end == -1) { sb.append(formatStr[i]); i++ }
                    else { sb.append(formatStr.substring(i + 1, end)); i = end + 1 }
                }
                formatStr.startsWith("YYYY", i) -> { sb.append("%04d".format(dt.year)); i += 4 }
                formatStr.startsWith("MMMM", i) -> {
                    sb.append(dt.month.name.lowercase().replaceFirstChar { it.uppercase() }); i += 4
                }
                formatStr.startsWith("MMM", i) -> {
                    sb.append(dt.month.name.lowercase().replaceFirstChar { it.uppercase() }.take(3)); i += 3
                }
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

    /**
     * Format a LocalDate using the format string.
     * Same token set as formatDate but treats the date as midnight UTC.
     */
    fun formatLocalDate(date: LocalDate, formatStr: String): String =
        formatDate(date.atStartOfDay().toInstant(ZoneOffset.UTC), formatStr)

    /**
     * Format a TimeOnly as a string given a format string.
     * Supported tokens: HH, mm, ss
     */
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

    /**
     * Expand an ISO 8601 duration string to English.
     *
     * Examples:
     *   "P1Y2M3D" → "1 year, 2 months, 3 days"
     *   "P1DT2H30M" → "1 day, 2 hours, 30 minutes"
     *   "PT30S" → "30 seconds"
     */
    fun expandDuration(iso: String): String {
        // Regex: P([nY][nM][nD])?([T][nH][nM][nS])?
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

    /**
     * Format a number with thousands separators.
     * Example: "9007199254740993" → "9,007,199,254,740,993"
     */
    fun groupDigits(digits: String): String {
        val isNeg = digits.startsWith("-")
        val abs = if (isNeg) digits.substring(1) else digits
        val grouped = abs.reversed().chunked(3).joinToString(",").reversed()
        return if (isNeg) "-$grouped" else grouped
    }

    /**
     * Format a byte count as a human-readable size string.
     * Examples: 1 → "1 byte", 1024 → "1.0 KB", 1048576 → "1.0 MB"
     */
    fun formatByteSize(bytes: Int): String = when {
        bytes == 1 -> "1 byte"
        bytes < 1024 -> "$bytes bytes"
        bytes < 1024 * 1024 -> "${"%.1f".format(bytes / 1024.0)} KB"
        else -> "${"%.1f".format(bytes / (1024.0 * 1024.0))} MB"
    }

    /**
     * Expand regex flag characters to their full names.
     * Example: "gi" → "global, case-insensitive"
     */
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

    /**
     * Detect the ambiguity classification of a Unix timestamp.
     * 10-digit timestamps could be seconds (year ~2001-2286) or milliseconds (year 1970+).
     */
    fun classifyUnixTimestamp(digits: String): String {
        val n = digits.toLongOrNull() ?: return "unknown"
        return when (digits.length) {
            10 -> "seconds (ambiguous — could also be milliseconds if before 1970-01-01)"
            13 -> "milliseconds"
            else -> if (n > 0) "seconds" else "unknown"
        }
    }
}
```

## Acceptance Criteria
- [ ] `expandDuration("P1Y2M3D")` returns `"1 year, 2 months, 3 days"`
- [ ] `expandDuration("PT30S")` returns `"30 seconds"`
- [ ] `expandDuration("P1DT2H30M")` returns `"1 day, 2 hours, 30 minutes"`
- [ ] `expandDuration("P1Y")` returns `"1 year"` (singular)
- [ ] `groupDigits("9007199254740993")` returns `"9,007,199,254,740,993"`
- [ ] `groupDigits("-1234567")` returns `"-1,234,567"`
- [ ] `formatByteSize(1)` returns `"1 byte"` (singular)
- [ ] `formatByteSize(1024)` returns `"1.0 KB"`
- [ ] `expandRegExpFlags("gi")` returns `"global, case-insensitive"`
- [ ] `expandRegExpFlags("")` returns `"no flags"`
- [ ] `formatDate` with `[UTC]` in the format string outputs the literal `UTC`
- [ ] `formatDate` with `MMMM` token outputs the full month name (e.g., `"January"`)

## Dependencies
- Depends on: task-001
- Blocks: task-025
