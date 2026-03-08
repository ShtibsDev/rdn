# Task 004: Implement `allow_nan` in pure Python serializer

**References:** [discovery.md](../discovery.md) | [tech-design.md](../tech-design.md) §5.5

## Objective

Add `allow_nan` parameter to `dumps()`, `_serializer.stringify()`, and `RDNEncoder`. When `False`, raise `ValueError` on NaN/Infinity values instead of serializing them.

## Changes

### 1. `packages/rdn-python/src/rdn/_serializer.py`

- Add `allow_nan: bool = True` to `stringify()` signature.
- In the float-handling block of `_encode()`, add guard before each special value:
  ```python
  if _isnan(value):
      if not allow_nan:
          raise ValueError("Out of range float values are not RDN compliant")
      return "NaN"
  if value == _INF:
      if not allow_nan:
          raise ValueError("Out of range float values are not RDN compliant")
      return "Infinity"
  if value == _NEG_INF:
      if not allow_nan:
          raise ValueError("Out of range float values are not RDN compliant")
      return "-Infinity"
  ```

### 2. `packages/rdn-python/src/rdn/encoder.py`

- Add `allow_nan: bool = True` to `RDNEncoder.__init__()` (after `check_circular`).
- Store `self.allow_nan = allow_nan`.
- Pass `allow_nan=self.allow_nan` to `_stringify()` in `encode()`.

### 3. `packages/rdn-python/src/rdn/__init__.py`

- Add `allow_nan: bool = True` to `dumps()` signature (after `check_circular`).
- Pass `allow_nan=allow_nan` to:
  - `cls()` instantiation
  - `_native_stringify()` call
  - `_stringify()` call

### 4. Add tests to `packages/rdn-python/tests/test_stringify.py`

New `TestAllowNan` class:
- Default (`allow_nan=True`): `NaN`, `Infinity`, `-Infinity` serialize normally
- `allow_nan=False` + `float('nan')` → `ValueError`
- `allow_nan=False` + `float('inf')` → `ValueError`
- `allow_nan=False` + `float('-inf')` → `ValueError`
- Nested: `[1, float('nan')]` with `allow_nan=False` → `ValueError`
- In dict value: `{"a": float('inf')}` with `allow_nan=False` → `ValueError`
- Via `RDNEncoder(allow_nan=False).encode(float('nan'))` → `ValueError`
- Normal floats unaffected: `dumps(3.14, allow_nan=False)` works fine

## Verification

```bash
cd packages/rdn-python && pytest tests/test_stringify.py::TestAllowNan -v
```
