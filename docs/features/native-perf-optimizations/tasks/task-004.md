# Task 004: Replace float formatting with ryu

## Status: pending

## Tier: Tier 1: Build & Low-Hanging Fruit

## Description
Add the `ryu` crate as a dependency and replace the Python `repr()` call for float formatting in the serializer with `ryu::Buffer`-based formatting. This eliminates the GIL-crossing call to Python's `repr()` that currently happens for every float value. Special float handling (NaN, Infinity, -Infinity) remains unchanged. Any tests comparing float string output against hardcoded expectations may need updating if `ryu` output differs from `repr()`.

## Files to Modify
- `packages/rdn-native/Cargo.toml` — add `ryu = "1"` dependency
- `packages/rdn-native/src/serializer.rs` — replace float formatting in `stringify_value()` (lines 365-373)

## Implementation Details
**What changes**: `serializer.rs` lines 365-373 (the float branch of `stringify_value`).

**Current pattern**:
```rust
// line 371: calls Python repr() through the GIL
let repr = value.repr()?.to_string();
return Ok(repr);
```

**New pattern**:
```rust
// Use ryu for shortest round-trip representation
let mut buf = ryu::Buffer::new();
let formatted = buf.format(f);
return Ok(formatted.to_string());
```

**Special values** (lines 367-369) remain unchanged -- they already return static strings (`"NaN"`, `"Infinity"`, `"-Infinity"`).

**Formatting differences**: `ryu` produces shortest round-trip representations per the Ryu algorithm. Differences from Python `repr()`:
- `ryu` always includes a decimal point: `1.0` -> `"1.0"` (matches Python).
- `ryu` uses lowercase `e`: `1e20` (Python may produce `1e+20` with explicit `+`).
- Integer-valued floats: `ryu` produces `1.0`, Python produces `1.0` (same).
- Edge cases are extremely rare and all representations are mathematically equivalent.

**Test updates**: Identify and update any tests that compare float string output against hardcoded expectations if the `ryu` output differs. Document each change.

**Notes:**
- `ryu` is ~30KB with no transitive dependencies.
- This is the single highest-impact change for stringify performance on numeric payloads, since the current code crosses the Python/C boundary for every float.

## Dependencies
- Depends on: 1
- Blocks: 7

## Acceptance Criteria
- [ ] `ryu = "1"` is added to `Cargo.toml` dependencies
- [ ] `ryu::Buffer` is used for f64 formatting in `stringify_value()`
- [ ] Special float values (NaN, Infinity, -Infinity) still use static strings
- [ ] No Python `repr()` calls remain for float formatting
- [ ] All tests pass (with any necessary float-format test updates documented)
- [ ] Float formatting differences from Python `repr()` are documented in test comments

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.1.3, Section 12 (Task 4)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
