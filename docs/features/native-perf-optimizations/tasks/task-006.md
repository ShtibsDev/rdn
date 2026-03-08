# Task 006: Add empty collection fast-paths in serializer

## Status: pending

## Tier: Tier 1: Build & Low-Hanging Fruit

## Description
Add `len() == 0` early-return checks before cycle detection for list, tuple, and dict collections in the serializer. These fast-paths avoid entering the `check_cycle` / `format_container` machinery for empty collections. The parser already has fast-paths for empty `[]`, `{}`, and `()`. The frozenset and set branches already have `len() == 0` checks.

## Files to Modify
- `packages/rdn-native/src/serializer.rs` — add early-return checks in list, tuple, and dict branches

## Implementation Details
Add fast-paths at the top of each container branch in `stringify_value()`:

- **`list` branch** (serializer.rs line 406-418): Before calling `check_cycle`, check `list.len() == 0` and return `"[]"` immediately.
- **`tuple` branch** (serializer.rs line 421-427): Check `tup.len() == 0` and return `"()"` immediately.
- **`dict` branch** (serializer.rs line 430-468): Check `dict.len() == 0` and return `"{}"` immediately.
- **`frozenset` branch** (serializer.rs line 471-480): Already has a `len() == 0` check (line 472). No change needed.
- **`set` branch** (serializer.rs line 482-500): Already has a `len() == 0` check (line 486). No change needed.

**Parser side**: Already has fast-paths for empty `[]` (parser.rs line 752-757), `{}` (line 811-816), and `()` (line 781-786). No changes needed.

**Pattern** for each branch:
```rust
// Before (list example):
} else if let Ok(list) = value.downcast::<PyList>() {
    self.check_cycle(value)?;
    // ... format container ...
}

// After:
} else if let Ok(list) = value.downcast::<PyList>() {
    if list.len() == 0 {
        return Ok("[]".to_string());
    }
    self.check_cycle(value)?;
    // ... format container ...
}
```

## Dependencies
- Depends on: 1
- Blocks: 7

## Acceptance Criteria
- [ ] List branch has `len() == 0` fast-path returning `"[]"`
- [ ] Tuple branch has `len() == 0` fast-path returning `"()"`
- [ ] Dict branch has `len() == 0` fast-path returning `"{}"`
- [ ] Fast-paths are before `check_cycle()` calls
- [ ] All tests pass
- [ ] No functional changes (output is identical)

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.1.5, Section 12 (Task 6)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
