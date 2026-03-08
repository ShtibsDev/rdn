# Task 008: Create cache.rs with TypeCache struct

## Status: pending

## Tier: Tier 2: Type Dispatch & Caching

## Description
Create a new `cache.rs` module containing the `TypeCache` struct that holds raw `*mut ffi::PyTypeObject` pointers for all 16 Python types used in serializer type dispatch. Include `PyObject` references to keep module-level types (datetime, time, timedelta, Pattern, bytearray) alive. Implement `TypeCache::new(py)` that imports the needed modules and extracts type pointers. Store the TypeCache in a module-level `static` and initialize it during `_native` module init.

## Files to Modify
- `packages/rdn-native/src/cache.rs` — new file with `TypeCache` struct
- `packages/rdn-native/src/lib.rs` — add `mod cache`, initialize TypeCache in module init (line 80-84)

## Implementation Details
**TypeCache struct** (from tech design Section 5.2):

```rust
/// Cached raw ob_type pointers for fast type dispatch.
/// Initialized once at module init. All pointers are for immortal
/// CPython type singletons (str, int, bool, float, list, dict, tuple,
/// set, frozenset, bytes, NoneType) and stable module-level types
/// (datetime, time, timedelta, re.Pattern, bytearray).
struct TypeCache {
    str_type: *mut ffi::PyTypeObject,
    int_type: *mut ffi::PyTypeObject,
    bool_type: *mut ffi::PyTypeObject,
    float_type: *mut ffi::PyTypeObject,
    list_type: *mut ffi::PyTypeObject,
    dict_type: *mut ffi::PyTypeObject,
    tuple_type: *mut ffi::PyTypeObject,
    set_type: *mut ffi::PyTypeObject,
    frozenset_type: *mut ffi::PyTypeObject,
    bytes_type: *mut ffi::PyTypeObject,
    none_type: *mut ffi::PyTypeObject,
    bytearray_type: *mut ffi::PyTypeObject,
    datetime_type: *mut ffi::PyTypeObject,
    time_type: *mut ffi::PyTypeObject,
    timedelta_type: *mut ffi::PyTypeObject,
    pattern_type: *mut ffi::PyTypeObject,
    // Keep Python references alive to prevent GC
    _datetime_ref: PyObject,
    _time_ref: PyObject,
    _timedelta_ref: PyObject,
    _pattern_ref: PyObject,
    _bytearray_ref: PyObject,
}
```

**Initialization** (`TypeCache::new(py: Python) -> PyResult<Self>`):
1. For built-in types (str, int, bool, float, list, dict, tuple, set, frozenset, bytes, NoneType): use `ffi::Py_TYPE(obj.as_ptr())` on singleton instances (e.g., `ffi::Py_TYPE(py.None().as_ptr())` for NoneType).
2. For module types: import `datetime` module, get `datetime.datetime`, `datetime.time`, `datetime.timedelta` type objects. Import `re` module, get `re.Pattern` type (via `type(re.compile(""))` or `re._pattern_type`). Get `bytearray` type from builtins.
3. Store the `PyObject` references (`_datetime_ref`, etc.) to prevent GC of the type objects.

**Storage**: Module-level `static` protected by the GIL:
```rust
static mut TYPE_CACHE: Option<TypeCache> = None;
```

**Thread-safety**: The GIL protects all access in CPython. The TypeCache is initialized under the GIL in `_native` module init and stored in a `static`. All reads happen while the GIL is held (PyO3 enforces this via `Python<'py>`).

**Module init integration** (`lib.rs` line 80-84): After adding the parse/stringify functions, call `TypeCache::new(py)` and store the result in the static.

## Dependencies
- Depends on: 7
- Blocks: 9

## Acceptance Criteria
- [ ] `cache.rs` exists with `TypeCache` struct containing all 16 type pointers
- [ ] `TypeCache::new(py)` correctly imports modules and extracts type pointers
- [ ] TypeCache is stored in a module-level static
- [ ] Module init in `lib.rs` initializes the TypeCache
- [ ] All existing tests pass (no behavioral changes yet)
- [ ] `mod cache` is declared in `lib.rs`

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 5.2, Section 6.2.1, Section 12 (Task 8)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
