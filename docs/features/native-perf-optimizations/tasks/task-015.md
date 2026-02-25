# Task 015: Integrate SIMD scanner into parser

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
Replace the string scanning loop in `parse_string()` with a call to `simd::find_string_end()`. The function returns `(end_pos, has_escape)`. If `!has_escape`, slice directly from source (fast path). If `has_escape`, call the existing escape materialization logic (slow path). Ensure control character detection (`< 0x20`) is handled correctly.

## Files to Modify
- `packages/rdn-native/src/parser.rs` — replace scanning loop in `parse_string()` (lines 114-149)

## Implementation Details
**Integration point**: Replace the scanning loop in `parse_string()` (parser.rs lines 114-149).

**Current pattern** (parser.rs lines 114-149):
```rust
// byte-by-byte while loop scanning for " and \
while self.pos < self.len {
    let b = self.bytes[self.pos];
    match b {
        b'"' => { /* end of string */ }
        b'\\' => { /* escape sequence, set has_escape = true */ }
        b if b < 0x20 => { /* invalid control character */ }
        _ => { self.pos += 1; }
    }
}
```

**New pattern**:
```rust
let (end_pos, has_escape) = simd::find_string_end(self.bytes, self.pos);
if end_pos >= self.len {
    return self.error("Unterminated string");
}
if !has_escape {
    // Fast path: no escapes, slice directly from source bytes
    let s = &self.bytes[self.pos..end_pos];
    self.pos = end_pos + 1; // skip closing "
    // Convert bytes to PyString directly
} else {
    // Slow path: has escape sequences, materialize string
    // Call existing escape handling logic starting from self.pos
    self.pos = self.pos; // reset to start of string content
    // ... existing materialize_string logic ...
}
```

**Control character detection**: `find_string_end()` must also detect bytes `< 0x20` and either return an error position or handle them. The scalar implementation extracts this from the existing parser logic. Ensure the integration preserves the existing error behavior for unescaped control characters.

## Dependencies
- Depends on: 14
- Blocks: 17, 18

## Acceptance Criteria
- [ ] `parse_string()` uses `simd::find_string_end()` instead of byte-by-byte loop
- [ ] Fast path (no escapes) slices directly from source
- [ ] Slow path (has escapes) still materializes correctly
- [ ] Control character detection (`< 0x20`) works correctly
- [ ] Behavior is identical to previous implementation (scalar fallback)
- [ ] All tests pass

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.3.1, Section 12 (Task 15)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
