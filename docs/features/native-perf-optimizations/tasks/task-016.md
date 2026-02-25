# Task 016: Integrate SIMD escape detection into serializer

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
Replace the first-pass escape detection loop in `escape_string()` with a call to `simd::needs_escape()`. If `None` is returned, take the no-escape fast path (string needs no escaping). If `Some(pos)` is returned, bulk-copy `s[..pos]` to the output, then enter the character-by-character escape loop starting at `pos`.

## Files to Modify
- `packages/rdn-native/src/serializer.rs` — replace escape detection in `escape_string()` (lines 82-91)

## Implementation Details
**Integration point**: Replace the first-pass loop in `escape_string()` (serializer.rs lines 82-91).

**Current pattern** (serializer.rs lines 82-91):
```rust
// First pass: check if any escaping is needed
let needs_escape = s.bytes().any(|b| {
    b < 0x20 || b == b'"' || b == b'\\' || (self.ensure_ascii && b > 0x7f)
});
if !needs_escape {
    // Fast path: just wrap in quotes
    return format!("\"{}\"", s);
}
// Slow path: character-by-character escape
```

**New pattern**:
```rust
match simd::needs_escape(s.as_bytes(), self.ensure_ascii) {
    None => {
        // Fast path: no escaping needed, just wrap in quotes
        return format!("\"{}\"", s);
    }
    Some(pos) => {
        // Bulk-copy the clean prefix, then escape from pos onward
        let mut result = String::with_capacity(s.len() + 2);
        result.push('"');
        result.push_str(&s[..pos]);
        // Enter character-by-character escape loop starting at pos
        for ch in s[pos..].chars() {
            match ch {
                // ... existing escape logic ...
            }
        }
        result.push('"');
        return result;
    }
}
```

**Key improvement**: Even with the scalar fallback, this restructures the code to bulk-copy the clean prefix before entering the per-character loop. The SIMD version (Tasks 17-19) will make the prefix scanning much faster.

## Dependencies
- Depends on: 14
- Blocks: 19

## Acceptance Criteria
- [ ] `escape_string()` uses `simd::needs_escape()` for escape detection
- [ ] Fast path (no escaping) still works correctly
- [ ] Slow path bulk-copies clean prefix before per-character escaping
- [ ] `ensure_ascii` flag is passed through correctly
- [ ] Behavior is identical to previous implementation (scalar fallback)
- [ ] All tests pass

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.3.2, Section 12 (Task 16)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
