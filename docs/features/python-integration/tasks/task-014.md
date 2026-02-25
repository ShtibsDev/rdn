# Task 14: Implement RDNDecoder and RDNEncoder classes

**Status:** completed
**Dependencies:** Tasks 9, 13

## Description

Implement the class-based API mirroring `json.JSONDecoder` and `json.JSONEncoder`: `RDNDecoder` (with `decode`, `raw_decode`) and `RDNEncoder` (with `encode`, `iterencode`, `default`). Wire up the `cls` parameter in `loads()`/`dumps()`.

### RDNDecoder Class

```python
class RDNDecoder:
    def __init__(self, *, object_hook=None, parse_float=None, parse_int=None,
                 parse_bigint=None, parse_datetime=None, parse_timeonly=None,
                 parse_duration=None, parse_regexp=None, parse_binary=None,
                 object_pairs_hook=None):
        # Store all hooks as instance attributes
        # parse_float defaults to float, parse_int defaults to int,
        # parse_bigint defaults to int
        ...

    def decode(self, s: str) -> Any:
        """Decode an RDN string and return the Python representation.
        Delegates to the module-level parser with stored hooks."""
        ...

    def raw_decode(self, s: str, idx: int = 0) -> tuple[Any, int]:
        """Decode an RDN value starting at position idx.
        Returns (parsed_value, end_position).
        Useful for parsing RDN values embedded in larger strings."""
        ...
```

### RDNEncoder Class

```python
class RDNEncoder:
    def __init__(self, *, ensure_ascii=True, check_circular=True,
                 indent=None, separators=None, default=None, sort_keys=False):
        # Store all settings as instance attributes
        ...

    def encode(self, o: Any) -> str:
        """Return the RDN string representation of a Python value."""
        ...

    def iterencode(self, o: Any) -> Iterator[str]:
        """Encode the given object and yield each string chunk.
        Useful for streaming large documents."""
        ...

    def default(self, o: Any) -> Any:
        """Override for custom type serialization.
        Called for objects the encoder cannot serialize by default.
        Should return a serializable object or raise TypeError."""
        raise TypeError(f"Object of type {type(o).__name__} is not RDN serializable")
```

### `cls` Parameter Wiring

When `cls` is passed to `loads()`, the decoder class is instantiated with the provided hook parameters and `decode()` is called:
```python
def loads(s, *, cls=None, ...):
    if cls is not None:
        decoder = cls(object_hook=object_hook, parse_float=parse_float, ...)
        return decoder.decode(s)
    ...
```

When `cls` is passed to `dumps()`, the encoder class is instantiated with the provided parameters and `encode()` is called.

### Custom Subclasses

Users can subclass `RDNEncoder` and override `default()` to handle custom types:
```python
class CustomEncoder(RDNEncoder):
    def default(self, o):
        if isinstance(o, MyType):
            return str(o)
        return super().default(o)
```

### `iterencode()` for Streaming

Yields string chunks instead of building the entire output in memory. Each leaf value is yielded as a single chunk. Container delimiters and separators are yielded separately.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/decoder.py` (modify)
- `packages/rdn-python/src/rdn/encoder.py` (modify)
- `packages/rdn-python/src/rdn/__init__.py` (modify)
- `packages/rdn-python/tests/test_decoder.py` (create)
- `packages/rdn-python/tests/test_encoder.py` (create)

## Acceptance Criteria
- `RDNDecoder().decode('{"a": 1}')` returns `{"a": 1}`
- `RDNDecoder(parse_bigint=lambda s: Decimal(s)).decode("42n")` returns `Decimal(42)`
- `RDNDecoder().raw_decode('[1, 2] extra', 0)` returns `([1, 2], 6)`
- `RDNEncoder().encode({"a": 1})` returns `'{"a":1}'`
- `RDNEncoder(indent=2).encode({"a": 1})` returns pretty-printed output
- `list(RDNEncoder().iterencode([1, 2]))` yields correct chunks
- Custom subclass with overridden `default()` works correctly
- `rdn.loads(text, cls=RDNDecoder)` uses the provided class
- `rdn.dumps(obj, cls=RDNEncoder)` uses the provided class
- All decoder hooks are passed through and applied correctly
- All encoder settings are passed through and applied correctly

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 14
- Tech Design: Section 3.5 (RDNDecoder Class -- full specification)
- Tech Design: Section 3.6 (RDNEncoder Class -- full specification)
- Tech Design: Section 3.4.10 (`iterencode()` for Streaming)
- Discovery: `docs/features/python-integration/discovery.md`
