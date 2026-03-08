# Task 019: Implement SSE2 and NEON SIMD for needs_escape

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
Add SSE2 and NEON implementations of `needs_escape()` in `simd.rs`. Use the same SIMD pattern as `find_string_end()` but check for `< 0x20`, `"`, `\`, and optionally `> 0x7F` (when `ensure_ascii` is true). Return the first position that needs escaping, or `None` if the entire string is clean.

## Files to Modify
- `packages/rdn-native/src/simd.rs` — add SSE2 and NEON implementations of `needs_escape()` in respective submodules

## Implementation Details
**SSE2 implementation** (`#[cfg(target_arch = "x86_64")]`):

Same pattern as `find_string_end()` -- load 16 bytes, compare against `"`, `\`, and range-check for `< 0x20`:

```rust
unsafe fn needs_escape_sse2(bytes: &[u8], ensure_ascii: bool) -> Option<usize> {
    let quote = _mm_set1_epi8(b'"' as i8);
    let backslash = _mm_set1_epi8(b'\\' as i8);
    let space = _mm_set1_epi8(0x20);
    let mut pos = 0;

    while pos + 16 <= bytes.len() {
        let chunk = _mm_loadu_si128(bytes.as_ptr().add(pos) as *const __m128i);

        // Check for " and \
        let quote_cmp = _mm_cmpeq_epi8(chunk, quote);
        let bs_cmp = _mm_cmpeq_epi8(chunk, backslash);

        // Check for < 0x20 (control characters)
        // _mm_cmplt_epi8 does signed comparison, so XOR with 0x80 to convert
        // unsigned range [0x00, 0x1F] to signed range [-128, -97]
        let ctrl_cmp = _mm_cmplt_epi8(chunk, space);
        // Note: This works because bytes < 0x20 are also < 0x20 in signed comparison
        // when the values are in range [0, 0x1F] (all positive, so signed == unsigned)

        let mut combined = _mm_or_si128(quote_cmp, _mm_or_si128(bs_cmp, ctrl_cmp));

        if ensure_ascii {
            // Check for > 0x7F (high bit set)
            let high_bit = _mm_set1_epi8(0x80u8 as i8);
            let high_cmp = _mm_and_si128(chunk, high_bit);
            let high_mask = _mm_cmpeq_epi8(high_cmp, high_bit);
            combined = _mm_or_si128(combined, high_mask);
        }

        let mask = _mm_movemask_epi8(combined) as u32;
        if mask != 0 {
            return Some(pos + mask.trailing_zeros() as usize);
        }
        pos += 16;
    }

    // Scalar tail
    scalar::needs_escape_from(bytes, pos, ensure_ascii)
}
```

**NEON implementation** (`#[cfg(target_arch = "aarch64")]`):
Same logic, using NEON intrinsics (`vld1q_u8`, `vceqq_u8`, `vorrq_u8`) and the same bitmask extraction approach from Task 18.

**cfg gating**: Update the top-level `needs_escape()` to route to the appropriate architecture-specific implementation.

## Dependencies
- Depends on: 16
- Blocks: 21

## Acceptance Criteria
- [ ] SSE2 implementation of `needs_escape()` exists behind `#[cfg(target_arch = "x86_64")]`
- [ ] NEON implementation of `needs_escape()` exists behind `#[cfg(target_arch = "aarch64")]`
- [ ] Checks for `< 0x20`, `"`, `\`, and `> 0x7F` (when ensure_ascii)
- [ ] Returns first position needing escape, or `None` if clean
- [ ] Escape detection behavior is identical to scalar fallback
- [ ] All tests pass on both x86_64 and ARM64

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.3.2, Section 12 (Task 19)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
