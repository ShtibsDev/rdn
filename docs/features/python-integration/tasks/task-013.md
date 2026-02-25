# Task 13: Wire up public stringify API

**Status:** completed
**Dependencies:** Task 12

## Description

Implement the public `stringify()` function in `_serializer.py` that manages module-level state. Wire up `dumps()` and `dump()` in `__init__.py`. Implement the `default` function support for custom type serialization.

### Public `stringify()` Entry Point

The `stringify()` function:

1. Accepts the input value and all serialization parameters (`ensure_ascii`, `check_circular`, `indent`, `separators`, `default`, `sort_keys`).
2. Initializes module-level state (cycle detection set if `check_circular=True`, separator strings, indent config).
3. Handles `separators` parameter: if `None`, use default (`(",", ":")` for compact or `(",\n", ": ")` for indented).
4. Calls `_stringify_value()`.
5. Cleans up state in a `finally` block.
6. Returns the serialized string.

### `default` Function Support

The `default` function is called for values that have no built-in serialization (after all type checks fail):

```python
if default is not None:
    replacement = default(value)
    return _stringify_value(replacement, None, key)  # No double-replacement
raise TypeError(f"Object of type {type(value).__name__} is not RDN serializable")
```

The return value of `default()` is serialized. If `default` returns a non-serializable value, `TypeError` is raised. The replacement value is serialized with `default=None` to prevent infinite recursion.

### Top-Level API Functions

```python
def dumps(obj, *, cls=None, ensure_ascii=True, check_circular=True,
          indent=None, separators=None, default=None, sort_keys=False):
    # If cls is provided, instantiate encoder class and use its encode()
    # Otherwise, call _serializer.stringify() directly
    ...

def dump(obj, fp, **kwargs):
    # Call dumps() and write result to fp
    fp.write(dumps(obj, **kwargs))
```

### Parameter Notes

- `allow_nan` is intentionally omitted -- RDN always supports NaN/Infinity natively.
- `skipkeys` is omitted since RDN requires string keys (non-string keys raise `TypeError`).
- `ensure_ascii` defaults to `True` (matching `json.dumps`).

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_serializer.py` (modify)
- `packages/rdn-python/src/rdn/__init__.py` (modify)
- `packages/rdn-python/tests/test_stringify.py` (modify)
- `packages/rdn-python/tests/test_file_io.py` (modify)

## Acceptance Criteria
- `rdn.dumps({"key": "value"})` returns `'{"key":"value"}'`
- `rdn.dumps({"key": "value"}, indent=2)` returns pretty-printed output
- `rdn.dump({"key": 42}, StringIO())` writes to file-like object
- `rdn.dumps(obj, default=lambda o: str(o))` calls `default` for unsupported types
- `rdn.dumps(obj, default=lambda o: o)` raises `TypeError` when default returns non-serializable
- `rdn.dumps({"b": 2, "a": 1}, sort_keys=True)` returns sorted output
- `rdn.dumps("hello", ensure_ascii=True)` escapes non-ASCII
- `rdn.dumps("hello", ensure_ascii=False)` passes non-ASCII through
- `rdn.dumps([1, 2], separators=(", ", ": "))` uses custom separators
- All `dumps` parameters work correctly
- File I/O tests: `dump` writes correctly, result can be read back with `load`

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 13
- Tech Design: Section 3.1 (Public API Surface -- `dumps`, `dump` signatures with all parameters)
- Tech Design: Section 3.4.9 (Replacer Application -- `default` function)
- Tech Design: Section 7.3 (Serialization Errors)
- Discovery: `docs/features/python-integration/discovery.md`
