# Task 005: Implement `skipkeys` and `allow_nan` in Rust native extension

**References:** [discovery.md](../discovery.md) | [tech-design.md](../tech-design.md) §5.4.4–5.4.5, §5.5.4–5.5.5

## Objective

Add `skipkeys` and `allow_nan` support to the Rust native serializer so the native hot path handles these parameters without falling back to pure Python.

## Changes

### 1. `packages/rdn-python/rust/serializer.rs`

**State bits:**
- Add `const STATE_SKIPKEYS_BIT: u32 = 0x400;` (after existing state bits)
- Add `const STATE_ALLOW_NAN_BIT: u32 = 0x800;`

**`Serializer::new()`:**
- Add `skipkeys: bool` and `allow_nan: bool` parameters.
- Set `STATE_SKIPKEYS_BIT` in state if `skipkeys` is true.
- Set `STATE_ALLOW_NAN_BIT` in state if `allow_nan` is true (default true = bit set).

**Dict serialization in `stringify_value()` — skipkeys:**
- **Unsorted path**: When `key.downcast::<PyString>()` fails and `STATE_SKIPKEYS_BIT` is set, `continue`. Replace enumerate-based separator logic with a `first: bool` variable.
- **Sorted path**: When collecting keys, skip non-string keys with `continue` if `STATE_SKIPKEYS_BIT` is set.
- Apply same changes in `stringify_fallback()` dict-subclass path.

**Float serialization in `stringify_value()` — allow_nan:**
- After detecting `f.is_nan()`, `f64::INFINITY`, or `f64::NEG_INFINITY`:
  ```rust
  if self.state & STATE_ALLOW_NAN_BIT == 0 {
      return Err(pyo3::exceptions::PyValueError::new_err(
          "Out of range float values are not RDN compliant"
      ));
  }
  ```
- Apply same check in `stringify_fallback()` float handling.

### 2. `packages/rdn-python/rust/lib.rs`

Update `stringify()` function:
- Add `skipkeys: bool` and `allow_nan: bool` to function signature.
- Update `#[pyo3(signature)]` macro:
  ```rust
  #[pyo3(signature = (value, *, skipkeys=false, ensure_ascii=true, check_circular=true, allow_nan=true, sort_keys=false, indent=None, separators=None))]
  ```
- Pass both params to `Serializer::new()`.

### 3. `packages/rdn-python/src/rdn/__init__.py`

Update the native hot-path call in `dumps()` to pass `skipkeys` and `allow_nan`:
```python
return _native_stringify(obj, skipkeys=skipkeys, ensure_ascii=ensure_ascii,
                         check_circular=check_circular, allow_nan=allow_nan,
                         sort_keys=sort_keys, indent=indent, separators=separators)
```

### 4. Add native tests to `packages/rdn-python/tests/test_native.py`

New sections (skip if `_USE_NATIVE` is False):

**`TestNativeSkipKeys`:**
- `_native_stringify({1: "a", "b": 2}, skipkeys=True)` → `{"b":2}`
- `_native_stringify({1: "a", "b": 2})` → `TypeError` (default False)
- `_native_stringify({1: "a"}, skipkeys=True)` → `{}`

**`TestNativeAllowNan`:**
- `_native_stringify(float('nan'))` → `"NaN"` (default True)
- `_native_stringify(float('nan'), allow_nan=False)` → `ValueError`
- `_native_stringify(float('inf'), allow_nan=False)` → `ValueError`

## Verification

```bash
cd packages/rdn-python
pip install -e . && pytest tests/test_native.py -v
```

## Notes

- The Rust extension must be recompiled (`pip install -e .`) after changes.
- Bit positions `0x400` and `0x800` are safe — existing bits go up to `0x200` (STATE_SORT_BIT), and depth uses bits 0-6.
- The `allow_nan` default is `True` (bit SET), so the "not allowed" check is `state & STATE_ALLOW_NAN_BIT == 0`.
