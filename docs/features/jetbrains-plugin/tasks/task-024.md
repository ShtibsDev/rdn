# Task 024: Implement Binary Utilities

## References
- [Tech Design](../tech-design.md) — Sections 3.6, 6.8
- [Discovery](../discovery.md)

## Description
Create `RdnBinaryUtils.kt` with base64/hex decoding utilities, ASCII preview generation, and image format detection from magic bytes (PNG, JPEG, GIF, WebP, BMP, ICO). Used by `RdnDocumentationProvider` (task-025) to render hover content for binary literals.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/documentation/RdnBinaryUtils.kt` — Binary utility functions

## Implementation Details

### `RdnBinaryUtils.kt`

```kotlin
package com.rdn.intellij.documentation

import java.util.Base64

data class ImageInfo(
    val format: String,       // "PNG", "JPEG", "GIF", "WebP", "BMP", "ICO"
    val data: ByteArray
) {
    override fun equals(other: Any?) = other is ImageInfo && format == other.format && data.contentEquals(other.data)
    override fun hashCode() = 31 * format.hashCode() + data.contentHashCode()
}

object RdnBinaryUtils {
    private const val MAX_PREVIEW_BYTES = 64
    private const val MAX_IMAGE_BYTES = 1024 * 1024 // 1 MB

    /**
     * Decode a base64 string to a byte array.
     * Returns null if the string is not valid base64.
     * @param b64 Base64-encoded string (may contain whitespace, which is stripped)
     * @param maxBytes Maximum number of bytes to decode (for performance)
     */
    fun decodeBase64ToBytes(b64: String, maxBytes: Int = MAX_IMAGE_BYTES): ByteArray? {
        return try {
            val clean = b64.replace("\\s".toRegex(), "")
            val decoded = Base64.getDecoder().decode(clean)
            if (decoded.size <= maxBytes) decoded else decoded.copyOf(maxBytes)
        } catch (e: IllegalArgumentException) {
            null
        }
    }

    /**
     * Decode a hex string to a byte array.
     * Returns null if the string contains non-hex characters.
     * @param hex Hexadecimal string (case-insensitive, no separators)
     * @param maxBytes Maximum number of bytes to decode
     */
    fun decodeHexToBytes(hex: String, maxBytes: Int = MAX_IMAGE_BYTES): ByteArray? {
        if (hex.length % 2 != 0) return null  // odd number of hex digits
        if (!hex.all { it.isHexDigit() }) return null
        val byteCount = minOf(hex.length / 2, maxBytes)
        return ByteArray(byteCount) { i ->
            hex.substring(i * 2, i * 2 + 2).toInt(16).toByte()
        }
    }

    /**
     * Detect image format from the first few bytes (magic bytes).
     * Returns ImageInfo with format name and full data, or null if not a recognized image.
     */
    fun detectImageFromBytes(bytes: ByteArray): ImageInfo? {
        if (bytes.size < 4) return null
        return when {
            // PNG: 89 50 4E 47
            bytes.size >= 4 &&
                bytes[0] == 0x89.toByte() &&
                bytes[1] == 0x50.toByte() &&
                bytes[2] == 0x4E.toByte() &&
                bytes[3] == 0x47.toByte() -> ImageInfo("PNG", bytes)

            // JPEG: FF D8 FF
            bytes.size >= 3 &&
                bytes[0] == 0xFF.toByte() &&
                bytes[1] == 0xD8.toByte() &&
                bytes[2] == 0xFF.toByte() -> ImageInfo("JPEG", bytes)

            // GIF: 47 49 46 38 (GIF8)
            bytes.size >= 4 &&
                bytes[0] == 0x47.toByte() &&
                bytes[1] == 0x49.toByte() &&
                bytes[2] == 0x46.toByte() &&
                bytes[3] == 0x38.toByte() -> ImageInfo("GIF", bytes)

            // WebP: 52 49 46 46 ?? ?? ?? ?? 57 45 42 50 (RIFF....WEBP)
            bytes.size >= 12 &&
                bytes[0] == 0x52.toByte() &&
                bytes[1] == 0x49.toByte() &&
                bytes[2] == 0x46.toByte() &&
                bytes[3] == 0x46.toByte() &&
                bytes[8] == 0x57.toByte() &&
                bytes[9] == 0x45.toByte() &&
                bytes[10] == 0x42.toByte() &&
                bytes[11] == 0x50.toByte() -> ImageInfo("WebP", bytes)

            // BMP: 42 4D (BM)
            bytes.size >= 2 &&
                bytes[0] == 0x42.toByte() &&
                bytes[1] == 0x4D.toByte() -> ImageInfo("BMP", bytes)

            // ICO: 00 00 01 00
            bytes.size >= 4 &&
                bytes[0] == 0x00.toByte() &&
                bytes[1] == 0x00.toByte() &&
                bytes[2] == 0x01.toByte() &&
                bytes[3] == 0x00.toByte() -> ImageInfo("ICO", bytes)

            else -> null
        }
    }

