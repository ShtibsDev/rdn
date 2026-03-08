/// SIMD-accelerated string scanning with scalar fallback.
///
/// Provides `find_string_end()` (parser hot path) and `needs_escape()`
/// (serializer hot path). Platform-specific SIMD implementations are
/// used when available (SSE2 on x86_64, NEON on aarch64), with a
/// scalar fallback for other architectures.

// ---------------------------------------------------------------------------
// Scalar implementations (always available)
// ---------------------------------------------------------------------------

mod scalar {
    /// Scan forward from `start` looking for `"` or `\` in the byte slice.
    ///
    /// Returns `(end_pos, has_escape)` where `end_pos` is the position of the
    /// closing `"` (or `bytes.len()` if unterminated) and `has_escape` indicates
    /// whether any `\` was found before the closing quote.
    #[inline]
    pub fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool) {
        let mut pos = start;
        let len = bytes.len();
        let mut has_escape = false;

        while pos < len {
            let c = bytes[pos];

            if c == b'"' {
                return (pos, has_escape);
            }

            if c == b'\\' {
                has_escape = true;
                pos += 1; // skip backslash
                if pos >= len {
                    break;
                }
                // Skip the escaped character (including \uXXXX which the
                // parser handles later — we only need to skip one byte here
                // so we don't mistake the next char for a quote or backslash)
                pos += 1;
                continue;
            }

            // Control characters (< 0x20) — continue scanning; the parser
            // validates these separately after find_string_end returns.
            pos += 1;
        }

        (len, has_escape)
    }

    /// Scan a byte slice and return the index of the first byte that needs
    /// escaping, or `None` if no escaping is needed.
    ///
    /// A byte needs escaping if:
    /// `b < 0x20 || b == b'"' || b == b'\\' || (ensure_ascii && b > 0x7F)`
    #[inline]
    pub fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize> {
        for (i, &b) in bytes.iter().enumerate() {
            if b < 0x20 || b == b'"' || b == b'\\' {
                return Some(i);
            }
            if ensure_ascii && b > 0x7F {
                return Some(i);
            }
        }
        None
    }
}

// ---------------------------------------------------------------------------
// SSE2 implementations (x86_64)
// ---------------------------------------------------------------------------

#[cfg(target_arch = "x86_64")]
mod sse2 {
    #[cfg(target_arch = "x86_64")]
    use std::arch::x86_64::*;

    /// SSE2-accelerated `find_string_end`. Processes 16 bytes at a time,
    /// searching for `"` or `\` using packed byte comparisons.
    ///
    /// # Safety
    /// Requires SSE2 (guaranteed on all x86_64 CPUs).
    #[target_feature(enable = "sse2")]
    pub unsafe fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool) {
        let len = bytes.len();
        let mut pos = start;
        let mut has_escape = false;

        let quote = _mm_set1_epi8(b'"' as i8);
        let backslash = _mm_set1_epi8(b'\\' as i8);

        // Process 16 bytes at a time
        while pos + 16 <= len {
            let chunk = _mm_loadu_si128(bytes.as_ptr().add(pos) as *const __m128i);
            let quote_cmp = _mm_cmpeq_epi8(chunk, quote);
            let bs_cmp = _mm_cmpeq_epi8(chunk, backslash);
            let combined = _mm_or_si128(quote_cmp, bs_cmp);
            let mask = _mm_movemask_epi8(combined) as u32;

            if mask != 0 {
                let offset = mask.trailing_zeros() as usize;
                let byte_pos = pos + offset;
                if bytes[byte_pos] == b'"' {
                    return (byte_pos, has_escape);
                } else {
                    // backslash
                    has_escape = true;
                    pos = byte_pos + 2; // skip backslash + escaped char
                    continue;
                }
            }
            pos += 16;
        }

        // Scalar tail
        while pos < len {
            match bytes[pos] {
                b'"' => return (pos, has_escape),
                b'\\' => { has_escape = true; pos += 2; continue; }
                _ => pos += 1,
            }
        }
        (len, has_escape)
    }

    /// SSE2-accelerated `needs_escape`. Processes 16 bytes at a time,
    /// checking for control chars (< 0x20), `"`, `\`, and optionally
    /// non-ASCII bytes (>= 0x80).
    ///
    /// # Safety
    /// Requires SSE2 (guaranteed on all x86_64 CPUs).
    #[target_feature(enable = "sse2")]
    pub unsafe fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize> {
        let len = bytes.len();
        let mut pos = 0;

        let quote = _mm_set1_epi8(b'"' as i8);
        let backslash = _mm_set1_epi8(b'\\' as i8);
        let space = _mm_set1_epi8(0x20);

        while pos + 16 <= len {
            let chunk = _mm_loadu_si128(bytes.as_ptr().add(pos) as *const __m128i);

            // Check: b == '"' || b == '\\'
            let q = _mm_cmpeq_epi8(chunk, quote);
            let bs = _mm_cmpeq_epi8(chunk, backslash);

            // Check: b < 0x20 (control chars)
            // SSE2 only has signed comparison, so we use the fact that
            // bytes 0x00-0x1F as signed i8 are 0-31, all less than 32
            let ctrl = _mm_cmplt_epi8(chunk, space);

            let mut combined = _mm_or_si128(_mm_or_si128(q, bs), ctrl);

            // Check: b >= 0x80 (non-ASCII) if ensure_ascii
            if ensure_ascii {
                // Bytes >= 0x80 have the high bit set. As signed, they are negative.
                // _mm_cmplt_epi8(chunk, zero) catches all bytes with high bit set
                let zero = _mm_setzero_si128();
                let non_ascii = _mm_cmplt_epi8(chunk, zero);
                combined = _mm_or_si128(combined, non_ascii);
            }

            let mask = _mm_movemask_epi8(combined) as u32;
            if mask != 0 {
                return Some(pos + mask.trailing_zeros() as usize);
            }
            pos += 16;
        }

        // Scalar tail
        while pos < len {
            let b = bytes[pos];
            if b < 0x20 || b == b'"' || b == b'\\' || (ensure_ascii && b > 0x7F) {
                return Some(pos);
            }
            pos += 1;
        }
        None
    }
}

