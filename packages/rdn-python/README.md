# rdn

A pure-Python parser and serializer for **RDN (Rich Data Notation)** -- a JSON superset with native support for dates, BigInts, regular expressions, binary data, Maps, Sets, tuples, TimeOnly, Duration, and special numeric values (NaN, Infinity).

Any valid JSON is valid RDN. The API mirrors Python's built-in `json` module, so switching from `json` to `rdn` is a one-line change.

## Installation

```bash
pip install rdn
```

Requires Python 3.10+. Zero runtime dependencies.

### Optional: Native Acceleration

For significantly better performance, install the optional Rust native extension:

```bash
pip install rdn-native
```

When installed, hot-path calls (without hooks/callbacks) are automatically routed to the compiled Rust implementation. Calls with hooks fall through to the pure Python implementation transparently.

The native extension includes SIMD-accelerated string scanning, type-pointer caching, string-interning key cache, and direct-to-buffer serialization. See the [rdn-native README](../rdn-native/README.md) for architecture details.

**Float formatting note:** The native extension uses `ryu` for float-to-string conversion, which may produce slightly different string representations compared to Python's `repr()` in edge cases (e.g., trailing digit differences). All outputs are mathematically equivalent -- they parse back to the identical IEEE 754 value.

```python
import rdn
print(rdn._USE_NATIVE)  # True if rdn-native is installed
```

## Quick Start

```python
import rdn
from datetime import datetime, timezone

# Parse an RDN string
data = rdn.loads('{"name": "Alice", "joined": @2024-01-15T09:30:00.000Z}')
# {'name': 'Alice', 'joined': datetime(2024, 1, 15, 9, 30, tzinfo=timezone.utc)}

# Serialize to RDN
rdn.dumps({"count": 42, "active": True})
# '{"count":42,"active":true}'

# Dates serialize automatically
rdn.dumps({"ts": datetime(2024, 1, 15, tzinfo=timezone.utc)})
# '{"ts":@2024-01-15T00:00:00.000Z}'

# BigInts (ints beyond JavaScript's safe integer range) auto-promote
rdn.dumps({"id": 2**53})
# '{"id":9007199254740992n}'

# Pretty-print
print(rdn.dumps({"a": 1, "b": [2, 3]}, indent=2))
# {
#   "a": 1,
#   "b": [
#     2,
#     3
#   ]
# }
```

## Type Mapping

RDN types map directly to Python stdlib types -- no wrapper classes needed.

| RDN Type | Python Type | Parse Example | Serialize Behavior |
|---|---|---|---|
| `null` | `None` | `null` -> `None` | `None` -> `null` |
| `true` / `false` | `bool` | `true` -> `True` | `True` -> `true` |
| integer | `int` | `42` -> `42` | `42` -> `42` |
| float | `float` | `3.14` -> `3.14` | `3.14` -> `3.14` |
| `NaN` | `float('nan')` | `NaN` -> `float('nan')` | `float('nan')` -> `NaN` |
| `Infinity` | `float('inf')` | `Infinity` -> `float('inf')` | `float('inf')` -> `Infinity` |
| BigInt (`42n`) | `int` | `42n` -> `42` | Auto-promote: `int` > `MAX_SAFE_INTEGER` -> `42n` |
| string | `str` | `"hello"` -> `'hello'` | `'hello'` -> `"hello"` |
| DateTime | `datetime` (UTC) | `@2024-01-15T00:00:00.000Z` -> `datetime(...)` | Always 24-char ISO format |
| TimeOnly | `datetime.time` | `@14:30:00.500` -> `time(14, 30, 0, 500000)` | `time` -> `@HH:MM:SS[.mmm]` |
| Duration (D/H/M/S) | `timedelta` | `@P3DT4H` -> `timedelta(days=3, hours=4)` | `timedelta` -> `@PnDTnHnMnS` |
| Duration (Y/M) | `str` | `@P1Y2M` -> `"P1Y2M"` | `str` starting with `P` -> `@P1Y2M` |
| RegExp | `re.Pattern` | `/pat/ims` -> `re.compile("pat", ...)` | Reconstruct `/pattern/flags` |
| Binary (base64) | `bytes` | `b64"SGVsbG8="` -> `b'Hello'` | `bytes` -> `b64"..."` |
| Binary (hex) | `bytes` | `hex"48656c6c6f"` -> `b'Hello'` | (parsed the same as base64) |
| Array | `list` | `[1, 2, 3]` -> `[1, 2, 3]` | `list` -> `[...]` |
| Tuple | `tuple` | `(1, 2, 3)` -> `(1, 2, 3)` | `tuple` -> `(...)` |
| Object | `dict` | `{"a": 1}` -> `{'a': 1}` | `dict` -> `{...}` |
| Map | `dict` | `{1 => "a"}` -> `{1: 'a'}` | `dict` -> `{...}` |
| Set | `set` / `frozenset` | `{1, 2, 3}` -> `{1, 2, 3}` | `set`/`frozenset` -> `Set{...}` |

