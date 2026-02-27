# Task 025: Implement DocumentationProvider for Hover

## References
- [Tech Design](../tech-design.md) — Sections 3.6, 5.6, 6.8
- [Discovery](../discovery.md)

## Description
Create `RdnDocumentationProvider.kt` with token detection logic (mirroring `detectToken()` in `hover.ts`) and HTML content generation for all 18 hover token kinds. Implement collection element counting, implicit map/set detection, regex flag expansion, and optional image preview embedding. Register in `plugin.xml`. All hover categories are individually toggleable via `RdnSettingsState`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/documentation/RdnDocumentationProvider.kt` — Hover documentation provider
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register extension

## Implementation Details

### Hover token kinds (from Section 5.6)

```kotlin
package com.rdn.intellij.documentation

import com.intellij.openapi.util.TextRange

enum class HoverTokenKind {
    DATE_TIME_FULL,
    DATE_TIME_NO_MILLIS,
    DATE_ONLY,
    UNIX_TIMESTAMP,
    TIME_ONLY,
    DURATION,
    BIGINT,
    BINARY_BASE64,
    BINARY_HEX,
    REGEXP,
    NAN,
    INFINITY,
    NEG_INFINITY,
    MAP_KEYWORD,
    SET_KEYWORD,
    MAP_ARROW,
    TUPLE,
    IMPLICIT_MAP,
    IMPLICIT_SET,
}

data class HoverTokenInfo(val kind: HoverTokenKind, val text: String, val range: TextRange)
```

### `RdnDocumentationProvider.kt`

```kotlin
package com.rdn.intellij.documentation

import com.intellij.lang.documentation.AbstractDocumentationProvider
import com.intellij.lang.documentation.DocumentationMarkup
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.rdn.intellij.lexer.RdnTokenTypes
import com.rdn.intellij.psi.*
import com.rdn.intellij.settings.RdnSettingsState
import java.time.Instant
import java.time.LocalDate

class RdnDocumentationProvider : AbstractDocumentationProvider() {

    override fun generateDoc(element: PsiElement, originalElement: PsiElement?): String? {
        val file = element.containingFile as? RdnFile ?: return null
        val project = file.project
        val settings = RdnSettingsState.getInstance(project)
        if (!settings.hoverEnabled) return null

        val offset = originalElement?.textOffset ?: element.textOffset
        val tokenInfo = detectToken(file, offset) ?: return null

        return when (tokenInfo.kind) {
            HoverTokenKind.DATE_TIME_FULL -> if (!settings.hoverDateTimeEnabled) null else renderDateTimeFull(tokenInfo, settings)
            HoverTokenKind.DATE_TIME_NO_MILLIS -> if (!settings.hoverDateTimeEnabled) null else renderDateTimeNoMillis(tokenInfo, settings)
            HoverTokenKind.DATE_ONLY -> if (!settings.hoverDateTimeEnabled) null else renderDateOnly(tokenInfo, settings)
            HoverTokenKind.UNIX_TIMESTAMP -> if (!settings.hoverDateTimeEnabled) null else renderUnixTimestamp(tokenInfo, settings)
            HoverTokenKind.TIME_ONLY -> if (!settings.hoverTimeOnlyEnabled) null else renderTimeOnly(tokenInfo, settings)
            HoverTokenKind.DURATION -> if (!settings.hoverDurationEnabled) null else renderDuration(tokenInfo)
            HoverTokenKind.BIGINT -> if (!settings.hoverBigintEnabled) null else renderBigInt(tokenInfo, settings)
            HoverTokenKind.BINARY_BASE64 -> if (!settings.hoverBinaryEnabled) null else renderBinaryBase64(tokenInfo, settings)
            HoverTokenKind.BINARY_HEX -> if (!settings.hoverBinaryEnabled) null else renderBinaryHex(tokenInfo, settings)
            HoverTokenKind.REGEXP -> if (!settings.hoverRegexpEnabled) null else renderRegExp(tokenInfo)
            HoverTokenKind.NAN -> if (!settings.hoverSpecialNumbersEnabled) null else renderNaN()
            HoverTokenKind.INFINITY -> if (!settings.hoverSpecialNumbersEnabled) null else renderInfinity(false)
            HoverTokenKind.NEG_INFINITY -> if (!settings.hoverSpecialNumbersEnabled) null else renderInfinity(true)
            HoverTokenKind.MAP_KEYWORD, HoverTokenKind.IMPLICIT_MAP -> if (!settings.hoverCollectionsEnabled) null else renderMap(tokenInfo)
            HoverTokenKind.SET_KEYWORD, HoverTokenKind.IMPLICIT_SET -> if (!settings.hoverCollectionsEnabled) null else renderSet(tokenInfo)
            HoverTokenKind.MAP_ARROW -> if (!settings.hoverCollectionsEnabled) null else renderMapArrow()
            HoverTokenKind.TUPLE -> if (!settings.hoverCollectionsEnabled) null else renderTuple(tokenInfo)
        }
    }