// ---------------------------------------------------------------------------
// NEON implementations (aarch64)
// ---------------------------------------------------------------------------

#[cfg(target_arch = "aarch64")]
mod neon {
    use std::arch::aarch64::*;

    /// Convert a NEON 16-byte comparison result to a 16-bit mask
    /// (one bit per byte, bit set if the comparison matched).
    #[inline(always)]
    unsafe fn neon_movemask(v: uint8x16_t) -> u16 {
        // Shift each byte right by 7 to extract the high bit (each byte becomes 0 or 1)
        let high_bits = vshrq_n_u8(v, 7);
        // Pack 16 bytes into a bitmask using multiply-and-add with power-of-2 weights
        let powers_low: [u8; 8] = [1, 2, 4, 8, 16, 32, 64, 128];
        let power_vec = vld1_u8(powers_low.as_ptr());
        let low = vget_low_u8(high_bits);
        let high = vget_high_u8(high_bits);
        let low_bits = vaddv_u8(vmul_u8(low, power_vec)) as u16;
        let high_bits_val = vaddv_u8(vmul_u8(high, power_vec)) as u16;
        low_bits | (high_bits_val << 8)
    }

    /// NEON-accelerated `find_string_end`. Processes 16 bytes at a time,
    /// searching for `"` or `\` using packed byte comparisons.
    ///
    /// NEON is always available on aarch64, so no `#[target_feature]` needed.
    pub fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool) {
        let len = bytes.len();
        let mut pos = start;
        let mut has_escape = false;

        let quote = unsafe { vdupq_n_u8(b'"') };
        let backslash = unsafe { vdupq_n_u8(b'\\') };

        while pos + 16 <= len {
            let chunk = unsafe { vld1q_u8(bytes.as_ptr().add(pos)) };
            let quote_cmp = unsafe { vceqq_u8(chunk, quote) };
            let bs_cmp = unsafe { vceqq_u8(chunk, backslash) };
            let combined = unsafe { vorrq_u8(quote_cmp, bs_cmp) };

            let mask = unsafe { neon_movemask(combined) };

            if mask != 0 {
                let offset = mask.trailing_zeros() as usize;
                let byte_pos = pos + offset;
                if bytes[byte_pos] == b'"' {
                    return (byte_pos, has_escape);
                } else {
                    has_escape = true;
                    pos = byte_pos + 2;
                    continue;
                }
            }
            pos += 16;
        }

        // Scalar tail
        while pos < len {
            match bytes[pos] {
                b'"' => return (pos, has_escape),
                b'\\' => { has_escape = true; pos += 2; continue; }
                _ => pos += 1,
            }
        }
        (len, has_escape)
    }

    /// NEON-accelerated `needs_escape`. Processes 16 bytes at a time,
    /// checking for control chars (< 0x20), `"`, `\`, and optionally
    /// non-ASCII bytes (>= 0x80).
    pub fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize> {
        let len = bytes.len();
        let mut pos = 0;

        let quote = unsafe { vdupq_n_u8(b'"') };
        let backslash = unsafe { vdupq_n_u8(b'\\') };
        let space = unsafe { vdupq_n_u8(0x20) };

        while pos + 16 <= len {
            let chunk = unsafe { vld1q_u8(bytes.as_ptr().add(pos)) };
            let q = unsafe { vceqq_u8(chunk, quote) };
            let bs = unsafe { vceqq_u8(chunk, backslash) };
            let ctrl = unsafe { vcltq_u8(chunk, space) };
            let mut combined = unsafe { vorrq_u8(vorrq_u8(q, bs), ctrl) };

            if ensure_ascii {
                let high = unsafe { vdupq_n_u8(0x80) };
                let non_ascii = unsafe { vcgeq_u8(chunk, high) };
                combined = unsafe { vorrq_u8(combined, non_ascii) };
            }

            let mask = unsafe { neon_movemask(combined) };
            if mask != 0 {
                return Some(pos + mask.trailing_zeros() as usize);
            }
            pos += 16;
        }

        // Scalar tail
        while pos < len {
            let b = bytes[pos];
            if b < 0x20 || b == b'"' || b == b'\\' || (ensure_ascii && b > 0x7F) {
                return Some(pos);
            }
            pos += 1;
        }
        None
    }
}