## API Reference

### `rdn.loads(s, *, cls=None, object_hook=None, parse_float=None, parse_int=None, parse_bigint=None, parse_datetime=None, parse_timeonly=None, parse_duration=None, parse_regexp=None, parse_binary=None, object_pairs_hook=None)`

Deserialize an RDN document to a Python object.

| Parameter | Type | Description |
|---|---|---|
| `s` | `str \| bytes \| bytearray` | The RDN document. `bytes`/`bytearray` are decoded as UTF-8. |
| `cls` | `type[RDNDecoder]` | Optional decoder class to use instead of the default. |
| `object_hook` | `Callable[[dict], Any]` | Called with each parsed dict. |
| `parse_float` | `Callable[[str], Any]` | Called with the string representation of each float. |
| `parse_int` | `Callable[[str], Any]` | Called with the string representation of each integer. |
| `parse_bigint` | `Callable[[str], Any]` | Called with the string of each BigInt (without `n` suffix). |
| `parse_datetime` | `Callable[[datetime], Any]` | Called with each parsed `datetime` object. |
| `parse_timeonly` | `Callable[[time], Any]` | Called with each parsed `time` object. |
| `parse_duration` | `Callable[[timedelta \| str], Any]` | Called with each parsed `timedelta` or `str`. |
| `parse_regexp` | `Callable[[re.Pattern], Any]` | Called with each parsed `re.Pattern` object. |
| `parse_binary` | `Callable[[bytes], Any]` | Called with each parsed `bytes` object. |
| `object_pairs_hook` | `Callable[[list[tuple[str, Any]]], Any]` | Called with `(key, value)` pairs for each object. Takes priority over `object_hook`. |

**Returns:** The decoded Python value.

**Raises:** `RDNDecodeError` if the input is not valid RDN; `TypeError` if `s` is not `str`, `bytes`, or `bytearray`.

### `rdn.dumps(obj, *, cls=None, ensure_ascii=True, check_circular=True, indent=None, separators=None, default=None, sort_keys=False)`

Serialize a Python object to an RDN-formatted string.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `obj` | `Any` | | The value to serialize. |
| `cls` | `type[RDNEncoder]` | `None` | Optional encoder class. |
| `ensure_ascii` | `bool` | `True` | Escape non-ASCII characters as `\uXXXX`. |
| `check_circular` | `bool` | `True` | Detect circular references. |
| `indent` | `int \| str \| None` | `None` | Pretty-print indent (spaces or string). |
| `separators` | `tuple[str, str]` | `None` | `(item_separator, key_separator)` override. |
| `default` | `Callable[[Any], Any]` | `None` | Fallback for non-serializable objects. |
| `sort_keys` | `bool` | `False` | Sort dictionary keys alphabetically. |

**Returns:** The RDN string.

**Raises:** `TypeError` if a value is not serializable and no `default` handles it; `ValueError` on circular references.

### `rdn.load(fp, **kwargs)`

Deserialize from a file-like object. Reads `fp.read()` and delegates to `loads()`.

```python
with open("data.rdn") as f:
    data = rdn.load(f)
```

