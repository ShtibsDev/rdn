package com.rdn.intellij.documentation

import com.intellij.lang.documentation.AbstractDocumentationProvider
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.RdnLanguage
import com.rdn.intellij.lexer.RdnTokenTypes
import com.rdn.intellij.settings.RdnSettingsState
import java.math.BigInteger
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset

/**
 * Classifies the token under the caret for hover documentation.
 */
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
    IMPLICIT_SET
}

/**
 * Holds the detected token kind, its raw text, and the range it covers.
 */
data class HoverTokenInfo(val kind: HoverTokenKind, val text: String, val range: TextRange)

/**
 * Provides hover documentation for RDN tokens such as dates, durations,
 * BigInts, binary literals, regular expressions, special numbers, and
 * collection keywords.
 *
 * All hover categories are individually toggleable via [RdnSettingsState].
 */
class RdnDocumentationProvider : AbstractDocumentationProvider() {

    override fun getCustomDocumentationElement(editor: Editor, file: PsiFile, contextElement: PsiElement?, targetOffset: Int): PsiElement? {
        if (contextElement == null || file.language != RdnLanguage) return null
        return contextElement
    }

    override fun generateDoc(element: PsiElement, originalElement: PsiElement?): String? {
        val project = element.project
        val settings = RdnSettingsState.getInstance(project)
        if (!settings.hoverEnabled) return null

        val target = originalElement ?: element
        val info = detectToken(target) ?: return null

        return when (info.kind) {
            HoverTokenKind.DATE_TIME_FULL -> if (settings.hoverDateTimeEnabled) renderDateTimeFull(info, settings) else null
            HoverTokenKind.DATE_TIME_NO_MILLIS -> if (settings.hoverDateTimeEnabled) renderDateTimeNoMillis(info, settings) else null
            HoverTokenKind.DATE_ONLY -> if (settings.hoverDateTimeEnabled) renderDateOnly(info, settings) else null
            HoverTokenKind.UNIX_TIMESTAMP -> if (settings.hoverDateTimeEnabled) renderUnixTimestamp(info, settings) else null
            HoverTokenKind.TIME_ONLY -> if (settings.hoverTimeOnlyEnabled) renderTimeOnly(info, settings) else null
            HoverTokenKind.DURATION -> if (settings.hoverDurationEnabled) renderDuration(info) else null
            HoverTokenKind.BIGINT -> if (settings.hoverBigintEnabled) renderBigInt(info, settings) else null
            HoverTokenKind.BINARY_BASE64 -> if (settings.hoverBinaryEnabled) renderBinaryBase64(info, settings) else null
            HoverTokenKind.BINARY_HEX -> if (settings.hoverBinaryEnabled) renderBinaryHex(info, settings) else null
            HoverTokenKind.REGEXP -> if (settings.hoverRegexpEnabled) renderRegExp(info) else null
            HoverTokenKind.NAN -> if (settings.hoverSpecialNumbersEnabled) renderNaN() else null
            HoverTokenKind.INFINITY -> if (settings.hoverSpecialNumbersEnabled) renderInfinity() else null
            HoverTokenKind.NEG_INFINITY -> if (settings.hoverSpecialNumbersEnabled) renderNegInfinity() else null
            HoverTokenKind.MAP_KEYWORD -> if (settings.hoverCollectionsEnabled) renderMapKeyword() else null
            HoverTokenKind.SET_KEYWORD -> if (settings.hoverCollectionsEnabled) renderSetKeyword() else null
            HoverTokenKind.MAP_ARROW -> if (settings.hoverCollectionsEnabled) renderMapArrow() else null
            HoverTokenKind.TUPLE -> if (settings.hoverCollectionsEnabled) renderTuple() else null
            HoverTokenKind.IMPLICIT_MAP -> if (settings.hoverCollectionsEnabled) renderImplicitMap() else null
            HoverTokenKind.IMPLICIT_SET -> if (settings.hoverCollectionsEnabled) renderImplicitSet() else null
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Token detection
    // ════════════════════════════════════════════════════════════════════════

    /**
     * Maps the PSI element under the caret to a [HoverTokenInfo].
     *
     * For composite tokens (e.g. date-time spans multiple lexer tokens)
     * we walk sibling nodes to collect the full text and range.
     */
    internal fun detectToken(element: PsiElement): HoverTokenInfo? {
        val elementType = element.node?.elementType ?: return null

        return when (elementType) {
            // ── Date / Time ─────────────────────────────────────────
            RdnTokenTypes.AT_SIGN -> detectAtContext(element)
            RdnTokenTypes.DATE_PART -> detectDateContext(element)
            RdnTokenTypes.TIME_SEPARATOR -> detectDateContext(findSiblingBack(element, RdnTokenTypes.DATE_PART) ?: element)
            RdnTokenTypes.TIME_PART -> detectTimePartContext(element)
            RdnTokenTypes.MILLIS_PART -> detectDateContext(findSiblingBack(element, RdnTokenTypes.DATE_PART) ?: element)
            RdnTokenTypes.TIMEZONE -> detectDateContext(findSiblingBack(element, RdnTokenTypes.DATE_PART) ?: element)
            RdnTokenTypes.UNIX_TIMESTAMP -> {
                val atSign = findSiblingBack(element, RdnTokenTypes.AT_SIGN)
                val text = (atSign?.text ?: "@") + element.text
                val startOffset = atSign?.textRange?.startOffset ?: element.textRange.startOffset
                HoverTokenInfo(HoverTokenKind.UNIX_TIMESTAMP, text, TextRange(startOffset, element.textRange.endOffset))
            }

            // ── Time Only ───────────────────────────────────────────
            // (TIME_PART is handled via detectTimePartContext above)

            // ── Duration ────────────────────────────────────────────
            RdnTokenTypes.DURATION_P, RdnTokenTypes.DURATION_NUMBER, RdnTokenTypes.DURATION_UNIT, RdnTokenTypes.DURATION_T -> {
                detectDurationContext(element)
            }

            // ── BigInt ──────────────────────────────────────────────
            RdnTokenTypes.BIGINT -> {
                HoverTokenInfo(HoverTokenKind.BIGINT, element.text, element.textRange)
            }

            // ── Binary ──────────────────────────────────────────────
            RdnTokenTypes.BINARY_PREFIX -> detectBinaryContext(element)
            RdnTokenTypes.BINARY_OPEN, RdnTokenTypes.BINARY_CONTENT, RdnTokenTypes.BINARY_CLOSE -> {
                val prefix = findSiblingBack(element, RdnTokenTypes.BINARY_PREFIX)
                if (prefix != null) detectBinaryContext(prefix) else null
            }

            // ── RegExp ──────────────────────────────────────────────
            RdnTokenTypes.REGEXP_OPEN, RdnTokenTypes.REGEXP_CLOSE, RdnTokenTypes.REGEXP_FLAGS,
            RdnTokenTypes.REGEXP_CONTENT, RdnTokenTypes.REGEXP_ESCAPE,
            RdnTokenTypes.REGEXP_CHAR_CLASS_ESCAPE, RdnTokenTypes.REGEXP_QUANTIFIER,
            RdnTokenTypes.REGEXP_ANCHOR, RdnTokenTypes.REGEXP_ALTERNATION,
            RdnTokenTypes.REGEXP_DOT, RdnTokenTypes.REGEXP_GROUP_OPEN,
            RdnTokenTypes.REGEXP_GROUP_CLOSE, RdnTokenTypes.REGEXP_LOOKAROUND,
            RdnTokenTypes.REGEXP_NAMED_GROUP, RdnTokenTypes.REGEXP_NON_CAPTURING,
            RdnTokenTypes.REGEXP_BACKREFERENCE, RdnTokenTypes.REGEXP_CHAR_CLASS_OPEN,
            RdnTokenTypes.REGEXP_CHAR_CLASS_CLOSE, RdnTokenTypes.REGEXP_NEGATION,
            RdnTokenTypes.REGEXP_RANGE -> {
                detectRegExpContext(element)
            }

            // ── Special numbers ─────────────────────────────────────
            RdnTokenTypes.NAN -> HoverTokenInfo(HoverTokenKind.NAN, element.text, element.textRange)
            RdnTokenTypes.INFINITY -> HoverTokenInfo(HoverTokenKind.INFINITY, element.text, element.textRange)
            RdnTokenTypes.NEG_INFINITY -> HoverTokenInfo(HoverTokenKind.NEG_INFINITY, element.text, element.textRange)

            // ── Collection keywords ─────────────────────────────────
            RdnTokenTypes.MAP_KEYWORD -> HoverTokenInfo(HoverTokenKind.MAP_KEYWORD, element.text, element.textRange)
            RdnTokenTypes.SET_KEYWORD -> HoverTokenInfo(HoverTokenKind.SET_KEYWORD, element.text, element.textRange)
            RdnTokenTypes.ARROW -> HoverTokenInfo(HoverTokenKind.MAP_ARROW, element.text, element.textRange)
            RdnTokenTypes.LPAREN -> HoverTokenInfo(HoverTokenKind.TUPLE, element.text, element.textRange)

            // ── Implicit Map / Set detection via { ───────────────────
            RdnTokenTypes.LBRACE -> detectBraceContext(element)

            else -> null
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Context detection helpers
    // ════════════════════════════════════════════════════════════════════════

    /**
     * When the cursor is on `@`, look ahead to determine what kind of @ literal follows.
     */
    private fun detectAtContext(atSign: PsiElement): HoverTokenInfo? {
        val next = skipWhitespace(atSign.nextSibling) ?: return null
        val nextType = next.node?.elementType

        return when (nextType) {
            RdnTokenTypes.DATE_PART -> detectDateContext(next)
            RdnTokenTypes.TIME_PART -> detectTimeOnlyContext(atSign)
            RdnTokenTypes.UNIX_TIMESTAMP -> {
                val text = atSign.text + next.text
                HoverTokenInfo(HoverTokenKind.UNIX_TIMESTAMP, text, TextRange(atSign.textRange.startOffset, next.textRange.endOffset))
            }
            RdnTokenTypes.DURATION_P -> detectDurationContext(next)
            else -> null
        }
    }

    /**
     * Detects a full date/time span starting from a DATE_PART element.
     */
    private fun detectDateContext(datePart: PsiElement): HoverTokenInfo? {
        val atSign = findSiblingBack(datePart, RdnTokenTypes.AT_SIGN)
        val startOffset = atSign?.textRange?.startOffset ?: datePart.textRange.startOffset

        // Collect forward from date part to find T, TIME_PART, MILLIS_PART, TIMEZONE
        var endElement = datePart
        var current = skipWhitespace(datePart.nextSibling)

        var hasTimeSeparator = false
        var hasMillis = false

        while (current != null) {
            val type = current.node?.elementType
            when (type) {
                RdnTokenTypes.TIME_SEPARATOR -> { hasTimeSeparator = true; endElement = current }
                RdnTokenTypes.TIME_PART -> { endElement = current }
                RdnTokenTypes.MILLIS_PART -> { hasMillis = true; endElement = current }
                RdnTokenTypes.TIMEZONE -> { endElement = current; current = null; continue }
                else -> break
            }
            current = skipWhitespace(current.nextSibling)
        }

        val sb = StringBuilder()
        if (atSign != null) sb.append(atSign.text)
        var node = if (atSign != null) skipWhitespace(atSign.nextSibling) else datePart
        while (node != null && node.textRange.startOffset <= endElement.textRange.startOffset) {
            sb.append(node.text)
            node = node.nextSibling
        }
        val fullText = sb.toString()

        val range = TextRange(startOffset, endElement.textRange.endOffset)

        return if (!hasTimeSeparator) {
            HoverTokenInfo(HoverTokenKind.DATE_ONLY, fullText, range)
        } else if (hasMillis) {
            HoverTokenInfo(HoverTokenKind.DATE_TIME_FULL, fullText, range)
        } else {
            HoverTokenInfo(HoverTokenKind.DATE_TIME_NO_MILLIS, fullText, range)
        }
    }

    /**
     * Determines whether a TIME_PART belongs to a date-time or a time-only literal.
     */
    private fun detectTimePartContext(element: PsiElement): HoverTokenInfo? {
        val datePart = findSiblingBack(element, RdnTokenTypes.DATE_PART)
        return if (datePart != null) {
            detectDateContext(datePart)
        } else {
            val atSign = findSiblingBack(element, RdnTokenTypes.AT_SIGN)
            if (atSign != null) detectTimeOnlyContext(atSign) else null
        }
    }

    /**
     * Collects a TimeOnly literal from @HH:MM:SS[.mmm].
     */
    private fun detectTimeOnlyContext(atSign: PsiElement): HoverTokenInfo? {
        var endElement = atSign
        var current = skipWhitespace(atSign.nextSibling)
        while (current != null) {
            val type = current.node?.elementType
            when (type) {
                RdnTokenTypes.TIME_PART, RdnTokenTypes.MILLIS_PART -> endElement = current
                else -> break
            }
            current = skipWhitespace(current.nextSibling)
        }

        val sb = StringBuilder()
        var node: PsiElement? = atSign
        while (node != null && node.textRange.startOffset <= endElement.textRange.startOffset) {
            sb.append(node.text)
            node = node.nextSibling
        }

        return HoverTokenInfo(HoverTokenKind.TIME_ONLY, sb.toString(), TextRange(atSign.textRange.startOffset, endElement.textRange.endOffset))
    }

    /**
     * Collects a duration literal starting from a DURATION_P token.
     */
    private fun detectDurationContext(element: PsiElement): HoverTokenInfo? {
        val atSign = findSiblingBack(element, RdnTokenTypes.AT_SIGN)
        val durationP = findSiblingBack(element, RdnTokenTypes.DURATION_P) ?: if (element.node?.elementType == RdnTokenTypes.DURATION_P) element else return null
        val startOffset = atSign?.textRange?.startOffset ?: durationP.textRange.startOffset

        var endElement = durationP
        var current = durationP.nextSibling
        while (current != null) {
            val type = current.node?.elementType
            when (type) {
                RdnTokenTypes.DURATION_NUMBER, RdnTokenTypes.DURATION_UNIT, RdnTokenTypes.DURATION_T -> endElement = current
                else -> break
            }
            current = current.nextSibling
        }

        val sb = StringBuilder()
        var node: PsiElement? = atSign ?: durationP
        while (node != null && node.textRange.startOffset <= endElement.textRange.startOffset) {
            sb.append(node.text)
            node = node.nextSibling
        }

        return HoverTokenInfo(HoverTokenKind.DURATION, sb.toString(), TextRange(startOffset, endElement.textRange.endOffset))
    }

    /**
     * Collects a binary literal from prefix through closing quote.
     */
    private fun detectBinaryContext(prefix: PsiElement): HoverTokenInfo? {
        val prefixText = prefix.text
        val kind = if (prefixText == "b") HoverTokenKind.BINARY_BASE64 else HoverTokenKind.BINARY_HEX

        var endElement = prefix
        var content = StringBuilder()
        var current = prefix.nextSibling
        while (current != null) {
            val type = current.node?.elementType
            when (type) {
                RdnTokenTypes.BINARY_OPEN -> endElement = current
                RdnTokenTypes.BINARY_CONTENT -> { content.append(current.text); endElement = current }
                RdnTokenTypes.BINARY_INVALID_CHAR -> { content.append(current.text); endElement = current }
                RdnTokenTypes.BINARY_CLOSE -> { endElement = current; current = null; continue }
                else -> break
            }
            current = current.nextSibling
        }

        val fullText = prefixText + "\"" + content.toString() + "\""
        return HoverTokenInfo(kind, fullText, TextRange(prefix.textRange.startOffset, endElement.textRange.endOffset))
    }

    /**
     * Collects a regular expression literal from opening / through flags.
     */
    private fun detectRegExpContext(element: PsiElement): HoverTokenInfo? {
        // Walk back to find REGEXP_OPEN
        val open = findSiblingBack(element, RdnTokenTypes.REGEXP_OPEN) ?: if (element.node?.elementType == RdnTokenTypes.REGEXP_OPEN) element else return null

        var endElement = open
        var current = open.nextSibling
        while (current != null) {
            val type = current.node?.elementType
            when (type) {
                RdnTokenTypes.REGEXP_CLOSE -> { endElement = current }
                RdnTokenTypes.REGEXP_FLAGS -> { endElement = current; current = null; continue }
                RdnTokenTypes.REGEXP_CONTENT, RdnTokenTypes.REGEXP_ESCAPE,
                RdnTokenTypes.REGEXP_CHAR_CLASS_ESCAPE, RdnTokenTypes.REGEXP_QUANTIFIER,
                RdnTokenTypes.REGEXP_ANCHOR, RdnTokenTypes.REGEXP_ALTERNATION,
                RdnTokenTypes.REGEXP_DOT, RdnTokenTypes.REGEXP_GROUP_OPEN,
                RdnTokenTypes.REGEXP_GROUP_CLOSE, RdnTokenTypes.REGEXP_LOOKAROUND,
                RdnTokenTypes.REGEXP_NAMED_GROUP, RdnTokenTypes.REGEXP_NON_CAPTURING,
                RdnTokenTypes.REGEXP_BACKREFERENCE, RdnTokenTypes.REGEXP_CHAR_CLASS_OPEN,
                RdnTokenTypes.REGEXP_CHAR_CLASS_CLOSE, RdnTokenTypes.REGEXP_NEGATION,
                RdnTokenTypes.REGEXP_RANGE -> { endElement = current }
                else -> {
                    // If we passed REGEXP_CLOSE, check for flags
                    if (endElement.node?.elementType == RdnTokenTypes.REGEXP_CLOSE) break
                    endElement = current
                }
            }
            current = current.nextSibling
        }

        val sb = StringBuilder()
        var node: PsiElement? = open
        while (node != null && node.textRange.startOffset <= endElement.textRange.startOffset) {
            sb.append(node.text)
            node = node.nextSibling
        }

        return HoverTokenInfo(HoverTokenKind.REGEXP, sb.toString(), TextRange(open.textRange.startOffset, endElement.textRange.endOffset))
    }

    /**
     * Detects implicit Map or Set when hovering on `{`.
     * Looks ahead past the first value for `:` (object), `=>` (implicit map), `,`/`}` (implicit set).
     */
    private fun detectBraceContext(lbrace: PsiElement): HoverTokenInfo? {
        // Scan forward through siblings looking for ARROW (=>) or meaningful tokens
        var current = skipWhitespace(lbrace.nextSibling)
        var depth = 0

        while (current != null) {
            val type = current.node?.elementType
            when (type) {
                RdnTokenTypes.RBRACE -> {
                    if (depth == 0) return null // empty {} is just an object
                    depth--
                }
                RdnTokenTypes.LBRACE, RdnTokenTypes.LBRACKET, RdnTokenTypes.LPAREN -> depth++
                RdnTokenTypes.ARROW -> {
                    if (depth == 0) return HoverTokenInfo(HoverTokenKind.IMPLICIT_MAP, "{", lbrace.textRange)
                }
                RdnTokenTypes.COLON -> {
                    if (depth == 0) return null // regular object
                }
                RdnTokenTypes.COMMA -> {
                    if (depth == 0) return HoverTokenInfo(HoverTokenKind.IMPLICIT_SET, "{", lbrace.textRange)
                }
                else -> {} // skip values
            }
            current = current.nextSibling
        }
        return null
    }

    // ════════════════════════════════════════════════════════════════════════
    // Sibling navigation helpers
    // ════════════════════════════════════════════════════════════════════════

    private fun skipWhitespace(element: PsiElement?): PsiElement? {
        var current = element
        while (current != null && current.node?.elementType == RdnTokenTypes.WHITE_SPACE) {
            current = current.nextSibling
        }
        return current
    }

    private fun findSiblingBack(element: PsiElement, targetType: IElementType): PsiElement? {
        var current = element.prevSibling
        while (current != null) {
            if (current.node?.elementType == targetType) return current
            if (current.node?.elementType != RdnTokenTypes.WHITE_SPACE) {
                // Only skip whitespace; stop if we encounter another token type
                // that would indicate we passed beyond the current literal
                val type = current.node?.elementType
                if (type != null && !isPartOfCurrentLiteral(targetType, type)) return null
            }
            current = current.prevSibling
        }
        return null
    }

    /**
     * Determines whether [candidateType] could appear between the current element
     * and the target element we are searching backward for.
     */
    private fun isPartOfCurrentLiteral(targetType: IElementType, candidateType: IElementType): Boolean {
        return when (targetType) {
            RdnTokenTypes.AT_SIGN -> candidateType in setOf(
                RdnTokenTypes.DATE_PART, RdnTokenTypes.TIME_SEPARATOR, RdnTokenTypes.TIME_PART,
                RdnTokenTypes.MILLIS_PART, RdnTokenTypes.TIMEZONE, RdnTokenTypes.UNIX_TIMESTAMP,
                RdnTokenTypes.DURATION_P, RdnTokenTypes.DURATION_NUMBER, RdnTokenTypes.DURATION_UNIT,
                RdnTokenTypes.DURATION_T
            )
            RdnTokenTypes.DATE_PART -> candidateType in setOf(
                RdnTokenTypes.TIME_SEPARATOR, RdnTokenTypes.TIME_PART,
                RdnTokenTypes.MILLIS_PART, RdnTokenTypes.TIMEZONE
            )
            RdnTokenTypes.DURATION_P -> candidateType in setOf(
                RdnTokenTypes.AT_SIGN, RdnTokenTypes.DURATION_NUMBER,
                RdnTokenTypes.DURATION_UNIT, RdnTokenTypes.DURATION_T
            )
            RdnTokenTypes.BINARY_PREFIX -> candidateType in setOf(
                RdnTokenTypes.BINARY_OPEN, RdnTokenTypes.BINARY_CONTENT,
                RdnTokenTypes.BINARY_INVALID_CHAR, RdnTokenTypes.BINARY_CLOSE
            )
            RdnTokenTypes.REGEXP_OPEN -> candidateType in setOf(
                RdnTokenTypes.REGEXP_CONTENT, RdnTokenTypes.REGEXP_ESCAPE,
                RdnTokenTypes.REGEXP_CHAR_CLASS_ESCAPE, RdnTokenTypes.REGEXP_QUANTIFIER,
                RdnTokenTypes.REGEXP_ANCHOR, RdnTokenTypes.REGEXP_ALTERNATION,
                RdnTokenTypes.REGEXP_DOT, RdnTokenTypes.REGEXP_GROUP_OPEN,
                RdnTokenTypes.REGEXP_GROUP_CLOSE, RdnTokenTypes.REGEXP_LOOKAROUND,
                RdnTokenTypes.REGEXP_NAMED_GROUP, RdnTokenTypes.REGEXP_NON_CAPTURING,
                RdnTokenTypes.REGEXP_BACKREFERENCE, RdnTokenTypes.REGEXP_CHAR_CLASS_OPEN,
                RdnTokenTypes.REGEXP_CHAR_CLASS_CLOSE, RdnTokenTypes.REGEXP_NEGATION,
                RdnTokenTypes.REGEXP_RANGE, RdnTokenTypes.REGEXP_CLOSE,
                RdnTokenTypes.REGEXP_FLAGS
            )
            else -> false
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Renderers
    // ════════════════════════════════════════════════════════════════════════

    private fun renderDateTimeFull(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val instant = parseInstantFromRdn(info.text) ?: return renderFallback("DateTime", info.text)
        val formatted = RdnFormatUtils.formatDate(instant, settings.hoverDateTimeFullFormat)
        return buildHtml("DateTime") {
            row("Formatted", formatted)
            row("ISO 8601", instant.toString())
            row("Epoch ms", instant.toEpochMilli().toString())
        }
    }

    private fun renderDateTimeNoMillis(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val instant = parseInstantFromRdn(info.text) ?: return renderFallback("DateTime", info.text)
        val formatted = RdnFormatUtils.formatDate(instant, settings.hoverDateTimeNoMillisFormat)
        return buildHtml("DateTime") {
            row("Formatted", formatted)
            row("ISO 8601", instant.toString())
            row("Epoch ms", instant.toEpochMilli().toString())
        }
    }

    private fun renderDateOnly(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val date = parseDateOnlyFromRdn(info.text) ?: return renderFallback("Date", info.text)
        val formatted = RdnFormatUtils.formatLocalDate(date, settings.hoverDateTimeDateOnlyFormat)
        return buildHtml("Date") {
            row("Formatted", formatted)
            row("ISO 8601", date.toString())
        }
    }

    private fun renderUnixTimestamp(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val digits = info.text.removePrefix("@")
        val classification = RdnFormatUtils.classifyUnixTimestamp(digits)
        val num = digits.toLongOrNull() ?: return renderFallback("Unix Timestamp", info.text)
        val epochMillis = if (digits.length <= 10) num * 1000 else num
        val instant = Instant.ofEpochMilli(epochMillis)
        val formatted = RdnFormatUtils.formatDate(instant, settings.hoverDateTimeUnixFormat)
        return buildHtml("Unix Timestamp") {
            row("Precision", classification)
            row("Formatted", formatted)
            row("ISO 8601", instant.toString())
            row("Epoch ms", epochMillis.toString())
        }
    }

    private fun renderTimeOnly(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = info.text.removePrefix("@")
        val parts = parseTimeOnlyParts(raw) ?: return renderFallback("TimeOnly", info.text)
        val formatted = RdnFormatUtils.formatTimeOnly(parts.hours, parts.minutes, parts.seconds, parts.millis, settings.hoverTimeOnlyFormat)
        return buildHtml("TimeOnly") {
            row("Formatted", formatted)
            row("Hours", parts.hours.toString())
            row("Minutes", parts.minutes.toString())
            row("Seconds", parts.seconds.toString())
            if (parts.millis > 0) row("Milliseconds", parts.millis.toString())
        }
    }

    private fun renderDuration(info: HoverTokenInfo): String {
        val iso = info.text.removePrefix("@")
        val expanded = RdnFormatUtils.expandDuration(iso)
        return buildHtml("Duration") {
            row("ISO 8601", iso)
            row("Expanded", expanded)
        }
    }

    private fun renderBigInt(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val raw = info.text.removeSuffix("n")
        val grouped = RdnFormatUtils.groupDigits(raw)
        val bi = try { BigInteger(raw) } catch (_: NumberFormatException) { null }
        return buildHtml("BigInt") {
            row("Value", grouped)
            if (settings.hoverBigintShowBitLength && bi != null) {
                row("Bit length", "${bi.bitLength()} bits")
            }
            if (bi != null) {
                row("Hex", "0x${bi.toString(16).uppercase()}")
            }
        }
    }

    private fun renderBinaryBase64(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val content = extractBinaryContent(info.text, "b")
        val bytes = RdnBinaryUtils.decodeBase64ToBytes(content)
        val byteCount = bytes?.size ?: 0
        val sizeStr = RdnFormatUtils.formatByteSize(byteCount)

        return buildHtml("Binary (base64)") {
            row("Size", sizeStr)
            if (bytes != null) {
                val imageInfo = RdnBinaryUtils.detectImageFromBytes(bytes)
                if (imageInfo != null) {
                    row("Image", "${imageInfo.format} detected")
                    val dataUri = RdnBinaryUtils.toDataUri(imageInfo)
                    if (dataUri.isNotEmpty()) {
                        rawRow("<img src=\"$dataUri\" style=\"max-width: 200px; max-height: 200px;\" alt=\"${imageInfo.format} preview\"/>")
                    }
                }
                if (settings.hoverBinaryShowPreview) {
                    val ascii = RdnBinaryUtils.bytesToAsciiPreview(bytes)
                    if (ascii != null) {
                        row("ASCII", "<code>$ascii</code>")
                    }
                }
            }
        }
    }

    private fun renderBinaryHex(info: HoverTokenInfo, settings: RdnSettingsState): String {
        val content = extractBinaryContent(info.text, "x")
        val bytes = RdnBinaryUtils.decodeHexToBytes(content)
        val byteCount = bytes?.size ?: 0
        val sizeStr = RdnFormatUtils.formatByteSize(byteCount)

        return buildHtml("Binary (hex)") {
            row("Size", sizeStr)
            if (bytes != null) {
                val imageInfo = RdnBinaryUtils.detectImageFromBytes(bytes)
                if (imageInfo != null) {
                    row("Image", "${imageInfo.format} detected")
                    val dataUri = RdnBinaryUtils.toDataUri(imageInfo)
                    if (dataUri.isNotEmpty()) {
                        rawRow("<img src=\"$dataUri\" style=\"max-width: 200px; max-height: 200px;\" alt=\"${imageInfo.format} preview\"/>")
                    }
                }
                if (settings.hoverBinaryShowPreview) {
                    val ascii = RdnBinaryUtils.bytesToAsciiPreview(bytes)
                    if (ascii != null) {
                        row("ASCII", "<code>$ascii</code>")
                    }
                }
            }
            if (RdnBinaryUtils.isOddHexLength(content)) {
                rawRow("<em>Warning: odd hex length (${content.length} chars)</em>")
            }
        }
    }

    private fun renderRegExp(info: HoverTokenInfo): String {
        val text = info.text
        // Extract pattern and flags from /pattern/flags
        val lastSlash = text.lastIndexOf('/')
        if (lastSlash <= 0) return renderFallback("RegExp", text)
        val pattern = text.substring(1, lastSlash)
        val flags = text.substring(lastSlash + 1)
        val expandedFlags = RdnFormatUtils.expandRegExpFlags(flags)
        return buildHtml("RegExp") {
            row("Pattern", "<code>${escapeHtml(pattern)}</code>")
            row("Flags", if (flags.isEmpty()) "<em>none</em>" else "<code>${escapeHtml(flags)}</code> ($expandedFlags)")
        }
    }

    private fun renderNaN(): String {
        return buildHtml("NaN") {
            row("Type", "Special numeric value")
            row("IEEE 754", "Not-a-Number: result of undefined or unrepresentable mathematical operations")
            row("Note", "NaN !== NaN (not equal to itself)")
        }
    }

    private fun renderInfinity(): String {
        return buildHtml("Infinity") {
            row("Type", "Special numeric value")
            row("IEEE 754", "Positive infinity: a value greater than any finite number")
            row("Hex", "0x7FF0000000000000")
        }
    }

    private fun renderNegInfinity(): String {
        return buildHtml("-Infinity") {
            row("Type", "Special numeric value")
            row("IEEE 754", "Negative infinity: a value less than any finite number")
            row("Hex", "0xFFF0000000000000")
        }
    }

    private fun renderMapKeyword(): String {
        return buildHtml("Map") {
            row("Syntax", "<code>Map{ key =&gt; value, ... }</code>")
            row("Description", "Explicit Map literal with any-type keys. Unlike objects, Map keys can be any RDN value (numbers, booleans, dates, nested structures, etc.).")
        }
    }

    private fun renderSetKeyword(): String {
        return buildHtml("Set") {
            row("Syntax", "<code>Set{ value, ... }</code>")
            row("Description", "Explicit Set literal containing unique values. Duplicate values are not allowed.")
        }
    }

    private fun renderMapArrow(): String {
        return buildHtml("Map Entry Separator") {
            row("Syntax", "<code>=&gt;</code>")
            row("Description", "Separates keys from values in Map entries. Used in both explicit (<code>Map{ k =&gt; v }</code>) and implicit (<code>{ k =&gt; v }</code>) map syntax.")
        }
    }

    private fun renderTuple(): String {
        return buildHtml("Tuple") {
            row("Syntax", "<code>( value, ... )</code>")
            row("Description", "Fixed-length, ordered, immutable sequence of values. Unlike arrays, tuples have a specific length that is part of their type identity.")
        }
    }

    private fun renderImplicitMap(): String {
        return buildHtml("Implicit Map") {
            row("Syntax", "<code>{ key =&gt; value, ... }</code>")
            row("Description", "Implicit Map syntax using brace disambiguation. The parser detects <code>=&gt;</code> after the first value to distinguish from Object and Set.")
            row("Explicit form", "<code>Map{ key =&gt; value, ... }</code>")
        }
    }

    private fun renderImplicitSet(): String {
        return buildHtml("Implicit Set") {
            row("Syntax", "<code>{ value, value, ... }</code>")
            row("Description", "Implicit Set syntax using brace disambiguation. The parser detects <code>,</code> or <code>}</code> after the first value (without <code>:</code> or <code>=&gt;</code>) to distinguish from Object and Map.")
            row("Explicit form", "<code>Set{ value, ... }</code>")
        }
    }

    private fun renderFallback(kind: String, text: String): String {
        return buildHtml(kind) {
            row("Raw", "<code>${escapeHtml(text)}</code>")
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Parsing helpers
    // ════════════════════════════════════════════════════════════════════════

    private fun parseInstantFromRdn(text: String): Instant? {
        return try {
            val raw = text.removePrefix("@")
            // Format: YYYY-MM-DDTHH:MM:SS[.mmm]Z
            val year = raw.substring(0, 4).toInt()
            val month = raw.substring(5, 7).toInt()
            val day = raw.substring(8, 10).toInt()
            val hours = raw.substring(11, 13).toInt()
            val minutes = raw.substring(14, 16).toInt()
            val seconds = raw.substring(17, 19).toInt()
            val millis = if (raw.length > 20 && raw[19] == '.') {
                raw.substring(20, raw.length - 1).padEnd(3, '0').take(3).toInt()
            } else {
                0
            }
            LocalDate.of(year, month, day).atTime(hours, minutes, seconds, millis * 1_000_000).toInstant(ZoneOffset.UTC)
        } catch (_: Exception) {
            null
        }
    }

    private fun parseDateOnlyFromRdn(text: String): LocalDate? {
        return try {
            val raw = text.removePrefix("@")
            val year = raw.substring(0, 4).toInt()
            val month = raw.substring(5, 7).toInt()
            val day = raw.substring(8, 10).toInt()
            LocalDate.of(year, month, day)
        } catch (_: Exception) {
            null
        }
    }

    private data class TimeOnlyParts(val hours: Int, val minutes: Int, val seconds: Int, val millis: Int)

    private fun parseTimeOnlyParts(raw: String): TimeOnlyParts? {
        return try {
            // Format: HH:MM:SS[.mmm]
            val hours = raw.substring(0, 2).toInt()
            val minutes = raw.substring(3, 5).toInt()
            val seconds = raw.substring(6, 8).toInt()
            val millis = if (raw.length > 9 && raw[8] == '.') {
                raw.substring(9).padEnd(3, '0').take(3).toInt()
            } else {
                0
            }
            TimeOnlyParts(hours, minutes, seconds, millis)
        } catch (_: Exception) {
            null
        }
    }

    private fun extractBinaryContent(text: String, prefix: String): String {
        // Text format: b"content" or x"content"
        val stripped = text.removePrefix(prefix)
        return if (stripped.startsWith("\"") && stripped.endsWith("\"")) {
            stripped.substring(1, stripped.length - 1)
        } else {
            stripped.removePrefix("\"").removeSuffix("\"")
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // HTML builder
    // ════════════════════════════════════════════════════════════════════════

    private class HtmlBuilder {
        private val rows = mutableListOf<String>()

        fun row(label: String, value: String) {
            rows.add("<tr><td valign=\"top\"><b>$label:</b></td><td>$value</td></tr>")
        }

        fun rawRow(html: String) {
            rows.add("<tr><td colspan=\"2\">$html</td></tr>")
        }

        fun build(title: String): String {
            val sb = StringBuilder()
            sb.append("<div class='definition'><pre><b>$title</b></pre></div>")
            sb.append("<div class='content'>")
            sb.append("<table>")
            for (row in rows) sb.append(row)
            sb.append("</table>")
            sb.append("</div>")
            return sb.toString()
        }
    }

    private fun buildHtml(title: String, block: HtmlBuilder.() -> Unit): String {
        val builder = HtmlBuilder()
        builder.block()
        return builder.build(title)
    }

    private fun escapeHtml(text: String): String {
        return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace("\"", "&quot;")
    }
}