    /**
     * Detect the hover token kind for the given file and offset.
     * Mirrors detectToken() in hover.ts.
     */
    private fun detectToken(file: PsiFile, offset: Int): HoverTokenInfo? {
        val element = file.findElementAt(offset) ?: return null
        val tokenType = element.node.elementType
        val text = element.text
        val range = element.textRange

        return when (tokenType) {
            RdnTokenTypes.BIGINT -> HoverTokenInfo(HoverTokenKind.BIGINT, text, range)
            RdnTokenTypes.NAN -> HoverTokenInfo(HoverTokenKind.NAN, text, range)
            RdnTokenTypes.INFINITY -> HoverTokenInfo(HoverTokenKind.INFINITY, text, range)
            RdnTokenTypes.NEG_INFINITY -> HoverTokenInfo(HoverTokenKind.NEG_INFINITY, text, range)
            RdnTokenTypes.BINARY_PREFIX -> {
                // Find the full binary literal span
                val parent = element.parent
                val fullText = parent?.text ?: text
                val fullRange = parent?.textRange ?: range
                val kind = if (text == "b") HoverTokenKind.BINARY_BASE64 else HoverTokenKind.BINARY_HEX
                HoverTokenInfo(kind, fullText, fullRange)
            }
            RdnTokenTypes.REGEXP_OPEN, RdnTokenTypes.REGEXP_CONTENT, RdnTokenTypes.REGEXP_CLOSE, RdnTokenTypes.REGEXP_FLAGS -> {
                val parent = element.parent
                HoverTokenInfo(HoverTokenKind.REGEXP, parent?.text ?: text, parent?.textRange ?: range)
            }
            RdnTokenTypes.AT_SIGN, RdnTokenTypes.DATE_PART, RdnTokenTypes.TIME_PART,
            RdnTokenTypes.MILLIS_PART, RdnTokenTypes.TIMEZONE, RdnTokenTypes.TIME_SEPARATOR -> {
                detectDateTimeToken(element)
            }
            RdnTokenTypes.UNIX_TIMESTAMP -> HoverTokenInfo(HoverTokenKind.UNIX_TIMESTAMP, text, range)
            RdnTokenTypes.DURATION_P, RdnTokenTypes.DURATION_NUMBER,
            RdnTokenTypes.DURATION_UNIT, RdnTokenTypes.DURATION_T -> {
                val parent = element.parent
                HoverTokenInfo(HoverTokenKind.DURATION, parent?.text ?: text, parent?.textRange ?: range)
            }
            RdnTokenTypes.MAP_KEYWORD -> HoverTokenInfo(HoverTokenKind.MAP_KEYWORD, text, range)
            RdnTokenTypes.SET_KEYWORD -> HoverTokenInfo(HoverTokenKind.SET_KEYWORD, text, range)
            RdnTokenTypes.ARROW -> HoverTokenInfo(HoverTokenKind.MAP_ARROW, text, range)
            RdnTokenTypes.LPAREN -> {
                val parent = element.parent
                if (parent?.node?.elementType == RdnElementTypes.TUPLE) {
                    HoverTokenInfo(HoverTokenKind.TUPLE, parent.text, parent.textRange)
                } else null
            }
            RdnTokenTypes.LBRACE -> {
                // Detect implicit map/set by looking at the parent PSI node
                val parent = element.parent
                when (parent?.node?.elementType) {
                    RdnElementTypes.MAP -> HoverTokenInfo(HoverTokenKind.IMPLICIT_MAP, parent!!.text, parent.textRange)
                    RdnElementTypes.SET -> HoverTokenInfo(HoverTokenKind.IMPLICIT_SET, parent!!.text, parent.textRange)
                    else -> null
                }
            }
            else -> null
        }
    }