// ---------------------------------------------------------------------------
// Public dispatch functions
// ---------------------------------------------------------------------------

/// Scan forward from `start` looking for `"` or `\` in the byte slice.
///
/// Returns `(end_pos, has_escape)` where `end_pos` is the position of the
/// closing `"` (or `bytes.len()` if unterminated) and `has_escape` indicates
/// whether any `\` was found before the closing quote.
#[inline]
pub fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool) {
    #[cfg(target_arch = "x86_64")]
    { return unsafe { sse2::find_string_end(bytes, start) }; }
    #[cfg(target_arch = "aarch64")]
    { return neon::find_string_end(bytes, start); }
    #[cfg(not(any(target_arch = "x86_64", target_arch = "aarch64")))]
    { return scalar::find_string_end(bytes, start); }
}

/// Scan a byte slice and return the index of the first byte that needs
/// escaping, or `None` if no escaping is needed.
///
/// A byte needs escaping if:
/// `b < 0x20 || b == b'"' || b == b'\\' || (ensure_ascii && b > 0x7F)`
#[inline]
pub fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize> {
    #[cfg(target_arch = "x86_64")]
    { return unsafe { sse2::needs_escape(bytes, ensure_ascii) }; }
    #[cfg(target_arch = "aarch64")]
    { return neon::needs_escape(bytes, ensure_ascii); }
    #[cfg(not(any(target_arch = "x86_64", target_arch = "aarch64")))]
    { return scalar::needs_escape(bytes, ensure_ascii); }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;

    // -- find_string_end ---------------------------------------------------

    #[test]
    fn finds_closing_quote_no_escape() {
        let input = b"hello\"rest";
        let (pos, esc) = find_string_end(input, 0);
        assert_eq!(pos, 5);
        assert!(!esc);
    }

    #[test]
    fn finds_closing_quote_with_escape() {
        let input = b"he\\\"llo\"rest";
        let (pos, esc) = find_string_end(input, 0);
        assert_eq!(pos, 7);
        assert!(esc);
    }

    #[test]
    fn escaped_backslash_before_quote() {
        // \\\" means escaped backslash then closing quote
        let input = b"ab\\\\\"rest";
        let (pos, esc) = find_string_end(input, 0);
        assert_eq!(pos, 4);
        assert!(esc);
    }

    #[test]
    fn unterminated_string() {
        let input = b"hello world";
        let (pos, esc) = find_string_end(input, 0);
        assert_eq!(pos, input.len());
        assert!(!esc);
    }

    #[test]
    fn empty_string() {
        let input = b"\"rest";
        let (pos, esc) = find_string_end(input, 0);
        assert_eq!(pos, 0);
        assert!(!esc);
    }

    #[test]
    fn start_offset() {
        let input = b"XXXhello\"rest";
        let (pos, esc) = find_string_end(input, 3);
        assert_eq!(pos, 8);
        assert!(!esc);
    }

    #[test]
    fn control_chars_not_treated_as_end() {
        let input = b"ab\x01cd\"rest";
        let (pos, esc) = find_string_end(input, 0);
        assert_eq!(pos, 5);
        assert!(!esc);
    }

    #[test]
    fn backslash_at_end_of_input() {
        let input = b"abc\\";
        let (pos, esc) = find_string_end(input, 0);
        assert_eq!(pos, input.len());
        assert!(esc);
    }

    // Test that exercises the SIMD path (input > 16 bytes)
    #[test]
    fn find_string_end_long_input() {
        // 32 bytes of 'a' then a closing quote
        let mut input = vec![b'a'; 32];
        input.push(b'"');
        input.extend_from_slice(b"rest");
        let (pos, esc) = find_string_end(&input, 0);
        assert_eq!(pos, 32);
        assert!(!esc);
    }

    #[test]
    fn find_string_end_long_with_escape() {
        // 20 bytes of 'a', then \n, then more 'a's, then quote
        let mut input = vec![b'a'; 20];
        input.push(b'\\');
        input.push(b'n');
        input.extend_from_slice(&[b'a'; 10]);
        input.push(b'"');
        // Quote is at index 32: 20 (a) + 1 (\) + 1 (n) + 10 (a) = 32
        let (pos, esc) = find_string_end(&input, 0);
        assert_eq!(pos, 32);
        assert!(esc);
    }

    #[test]
    fn find_string_end_escape_in_first_chunk() {
        // Escape at position 5 within the first 16-byte chunk
        let mut input = vec![b'a'; 5];
        input.push(b'\\');
        input.push(b'"'); // this is the escaped quote
        input.extend_from_slice(&[b'a'; 20]);
        input.push(b'"'); // this is the real closing quote
        let (pos, esc) = find_string_end(&input, 0);
        assert_eq!(pos, 27);
        assert!(esc);
    }

    // -- needs_escape -------------------------------------------------------

    #[test]
    fn no_escape_needed_ascii() {
        assert_eq!(needs_escape(b"hello world", false), None);
    }

    #[test]
    fn escape_control_char() {
        assert_eq!(needs_escape(b"ab\ncd", false), Some(2));
    }

    #[test]
    fn escape_quote() {
        assert_eq!(needs_escape(b"ab\"cd", false), Some(2));
    }

    #[test]
    fn escape_backslash() {
        assert_eq!(needs_escape(b"ab\\cd", false), Some(2));
    }

    #[test]
    fn escape_high_byte_when_ensure_ascii() {
        assert_eq!(needs_escape(b"ab\x80cd", true), Some(2));
    }

    #[test]
    fn no_escape_high_byte_when_not_ensure_ascii() {
        assert_eq!(needs_escape(b"ab\x80cd", false), None);
    }

    #[test]
    fn empty_input() {
        assert_eq!(needs_escape(b"", false), None);
        let (pos, esc) = find_string_end(b"", 0);
        assert_eq!(pos, 0);
        assert!(!esc);
    }

    #[test]
    fn escape_first_byte() {
        assert_eq!(needs_escape(b"\x00hello", false), Some(0));
    }

    // Tests that exercise the SIMD path for needs_escape (input > 16 bytes)
    #[test]
    fn needs_escape_long_clean() {
        let input = b"abcdefghijklmnopqrstuvwxyz012345";
        assert_eq!(needs_escape(input, false), None);
    }

    #[test]
    fn needs_escape_long_with_quote() {
        let mut input = vec![b'a'; 20];
        input.push(b'"');
        input.extend_from_slice(&[b'b'; 10]);
        assert_eq!(needs_escape(&input, false), Some(20));
    }

    #[test]
    fn needs_escape_long_with_control() {
        let mut input = vec![b'a'; 25];
        input.push(b'\n');
        assert_eq!(needs_escape(&input, false), Some(25));
    }

    #[test]
    fn needs_escape_long_ascii_mode() {
        let mut input = vec![b'a'; 18];
        input.push(0x80);
        assert_eq!(needs_escape(&input, true), Some(18));
        assert_eq!(needs_escape(&input, false), None);
    }
}
