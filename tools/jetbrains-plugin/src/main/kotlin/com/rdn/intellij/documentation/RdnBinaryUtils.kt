package com.rdn.intellij.documentation

import java.util.Base64

data class ImageInfo(val format: String, val data: ByteArray) {
    override fun equals(other: Any?) = other is ImageInfo && format == other.format && data.contentEquals(other.data)
    override fun hashCode() = 31 * format.hashCode() + data.contentHashCode()
}

object RdnBinaryUtils {
    private const val MAX_PREVIEW_BYTES = 64
    private const val MAX_IMAGE_BYTES = 1024 * 1024

    fun decodeBase64ToBytes(b64: String, maxBytes: Int = MAX_IMAGE_BYTES): ByteArray? {
        return try {
            val clean = b64.replace("\\s".toRegex(), "")
            val decoded = Base64.getDecoder().decode(clean)
            if (decoded.size <= maxBytes) decoded else decoded.copyOf(maxBytes)
        } catch (e: IllegalArgumentException) {
            null
        }
    }

    fun decodeHexToBytes(hex: String, maxBytes: Int = MAX_IMAGE_BYTES): ByteArray? {
        if (hex.length % 2 != 0) return null
        if (!hex.all { it.isHexDigit() }) return null
        val byteCount = minOf(hex.length / 2, maxBytes)
        return ByteArray(byteCount) { i ->
            hex.substring(i * 2, i * 2 + 2).toInt(16).toByte()
        }
    }

    fun detectImageFromBytes(bytes: ByteArray): ImageInfo? {
        if (bytes.size < 4) return null
        return when {
            bytes.size >= 4 && bytes[0] == 0x89.toByte() && bytes[1] == 0x50.toByte() && bytes[2] == 0x4E.toByte() && bytes[3] == 0x47.toByte() -> ImageInfo("PNG", bytes)
            bytes.size >= 3 && bytes[0] == 0xFF.toByte() && bytes[1] == 0xD8.toByte() && bytes[2] == 0xFF.toByte() -> ImageInfo("JPEG", bytes)
            bytes.size >= 4 && bytes[0] == 0x47.toByte() && bytes[1] == 0x49.toByte() && bytes[2] == 0x46.toByte() && bytes[3] == 0x38.toByte() -> ImageInfo("GIF", bytes)
            bytes.size >= 12 && bytes[0] == 0x52.toByte() && bytes[1] == 0x49.toByte() && bytes[2] == 0x46.toByte() && bytes[3] == 0x46.toByte() && bytes[8] == 0x57.toByte() && bytes[9] == 0x45.toByte() && bytes[10] == 0x42.toByte() && bytes[11] == 0x50.toByte() -> ImageInfo("WebP", bytes)
            bytes.size >= 2 && bytes[0] == 0x42.toByte() && bytes[1] == 0x4D.toByte() -> ImageInfo("BMP", bytes)
            bytes.size >= 4 && bytes[0] == 0x00.toByte() && bytes[1] == 0x00.toByte() && bytes[2] == 0x01.toByte() && bytes[3] == 0x00.toByte() -> ImageInfo("ICO", bytes)
            else -> null
        }
    }

    fun bytesToAsciiPreview(bytes: ByteArray, maxChars: Int = MAX_PREVIEW_BYTES): String? {
        val preview = bytes.take(maxChars).joinToString("") { byte ->
            val c = byte.toInt() and 0xFF
            if (c in 0x20..0x7E) c.toChar().toString() else "."
        }
        val printableCount = bytes.take(maxChars).count { byte ->
            val c = byte.toInt() and 0xFF
            c in 0x20..0x7E
        }
        return if (printableCount > maxChars / 4) preview else null
    }

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

    fun isOddHexLength(hex: String): Boolean = hex.length % 2 != 0

    private fun Char.isHexDigit(): Boolean = this in '0'..'9' || this in 'a'..'f' || this in 'A'..'F'
}