    private fun detectDateTimeToken(element: PsiElement): HoverTokenInfo? {
        val parent = element.parent ?: return null
        val fullText = parent.text
        val range = parent.textRange
        return when {
            fullText.matches(Regex("@\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z?")) ->
                HoverTokenInfo(HoverTokenKind.DATE_TIME_FULL, fullText, range)
            fullText.matches(Regex("@\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z?")) ->
                HoverTokenInfo(HoverTokenKind.DATE_TIME_NO_MILLIS, fullText, range)
            fullText.matches(Regex("@\\d{4}-\\d{2}-\\d{2}")) ->
                HoverTokenInfo(HoverTokenKind.DATE_ONLY, fullText, range)
            fullText.matches(Regex("@\\d{2}:\\d{2}:\\d{2}(\\.\\d{3})?")) ->
                HoverTokenInfo(HoverTokenKind.TIME_ONLY, fullText, range)
            else -> HoverTokenInfo(HoverTokenKind.DATE_TIME_FULL, fullText, range)
        }
    }

    // ===== Renderers =====

    private fun renderDateTimeFull(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = token.text.removePrefix("@")
        val instant = try { Instant.parse(if (raw.endsWith("Z")) raw else "${raw}Z") } catch (e: Exception) { return "" }
        val formatted = RdnFormatUtils.formatDate(instant, settings.hoverDateTimeFullFormat)
        return buildDoc("DateTime", "full ISO 8601", formatted)
    }

    private fun renderDateTimeNoMillis(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = token.text.removePrefix("@")
        val instant = try { Instant.parse(if (raw.endsWith("Z")) raw else "${raw}Z") } catch (e: Exception) { return "" }
        val formatted = RdnFormatUtils.formatDate(instant, settings.hoverDateTimeNoMillisFormat)
        return buildDoc("DateTime", "no milliseconds", formatted)
    }

    private fun renderDateOnly(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = token.text.removePrefix("@")
        val date = try { LocalDate.parse(raw) } catch (e: Exception) { return "" }
        val formatted = RdnFormatUtils.formatLocalDate(date, settings.hoverDateTimeDateOnlyFormat)
        return buildDoc("DateTime", "date only", formatted)
    }

    private fun renderUnixTimestamp(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val digits = token.text.removePrefix("@")
        val n = digits.toLongOrNull() ?: return ""
        val (instant, unit) = if (digits.length >= 13) {
            Instant.ofEpochMilli(n) to "milliseconds"
        } else {
            Instant.ofEpochSecond(n) to "seconds"
        }
        val formatted = RdnFormatUtils.formatDate(instant, settings.hoverDateTimeUnixFormat)
        val hint = if (digits.length == 10) "\n\nAmbiguous: could be seconds or milliseconds." else ""
        return buildDoc("Unix Timestamp", unit, "$formatted$hint")
    }

    private fun renderTimeOnly(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = token.text.removePrefix("@")
        val parts = raw.split(":").map { it.toIntOrNull() ?: 0 }
        val h = parts.getOrElse(0) { 0 }
        val m = parts.getOrElse(1) { 0 }
        val s = parts.getOrElse(2) { 0 }
        val formatted = RdnFormatUtils.formatTimeOnly(h, m, s, 0, settings.hoverTimeOnlyFormat)
        return buildDoc("TimeOnly", null, formatted)
    }

    private fun renderDuration(token: HoverTokenInfo): String {
        val iso = token.text.removePrefix("@")
        val expanded = RdnFormatUtils.expandDuration(iso)
        return buildDoc("Duration", null, expanded)
    }

    private fun renderBigInt(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = token.text.removeSuffix("n")
        val grouped = RdnFormatUtils.groupDigits(raw)
        val bitLen = if (settings.hoverBigintShowBitLength) {
            try { java.math.BigInteger(raw).bitLength() } catch (e: Exception) { null }
        } else null
        val detail = if (bitLen != null) "$grouped ($bitLen bits)" else grouped
        return buildDoc("BigInt", null, detail)
    }

    private fun renderBinaryBase64(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = token.text.removePrefix("b\"").removeSuffix("\"")
        val bytes = RdnBinaryUtils.decodeBase64ToBytes(raw) ?: return buildDoc("Base64 Binary", null, "Invalid base64")
        val size = RdnFormatUtils.formatByteSize(bytes.size)
        val imageInfo = RdnBinaryUtils.detectImageFromBytes(bytes)
        val preview = if (settings.hoverBinaryShowPreview) {
            if (imageInfo != null) {
                "<img src=\"${RdnBinaryUtils.toDataUri(imageInfo)}\" style=\"max-width:200px;\"/>"
            } else {
                RdnBinaryUtils.bytesToAsciiPreview(bytes)?.let { "<code>$it</code>" } ?: ""
            }
        } else ""
        return buildDoc("Base64 Binary", null, "$size${ if (preview.isNotEmpty()) "<br/>$preview" else "" }")
    }

