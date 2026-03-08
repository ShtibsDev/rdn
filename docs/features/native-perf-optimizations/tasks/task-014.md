# Task 014: Create simd.rs with scalar fallback

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
Create a new `simd.rs` module with the `find_string_end()` and `needs_escape()` functions. Initially implement only the scalar fallback versions -- extract the byte-by-byte scanning logic from `parse_string()` and `escape_string()` into the new functions. This establishes the interface and integration points without introducing SIMD yet.

## Files to Modify
- `packages/rdn-native/src/simd.rs` — new file with scalar implementations
- `packages/rdn-native/src/lib.rs` — add `mod simd`

## Implementation Details
**File**: New `src/simd.rs` module.

**Interface for `find_string_end()`**:
```rust
/// Scan for the end of a JSON/RDN string starting at `start`.
/// The byte at `start - 1` should be the opening `"`.
/// Returns (pos, has_escape) where `pos` is the index of the closing `"`
/// (or bytes.len() if unterminated) and `has_escape` is true if any `\` was seen.
pub fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool);
```

**Interface for `needs_escape()`**:
```rust
/// Scan a byte slice and return the position of the first byte that
/// needs escaping (< 0x20, `"`, `\`, or > 0x7F if ensure_ascii).
/// Returns None if no bytes need escaping.
pub fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize>;
```

**Scalar `find_string_end()` implementation**: Extract the current byte-by-byte loop from `parse_string()` (parser.rs lines 114-149) into the `find_string_end` interface. The logic walks byte by byte, checking for `"` (end of string) and `\` (escape sequence). Also checks for control characters (`< 0x20`).

**Scalar `needs_escape()` implementation**: Extract the current escape detection loop from `escape_string()` (serializer.rs lines 82-91). The logic checks each byte for: `b < 0x20 || b == '"' || b == '\\' || (ensure_ascii && b > 0x7F)`.

**Module structure** (prepared for future SIMD additions):
```rust
#[cfg(target_arch = "x86_64")]
mod sse2 { /* ... will be added in Task 17 */ }
#[cfg(target_arch = "aarch64")]
mod neon { /* ... will be added in Task 18 */ }
mod scalar { ... }

pub fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool) {
    // For now, always use scalar. SIMD paths added in Tasks 17-18.
    scalar::find_string_end(bytes, start)
}

pub fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize> {
    // For now, always use scalar. SIMD paths added in Task 19.
    scalar::needs_escape(bytes, ensure_ascii)
}
```

## Dependencies
- Depends on: 13
- Blocks: 15, 16

## Acceptance Criteria
- [ ] `simd.rs` exists with scalar implementations of `find_string_end()` and `needs_escape()`
- [ ] Module structure is prepared for future SIMD additions with cfg gates
- [ ] `mod simd` is declared in `lib.rs`
- [ ] All tests pass (no behavioral change yet -- functions are not integrated)

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 5.2, Section 6.3.1, Section 6.3.2, Section 12 (Task 14)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
