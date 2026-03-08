# Task 003: Replace integer formatting with itoa

## Status: pending

## Tier: Tier 1: Build & Low-Hanging Fruit

## Description
Add the `itoa` crate as a dependency and replace all `i64.to_string()` and `format!("{}n", v)` calls in the serializer's integer branch with `itoa::Buffer`-based formatting. This eliminates heap allocations for integer-to-string conversion. The very-large-int path (arbitrary precision BigInts that overflow i64) remains unchanged since it still requires Python's `str()`.

## Files to Modify
- `packages/rdn-native/Cargo.toml` — add `itoa = "1"` dependency
- `packages/rdn-native/src/serializer.rs` — replace integer formatting in `stringify_value()` (lines 347-361)

## Implementation Details
**What changes**: `serializer.rs` lines 347-361 (the integer branch of `stringify_value`).

**Current pattern**:
```rust
// line 354: small int
return Ok(v.to_string());
// line 352-353: BigInt from i64
return Ok(format!("{}n", v));
// line 358-359: very large int, Python str()
return Ok(format!("{}n", s));
```

**New pattern**:
```rust
// small int: use itoa to write directly to a stack buffer
let mut buf = itoa::Buffer::new();
let formatted = buf.format(v);
return Ok(formatted.to_string());
// BigInt from i64: itoa + "n" suffix
let mut buf = itoa::Buffer::new();
let formatted = buf.format(v);
return Ok(format!("{}n", formatted));
// very large int: unchanged (still needs Python str() for arbitrary precision)
```

**Notes:**
- `itoa::Buffer::new()` allocates on the stack (a `[u8; 20]`). The `to_string()` on the result still allocates, but in Tier 3 (Task 21) we eliminate that by writing directly to the `WriteBuffer`.
- BigInt formatting for values that overflow `i64` still uses `value.str()?.to_string()` + `"n"` suffix, since `itoa` only handles fixed-width integers.
- `itoa` is ~15KB with no transitive dependencies.

## Dependencies
- Depends on: 1
- Blocks: 7

## Acceptance Criteria
- [ ] `itoa = "1"` is added to `Cargo.toml` dependencies
- [ ] `itoa::Buffer` is used for i64 integer formatting in `stringify_value()`
- [ ] Very-large-int path (arbitrary precision) remains unchanged
- [ ] All tests pass
- [ ] No functional changes (output is identical)

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.1.2, Section 12 (Task 3)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