    /**
     * Generate an ASCII preview of binary data.
     * Printable ASCII characters (0x20-0x7E) are shown as-is; others are shown as '.'.
     * Returns null if the data contains no printable characters.
     *
     * @param bytes The bytes to preview
     * @param maxChars Maximum number of characters in the preview
     */
    fun bytesToAsciiPreview(bytes: ByteArray, maxChars: Int = MAX_PREVIEW_BYTES): String? {
        val preview = bytes.take(maxChars).joinToString("") { byte ->
            val c = byte.toInt() and 0xFF
            if (c in 0x20..0x7E) c.toChar().toString() else "."
        }
        val printableCount = bytes.take(maxChars).count { byte ->
            val c = byte.toInt() and 0xFF
            c in 0x20..0x7E
        }
        // Only return preview if at least 25% of bytes are printable
        return if (printableCount > maxChars / 4) preview else null
    }

    /**
     * Generate an HTML data URI for an image for embedding in hover content.
     * Returns null if the data is not a recognized image format.
     */
    fun toDataUri(imageInfo: ImageInfo): String {
        val mimeType = when (imageInfo.format) {
            "PNG" -> "image/png"
            "JPEG" -> "image/jpeg"
            "GIF" -> "image/gif"
            "WebP" -> "image/webp"
            "BMP" -> "image/bmp"
            "ICO" -> "image/x-icon"
            else -> return ""
        }
        val b64 = Base64.getEncoder().encodeToString(imageInfo.data)
        return "data:$mimeType;base64,$b64"
    }

    /**
     * Check whether hex data has an odd number of digits (which is invalid).
     */
    fun isOddHexLength(hex: String): Boolean = hex.length % 2 != 0

    private fun Char.isHexDigit(): Boolean = this in '0'..'9' || this in 'a'..'f' || this in 'A'..'F'
}
```

## Acceptance Criteria
- [ ] `decodeBase64ToBytes("SGVsbG8=")` returns `byteArrayOf(72, 101, 108, 108, 111)` ("Hello")
- [ ] `decodeBase64ToBytes("not-base64!!!")` returns `null`
- [ ] `decodeHexToBytes("48656C6C6F")` returns `byteArrayOf(0x48, 0x65, 0x6C, 0x6C, 0x6F)` ("Hello")
- [ ] `decodeHexToBytes("12345")` returns `null` (odd length)
- [ ] `decodeHexToBytes("GGGG")` returns `null` (non-hex character)
- [ ] PNG magic bytes `[0x89, 0x50, 0x4E, 0x47, ...]` are detected as `ImageInfo("PNG", ...)`
- [ ] JPEG magic bytes `[0xFF, 0xD8, 0xFF, ...]` are detected as `ImageInfo("JPEG", ...)`
- [ ] `detectImageFromBytes(byteArrayOf(1, 2, 3, 4))` returns `null` (no image magic bytes)
- [ ] `bytesToAsciiPreview(byteArrayOf(72, 101, 108, 108, 111))` returns `"Hello"`
- [ ] `bytesToAsciiPreview(byteArrayOf(0, 1, 2, 3))` returns `null` (no printable characters)
- [ ] `isOddHexLength("123")` returns `true`
- [ ] `isOddHexLength("1234")` returns `false`

## Dependencies
- Depends on: task-001
- Blocks: task-025
