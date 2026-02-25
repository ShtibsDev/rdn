# Task 005: Add hot/cold path annotations

## Status: pending

## Tier: Tier 1: Build & Low-Hanging Fruit

## Description
Add `#[cold]` and `#[inline(never)]` annotations to rarely-executed serializer formatting functions and error paths. Extract the extended-type dispatch (datetime through set) from `stringify_value()` into a new `#[cold] fn stringify_extended_value()` method. This keeps common-type code in the instruction cache and improves branch prediction for the hot path (None, str, bool, int, float, list, tuple, dict).

## Files to Modify
- `packages/rdn-native/src/serializer.rs` — add annotations, extract `stringify_extended_value()`
- `packages/rdn-native/src/parser.rs` — add `#[cold]` to `Parser::error()` (line 70)
- `packages/rdn-native/src/error.rs` — add `#[cold]` to `raise_decode_error()` (line 19)

## Implementation Details
**Functions to annotate with `#[cold]` and `#[inline(never)]`**:
- `format_datetime()` (serializer.rs line 185)
- `format_timeonly()` (serializer.rs line 216)
- `format_duration()` (serializer.rs line 230)
- `format_regexp()` (serializer.rs line 275)
- `format_binary()` (serializer.rs line 287)

**Error paths to annotate with `#[cold]` and `#[inline(never)]`**:
- `Parser::error()` (parser.rs line 70)
- `raise_decode_error()` (error.rs line 19)

**Restructuring `stringify_value()`**: The current method (serializer.rs lines 328-505) checks 16 types in sequence. Extract the type dispatch for items 6-16 (datetime through set) into a separate `#[cold]` function `stringify_extended_value()`. The main `stringify_value()` handles None, str, bool, int, float, list, tuple, dict inline, and calls `stringify_extended_value()` as the final fallback.

**Before** (pseudo-code):
```rust
fn stringify_value(&self, value, level) -> PyResult<String> {
    if is_none { return "null" }
    if is_str { ... }
    if is_bool { ... }
    if is_int { ... }
    if is_float { ... }
    if is_list { ... }
    if is_tuple { ... }
    if is_dict { ... }
    // items 6-16: datetime, time, timedelta, pattern, bytes, bytearray, frozenset, set, etc.
    if is_datetime { ... }
    if is_time { ... }
    // ...
}
```

**After** (pseudo-code):
```rust
fn stringify_value(&self, value, level) -> PyResult<String> {
    if is_none { return "null" }
    if is_str { ... }
    if is_bool { ... }
    if is_int { ... }
    if is_float { ... }
    if is_list { ... }
    if is_tuple { ... }
    if is_dict { ... }
    self.stringify_extended_value(value, level)
}

#[cold]
#[inline(never)]
fn stringify_extended_value(&self, value, level) -> PyResult<String> {
    if is_datetime { ... }
    if is_time { ... }
    // ... all extended types
}
```

## Dependencies
- Depends on: 1
- Blocks: 7

## Acceptance Criteria
- [ ] `#[cold]` and `#[inline(never)]` annotations on all five formatting functions
- [ ] `#[cold]` and `#[inline(never)]` on `Parser::error()` and `raise_decode_error()`
- [ ] `stringify_value()` is split: hot path handles 8 common types inline, cold path in `stringify_extended_value()`
- [ ] All tests pass
- [ ] No functional changes (output is identical)

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.1.4, Section 12 (Task 5)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
