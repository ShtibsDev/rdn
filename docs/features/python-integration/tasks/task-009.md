# Task 9: Wire up public parse API with hooks

**Status:** completed
**Dependencies:** Task 8

## Description

Implement the public `parse()` function in `_parser.py` that manages the module-level state lifecycle. Wire up `loads()` and `load()` in `__init__.py`. Implement all parse hooks (`parse_float`, `parse_int`, `parse_bigint`, `parse_datetime`, `parse_timeonly`, `parse_duration`, `parse_regexp`, `parse_binary`, `object_hook`, `object_pairs_hook`).

### Public `parse()` Entry Point

The `parse()` function is the internal entry point that:

1. Accepts the input string and all hook parameters.
2. Sets module-level state (`_source`, `_pos`, `_len`, `_depth`) and all hook variables.
3. Calls `_parse_value()`.
4. Verifies no trailing content after the parsed value ("Unexpected data after value").
5. Cleans up state in a `finally` block (always, even on exception).
6. Returns the parsed value.

Supports `str`, `bytes`, and `bytearray` input. `bytes`/`bytearray` are decoded as UTF-8 before parsing.

### Hook Application

Parse hooks are stored in module-level variables and checked during parsing:

- **`parse_int`**: Called with the string representation of each integer (e.g., `"42"`). Default: `int()`.
- **`parse_float`**: Called with the string representation of each float (e.g., `"3.14"`). Default: `float()`.
- **`parse_bigint`**: Called with the string representation of each bigint (e.g., `"42"`, without the `n` suffix). Default: `int()`.
- **`parse_datetime`**: Called with the parsed `datetime` object. Default: identity (return as-is).
- **`parse_timeonly`**: Called with the parsed `time` object. Default: identity.
- **`parse_duration`**: Called with the parsed `timedelta` or `str`. Default: identity.
- **`parse_regexp`**: Called with the parsed `re.Pattern` object. Default: identity.
- **`parse_binary`**: Called with the parsed `bytes` object. Default: identity.
- **`object_hook`**: Called with each parsed dict. Default: `None` (no-op).
- **`object_pairs_hook`**: Called with list of `(key, value)` pairs. Takes priority over `object_hook`.

### Top-Level API Functions

```python
def loads(s, *, cls=None, object_hook=None, parse_float=None, parse_int=None,
          parse_bigint=None, parse_datetime=None, parse_timeonly=None,
          parse_duration=None, parse_regexp=None, parse_binary=None,
          object_pairs_hook=None):
    # If cls is provided, instantiate decoder class and use its decode()
    # Otherwise, call _parser.parse() directly with hooks
    ...

def load(fp, **kwargs):
    # Read fp.read(), then delegate to loads()
    ...
```

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_parser.py` (modify)
- `packages/rdn-python/src/rdn/__init__.py` (modify)
- `packages/rdn-python/tests/test_parse.py` (modify)
- `packages/rdn-python/tests/test_file_io.py` (create)

## Acceptance Criteria
- `rdn.loads('{"key": "value"}')` returns `{"key": "value"}`
- `rdn.loads(b'"hello"')` returns `"hello"` (bytes input decoded as UTF-8)
- `rdn.load(StringIO('42'))` returns `42` (file-like object)
- `rdn.loads('42n', parse_bigint=lambda s: Decimal(s))` calls the hook with `"42"`
- `rdn.loads('3.14', parse_float=Decimal)` calls the hook with `"3.14"`
- `rdn.loads('{"a": 1}', object_hook=lambda d: SimpleNamespace(**d))` returns a namespace
- `rdn.loads('{"a": 1}', object_pairs_hook=OrderedDict)` returns an OrderedDict
- `object_pairs_hook` takes priority over `object_hook` when both provided
- All parse hooks are called correctly with the specified argument types
- Trailing content after a valid value raises `RDNDecodeError` ("Unexpected data after value")
- `bytes`/`bytearray` input is decoded as UTF-8

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 9
- Tech Design: Section 3.1 (Public API Surface -- `loads`, `load` signatures)
- Tech Design: Section 3.3.6 (Hook Application -- all hook descriptions)
- Tech Design: Section 7.2 ("Unexpected data after value" error)
- Discovery: `docs/features/python-integration/discovery.md`
