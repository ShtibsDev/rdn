# Task 009: Refactor serializer to use cached type pointers

## Status: pending

## Tier: Tier 2: Type Dispatch & Caching

## Description
Refactor `stringify_value()` in the serializer to use `ffi::Py_TYPE(value.as_ptr())` pointer comparison against the cached type pointers from `TypeCache` for all 16 types. The existing isinstance/downcast calls become the fallback path for subclasses (the cold `stringify_extended_value()` method). Remove the `TypeCaches` struct from `serializer.rs` (currently at lines 16-34) since it's replaced by the module-level `TypeCache`. Remove the `bytearray` string comparison (line 400).

## Files to Modify
- `packages/rdn-native/src/serializer.rs` — refactor `stringify_value()` type dispatch, remove old `TypeCaches` struct
- `packages/rdn-native/src/cache.rs` — add accessor function for the module-level TypeCache

## Implementation Details
**Serializer type dispatch changes** (`serializer.rs` `stringify_value()`):

Current chain (lines 330-500) uses `is_instance_of`, `downcast`, `is_instance()`, and string comparison. Replace with pointer comparison first, isinstance fallback for subclasses:

```rust
// Pseudo-code for the new dispatch
let obj_type = ffi::Py_TYPE(value.as_ptr());
if obj_type == type_cache.none_type {
    return "null"
} else if obj_type == type_cache.str_type {
    // fast string path
} else if obj_type == type_cache.bool_type {
    // fast bool path
} else if obj_type == type_cache.int_type {
    // fast int path
} else if obj_type == type_cache.float_type {
    // fast float path
} else if obj_type == type_cache.list_type {
    // fast list path
} else if obj_type == type_cache.dict_type {
    // fast dict path
} else if obj_type == type_cache.tuple_type {
    // fast tuple path
} else if obj_type == type_cache.frozenset_type {
    // fast frozenset path
} else if obj_type == type_cache.set_type {
    // fast set path
} else if obj_type == type_cache.bytes_type {
    // fast bytes path
} else if obj_type == type_cache.bytearray_type {
    // fast bytearray path (replaces string comparison)
} else if obj_type == type_cache.datetime_type {
    // fast datetime path (replaces isinstance call)
} else if obj_type == type_cache.time_type {
    // fast time path
} else if obj_type == type_cache.timedelta_type {
    // fast timedelta path
} else if obj_type == type_cache.pattern_type {
    // fast pattern path
} else {
    // Fallback: use isinstance for subclass support
    stringify_extended_value_slow(value, level)
}
```

The fallback path uses the existing `is_instance()` / `downcast` chain for subclass correctness. The common case (exact type match) takes O(1) pointer comparisons.

**Items to remove**:
- `TypeCaches` struct from `serializer.rs` (lines 16-34) -- replaced by module-level `TypeCache`
- `bytearray` string comparison (line 400) -- replaced by pointer comparison

**Thread-safety**: All `ffi::Py_TYPE()` calls and pointer comparisons happen while the GIL is held. Built-in type pointers are immortal singletons. Module-level type pointers (datetime, etc.) are stable once their modules are imported.

## Dependencies
- Depends on: 8
- Blocks: 12, 13

## Acceptance Criteria
- [ ] `stringify_value()` uses `ffi::Py_TYPE()` pointer comparison for all 16 types
- [ ] No `is_instance()` Python calls for exact type matches
- [ ] No string comparison for bytearray detection
- [ ] Old `TypeCaches` struct is removed from `serializer.rs`
- [ ] Subclasses still work via isinstance fallback path
- [ ] All tests pass

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.2.1, Section 12 (Task 9)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