    private fun renderBinaryHex(token: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = token.text.removePrefix("x\"").removeSuffix("\"")
        val oddWarning = if (RdnBinaryUtils.isOddHexLength(raw)) " (odd number of digits — incomplete byte)" else ""
        val bytes = RdnBinaryUtils.decodeHexToBytes(raw)
        val size = bytes?.let { RdnFormatUtils.formatByteSize(it.size) } ?: "?"
        val imageInfo = bytes?.let { RdnBinaryUtils.detectImageFromBytes(it) }
        val preview = if (settings.hoverBinaryShowPreview && bytes != null) {
            if (imageInfo != null) {
                "<img src=\"${RdnBinaryUtils.toDataUri(imageInfo)}\" style=\"max-width:200px;\"/>"
            } else {
                RdnBinaryUtils.bytesToAsciiPreview(bytes)?.let { "<code>$it</code>" } ?: ""
            }
        } else ""
        return buildDoc("Hex Binary", null, "$size$oddWarning${ if (preview.isNotEmpty()) "<br/>$preview" else "" }")
    }

    private fun renderRegExp(token: HoverTokenInfo): String {
        val raw = token.text
        val lastSlash = raw.lastIndexOf('/')
        val flags = if (lastSlash > 0) raw.substring(lastSlash + 1) else ""
        val expanded = RdnFormatUtils.expandRegExpFlags(flags)
        return buildDoc("RegExp", null, "Flags: $expanded")
    }

    private fun renderNaN() = buildDoc("NaN", null, "IEEE 754 Not-a-Number. Result of undefined mathematical operations such as 0/0 or Math.sqrt(-1).")
    private fun renderInfinity(negative: Boolean) = buildDoc(if (negative) "-Infinity" else "Infinity", null, "IEEE 754 ${if (negative) "negative" else "positive"} infinity. Exceeds the representable range of floating-point numbers.")

    private fun renderMap(token: HoverTokenInfo): String {
        val elementCount = token.text.count { it == '>' }  // rough count of => separators
        val implicit = token.kind == HoverTokenKind.IMPLICIT_MAP
        return buildDoc("Map", if (implicit) "implicit" else null, "$elementCount ${if (elementCount == 1) "entry" else "entries"}")
    }

    private fun renderSet(token: HoverTokenInfo): String {
        val implicit = token.kind == HoverTokenKind.IMPLICIT_SET
        return buildDoc("Set", if (implicit) "implicit" else null, "")
    }

    private fun renderMapArrow() = buildDoc("=>", null, "Map entry separator. Separates a Map key from its corresponding value.")

    private fun renderTuple(token: HoverTokenInfo): String {
        val elementCount = token.text.count { it == ',' } + 1
        return buildDoc("Tuple", null, "$elementCount ${if (elementCount == 1) "element" else "elements"}")
    }

    /** Builds the HTML documentation string. */
    private fun buildDoc(title: String, subtitle: String?, content: String): String {
        val sub = if (subtitle != null) " <i>($subtitle)</i>" else ""
        return "<b>$title</b>$sub<br/>$content"
    }
}
```

### `plugin.xml` additions

```xml
<lang.documentationProvider
    language="RDN"
    implementationClass="com.rdn.intellij.documentation.RdnDocumentationProvider"
    order="first"/>
```

## Acceptance Criteria
- [ ] Hovering over `@2024-01-15T10:30:00.000Z` shows "DateTime (full ISO 8601)" with formatted date
- [ ] Hovering over `@2024-01-15` shows "DateTime (date only)" with formatted date
- [ ] Hovering over `@14:30:00` shows "TimeOnly" with formatted time
- [ ] Hovering over `@P1Y2M3D` shows "Duration" with "1 year, 2 months, 3 days"
- [ ] Hovering over `42n` shows "BigInt" with grouped digits
- [ ] Hovering over `b"SGVsbG8="` shows "Base64 Binary" with byte size
- [ ] Hovering over `x"48656C6C6F"` shows "Hex Binary" with byte size
- [ ] Hovering over `/\w+/gi` shows "RegExp" with "global, case-insensitive"
- [ ] Hovering over `NaN` shows IEEE 754 explanation
- [ ] Disabling `hoverEnabled` in settings shows no hover content
- [ ] Disabling `hoverDateTimeEnabled` hides DateTime hover
- [ ] PNG image bytes in base64 show image preview in hover

## Dependencies
- Depends on: task-009, task-023, task-024, task-026
- Blocks: None
