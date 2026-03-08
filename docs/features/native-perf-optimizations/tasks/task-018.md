# Task 018: Implement NEON SIMD for find_string_end

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
Add the NEON implementation of `find_string_end()` behind `#[cfg(target_arch = "aarch64")]` in `simd.rs`. Use NEON intrinsics to process 16 bytes per iteration on ARM64 (Apple Silicon). Implement the bitmask extraction workaround since NEON lacks a direct `movemask` equivalent. Handle tail bytes with the scalar fallback.

## Files to Modify
- `packages/rdn-native/src/simd.rs` — add NEON implementation in `neon` submodule

## Implementation Details
**NEON implementation outline** (`#[cfg(target_arch = "aarch64")]`):

Same algorithm as SSE2, different intrinsics:
- `vld1q_u8` instead of `_mm_loadu_si128` (load 16 bytes)
- `vceqq_u8` instead of `_mm_cmpeq_epi8` (byte-wise equality comparison)
- `vorrq_u8` instead of `_mm_or_si128` (bitwise OR)
- **Bitmask extraction**: NEON lacks a direct `movemask` equivalent. Use `vshrn_n_u16` + `vget_lane_u64` to pack comparison results into a bitmask, then `trailing_zeros()`.

**Bitmask extraction approach**:
```rust
use std::arch::aarch64::*;

// After getting the combined comparison result (uint8x16_t):
// 1. Narrow 16-bit pairs into 8-bit by right-shifting
// 2. Extract as u64 and use trailing_zeros()

// Alternative approach using a lookup table or bit manipulation:
unsafe fn neon_movemask(v: uint8x16_t) -> u16 {
    // Take the high bit of each byte
    let shift = vld1q_u8([1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128].as_ptr());
    let bits = vandq_u8(v, shift);
    let paired = vpaddlq_u8(bits);    // pairwise add: 16 u8 -> 8 u16
    let quads = vpaddlq_u16(paired);  // 8 u16 -> 4 u32
    let octets = vpaddlq_u32(quads);  // 4 u32 -> 2 u64
    let lo = vgetq_lane_u64(vreinterpretq_u64_u8(octets), 0) as u8;
    let hi = vgetq_lane_u64(vreinterpretq_u64_u8(octets), 1) as u8;
    ((hi as u16) << 8) | (lo as u16)
}
```

**cfg gating**: Update the top-level `find_string_end()` to route to `neon::find_string_end()` on aarch64.

**Testing on ARM64**: Run the full test suite and SIMD boundary tests on Apple Silicon (M1/M2/M3). Verify the NEON bitmask extraction produces identical results to the SSE2 `movemask`.

## Dependencies
- Depends on: 15
- Blocks: 21

## Acceptance Criteria
- [ ] NEON implementation of `find_string_end()` exists behind `#[cfg(target_arch = "aarch64")]`
- [ ] Uses `vld1q_u8`, `vceqq_u8`, `vorrq_u8` for 16-byte stride scanning
- [ ] Bitmask extraction works correctly (NEON movemask workaround)
- [ ] Handles tail bytes with scalar fallback
- [ ] All existing tests pass on ARM64 (Apple Silicon)
- [ ] SIMD-specific boundary tests pass
- [ ] `#[cfg]` correctly gates the implementation

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.3.1, Section 8, Section 12 (Task 18)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