### `rdn.dump(obj, fp, **kwargs)`

Serialize to a file-like object. Calls `dumps()` and writes the result to `fp`.

```python
with open("data.rdn", "w") as f:
    rdn.dump({"key": "value"}, f)
```

## Parse Hooks

Parse hooks let you customize how specific types are decoded. Each hook receives either a string representation or the already-parsed Python object.

```python
from decimal import Decimal
from datetime import datetime

# Use Decimal for floats
data = rdn.loads("3.14", parse_float=Decimal)
# Decimal('3.14')

# Use Decimal for BigInts
data = rdn.loads("999999999999999999n", parse_bigint=Decimal)
# Decimal('999999999999999999')

# Transform datetimes to epoch timestamps
data = rdn.loads('@2024-01-15T00:00:00.000Z', parse_datetime=lambda dt: dt.timestamp())
# 1705276800.0

# Convert binary to a hex string
data = rdn.loads('b64"SGVsbG8="', parse_binary=lambda b: b.hex())
# '48656c6c6f'

# Custom object construction
data = rdn.loads('{"x": 1, "y": 2}', object_pairs_hook=lambda pairs: dict(reversed(pairs)))
# {'y': 2, 'x': 1}
```

## Class-Based API

### RDNDecoder

```python
from rdn import RDNDecoder

# Basic usage
decoder = RDNDecoder()
result = decoder.decode('{"key": @2024-01-15T00:00:00.000Z}')

# With hooks
decoder = RDNDecoder(parse_float=Decimal)
result = decoder.decode("[1.1, 2.2, 3.3]")

# raw_decode returns (value, end_index) -- useful for parsing
# a value from the beginning of a larger string
value, idx = decoder.raw_decode('42  trailing text')
# value=42, idx=2
```

### RDNEncoder

```python
from rdn import RDNEncoder

# Basic usage
encoder = RDNEncoder(indent=2, sort_keys=True)
result = encoder.encode({"b": 2, "a": 1})

# iterencode yields chunks (useful for streaming)
for chunk in encoder.iterencode({"key": "value"}):
    print(chunk, end="")

# Subclass to handle custom types
class CustomEncoder(RDNEncoder):
    def default(self, o):
        if isinstance(o, set):
            return sorted(o)
        return super().default(o)
```

## File I/O

```python
import rdn
from datetime import datetime, timezone

data = {"name": "Alice", "created": datetime(2024, 1, 15, tzinfo=timezone.utc), "scores": (95, 87, 92)}

# Write to file
with open("data.rdn", "w") as f:
    rdn.dump(data, f, indent=2)

# Read from file
with open("data.rdn") as f:
    loaded = rdn.load(f)

# Works with bytes too
with open("data.rdn", "rb") as f:
    loaded = rdn.load(f)
```

## Error Handling

Parse errors raise `RDNDecodeError`, a subclass of `ValueError`:

```python
from rdn import loads, RDNDecodeError

try:
    loads("{invalid")
except RDNDecodeError as e:
    print(e)          # Human-readable message with position info
    print(e.msg)      # Error description
    print(e.pos)      # Character offset where the error occurred
    print(e.lineno)   # Line number (1-based)
    print(e.colno)    # Column number (1-based)
```

Serialization errors raise `TypeError` (non-serializable value) or `ValueError` (circular reference):

```python
try:
    rdn.dumps(object())
except TypeError:
    print("Not serializable")

a = []
a.append(a)
try:
    rdn.dumps(a)
except ValueError:
    print("Circular reference detected")
```

## Constants

```python
from rdn import MAX_SAFE_INTEGER

# JavaScript's Number.MAX_SAFE_INTEGER (2^53 - 1)
print(MAX_SAFE_INTEGER)  # 9007199254740991

# Integers beyond this threshold auto-serialize as BigInt
rdn.dumps(MAX_SAFE_INTEGER)      # '9007199254740991'
rdn.dumps(MAX_SAFE_INTEGER + 1)  # '9007199254740992n'
```

## License

MIT
