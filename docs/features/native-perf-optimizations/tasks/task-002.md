# Task 002: Add Cargo release profile

## Status: pending

## Tier: Tier 1: Build & Low-Hanging Fruit

## Description
Add a `[profile.release]` section to the native extension's `Cargo.toml` with optimized settings: `opt-level = 3`, `lto = "fat"`, `codegen-units = 1`, `panic = "abort"`. This matches orjson's build configuration and provides significant performance gains with zero code changes. All `unwrap()` calls in the codebase have been audited (see discovery.md Section 8) and are provably safe, so `panic = "abort"` is acceptable.

## Files to Modify
- `packages/rdn-native/Cargo.toml` — add `[profile.release]` section after `[dependencies]`

## Implementation Details
Add the following section to `Cargo.toml` after the `[dependencies]` section:

```toml
[profile.release]
opt-level = 3
lto = "fat"
codegen-units = 1
panic = "abort"
```

**Rationale for each setting:**
- `opt-level = 3`: Maximum optimization level
- `lto = "fat"`: Full link-time optimization across all crates; increases compile time but is one-time for release builds
- `codegen-units = 1`: Forces single codegen unit, enabling better cross-function optimization (default is 16, which limits optimization scope)
- `panic = "abort"`: Eliminates unwinding code. Safe because all `unwrap()` calls have been audited as provably safe

No code changes required. Rebuild with `maturin develop --release` and verify all tests pass.

## Dependencies
- Depends on: 1
- Blocks: 7

## Acceptance Criteria
- [ ] `Cargo.toml` has `[profile.release]` section with all four settings
- [ ] `maturin develop --release` succeeds without errors
- [ ] All pytest tests pass
- [ ] No functional changes (output is identical)

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.1.1, Section 12 (Task 2)
- Discovery: `docs/features/native-perf-optimizations/discovery.md` Section 8
