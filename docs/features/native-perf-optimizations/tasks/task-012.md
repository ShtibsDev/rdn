# Task 012: Implement bit-packed serializer state

## Status: pending

## Tier: Tier 2: Type Dispatch & Caching

## Description
Replace the individual `ensure_ascii: bool`, `check_circular: bool`, `sort_keys: bool` fields and the `level: usize` parameter in the Serializer with a single bit-packed `state: u32`. This reduces struct size and enables the compiler to keep the state in a register during recursive `stringify_value()` calls.

## Files to Modify
- `packages/rdn-native/src/serializer.rs` — replace struct fields, update all access patterns

## Implementation Details
**New state `u32` layout** (from tech design Section 5.2):
- Bits 0-6: depth (7 bits, range 0-128)
- Bit 7: `ensure_ascii`
- Bit 8: `check_circular`
- Bit 9: `sort_keys`
- Bits 10-31: reserved

**Constants**:
```rust
const STATE_DEPTH_MASK: u32    = 0b0000_0000_0000_0000_0000_0000_0111_1111;
const STATE_ASCII_BIT: u32     = 0b0000_0000_0000_0000_0000_0000_1000_0000;
const STATE_CIRCULAR_BIT: u32  = 0b0000_0000_0000_0000_0000_0001_0000_0000;
const STATE_SORT_BIT: u32      = 0b0000_0000_0000_0000_0000_0010_0000_0000;
```

**Replaces**: The current `Serializer` struct fields `ensure_ascii: bool`, `check_circular: bool`, `sort_keys: bool` (serializer.rs lines 38-40). The `depth` parameter currently passed as `level: usize` through `stringify_value()` is embedded in the state.

**Access patterns**:
```rust
// Read depth
let depth = (self.state & STATE_DEPTH_MASK) as usize;
// Increment depth
self.state += 1;
// Decrement depth (after recursive call)
self.state -= 1;
// Check ensure_ascii
if self.state & STATE_ASCII_BIT != 0 { ... }
// Check check_circular
if self.state & STATE_CIRCULAR_BIT != 0 { ... }
// Check sort_keys
if self.state & STATE_SORT_BIT != 0 { ... }
```

**Constructor update** (`Serializer::new()`):
```rust
fn new(ensure_ascii: bool, check_circular: bool, sort_keys: bool) -> Self {
    let mut state: u32 = 0;
    if ensure_ascii { state |= STATE_ASCII_BIT; }
    if check_circular { state |= STATE_CIRCULAR_BIT; }
    if sort_keys { state |= STATE_SORT_BIT; }
    // depth starts at 0 (already zero)
    Self { state, /* other fields */ }
}
```

**Method signature change**: `stringify_value(&mut self, value, level: usize)` becomes `stringify_value(&mut self, value)` since depth is now in `self.state`. Update all call sites.

## Dependencies
- Depends on: 9
- Blocks: 13

## Acceptance Criteria
- [ ] `Serializer` struct has `state: u32` instead of three separate bool fields
- [ ] Bit constants are defined for depth, ensure_ascii, check_circular, sort_keys
- [ ] `stringify_value()` no longer takes a `level` parameter
- [ ] All reads of ensure_ascii/check_circular/sort_keys use bitmask operations
- [ ] Depth increment/decrement uses `self.state += 1` / `self.state -= 1`
- [ ] `Serializer::new()` packs the initial state correctly
- [ ] All tests pass

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 5.2, Section 6.2.3, Section 12 (Task 12)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
