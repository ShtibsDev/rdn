# Task 023: Update documentation

## Status: done

## Tier: Wrap-up

## Description
Update all relevant documentation to reflect the performance optimizations applied to the native extension. Document any behavioral differences (float formatting with ryu vs Python repr). Update build instructions if needed. Update `CLAUDE.md` with notes about the new modules (`simd.rs`, `cache.rs`, `buffer.rs`), the release profile, and the SIMD architecture.

## Files to Modify
- `packages/rdn-native/README.md` — document performance optimizations, new modules, build instructions (create if absent)
- `packages/rdn-python/README.md` — note performance improvements, float formatting differences
- `CLAUDE.md` — add notes about new modules, release profile, SIMD architecture

## Implementation Details
**`packages/rdn-native/README.md`**:
- Document the three tiers of optimization applied
- List the new modules: `simd.rs` (SIMD string scanning), `cache.rs` (TypeCache + KeyCache), `buffer.rs` (WriteBuffer)
- Note the release profile settings (`opt-level = 3`, `lto = "fat"`, `codegen-units = 1`, `panic = "abort"`)
- Document the SIMD architecture: SSE2 on x86_64, NEON on aarch64, scalar fallback
- Note new dependencies: `itoa`, `ryu`, `xxhash-rust`, `smallvec`
- Include benchmark results summary from Task 22

**`packages/rdn-python/README.md`**:
- Note that the native extension has been significantly optimized
- Document float formatting differences: `ryu` vs Python `repr()` -- minor differences possible in edge cases, all mathematically equivalent
- Reference the native extension README for details

**`CLAUDE.md`**:
- Update the "Python Native Extension (`packages/rdn-native/`)" section with:
  - New modules: `simd.rs`, `cache.rs`, `buffer.rs`
  - Release profile configuration
  - SIMD architecture (SSE2 + NEON + scalar fallback)
  - TypeCache and KeyCache design
  - WriteBuffer for direct-to-buffer serialization
  - Dependencies: itoa, ryu, xxhash-rust, smallvec

## Dependencies
- Depends on: 22
- Blocks: 24

## Acceptance Criteria
- [ ] `packages/rdn-native/README.md` documents all optimizations and new modules
- [ ] `packages/rdn-python/README.md` notes float formatting differences
- [ ] `CLAUDE.md` is updated with new module descriptions and architecture notes
- [ ] Build instructions are accurate
- [ ] Float formatting differences are clearly documented

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 7, Section 12 (Task 23)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
