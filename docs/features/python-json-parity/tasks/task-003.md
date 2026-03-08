# Task 003: Implement `skipkeys` in pure Python serializer

**References:** [discovery.md](../discovery.md) | [tech-design.md](../tech-design.md) §5.4

## Objective

Add `skipkeys` parameter to `dumps()`, `_serializer.stringify()`, and `RDNEncoder`. When `True`, silently skip non-string dict keys instead of raising `TypeError`.

## Changes

### 1. `packages/rdn-python/src/rdn/_serializer.py`

- Add `skipkeys: bool = False` to `stringify()` signature.
- In the dict-handling block of `_encode()`, where non-string keys raise `TypeError`:
  ```python
  if not _isinstance(k, _str):
      if skipkeys:
          continue
      raise TypeError(f"Object key must be a string, got {type(k).__name__}")
  ```

### 2. `packages/rdn-python/src/rdn/encoder.py`

- Add `skipkeys: bool = False` to `RDNEncoder.__init__()` (first keyword param).
- Store `self.skipkeys = skipkeys`.
- Pass `skipkeys=self.skipkeys` to `_stringify()` in `encode()`.

### 3. `packages/rdn-python/src/rdn/__init__.py`

- Add `skipkeys: bool = False` to `dumps()` signature (first keyword param, before `ensure_ascii`).
- Pass `skipkeys=skipkeys` to:
  - `cls()` instantiation (when `cls is not None`)
  - `_native_stringify()` call (native path)
  - `_stringify()` call (pure Python path)

### 4. Add tests to `packages/rdn-python/tests/test_stringify.py`

New `TestSkipKeys` class:
- `skipkeys=True` skips non-string keys: `{1: "a", "b": 2}` → `{"b":2}`
- `skipkeys=False` (default) raises `TypeError`
- All keys skipped → `{}`
- Nested dicts: skips at all levels
- `skipkeys=True` + `sort_keys=True` works
- Via `RDNEncoder(skipkeys=True).encode(...)`

## Verification

```bash
cd packages/rdn-python && pytest tests/test_stringify.py::TestSkipKeys -v
```

## Edge Cases

- Dict where all keys are non-string → result is `{}`
- Mixed string/non-string keys → only string keys appear in output
- `skipkeys` does NOT affect Map serialization (Maps allow non-string keys natively)
