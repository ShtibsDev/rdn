# Task 017: Implement SSE2 SIMD for find_string_end

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
Add the SSE2 implementation of `find_string_end()` behind `#[cfg(target_arch = "x86_64")]` in `simd.rs`. Use SSE2 intrinsics to process 16 bytes per iteration, scanning for `"` and `\` characters. Handle tail bytes with the scalar fallback. Add SIMD-specific tests for boundary conditions.

## Files to Modify
- `packages/rdn-native/src/simd.rs` — add SSE2 implementation in `sse2` submodule

## Implementation Details
**SSE2 implementation outline** (`#[cfg(target_arch = "x86_64")]`):

1. Load two 128-bit constants: `quote_mask` = 16 copies of `"` (0x22), `backslash_mask` = 16 copies of `\` (0x5C).
2. Process 16 bytes per iteration:
   - `_mm_loadu_si128` to load 16 bytes (unaligned load).
   - `_mm_cmpeq_epi8` against `quote_mask` -> `quote_hits`.
   - `_mm_cmpeq_epi8` against `backslash_mask` -> `backslash_hits`.
   - `_mm_or_si128(quote_hits, backslash_hits)` -> `combined`.
   - `_mm_movemask_epi8(combined)` -> `mask` (16-bit bitmask).
   - If `mask != 0`: find lowest set bit via `mask.trailing_zeros()`. Determine if it's a quote or backslash. If quote, return `(position, has_escape)`. If backslash, set `has_escape = true`, skip 2 bytes (backslash + escaped char), resume.
3. Also check for control characters (`< 0x20`) to detect invalid unescaped control chars. Use `_mm_cmplt_epi8` with a zeroed vector after XOR with 0x80 to handle signed comparison (since `_mm_cmplt_epi8` does signed comparison).
4. Handle the tail (< 16 remaining bytes) with the scalar fallback to avoid out-of-bounds reads.

**Important intrinsics**:
```rust
use std::arch::x86_64::*;

unsafe fn find_string_end_sse2(bytes: &[u8], start: usize) -> (usize, bool) {
    let quote = _mm_set1_epi8(b'"' as i8);
    let backslash = _mm_set1_epi8(b'\\' as i8);
    let mut pos = start;
    let mut has_escape = false;

    while pos + 16 <= bytes.len() {
        let chunk = _mm_loadu_si128(bytes.as_ptr().add(pos) as *const __m128i);
        let quote_cmp = _mm_cmpeq_epi8(chunk, quote);
        let bs_cmp = _mm_cmpeq_epi8(chunk, backslash);
        let combined = _mm_or_si128(quote_cmp, bs_cmp);
        let mask = _mm_movemask_epi8(combined) as u32;
        if mask != 0 {
            let offset = mask.trailing_zeros() as usize;
            if bytes[pos + offset] == b'"' {
                return (pos + offset, has_escape);
            } else {
                // backslash
                has_escape = true;
                pos += offset + 2; // skip backslash + escaped char
                continue;
            }
        }
        pos += 16;
    }
    // Tail: use scalar fallback for remaining bytes
    scalar::find_string_end_from(bytes, pos, has_escape)
}
```

**cfg gating**: Update the top-level `find_string_end()` to route to `sse2::find_string_end()` on x86_64.

**SIMD-specific boundary tests** (from tech design Section 8):
- Strings shorter than 16 bytes (no SIMD pass)
- Strings exactly 16, 32, 48 bytes (aligned to register width)
- Strings with `"` or `\` at every possible position within a 16-byte window
- Strings ending mid-register (e.g., 17 bytes -- one full SIMD pass + 1 byte tail)
- UTF-8 multi-byte sequences spanning SIMD boundaries
- Empty strings, single-character strings

## Dependencies
- Depends on: 15
- Blocks: 21

## Acceptance Criteria
- [ ] SSE2 implementation of `find_string_end()` exists behind `#[cfg(target_arch = "x86_64")]`
- [ ] Uses `_mm_loadu_si128`, `_mm_cmpeq_epi8`, `_mm_or_si128`, `_mm_movemask_epi8`
- [ ] Handles tail bytes with scalar fallback
- [ ] Control character detection (`< 0x20`) works correctly
- [ ] All existing tests pass on x86_64
- [ ] SIMD-specific boundary tests pass
- [ ] `#[cfg]` correctly gates the implementation

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.3.1, Section 8, Section 12 (Task 17)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
