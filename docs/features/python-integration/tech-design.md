# Tech Design: Python RDN Integration

## 1. Overview

This feature builds a Python ecosystem for RDN (Rich Data Notation) consisting of three packages:

1. **`rdn`** (PyPI: `rdn`) -- Core pure Python parser and serializer with an API modeled after Python's `json` module. Supports all RDN types: dates, BigInts, regular expressions, binary data, Maps, Sets, tuples, TimeOnly, Duration, and special numeric values. A Rust/maturin C extension is planned for Phase 2 (separate tech design).

2. **`rdn-pydantic`** (PyPI: `rdn-pydantic`) -- Pydantic v2 integration providing custom annotated types for all RDN-specific value types, plus `model_dump_rdn()` and `model_validate_rdn()` methods for full model-level RDN serialization/deserialization.

3. **`rdn-fastapi`** (PyPI: `rdn-fastapi`) -- FastAPI integration with `RDNResponse` (custom response class), `RDNRoute` (auto-parsing request bodies), and `RDNMiddleware` (ASGI content-type negotiation).

All three packages share the core `rdn` package for parsing and serialization. The design mirrors the TypeScript reference implementation (`packages/rdn-js/`) algorithmically while following Python idioms (the `json` module API surface, `ValueError` exceptions, `dataclass` types).

## 2. Package Architecture

### 2.1 Monorepo Layout

```
packages/
  rdn-python/          # Core RDN package (published as "rdn" on PyPI)
  rdn-pydantic/        # Pydantic v2 integration (published as "rdn-pydantic")
  rdn-fastapi/         # FastAPI integration (published as "rdn-fastapi")
```

This follows the existing monorepo convention where each language implementation lives under `packages/`.

### 2.2 Package Dependencies

```
rdn-fastapi
  --> rdn-pydantic (optional, for model integration)
  --> rdn (required, for parsing/serialization)

rdn-pydantic
  --> rdn (required, for types and serialization)
  --> pydantic >= 2.0 (required)

rdn
  --> (no runtime dependencies)
```

- `rdn` is zero-dependency (pure Python, stdlib only).
- `rdn-pydantic` requires `rdn` and `pydantic>=2.0`.
- `rdn-fastapi` requires `rdn` and `fastapi>=0.100.0`. It has an optional dependency on `rdn-pydantic` for model integration but functions without it.

### 2.3 Package Structure (rdn-python)

```
packages/rdn-python/
  pyproject.toml
  README.md
  src/
    rdn/
      __init__.py          # Public API: loads, dumps, load, dump, classes, exceptions, constants
      _parser.py           # Pure Python recursive-descent parser
      _serializer.py       # Pure Python serializer with cycle detection
      _tables.py           # Dispatch table, base64/hex decode tables, escape table
      decoder.py           # RDNDecoder class (mirrors json.JSONDecoder)
      encoder.py           # RDNEncoder class (mirrors json.JSONEncoder)
      exceptions.py        # RDNDecodeError(ValueError)
  tests/
    __init__.py
    test_parse.py          # Unit tests for parser
    test_stringify.py      # Unit tests for serializer
    test_decoder.py        # Unit tests for RDNDecoder
    test_encoder.py        # Unit tests for RDNEncoder
    test_conformance.py    # Shared test-suite runner
    test_file_io.py        # Tests for load/dump file I/O
    test_edge_cases.py     # Edge cases: unicode, depth limits, large values
```

### 2.4 Package Structure (rdn-pydantic)

```
packages/rdn-pydantic/
  pyproject.toml
  README.md
  src/
    rdn_pydantic/
      __init__.py          # Re-exports all public types and functions
      types.py             # Pydantic-compatible annotated types for all RDN types
      model.py             # RDNModel mixin: model_dump_rdn(), model_validate_rdn()
  tests/
    __init__.py
    test_types.py          # Tests for each Pydantic RDN type
    test_model.py          # Tests for model_dump_rdn / model_validate_rdn
```

### 2.5 Package Structure (rdn-fastapi)

```
packages/rdn-fastapi/
  pyproject.toml
  README.md
  src/
    rdn_fastapi/
      __init__.py          # Re-exports RDNResponse, RDNRoute, RDNMiddleware
      response.py          # RDNResponse class
      routing.py           # RDNRoute class
      middleware.py         # RDNMiddleware ASGI middleware
  tests/
    __init__.py
    test_response.py       # Tests for RDNResponse
    test_routing.py        # Tests for RDNRoute
    test_middleware.py      # Tests for RDNMiddleware
    test_integration.py    # End-to-end FastAPI integration tests
```

## 3. Core Package (rdn-python) Design

### 3.1 Public API Surface

The public API mirrors Python's `json` module with RDN-specific extensions.

```python
# --- Top-level functions ---

def loads(
    s: str | bytes | bytearray,
    *,
    cls: type[RDNDecoder] | None = None,
    object_hook: Callable[[dict[str, Any]], Any] | None = None,
    parse_float: Callable[[str], Any] | None = None,
    parse_int: Callable[[str], Any] | None = None,
    parse_bigint: Callable[[str], Any] | None = None,
    parse_datetime: Callable[[datetime], Any] | None = None,
    parse_timeonly: Callable[[time], Any] | None = None,
    parse_duration: Callable[[timedelta | str], Any] | None = None,
    parse_regexp: Callable[[re.Pattern], Any] | None = None,
    parse_binary: Callable[[bytes], Any] | None = None,
    object_pairs_hook: Callable[[list[tuple[str, Any]]], Any] | None = None,
) -> Any: ...

def dumps(
    obj: Any,
    *,
    cls: type[RDNEncoder] | None = None,
    ensure_ascii: bool = True,
    check_circular: bool = True,
    indent: int | str | None = None,
    separators: tuple[str, str] | None = None,
    default: Callable[[Any], Any] | None = None,
    sort_keys: bool = False,
) -> str: ...

def load(
    fp: IO[str] | IO[bytes],
    *,
    cls: type[RDNDecoder] | None = None,
    object_hook: Callable[[dict[str, Any]], Any] | None = None,
    parse_float: Callable[[str], Any] | None = None,
    parse_int: Callable[[str], Any] | None = None,
    parse_bigint: Callable[[str], Any] | None = None,
    parse_datetime: Callable[[datetime], Any] | None = None,
    parse_timeonly: Callable[[time], Any] | None = None,
    parse_duration: Callable[[timedelta | str], Any] | None = None,
    parse_regexp: Callable[[re.Pattern], Any] | None = None,
    parse_binary: Callable[[bytes], Any] | None = None,
    object_pairs_hook: Callable[[list[tuple[str, Any]]], Any] | None = None,
) -> Any: ...

def dump(
    obj: Any,
    fp: IO[str],
    *,
    cls: type[RDNEncoder] | None = None,
    ensure_ascii: bool = True,
    check_circular: bool = True,
    indent: int | str | None = None,
    separators: tuple[str, str] | None = None,
    default: Callable[[Any], Any] | None = None,
    sort_keys: bool = False,
) -> None: ...
```

**Note on `loads` hooks**: The `parse_*` hooks follow the `json.loads` pattern. Each hook is called with either a string representation (for `parse_float`, `parse_int`, `parse_bigint`) or the parsed Python object (for `parse_datetime`, `parse_timeonly`, `parse_duration`, `parse_regexp`, `parse_binary`). The return value replaces the parsed result. `object_pairs_hook` takes priority over `object_hook` when both are provided.

**Note on `dumps` parameters**: `allow_nan` is intentionally omitted -- RDN always supports NaN/Infinity natively. `skipkeys` is omitted since RDN, like JSON, requires string keys for objects (non-string keys raise `TypeError`).

**Note on native types**: All RDN types map to Python stdlib types. No custom wrapper classes are needed. BigInt parses to `int`, TimeOnly to `datetime.time`, Duration to `timedelta` (or `str` for Y/M durations), RegExp to `re.Pattern`.

### 3.2 Type Mapping (types.py)

All RDN types map to Python stdlib types. No custom wrapper classes.

```python
from __future__ import annotations
import re
from datetime import datetime, time, timedelta, timezone
from typing import Any, Union

# Constants
MAX_SAFE_INTEGER = 2**53 - 1  # 9007199254740991 — JS Number.MAX_SAFE_INTEGER

# --- RDNValue type alias ---

RDNValue = Union[
    None,
    bool,
    int,              # BigInt also maps to int (auto-promote > MAX_SAFE_INTEGER on dumps)
    float,            # includes NaN, Infinity, -Infinity
    str,              # also used as fallback for Duration with Y/M components
    datetime,         # DateTime (always UTC)
    time,             # TimeOnly (milliseconds stored as microseconds * 1000)
    timedelta,        # Duration (D/H/M/S only — Y/M durations fall back to str)
    re.Pattern,       # RegExp (JS-only flags silently dropped)
    bytes,            # Binary (base64 and hex both parse to bytes)
    list,             # Array — list[RDNValue]
    tuple,            # Tuple — tuple[RDNValue, ...]
    dict,             # Object and Map — dict[str, RDNValue]
    set,              # Set — set[RDNValue]
    frozenset,        # Set (single-element from brace disambiguation)
]
```

**Type mapping details**:

| RDN Type | Python Type | Parse Behavior | Serialize Behavior |
|----------|-------------|----------------|-------------------|
| BigInt (`42n`) | `int` | `int("42")` | Auto-promote: `int` > `MAX_SAFE_INTEGER` → `"42n"`, otherwise → `"42"` |
| TimeOnly (`@14:30:00.500`) | `datetime.time` | `time(14, 30, 0, 500_000)` — ms stored as microseconds × 1000 | Format `time` → `@HH:MM:SS[.mmm]` using `microsecond // 1000` |
| Duration (`@P3DT4H`) | `datetime.timedelta` | Parse D/H/M/S components → `timedelta(days=3, hours=4)` | Format `timedelta` → `@PnDTnHnMnS` |
| Duration (`@P1Y2M`) | `str` | Y/M components can't be `timedelta` → return raw ISO string `"P1Y2M"` | `str` starting with `P` → `@P1Y2M` |
| RegExp (`/pat/ims`) | `re.Pattern` | `re.compile("pat", re.I \| re.M \| re.S)` — JS-only flags (`d`,`g`,`v`,`y`) silently dropped | `re.Pattern` → `"/pattern/flags"` — reconstruct flags from `pattern.flags` |
| DateTime | `datetime` | Always UTC (`tzinfo=timezone.utc`) | Always 24-char ISO format |
| Map | `dict` | Error on unhashable keys | Serialized as Object `{...}` (Map identity lost) |
| Set | `set`/`frozenset` | Error on unhashable elements | Always `Set{...}` prefix |
| Tuple | `tuple` | Parsed as `tuple` | Serialized as `(...)` |

**Key design rationale**:

- **`int` for BigInt**: Python `int` is already arbitrary precision. No wrapper needed for parsing. For serialization, auto-promote ints exceeding `MAX_SAFE_INTEGER` to BigInt format (`42n`). Ints within safe range serialize as plain JSON numbers.
- **`datetime.time` for TimeOnly**: Stores milliseconds as `microsecond = ms * 1000`. The microsecond field has sufficient precision (0-999999). No timezone is attached (TimeOnly has no timezone in the spec).
- **`timedelta` for Duration**: Handles the common case (days, hours, minutes, seconds). ISO 8601 durations with year/month components (`P1Y2M`) cannot be represented by `timedelta` (variable-length months), so those fall back to a plain `str`. The serializer detects `str` values starting with `"P"` and emits them as `@<str>`.
- **`re.Pattern` for RegExp**: JS-only flags (`d`, `g`, `v`, `y`) are silently dropped during compilation. This loses round-trip fidelity for those flags, but keeps the API simple with native types. The pattern source and Python-compatible flags are preserved in the `re.Pattern` object.
- **`dict`/`set`/`tuple`**: Native Python containers. No wrappers.

### 3.3 Parser Architecture (_parser.py)

The parser is a recursive-descent parser following the same architecture as the TypeScript reference implementation (`packages/rdn-js/src/parser.ts`). It uses module-level cursor state and a dispatch table for O(1) first-character branching.

#### 3.3.1 Module-Level State

```python
# Module-level parser state (set on entry, cleared in finally)
_source: str = ""
_pos: int = 0
_len: int = 0
_depth: int = 0

MAX_DEPTH: int = 128
MAX_BINARY_SIZE: int = 100 * 1024 * 1024  # 100 MB
```

Module-level state is chosen over a class-based approach or a passed context object for three reasons:
1. **Performance**: Avoids `self.` attribute lookups on every character access (significant in a hot loop).
2. **Matches TS**: The TypeScript implementation uses module-scoped `source`, `pos`, `len`, `depth` (`parser.ts:7-9`).
3. **Thread safety is not a concern**: The `json` module itself uses the same pattern. Python's GIL ensures single-threaded execution of pure Python code.

A `global` declaration is used in each function that modifies `_pos` or `_depth`. The state is always cleaned up in a `finally` block in the public `parse()` entry point.

#### 3.3.2 Dispatch Table

```python
# _tables.py

# Token constants
TOKEN_INVALID = 0
TOKEN_STRING = 1       # "
TOKEN_NUMBER = 2       # 0-9
TOKEN_MINUS = 3        # -
TOKEN_OPEN_BRACE = 4   # {
TOKEN_OPEN_BRACKET = 6 # [
TOKEN_OPEN_PAREN = 8   # (
TOKEN_TRUE = 12        # t
TOKEN_FALSE = 13       # f
TOKEN_NULL = 14        # n
TOKEN_AT = 15          # @
TOKEN_SLASH = 16       # /
TOKEN_B64 = 17         # b
TOKEN_HEX = 18         # x
TOKEN_INFINITY = 19    # I
TOKEN_NAN = 20         # N
TOKEN_MAP = 21         # M
TOKEN_SET = 22         # S

# 256-entry dispatch table: ord(char) -> token constant
TOKEN_TABLE: list[int] = [TOKEN_INVALID] * 256
TOKEN_TABLE[0x22] = TOKEN_STRING       # "
for _i in range(0x30, 0x3A):
    TOKEN_TABLE[_i] = TOKEN_NUMBER     # 0-9
TOKEN_TABLE[0x2D] = TOKEN_MINUS        # -
TOKEN_TABLE[0x7B] = TOKEN_OPEN_BRACE   # {
TOKEN_TABLE[0x5B] = TOKEN_OPEN_BRACKET # [
TOKEN_TABLE[0x28] = TOKEN_OPEN_PAREN   # (
TOKEN_TABLE[0x74] = TOKEN_TRUE         # t
TOKEN_TABLE[0x66] = TOKEN_FALSE        # f
TOKEN_TABLE[0x6E] = TOKEN_NULL         # n
TOKEN_TABLE[0x40] = TOKEN_AT           # @
TOKEN_TABLE[0x2F] = TOKEN_SLASH        # /
TOKEN_TABLE[0x62] = TOKEN_B64          # b
TOKEN_TABLE[0x78] = TOKEN_HEX          # x
TOKEN_TABLE[0x49] = TOKEN_INFINITY     # I
TOKEN_TABLE[0x4E] = TOKEN_NAN          # N
TOKEN_TABLE[0x4D] = TOKEN_MAP          # M
TOKEN_TABLE[0x53] = TOKEN_SET          # S
```

The dispatch table is a `list[int]` of 256 entries, indexed by `ord(char)`. This is equivalent to the TypeScript `Uint8Array(256)` table (`tables.ts:30-60`). In Python, `list` indexing is faster than `dict` lookup for this use case.

#### 3.3.3 Parse Functions

**`_parse_value()`** -- Main dispatch (mirrors `parseValue()` at `parser.ts:709-743`)

```python
def _parse_value() -> Any:
    global _pos
    _skip_ws()
    if _pos >= _len:
        _error("Unexpected end of input")
    ch = ord(_source[_pos])
    token = TOKEN_TABLE[ch]
    if token == TOKEN_STRING:
        return _parse_string()
    elif token == TOKEN_NUMBER:
        return _parse_number(negative=False)
    elif token == TOKEN_MINUS:
        _pos += 1
        if _pos < _len and _source[_pos] == "I":
            _parse_literal("Infinity")
            return float("-inf")
        return _parse_number(negative=True)
    elif token == TOKEN_OPEN_BRACE:
        return _parse_brace()
    elif token == TOKEN_OPEN_BRACKET:
        return _parse_array()
    elif token == TOKEN_OPEN_PAREN:
        return _parse_tuple()
    elif token == TOKEN_TRUE:
        _parse_literal("true")
        return True
    elif token == TOKEN_FALSE:
        _parse_literal("false")
        return False
    elif token == TOKEN_NULL:
        _parse_literal("null")
        return None
    elif token == TOKEN_AT:
        return _parse_at()
    elif token == TOKEN_SLASH:
        return _parse_regexp()
    elif token == TOKEN_B64:
        return _parse_binary_b64()
    elif token == TOKEN_HEX:
        return _parse_binary_hex()
    elif token == TOKEN_INFINITY:
        _parse_literal("Infinity")
        return float("inf")
    elif token == TOKEN_NAN:
        _parse_literal("NaN")
        return float("nan")
    elif token == TOKEN_MAP:
        return _parse_explicit_map()
    elif token == TOKEN_SET:
        return _parse_explicit_set()
    else:
        _error(f"Unexpected character '{chr(ch)}'")
```

We use `elif` chains rather than `match/case` because the token values are integers that map to function calls -- an `elif` chain is the most direct translation of the TS `switch` statement and has equivalent performance in CPython.

**`_parse_string()`** -- String parsing with deferred materialization (mirrors `parseString()` at `parser.ts:37-69`)

Algorithm:
1. Skip opening `"`.
2. Fast-scan: iterate character-by-character looking for `"` (end), `\` (escape), or control chars (error). Track whether any escape was seen.
3. If no escape was found, return `_source[start:_pos]` -- zero-copy slice. This is the fast path for the vast majority of strings.
4. If escapes were found, call `_materialize_string(start, end)` which processes escape sequences: `\"`, `\\`, `\/`, `\b`, `\f`, `\n`, `\r`, `\t`, `\uXXXX`. Unknown escapes raise `RDNDecodeError`. The materialization uses a list of string parts joined at the end (same as TS `parser.ts:71-108`).
5. `\uXXXX` handling: Parse 4 hex digits, convert to `chr(code_point)`. Surrogate pairs are handled transparently by Python's UCS-4 internal representation.

**`_parse_number(negative: bool)`** -- Number parsing (mirrors `parseNumber()` at `parser.ts:112-185`)

Algorithm:
1. Record start position. Accumulate integer digits into a running `int_value` (avoid string→number conversion for the common case of small integers).
2. Count digits. If `digit_count == 0`, error.
3. Leading zero check: if `digit_count > 1` and first digit is `0`, error (matches JSON spec).
4. Check for bigint suffix `n` at current position. If found, advance past `n` and return `int(_source[start:_pos-1])`. If the number had a decimal point or exponent before `n`, error (`parser.ts:175-177`).
5. Check for fraction (`.` followed by digits). Set `is_float = True`.
6. Check for exponent (`e`/`E` with optional sign and digits). Set `is_float = True`.
7. Fast path: if `not is_float and digit_count <= 15`, return the accumulated `int_value` directly (negative sign applied). This avoids `int()` string parsing.
8. Slow path: return `float(_source[start:_pos])` or `int(_source[start:_pos])` depending on `is_float`.

**`_parse_at()`** -- `@`-prefix disambiguation (mirrors `parseAt()` at `parser.ts:219-249`)

Algorithm:
1. Skip `@`.
2. If next char is `P` -> `_parse_duration()`.
3. If next char is a digit and char at `_pos+2` is `:` -> `_parse_timeonly()`.
4. If next char is a digit and char at `_pos+4` is `-` -> `_parse_datetime()`.
5. If next char is a digit (all digits) -> `_parse_unix_timestamp()`.
6. Otherwise error.

**`_parse_datetime()`** -- DateTime parsing (mirrors `parseDateTime()` at `parser.ts:251-278`)

Algorithm:
1. Read 4 digits (year), expect `-`, read 2 digits (month), expect `-`, read 2 digits (day).
2. If next char is not `T`, return `datetime(year, month, day, tzinfo=timezone.utc)` (date-only format).
3. Skip `T`, read 2 digits (hours), expect `:`, read 2 digits (minutes), expect `:`, read 2 digits (seconds).
4. If next char is `.`, skip it and read 3 digits (milliseconds). Convert to microseconds (`ms * 1000`).
5. Expect `Z`.
6. Return `datetime(year, month, day, hours, minutes, seconds, microsecond=ms*1000, tzinfo=timezone.utc)`.

**`_parse_timeonly()`** -- TimeOnly parsing (mirrors `parseTimeOnly()` at `parser.ts:280-294`)

Algorithm:
1. Read 2+2+2 digit groups for hours:minutes:seconds.
2. Optional `.` followed by 3 digits for milliseconds.
3. Return `time(hours, minutes, seconds, milliseconds * 1000)` — store ms as microseconds.

**`_parse_duration()`** -- Duration parsing (mirrors `parseDuration()` at `parser.ts:296-312`)

Algorithm:
1. Record start at `P`.
2. Scan forward while characters are digits, `Y`, `M`, `D`, `T`, `H`, `S`, or `.`.
3. Slice the ISO string (excluding `@` prefix, including `P`).
4. If length < 2, error.
5. Parse the duration components (D, H, M, S). If only D/H/M/S components are present, return `timedelta(...)`. If Y or M components are present, return the raw ISO string (e.g., `"P1Y2M3D"`) since `timedelta` cannot represent variable-length months/years.

**`_parse_regexp()`** -- RegExp parsing (mirrors `parseRegExp()` at `parser.ts:332-372`)

Algorithm:
1. Skip opening `/`.
2. Scan for closing `/`. Handle `\\` (escaped backslash) and `\/` (escaped slash) -- set an `escaped` flag on `\`, next char is always consumed.
3. If end of input before closing `/`, error "Unterminated regular expression".
4. Slice pattern.
5. Read flags: scan while char is in `{d, g, i, m, s, u, v, y}`.
6. Map compatible flags to Python `re` flags (`i`→`IGNORECASE`, `m`→`MULTILINE`, `s`→`DOTALL`). JS-only flags (`d`, `g`, `u`, `v`, `y`) are silently dropped.
7. Return `re.compile(pattern, mapped_flags)`.

**`_parse_binary_b64()`** -- Base64 binary parsing (mirrors `parseBinaryB64()` at `parser.ts:376-434`)

Algorithm:
1. Skip `b`, expect `"`, skip it.
2. Scan to closing `"`.
3. If content is empty, return `b""`.
4. Validate length is multiple of 4.
5. Decode using `base64.b64decode(content, validate=True)`. Python's `base64.b64decode` with `validate=True` performs strict RFC 4648 validation including padding checks.
6. Check decoded length against `MAX_BINARY_SIZE`.
7. Additionally validate non-zero padding bits by checking the last encoded characters manually (the TS implementation does this at `parser.ts:417-418` and `parser.ts:422-423`).
8. Return `bytes` result.

**`_parse_binary_hex()`** -- Hex binary parsing (mirrors `parseBinaryHex()` at `parser.ts:436-459`)

Algorithm:
1. Skip `x`, expect `"`, skip it.
2. Scan to closing `"`.
3. If empty, return `b""`.
4. Validate even length.
5. Validate all characters are hex digits.
6. Check decoded length against `MAX_BINARY_SIZE`.
7. Return `bytes.fromhex(content)`.

**`_parse_brace()`** -- Brace disambiguation (mirrors `parseBrace()` at `parser.ts:513-561`)

Algorithm:
1. Enter container (depth check).
2. Skip `{`, skip whitespace.
3. If next char is `}`, return empty `dict` (empty `{}` is always Object per spec section 5).
4. Parse first value.
5. Skip whitespace. Inspect separator:
   - `:` -> first value must be `str`, call `_finish_object(first_key)`.
   - `=` followed by `>` -> call `_finish_map(first_key)`.
   - `,` -> call `_finish_set(first_value)`.
   - `}` -> return `frozenset({first_value})` (single-element Set).
6. Each `_finish_*` function parses remaining entries and the closing `}`.

**Object parsing (`_finish_object`)**: Uses `dict` (preserves insertion order in Python 3.7+). If `object_pairs_hook` is provided, collects `(key, value)` pairs into a list and passes to the hook. If `object_hook` is provided, passes the constructed dict. Objects are created with `dict()` constructor (not `object.__new__`), which means `__proto__` keys are safe -- they are just regular dict entries in Python.

**Map parsing (`_finish_map`)**: Inserts entries into a `dict`. Raises `RDNDecodeError` if a key is unhashable (e.g., a list or dict).

**Set parsing (`_finish_set`)**: Inserts elements into a `set`. Raises `RDNDecodeError` if an element is unhashable.

**`_parse_array()`** -- Array parsing (mirrors `parseArray()` at `parser.ts:467-488`)

1. Enter container, skip `[`, skip whitespace.
2. If `]`, return empty `list`.
3. Parse values separated by `,` until `]`.
4. Return `list`.

**`_parse_tuple()`** -- Tuple parsing (mirrors `parseTuple()` at `parser.ts:490-511`)

1. Enter container, skip `(`, skip whitespace.
2. If `)`, return empty `tuple`.
3. Parse values separated by `,` until `)`.
4. Return `tuple` (convert from intermediate list).

**`_parse_explicit_map()`** -- `Map{...}` parsing (mirrors `parseExplicitMap()` at `parser.ts:626-666`)

1. Verify `Map{` prefix (4 characters).
2. Skip past prefix, enter container.
3. If `}`, return empty `dict`.
4. Parse `key => value` entries separated by `,`.
5. Return `dict`.

**`_parse_explicit_set()`** -- `Set{...}` parsing (mirrors `parseExplicitSet()` at `parser.ts:668-693`)

1. Verify `Set{` prefix (4 characters).
2. Skip past prefix, enter container.
3. If `}`, return empty `set`.
4. Parse values separated by `,`.
5. Return `set`.

**`_parse_literal(expected: str)`** -- Keyword matching (mirrors `parseLiteral()` at `parser.ts:697-705`)

Consumes `len(expected)` characters from `_source`, comparing each. Errors if mismatch. Used for `null`, `true`, `false`, `NaN`, `Infinity`.

#### 3.3.4 Error Handling

All parse errors raise `RDNDecodeError`. The `_error(msg)` helper constructs:
```python
raise RDNDecodeError(msg, _source, _pos)
```
Which produces a string representation: `"<msg> in RDN at position <pos>"` (matching the spec section 12 format and the TS implementation at `parser.ts:17-19`).

#### 3.3.5 Max Depth

`MAX_DEPTH = 128`, matching the TypeScript implementation (`parser.ts:11`). The `_enter_container()` helper increments `_depth` and raises `RDNDecodeError("Maximum nesting depth exceeded (128)")` if exceeded. This is explicit depth tracking rather than relying on Python's recursion limit, which provides consistent behavior across platforms.

#### 3.3.6 Hook Application

Parse hooks (`parse_float`, `parse_int`, `parse_bigint`, `parse_datetime`, `parse_timeonly`, `parse_duration`, `parse_regexp`, `parse_binary`) are applied immediately at parse time:

- `parse_int`: called with the string representation of each integer (e.g., `"42"`). Default: `int()`.
- `parse_float`: called with the string representation of each float (e.g., `"3.14"`). Default: `float()`.
- `parse_bigint`: called with the string representation of each bigint (e.g., `"42"`, without the `n` suffix). Default: `int()`.
- `parse_datetime`: called with the parsed `datetime` object. Default: identity.
- `parse_timeonly`: called with the parsed `datetime.time` object. Default: identity.
- `parse_duration`: called with the parsed `timedelta` or `str` (for Y/M durations). Default: identity.
- `parse_regexp`: called with the parsed `re.Pattern` object. Default: identity.
- `parse_binary`: called with the parsed `bytes` object. Default: identity.

`object_hook` and `object_pairs_hook` are applied after each object is fully parsed, mirroring `json.loads()` behavior. `object_pairs_hook` takes priority over `object_hook`.

The hooks are stored in module-level variables (like the cursor state) and checked during parsing. If a hook is `None`, no call is made (default behavior).

### 3.4 Serializer Architecture (_serializer.py)

The serializer converts Python values to RDN text. It follows the same structure as the TypeScript implementation (`serializer.ts`).

#### 3.4.1 Cycle Detection

```python
_seen: set[int] | None = None  # set of id() values

def _check_cycle(obj: object) -> None:
    obj_id = id(obj)
    if obj_id in _seen:
        raise ValueError("Converting circular structure to RDN")
    _seen.add(obj_id)

def _remove_cycle(obj: object) -> None:
    _seen.discard(id(obj))
```

Uses `set[int]` of `id()` values instead of `WeakSet` (TS uses `WeakSet`, `serializer.ts:5`). In Python, `WeakSet` cannot hold unhashable types and has higher overhead. `id()` is guaranteed unique for simultaneously alive objects.

Cycle detection is enabled by default (`check_circular=True`). When disabled, no `_seen` set is created and `_check_cycle`/`_remove_cycle` are no-ops.

#### 3.4.2 Type Dispatch Order

The `_stringify_value()` function checks types in this order (matching `serializer.ts:98-215`):

1. `value is None` -> `"null"`
2. `isinstance(value, str)` -> `_escape_string(value)`. Duration Y/M fallback strings (e.g., `"P1Y2M"`) serialize as plain strings — they do NOT auto-convert back to `@P1Y2M`. This is a known one-way parse behavior (see design decision #21).
3. `isinstance(value, bool)` -> `"true"` / `"false"` (must be before `int` since `bool` is a subclass of `int`)
4. `isinstance(value, int)` -> number or bigint formatting (see 3.4.5)
5. `isinstance(value, float)` -> number formatting with NaN/Infinity handling
6. `isinstance(value, datetime)` -> `_format_date(value)`
7. `isinstance(value, time)` -> `_format_timeonly(value)` — format as `@HH:MM:SS[.mmm]`
8. `isinstance(value, timedelta)` -> `_format_duration(value)` — format as `@PnDTnHnMnS`
9. `isinstance(value, re.Pattern)` -> `"/" + pattern.pattern + "/" + _reconstruct_flags(pattern.flags)`
10. `isinstance(value, bytes)` -> `'b"' + base64.b64encode(value).decode() + '"'`
11. `isinstance(value, bytearray)` -> convert to bytes, same as above
12. `isinstance(value, dict)` -> object serialization
13. `isinstance(value, list)` -> array serialization
14. `isinstance(value, tuple)` -> tuple serialization (output with `(...)`)
15. `isinstance(value, (set, frozenset))` -> set serialization
16. Fall through: call `default` function if provided, otherwise raise `TypeError`

The `bool` check MUST come before `int` because `isinstance(True, int)` is `True` in Python. This is a critical ordering constraint.

#### 3.4.3 String Escaping

```python
# Pre-computed escape table for chars 0x00-0xFF
ESCAPE_TABLE: list[str] = [""] * 256
ESCAPE_TABLE[0x22] = '\\"'     # "
ESCAPE_TABLE[0x5C] = "\\\\"    # \
ESCAPE_TABLE[0x08] = "\\b"     # backspace
ESCAPE_TABLE[0x09] = "\\t"     # tab
ESCAPE_TABLE[0x0A] = "\\n"     # LF
ESCAPE_TABLE[0x0C] = "\\f"     # form feed
ESCAPE_TABLE[0x0D] = "\\r"     # CR
for _i in range(0x20):
    if ESCAPE_TABLE[_i] == "":
        ESCAPE_TABLE[_i] = f"\\u{_i:04x}"
```

Fast path: scan the string looking for any char that needs escaping (`< 0x20`, `"`, `\`). If none found, return `'"' + s + '"'` (zero-copy wrap). This matches the TS fast-scan at `serializer.ts:18-28`.

Slow path: build an output list, flushing plain segments and inserting escape sequences. Same as TS `serializer.ts:30-43`.

When `ensure_ascii=True` (default), also escape all chars with `ord(c) > 0x7F` using `\uXXXX` sequences. For codepoints above U+FFFF, use surrogate pair encoding (`\uD800-\uDBFF` + `\uDC00-\uDFFF`).

#### 3.4.4 Number Formatting

- **int (within safe range)**: `str(value)`. Python's `str(int)` produces the same output as JavaScript's `String(number)` for integers.
- **float (finite)**: Use `repr(value)` which produces the shortest representation that round-trips. However, we need to ensure no trailing `.0` for whole numbers that should stay as floats (e.g., `1e10` should serialize as `10000000000.0` or as the exponent form). We use `repr()` and let Python pick the shortest form, which matches JS `String(number)` behavior for standard IEEE 754 values.
- **float (NaN)**: `"NaN"`
- **float (+Infinity)**: `"Infinity"`
- **float (-Infinity)**: `"-Infinity"`

#### 3.4.5 BigInt Detection and Serialization

Serialization uses a simple auto-promote approach:

1. **Auto-promote**: `int` values where `abs(value) > MAX_SAFE_INTEGER` (2^53-1 = 9007199254740991) -> serialize with `n` suffix: `str(value) + "n"`.
2. **Normal**: `int` within safe range -> serialize as regular JSON number: `str(value)`.

No wrapper class needed. All `int` values are treated uniformly — the serializer checks the magnitude.

#### 3.4.6 Date Formatting

```python
_DIGIT_PAIRS: list[str] = [f"{i:02d}" for i in range(100)]

def _format_date(d: datetime) -> str:
    if d.tzinfo is None:
        # Naive datetime: treat as UTC for serialization
        pass
    year = f"{d.year:04d}"
    return (
        "@" + year + "-" + _DIGIT_PAIRS[d.month] + "-" + _DIGIT_PAIRS[d.day]
        + "T" + _DIGIT_PAIRS[d.hour] + ":" + _DIGIT_PAIRS[d.minute]
        + ":" + _DIGIT_PAIRS[d.second]
        + "." + f"{d.microsecond // 1000:03d}" + "Z"
    )
```

Always outputs the 24-character ISO format (`@YYYY-MM-DDTHH:mm:ss.sssZ`) per spec section 4.3. Uses the `_DIGIT_PAIRS` table for fast 2-digit formatting, matching the TS `DIGIT_PAIRS` approach (`serializer.ts:48-54`). Microseconds are divided by 1000 to produce milliseconds.

If the datetime has a non-UTC timezone, it is first converted to UTC. If the datetime is naive (no tzinfo), it is treated as UTC.

#### 3.4.7 Container Serialization

**Array (list)**:
```python
_check_cycle(obj)
parts = []
for i, item in enumerate(obj):
    el = _stringify_value(item, replacer, str(i))
    parts.append("null" if el is None else el)
_remove_cycle(obj)
return "[" + item_sep.join(parts) + "]"
```

Undefined/non-serializable elements in arrays are replaced with `"null"` (matching JSON behavior and `serializer.ts:152`).

**Object (dict)**:
```python
_check_cycle(obj)
parts = []
keys = sorted(obj.keys()) if sort_keys else obj.keys()
for k in keys:
    if not isinstance(k, str):
        raise TypeError(f"Object key must be a string, got {type(k).__name__}")
    sv = _stringify_value(obj[k], replacer, k)
    if sv is not None:
        parts.append(_escape_string(k) + key_sep + sv)
_remove_cycle(obj)
return "{" + item_sep.join(parts) + "}"
```

Non-string keys raise `TypeError`. Properties with non-serializable values are omitted (matching `serializer.ts:207-209`).

**Map (dict with explicit Map syntax)**: Maps are serialized differently from objects. However, since the resolved decision uses native `dict` for both objects and maps, we need a way to distinguish them during serialization. The approach:
- Regular `dict` -> serialized as Object (`{key: value}`)
- There is no separate Map serialization from a plain `dict`. If users need Map syntax, they should use a future `RDNMap` wrapper or the encoder's `default` method.
- When parsing, `Map{...}` and `{k => v}` produce a `dict` -- the distinction is lost. This is an acceptable trade-off given the resolved decision to use native `dict`.

**Set (set/frozenset)**:
```python
_check_cycle(obj)  # only for mutable set
if len(obj) == 0:
    return "Set{}"
parts = []
for item in obj:
    s = _stringify_value(item, replacer, item)
    if s is not None:
        parts.append(s)
_remove_cycle(obj)
return "Set{" + item_sep.join(parts) + "}"
```

Always uses explicit `Set{...}` prefix. Empty sets serialize as `Set{}`.

**Tuple**:
```python
parts = []
for i, item in enumerate(obj):
    el = _stringify_value(item, replacer, str(i))
    parts.append("null" if el is None else el)
return "(" + item_sep.join(parts) + ")"
```

Tuples serialize with `(...)` parentheses, distinguishing them from lists.

#### 3.4.8 Indent / Pretty-Print Support

When `indent` is not `None`:
- `indent` can be an `int` (number of spaces) or `str` (literal indent string, e.g., `"\t"`).
- Default separators change from `(",", ":")` to `(",\n", ": ")`.
- Each nesting level adds one `indent` prefix.
- Closing delimiters are on their own line with the parent's indent.
- Map entries use ` => ` with spaces for readability.

Implementation: The `_stringify_value` function accepts `level: int` parameter. When indent is active, newlines and indentation are inserted between elements. This is the same approach used by `json.JSONEncoder.iterencode()`.

#### 3.4.9 Replacer Application

The `default` function is called for values that have no built-in serialization:

```python
if default is not None:
    replacement = default(value)
    return _stringify_value(replacement, None, key)  # No double-replacement
raise TypeError(f"Object of type {type(value).__name__} is not RDN serializable")
```

This matches `json.JSONEncoder.default()` behavior. The return value of `default()` is serialized. If `default` itself returns a non-serializable value, `TypeError` is raised.

#### 3.4.10 `iterencode()` for Streaming

The `RDNEncoder.iterencode()` method yields string chunks instead of building the entire output in memory. Useful for streaming large documents to a file or network socket. Implementation follows `json.JSONEncoder.iterencode()` -- each leaf value is yielded as a single chunk, and container delimiters/separators are yielded separately.

### 3.5 RDNDecoder Class

```python
class RDNDecoder:
    """RDN decoder class, mirroring json.JSONDecoder.

    Subclass and override methods for custom parsing behavior.
    """

    def __init__(
        self,
        *,
        object_hook: Callable[[dict[str, Any]], Any] | None = None,
        parse_float: Callable[[str], Any] | None = None,
        parse_int: Callable[[str], Any] | None = None,
        parse_bigint: Callable[[str], Any] | None = None,
        parse_datetime: Callable[[datetime], Any] | None = None,
        parse_timeonly: Callable[[time], Any] | None = None,
        parse_duration: Callable[[timedelta | str], Any] | None = None,
        parse_regexp: Callable[[re.Pattern], Any] | None = None,
        parse_binary: Callable[[bytes], Any] | None = None,
        object_pairs_hook: Callable[[list[tuple[str, Any]]], Any] | None = None,
    ) -> None:
        self.object_hook = object_hook
        self.parse_float = parse_float or float
        self.parse_int = parse_int or int
        self.parse_bigint = parse_bigint or int
        self.parse_datetime = parse_datetime
        self.parse_timeonly = parse_timeonly
        self.parse_duration = parse_duration
        self.parse_regexp = parse_regexp
        self.parse_binary = parse_binary
        self.object_pairs_hook = object_pairs_hook

    def decode(self, s: str) -> Any:
        """Decode an RDN string and return the Python representation."""
        # Delegates to the module-level parser with stored hooks
        ...

    def raw_decode(self, s: str, idx: int = 0) -> tuple[Any, int]:
        """Decode an RDN value starting at position idx.

        Returns a tuple of (parsed_value, end_position).
        Useful for parsing RDN values embedded in larger strings.
        """
        ...
```

The `decode()` method delegates to the internal `_parse()` function, passing all hook parameters. The `raw_decode()` method allows starting parsing at an arbitrary position and returns the end position, enabling partial parsing.

### 3.6 RDNEncoder Class

```python
class RDNEncoder:
    """RDN encoder class, mirroring json.JSONEncoder.

    Subclass and override the default() method for custom type serialization.
    """

    def __init__(
        self,
        *,
        ensure_ascii: bool = True,
        check_circular: bool = True,
        indent: int | str | None = None,
        separators: tuple[str, str] | None = None,
        default: Callable[[Any], Any] | None = None,
        sort_keys: bool = False,
    ) -> None:
        self.ensure_ascii = ensure_ascii
        self.check_circular = check_circular
        self.indent = indent
        self.separators = separators
        self.default_func = default
        self.sort_keys = sort_keys

    def encode(self, o: Any) -> str:
        """Return the RDN string representation of a Python value."""
        ...

    def iterencode(self, o: Any) -> Iterator[str]:
        """Encode the given object and yield each string chunk."""
        ...

    def default(self, o: Any) -> Any:
        """Override this method for custom type serialization.

        Called for objects that the encoder cannot serialize by default.
        Should return a serializable object or raise TypeError.
        """
        raise TypeError(f"Object of type {type(o).__name__} is not RDN serializable")
```

When `cls` is passed to `dumps()`, the encoder class is instantiated with the provided parameters and `encode()` is called. Users can subclass `RDNEncoder` and override `default()` to handle custom types.

### 3.7 C Extension Strategy (Rust + maturin)

**Phase 1** (this tech design): Pure Python only. All logic lives in `_parser.py` and `_serializer.py`. Zero native dependencies.

**Phase 2** (separate tech design): Rust + maturin C extension.
- Complete the Rust parser/serializer in `packages/rdn-rust/`.
- Add PyO3 bindings to expose `parse()` and `stringify()` to Python.
- Build with `maturin build` to produce platform-specific wheels.
- Pure Python remains the fallback.

**Fallback mechanism** in `__init__.py`:
```python
try:
    from rdn._native import parse as _native_parse, stringify as _native_stringify
    _USE_NATIVE = True
except ImportError:
    _USE_NATIVE = False

def loads(s, **kwargs):
    if _USE_NATIVE and not kwargs:  # Native path only for simple calls
        return _native_parse(s)
    # Fall back to pure Python with full hook support
    ...
```

The native extension handles the hot path (no hooks, no custom decoder class). When hooks are provided, the pure Python implementation is used to ensure full flexibility.

## 4. Design Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | Parser state management | Module-level globals | Matches TS implementation (`parser.ts:7-9`). Avoids `self.` overhead in hot loops. Thread-safe under GIL. Cleaned up in `finally` block. |
| 2 | Dispatch table type | `list[int]` of 256 entries | O(1) lookup by `ord(char)`. Faster than `dict` for dense integer keys. Matches TS `Uint8Array(256)` (`tables.ts:30-60`). |
| 3 | Value dispatch mechanism | `if/elif` chain on token constants | Most direct translation of TS `switch` (`parser.ts:716-743`). `match/case` offers no perf benefit for integer dispatch. |
| 4 | BigInt representation | Plain `int` (auto-promote on dumps) | Python `int` is already arbitrary precision. No wrapper needed. Serializer auto-promotes `int > MAX_SAFE_INTEGER` to `42n` format. |
| 5 | TimeOnly representation | `datetime.time` | Milliseconds stored as `microsecond = ms * 1000`. No custom type needed. Native stdlib type. |
| 6 | Duration representation | `timedelta` + `str` fallback | `timedelta` for D/H/M/S durations. Plain `str` for Y/M durations (e.g., `"P1Y2M"`). Serializer detects `str` starting with `"P"`. |
| 7 | RegExp representation | `re.Pattern` | JS-only flags (`d`, `g`, `v`, `y`) silently dropped. Compatible flags mapped to `re` constants. Loses round-trip fidelity for JS-only flags but keeps API simple. |
| 8 | Map representation | Native `dict` | Per resolved decision. Error on unhashable keys. Covers 99% of real-world use cases. Preserves insertion order. |
| 9 | Set representation | Native `set` / `frozenset` | Per resolved decision. Error on unhashable elements. `frozenset` for single-element set from brace disambiguation (immutable). |
| 10 | Tuple representation | Native `tuple` | Natural Python mapping. Serializes as `(...)` to distinguish from list `[...]`. |
| 11 | Cycle detection | `set[int]` of `id()` values | Simpler than `WeakSet`. `id()` is unique for alive objects. Matches conceptual approach of TS `WeakSet` (`serializer.ts:5`). |
| 12 | String escaping | Fast-scan + slow-path build | Matches TS pattern (`serializer.ts:18-44`). Most strings need no escaping, so the fast scan avoids allocation. |
| 13 | Date formatting | Pre-computed `DIGIT_PAIRS` table | Matches TS approach (`serializer.ts:48-54`). Avoids `strftime` overhead. Always outputs 24-char ISO format per spec. |
| 14 | Binary encoding | Python `base64` stdlib module | C-backed, fast. Strict validation with `validate=True`. No need to reimplement. |
| 15 | Error format | `"<msg> in RDN at position <pos>"` | Matches spec section 12 format and TS implementation (`parser.ts:17-19`). |
| 16 | `bool` before `int` in dispatch | Explicit ordering | `isinstance(True, int)` is `True` in Python. Must check `bool` first to avoid serializing `True` as `1`. |
| 17 | Map serialization round-trip | Lost distinction | Parsing `Map{...}` produces `dict`, which serializes as `{...}` (Object). This is acceptable per the resolved decision. A future `RDNMap` wrapper could preserve the distinction. |
| 18 | Base64 non-zero padding bits | Manual check after `b64decode` | Python's `base64.b64decode(validate=True)` does not reject non-zero padding bits. We manually check the last encoded characters to match the TS strict validation (`parser.ts:417-423`). |
| 19 | `ensure_ascii` default | `True` (matching `json.dumps`) | Conservative default. Non-ASCII chars are escaped with `\uXXXX`. Users can set `ensure_ascii=False` for UTF-8 output. |
| 20 | Input types | `str`, `bytes`, `bytearray` | Matches `json.loads()`. `bytes`/`bytearray` are decoded as UTF-8 before parsing. |
| 21 | Duration Y/M round-trip | One-way parse (no auto-serialize) | `@P1Y2M` parses to `str("P1Y2M")` but serializes back as `"P1Y2M"` (a quoted string, not `@P1Y2M`). Users needing Y/M duration round-trip must use a custom encoder `default()`. This is acceptable since Y/M durations are rare and `timedelta` is the primary representation. |
| 22 | RegExp flag round-trip | Lossy for JS-only flags | JS flags `d`, `g`, `v`, `y` are dropped at parse time. Round-trip loses these flags. Acceptable trade-off for using native `re.Pattern`. |

## 5. Pydantic Package (rdn-pydantic) Design

### 5.1 Custom Types

Each RDN type is exposed as a Pydantic-compatible annotated type using `__get_pydantic_core_schema__`:

```python
from typing import Annotated
from pydantic import GetCoreSchemaHandler
from pydantic_core import core_schema
from datetime import datetime, time, timedelta
import re

class _RDNDateTimeAnnotation:
    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: type, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @classmethod
    def _validate(cls, value: datetime | str) -> datetime:
        if isinstance(value, datetime):
            return value
        if isinstance(value, str):
            return rdn.loads(f'@{value}')  # Parse as RDN DateTime
        raise ValueError("Invalid RDN DateTime")

    @classmethod
    def _serialize(cls, value: datetime) -> str:
        return value.isoformat()

PydanticRDNDateTime = Annotated[datetime, _RDNDateTimeAnnotation]
```

Since all RDN types map to native Python types, the Pydantic types are thin `Annotated` wrappers that add RDN-aware validation and serialization:

| Export Name | Python Type | RDN Type |
|-------------|-------------|----------|
| `PydanticRDNBigInt` | `Annotated[int, ...]` | BigInt (`42n`) — validates as `int` |
| `PydanticRDNDateTime` | `Annotated[datetime, ...]` | DateTime (`@2024-01-15T...Z`) |
| `PydanticRDNTimeOnly` | `Annotated[time, ...]` | TimeOnly (`@14:30:00`) |
| `PydanticRDNDuration` | `Annotated[timedelta, ...]` | Duration (`@P3DT4H`) |
| `PydanticRDNRegExp` | `Annotated[re.Pattern, ...]` | RegExp (`/pattern/flags`) |
| `PydanticRDNBinary` | `Annotated[bytes, ...]` | Binary (`b"..."`) |
| `PydanticRDNSet` | `Annotated[set, ...]` | Set (`Set{...}`) |

Each type implements both validation (accepting the native Python type or a raw value that can be converted) and serialization (producing a Pydantic-friendly representation).

### 5.2 Model Integration

The `RDNModel` mixin extends `pydantic.BaseModel` with RDN-specific methods:

```python
from pydantic import BaseModel
import rdn

class RDNModel(BaseModel):
    """Base model with RDN serialization support.

    Usage:
        class User(RDNModel):
            name: str
            created: PydanticRDNDateTime
            tags: PydanticRDNSet
    """

    def model_dump_rdn(
        self,
        *,
        indent: int | None = None,
        exclude_none: bool = False,
        by_alias: bool = False,
    ) -> str:
        """Serialize the model to an RDN string.

        Uses model_dump() to get the dict representation, then
        serializes to RDN. RDN-specific types (BigInt, DateTime, etc.)
        are preserved through the serialization pipeline.
        """
        data = self.model_dump(mode="python", exclude_none=exclude_none, by_alias=by_alias)
        return rdn.dumps(data, indent=indent)

    @classmethod
    def model_validate_rdn(
        cls,
        rdn_data: str | bytes,
        *,
        strict: bool = False,
    ) -> "RDNModel":
        """Validate and create a model instance from an RDN string.

        Parses the RDN string, then validates the result through
        Pydantic's validation pipeline.
        """
        parsed = rdn.loads(rdn_data)
        return cls.model_validate(parsed, strict=strict)
```

**Design choice**: Mixin class rather than monkey-patching `BaseModel`. This is explicit, opt-in, and avoids modifying global Pydantic behavior. Users inherit from `RDNModel` instead of `BaseModel`.

### 5.3 Serialization Config

For fine-grained control, the Pydantic types support Pydantic's standard configuration:

```python
class User(RDNModel):
    model_config = ConfigDict(
        populate_by_name=True,
        json_schema_extra={"format": "rdn"},
    )

    name: str
    id: PydanticRDNBigInt = Field(alias="userId")
    created_at: PydanticRDNDateTime
```

The `model_dump_rdn()` method respects `by_alias`, `exclude_none`, and other standard Pydantic dump parameters. The RDN types integrate with Pydantic's JSON Schema generation, adding appropriate `format` hints.

## 6. FastAPI Package (rdn-fastapi) Design

### 6.1 RDNResponse

```python
from fastapi.responses import Response
from typing import Any
import rdn

class RDNResponse(Response):
    """FastAPI response class that serializes content as RDN.

    Usage:
        @app.get("/data", response_class=RDNResponse)
        async def get_data():
            return {"key": "value", "created": datetime.now(timezone.utc)}
    """
    media_type = "application/x-rdn"

    def __init__(
        self,
        content: Any = None,
        status_code: int = 200,
        headers: dict[str, str] | None = None,
        media_type: str | None = None,
        background: Any = None,
        *,
        indent: int | None = None,
    ) -> None:
        self.indent = indent
        super().__init__(content, status_code, headers, media_type, background)

    def render(self, content: Any) -> bytes:
        """Serialize content to RDN and encode as UTF-8 bytes."""
        if content is None:
            return b""
        return rdn.dumps(content, indent=self.indent).encode("utf-8")
```

### 6.2 RDNRoute

```python
from fastapi.routing import APIRoute
from fastapi import Request, Response
from typing import Callable

class RDNRoute(APIRoute):
    """Custom APIRoute that auto-parses RDN request bodies.

    Usage:
        app = FastAPI()
        app.router.route_class = RDNRoute
    """

    def get_route_handler(self) -> Callable:
        original_handler = super().get_route_handler()

        async def rdn_handler(request: Request) -> Response:
            content_type = request.headers.get("content-type", "")
            if "application/x-rdn" in content_type:
                body = await request.body()
                request._rdn_body = rdn.loads(body)  # Attach parsed body
            return await original_handler(request)

        return rdn_handler
```

When the incoming `Content-Type` is `application/x-rdn`, the request body is parsed as RDN and attached to the request object. Route handlers can access the parsed data via a dependency:

```python
from fastapi import Depends

async def get_rdn_body(request: Request) -> Any:
    """Dependency that extracts the parsed RDN body."""
    return getattr(request, "_rdn_body", None) or rdn.loads(await request.body())

@app.post("/data")
async def create_data(data: dict = Depends(get_rdn_body)):
    return data
```

### 6.3 RDN Middleware

```python
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

class RDNMiddleware(BaseHTTPMiddleware):
    """ASGI middleware for transparent RDN content-type negotiation.

    When the client sends Accept: application/x-rdn, the middleware
    intercepts the JSON response and re-serializes it as RDN.

    When the client sends Content-Type: application/x-rdn, the middleware
    parses the RDN body and re-encodes it as JSON for downstream handlers.

    Usage:
        app = FastAPI()
        app.add_middleware(RDNMiddleware)
    """

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        # Parse RDN request bodies
        content_type = request.headers.get("content-type", "")
        accept = request.headers.get("accept", "")
        wants_rdn = "application/x-rdn" in accept

        response = await call_next(request)

        # Re-serialize response as RDN if requested
        if wants_rdn and response.headers.get("content-type", "").startswith("application/json"):
            body = b""
            async for chunk in response.body_iterator:
                body += chunk if isinstance(chunk, bytes) else chunk.encode()
            import json
            data = json.loads(body)
            rdn_body = rdn.dumps(data).encode("utf-8")
            return Response(
                content=rdn_body,
                status_code=response.status_code,
                headers={**dict(response.headers), "content-type": "application/x-rdn"},
            )

        return response
```

### 6.4 Integration with rdn-pydantic

When `rdn-pydantic` is installed, the FastAPI package can leverage `RDNModel` for automatic validation:

```python
from rdn_pydantic import RDNModel, PydanticRDNDateTime
from rdn_fastapi import RDNResponse, RDNRoute

class CreateUserRequest(RDNModel):
    name: str
    created: PydanticRDNDateTime

app = FastAPI()
app.router.route_class = RDNRoute

@app.post("/users", response_class=RDNResponse)
async def create_user(user: CreateUserRequest):
    # user is automatically validated from RDN body
    return user.model_dump()
```

The integration is optional -- `rdn-fastapi` works with plain dicts and `rdn.loads()`/`rdn.dumps()` without Pydantic.

## 7. Error Handling

### 7.1 RDNDecodeError

```python
class RDNDecodeError(ValueError):
    """Exception raised for RDN parsing errors.

    Attributes:
        msg: Human-readable error description
        doc: The RDN document being parsed
        pos: Character offset where the error was detected
        lineno: Line number (1-indexed)
        colno: Column number (1-indexed)
    """

    def __init__(self, msg: str, doc: str, pos: int) -> None:
        self.msg = msg
        self.doc = doc
        self.pos = pos
        # Compute line/column from position
        self.lineno = doc.count("\n", 0, pos) + 1
        self.colno = pos - doc.rfind("\n", 0, pos)
        # Format matches spec section 12
        errmsg = f"{msg} in RDN at position {pos} (line {self.lineno} column {self.colno})"
        super().__init__(errmsg)
```

Inherits from `ValueError` (matching `json.JSONDecodeError(ValueError)`). The `str()` representation includes both the byte offset (matching the spec) and line/column for developer convenience.

### 7.2 Parse Error Messages

Comprehensive list of all error messages produced by the parser, matching the TS implementation:

| Error Message | Condition | TS Reference |
|---------------|-----------|--------------|
| `"Unexpected end of input"` | EOF when value expected | `parser.ts:711` |
| `"Unexpected character '<c>'"` | Invalid first character for value | `parser.ts:742` |
| `"Unterminated string"` | EOF inside string literal | `parser.ts:68` |
| `"Unescaped control character in string"` | Char < 0x20 inside string | `parser.ts:65` |
| `"Invalid escape sequence '\\<c>'"` | Unknown escape after `\` | `parser.ts:97` |
| `"Invalid unicode escape"` | Bad `\uXXXX` sequence | `parser.ts:90-92` |
| `"Expected digit"` | No digits in number | `parser.ts:124` |
| `"Leading zeros not allowed"` | `01`, `007`, etc. | `parser.ts:128` |
| `"Expected digit after decimal point"` | `3.` with no following digits | `parser.ts:150` |
| `"Expected digit in exponent"` | `3e` with no following digits | `parser.ts:170` |
| `"BigInt cannot have decimal point or exponent"` | `3.14n` or `1e10n` | `parser.ts:176` |
| `"Unexpected end after @"` | EOF after `@` | `parser.ts:222` |
| `"Invalid @ literal"` | Unrecognized `@` format | `parser.ts:248` |
| `"Expected 2-digit number"` | Non-digit in time/date field | `parser.ts:193` |
| `"Expected 3-digit number"` | Non-digit in milliseconds | `parser.ts:203` |
| `"Expected 4-digit year"` | Non-digit in year | `parser.ts:214` |
| `"Expected '<c>'"` | Missing expected character (`:`, `-`, `T`, `Z`) | `parser.ts:29-32` |
| `"Invalid duration"` | Duration with < 2 chars | `parser.ts:310` |
| `"Unterminated regular expression"` | EOF inside regex | `parser.ts:355` |
| `"Expected '\"' after 'b'"` | Missing `"` after `b` prefix | `parser.ts:378` |
| `"Expected '\"' after 'x'"` | Missing `"` after `x` prefix | `parser.ts:438` |
| `"Unterminated binary literal"` | EOF inside `b"..."` | `parser.ts:384` |
| `"Unterminated hex literal"` | EOF inside `x"..."` | `parser.ts:443` |
| `"Invalid base64: length must be a multiple of 4"` | Bad base64 length | `parser.ts:391` |
| `"Invalid base64 character"` | Non-base64 char in `b"..."` | `parser.ts:410-411` |
| `"Invalid base64: non-zero padding bits"` | Padding bits not zero | `parser.ts:418,423` |
| `"Invalid hex: odd length"` | Odd number of hex chars | `parser.ts:448` |
| `"Invalid hex character"` | Non-hex char in `x"..."` | `parser.ts:455` |
| `"Binary data too large"` | Exceeds MAX_BINARY_SIZE | `parser.ts:399,449` |
| `"Maximum nesting depth exceeded (128)"` | Depth > 128 | `parser.ts:464` |
| `"Unterminated brace expression"` | EOF inside `{...}` | `parser.ts:528` |
| `"Object key must be a string"` | Non-string key before `:` | `parser.ts:534` |
| `"Expected '=>'"` | `=` without `>` in brace | `parser.ts:543` |
| `"Expected ':', '=>', ',' or '}' after value in brace expression"` | Invalid separator | `parser.ts:560` |
| `"Expected '=>' in map entry"` | Missing `=>` in map | `parser.ts:595,644,656` |
| `"Expected 'Map{'"` | Invalid Map prefix | `parser.ts:630` |
| `"Expected 'Set{'"` | Invalid Set prefix | `parser.ts:671` |
| `"Expected 'true'"`, `"Expected 'false'"`, etc. | Partial keyword | `parser.ts:700` |
| `"Unexpected data after value"` | Trailing content | `parser.ts:813` |

### 7.3 Serialization Errors

| Exception Type | Condition |
|----------------|-----------|
| `ValueError("Converting circular structure to RDN")` | Circular reference detected |
| `TypeError("Object key must be a string, got <type>")` | Non-string dict key |
| `TypeError("Object of type <type> is not RDN serializable")` | Unsupported type with no `default` handler |

## 8. Testing Strategy

### 8.1 Conformance Test Runner

The conformance test runner (`tests/test_conformance.py`) consumes the shared test suite at `test-suite/` relative to the repo root.

```python
import json
import math
import os
import pytest
import rdn

SUITE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "..", "test-suite")

def normalize_for_comparison(value):
    """Convert parsed RDN values to $type-tagged dicts for comparison
    with the expected JSON output."""
    if value is None:
        return None
    if isinstance(value, bool):
        return value
    if isinstance(value, float):
        if math.isnan(value):
            return {"$type": "Number", "value": "NaN"}
        if value == float("inf"):
            return {"$type": "Number", "value": "Infinity"}
        if value == float("-inf"):
            return {"$type": "Number", "value": "-Infinity"}
        return value
    if isinstance(value, int):
        # Note: BigInt and regular int are both `int` — we check if the conformance
        # test expected a BigInt by comparing against the expected JSON side
        return value
    if isinstance(value, str):
        # Duration Y/M fallback strings are just `str` — check if expected is Duration
        return value
    if isinstance(value, datetime):
        return {"$type": "Date", "value": value.strftime("%Y-%m-%dT%H:%M:%S.") + f"{value.microsecond // 1000:03d}Z"}
    if isinstance(value, time):
        return {"$type": "TimeOnly", "value": {"hours": value.hour, "minutes": value.minute, "seconds": value.second, "milliseconds": value.microsecond // 1000}}
    if isinstance(value, timedelta):
        return {"$type": "Duration", "value": _timedelta_to_iso(value)}
    if isinstance(value, re.Pattern):
        return {"$type": "RegExp", "value": {"source": value.pattern, "flags": _reconstruct_flags(value.flags)}}
    if isinstance(value, bytes):
        import base64
        return {"$type": "Binary", "value": base64.b64encode(value).decode()}
    if isinstance(value, dict):
        # Could be Object or Map (both are dict in our impl)
        return {k: normalize_for_comparison(v) for k, v in value.items()}
    if isinstance(value, (set, frozenset)):
        return {"$type": "Set", "value": [normalize_for_comparison(v) for v in value]}
    if isinstance(value, (list, tuple)):
        return [normalize_for_comparison(v) for v in value]
    raise TypeError(f"Cannot normalize {type(value)}")
```

The runner uses `pytest.mark.parametrize` to generate one test per file:

- **Valid tests**: Read `.rdn`, parse with `rdn.loads()`, normalize, read `.expected.json`, parse with `json.loads()`, deep-compare.
- **Invalid tests**: Read `.rdn`, assert `rdn.loads()` raises `rdn.RDNDecodeError`.
- **Roundtrip tests**: Read `.rdn`, parse, stringify, parse again, normalize both, deep-compare.

**Note on Map/dict normalization**: Since Maps parse to `dict` and we lose the `Map` vs `Object` distinction, the conformance runner normalizes `dict` as a plain object. For the `map.expected.json` test file which expects `{"$type": "Map", ...}`, we need special handling: when the `.expected.json` contains a `$type: "Map"` tag, we compare the `dict` entries as ordered pairs. This is handled by the `normalize_for_comparison` function detecting Maps from the expected output side.

### 8.2 Unit Tests

| File | Coverage |
|------|----------|
| `test_parse.py` | All parse functions individually: strings (plain, escaped, unicode), numbers (int, float, bigint, NaN, Infinity), dates (4 formats), TimeOnly, Duration, RegExp, binary (base64, hex), arrays, tuples, objects, maps, sets, brace disambiguation, depth limit, error messages |
| `test_stringify.py` | All serialization paths: primitives, strings (escape fast/slow path, ensure_ascii), numbers, BigInt auto-promote, Date formatting, `re.Pattern`, binary (base64), `time` (TimeOnly), `timedelta` (Duration), containers (list, tuple, dict, set), cycle detection, indent, sort_keys, default function |
| `test_decoder.py` | RDNDecoder class: all hooks, raw_decode, custom subclass |
| `test_encoder.py` | RDNEncoder class: encode, iterencode, default override, custom subclass |
| `test_file_io.py` | `load(fp)`, `dump(obj, fp)` with StringIO, BytesIO, real files |
| `test_edge_cases.py` | Unicode surrogate pairs, max depth, empty inputs, whitespace-only, very large numbers, very long strings, binary size limit |

### 8.3 Integration Tests

| Package | File | Coverage |
|---------|------|----------|
| rdn-pydantic | `test_types.py` | Each Pydantic RDN type: validation from raw values, serialization, JSON schema generation |
| rdn-pydantic | `test_model.py` | `model_dump_rdn()` and `model_validate_rdn()` with complex models, nested models, optional fields |
| rdn-fastapi | `test_response.py` | `RDNResponse` render with various content types |
| rdn-fastapi | `test_routing.py` | `RDNRoute` request body parsing with Content-Type header |
| rdn-fastapi | `test_middleware.py` | `RDNMiddleware` content negotiation |
| rdn-fastapi | `test_integration.py` | Full FastAPI app with TestClient: POST RDN body, GET RDN response, error handling |

## 9. Configuration & Build

### 9.1 pyproject.toml (rdn-python)

```toml
[project]
name = "rdn"
version = "0.1.0"
description = "RDN (Rich Data Notation) parser and serializer for Python"
readme = "README.md"
license = "MIT"
requires-python = ">=3.10"
authors = [{ name = "RDN Contributors" }]
keywords = ["rdn", "json", "parser", "serializer", "data-notation"]
classifiers = [
    "Development Status :: 3 - Alpha",
    "Intended Audience :: Developers",
    "License :: OSI Approved :: MIT License",
    "Programming Language :: Python :: 3",
    "Programming Language :: Python :: 3.10",
    "Programming Language :: Python :: 3.11",
    "Programming Language :: Python :: 3.12",
    "Programming Language :: Python :: 3.13",
    "Topic :: Software Development :: Libraries :: Python Modules",
    "Typing :: Typed",
]
dependencies = []

[project.urls]
Homepage = "https://github.com/AstroSnout/rdn"
Repository = "https://github.com/AstroSnout/rdn"
Documentation = "https://github.com/AstroSnout/rdn/tree/main/packages/rdn-python"

[build-system]
requires = ["setuptools>=68.0", "setuptools-scm"]
build-backend = "setuptools.build_meta"

[tool.setuptools.packages.find]
where = ["src"]

[tool.pytest.ini_options]
testpaths = ["tests"]
pythonpath = ["src"]

[tool.mypy]
python_version = "3.10"
strict = true
warn_return_any = true
warn_unused_configs = true

[tool.ruff]
target-version = "py310"
line-length = 120

[tool.ruff.lint]
select = ["E", "F", "I", "N", "W", "UP"]
```

### 9.2 pyproject.toml (rdn-pydantic)

```toml
[project]
name = "rdn-pydantic"
version = "0.1.0"
description = "Pydantic v2 integration for RDN (Rich Data Notation)"
readme = "README.md"
license = "MIT"
requires-python = ">=3.10"
authors = [{ name = "RDN Contributors" }]
keywords = ["rdn", "pydantic", "validation", "serialization"]
classifiers = [
    "Development Status :: 3 - Alpha",
    "Intended Audience :: Developers",
    "License :: OSI Approved :: MIT License",
    "Programming Language :: Python :: 3",
    "Typing :: Typed",
]
dependencies = [
    "rdn>=0.1.0",
    "pydantic>=2.0",
]

[project.urls]
Homepage = "https://github.com/AstroSnout/rdn"
Repository = "https://github.com/AstroSnout/rdn"

[build-system]
requires = ["setuptools>=68.0"]
build-backend = "setuptools.build_meta"

[tool.setuptools.packages.find]
where = ["src"]

[tool.pytest.ini_options]
testpaths = ["tests"]
pythonpath = ["src"]
```

### 9.3 pyproject.toml (rdn-fastapi)

```toml
[project]
name = "rdn-fastapi"
version = "0.1.0"
description = "FastAPI integration for RDN (Rich Data Notation)"
readme = "README.md"
license = "MIT"
requires-python = ">=3.10"
authors = [{ name = "RDN Contributors" }]
keywords = ["rdn", "fastapi", "asgi", "api"]
classifiers = [
    "Development Status :: 3 - Alpha",
    "Intended Audience :: Developers",
    "License :: OSI Approved :: MIT License",
    "Programming Language :: Python :: 3",
    "Typing :: Typed",
]
dependencies = [
    "rdn>=0.1.0",
    "fastapi>=0.100.0",
]

[project.optional-dependencies]
pydantic = ["rdn-pydantic>=0.1.0"]

[project.urls]
Homepage = "https://github.com/AstroSnout/rdn"
Repository = "https://github.com/AstroSnout/rdn"

[build-system]
requires = ["setuptools>=68.0"]
build-backend = "setuptools.build_meta"

[tool.setuptools.packages.find]
where = ["src"]

[tool.pytest.ini_options]
testpaths = ["tests"]
pythonpath = ["src"]
```

### 9.4 CI Configuration

Add a `python` job to `.github/workflows/ci.yml`:

```yaml
  python:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        python-version: ["3.10", "3.11", "3.12", "3.13"]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with:
          python-version: ${{ matrix.python-version }}
      - name: Install rdn
        run: pip install -e packages/rdn-python[dev]
      - name: Run rdn tests
        run: pytest packages/rdn-python/tests -v
      - name: Type check rdn
        run: mypy packages/rdn-python/src/rdn
      - name: Install rdn-pydantic
        run: pip install -e packages/rdn-pydantic[dev]
      - name: Run rdn-pydantic tests
        run: pytest packages/rdn-pydantic/tests -v
      - name: Install rdn-fastapi
        run: pip install -e packages/rdn-fastapi[dev]
      - name: Run rdn-fastapi tests
        run: pytest packages/rdn-fastapi/tests -v
```

## 10. Performance Considerations

The pure Python implementation will be significantly slower than the C-backed `json` module or libraries like `orjson`. The following optimizations mitigate this:

| Optimization | Description | Impact |
|-------------|-------------|--------|
| Dispatch table (`list[int]`) | O(1) character classification via list indexing | Avoids dict lookup per character |
| Deferred string materialization | Fast-scan for unescaped strings; only build parts list when escapes found | ~80% of strings need no escape processing |
| Integer accumulation | Accumulate small integers directly in `int` variable instead of slicing + `int()` | Avoids string allocation for most numbers |
| Module-level state | `global` variables instead of `self.attr` | ~30% faster attribute access in hot loops |
| Pre-computed tables | `ESCAPE_TABLE`, `DIGIT_PAIRS` | Table lookup vs. conditional branches |
| String fast-scan escaping | Scan entire string before building escape list | Early termination for clean strings |
| `base64` stdlib | C-backed base64 encode/decode | Avoids pure Python byte manipulation |
| `bytes.fromhex()` | C-backed hex decode | Avoids pure Python hex parsing |
| Bulk string slicing | In `materializeString`, copy non-escaped segments in bulk using `_source[i:j]` | Fewer list appends |

**Expected performance relative to `json` module**: 3-5x slower for simple JSON-compatible documents (string scanning overhead), comparable for documents heavy in dates/regex/binary (the stdlib `json` module cannot parse these at all).

## 11. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Pure Python parser too slow for production use | High | Medium | Phase 2 Rust extension. Profile and optimize hot paths. Consider `__slots__` on all classes. |
| Map/Set unhashable key/element errors confuse users | Medium | Low | Clear error message explaining which value is unhashable and suggesting alternatives. Document the limitation prominently. |
| `json.loads` API hooks too complex (many parameters) | Low | Low | All hooks are optional with sensible defaults. Document each hook with examples. |
| Conformance test suite map distinction | Medium | Low | Maps parsed to `dict` lose `Map` identity. Conformance runner needs special handling for `$type: "Map"` expected values. |
| Pydantic v2 breaking changes | Low | Medium | Pin `pydantic>=2.0,<3.0`. Follow Pydantic changelog. |
| FastAPI version compatibility | Low | Medium | Pin `fastapi>=0.100.0`. Starlette middleware API is stable. |
| Python 3.10 minimum excludes some users | Low | Low | 3.10 is 3+ years old. Enables `match`, `TypeAlias`, `ParamSpec`, union `X | Y` syntax. |
| `datetime` microsecond precision loss | Low | Low | RDN spec uses milliseconds (3 digits). Python `datetime` has microsecond precision. We divide by 1000 during formatting and multiply by 1000 during parsing. No data loss for spec-compliant values. |
| ReDoS via parsed regex patterns | Medium | High | Document the risk prominently. `re.compile()` is called at parse time. Patterns with catastrophic backtracking may cause issues when used in matching operations. Users should validate patterns before use. |
| Thread safety of module-level state | Low | Low | Python's GIL prevents concurrent pure Python execution. The state is set/cleared within a single `parse()` call via `try/finally`. Async code (FastAPI) uses the GIL correctly. |

## 12. Ordered Task List

### Task 1: Create rdn-python package scaffolding
Set up the package directory structure, `pyproject.toml`, and empty module files.

- Files:
  - `packages/rdn-python/pyproject.toml` (rewrite)
  - `packages/rdn-python/src/rdn/__init__.py` (create)
  - `packages/rdn-python/src/rdn/exceptions.py` (create)
  - `packages/rdn-python/src/rdn/_tables.py` (create)
  - `packages/rdn-python/src/rdn/_parser.py` (create)
  - `packages/rdn-python/src/rdn/_serializer.py` (create)
  - `packages/rdn-python/src/rdn/decoder.py` (create)
  - `packages/rdn-python/src/rdn/encoder.py` (create)
  - `packages/rdn-python/tests/__init__.py` (create)
- Dependencies: None
- Acceptance: `pip install -e packages/rdn-python` succeeds. `import rdn` works (empty module).

### Task 2: Implement exceptions and constants
Implement `RDNDecodeError(ValueError)` with `msg`, `doc`, `pos` attributes and the `MAX_SAFE_INTEGER` constant. No custom type classes needed — all RDN types map to stdlib types.

- Files:
  - `packages/rdn-python/src/rdn/exceptions.py`
- Dependencies: Task 1
- Acceptance: `RDNDecodeError("msg", "doc", 5)` formats as `"msg in RDN at position 5"`. `str()` and `repr()` work. Has `msg`, `doc`, `pos` attributes.

### Task 3: Implement lookup tables
Implement `TOKEN_TABLE`, `B64_DECODE`, `HEX_DECODE`, `ESCAPE_TABLE`, `DIGIT_PAIRS` tables.

- Files:
  - `packages/rdn-python/src/rdn/_tables.py`
- Dependencies: Task 1
- Acceptance: All table entries match the TypeScript tables (`tables.ts`). `TOKEN_TABLE[ord('"')] == TOKEN_STRING`, etc.

### Task 4: Implement parser -- primitives and strings
Implement the parser module scaffold (module-level state, `_skip_ws`, `_error`, `_expect`, `_parse_literal`), string parsing (`_parse_string`, `_materialize_string`), and basic `_parse_value` dispatch for strings and keywords (`null`, `true`, `false`).

- Files:
  - `packages/rdn-python/src/rdn/_parser.py`
  - `packages/rdn-python/tests/test_parse.py` (create)
- Dependencies: Tasks 2, 3
- Acceptance: `parse('"hello"')` returns `"hello"`. `parse('"esc\\nape"')` returns `"esc\nape"`. `parse("null")` returns `None`. `parse("true")` returns `True`. Unicode escapes work. Control character errors work. `pytest tests/test_parse.py` passes for string/keyword tests.

### Task 5: Implement parser -- numbers and BigInt
Add number parsing (`_parse_number`) and BigInt detection to the parser. Handle NaN, Infinity, -Infinity. BigInt (`42n`) parses to plain `int`.

- Files:
  - `packages/rdn-python/src/rdn/_parser.py`
  - `packages/rdn-python/tests/test_parse.py`
- Dependencies: Task 4
- Acceptance: `parse("42")` returns `42` (int). `parse("3.14")` returns `3.14` (float). `parse("42n")` returns `42` (int). `parse("NaN")` returns `float('nan')`. `parse("-Infinity")` returns `float('-inf')`. Leading zero errors work. `3.14n` raises error.

### Task 6: Implement parser -- DateTime, TimeOnly, Duration
Add `_parse_at`, `_parse_datetime`, `_parse_timeonly`, `_parse_duration`, `_parse_unix_timestamp`.

- Files:
  - `packages/rdn-python/src/rdn/_parser.py`
  - `packages/rdn-python/tests/test_parse.py`
- Dependencies: Task 5
- Acceptance: All 4 DateTime formats parse correctly. `@14:30:00.500` returns `time(14, 30, 0, 500000)`. `@P3DT4H5M6S` returns `timedelta(days=3, hours=4, minutes=5, seconds=6)`. `@P1Y2M3D` returns `"P1Y2M3D"` (str fallback for Y/M). Unix timestamps handle seconds vs milliseconds threshold.

### Task 7: Implement parser -- RegExp and Binary
Add `_parse_regexp`, `_parse_binary_b64`, `_parse_binary_hex`.

- Files:
  - `packages/rdn-python/src/rdn/_parser.py`
  - `packages/rdn-python/tests/test_parse.py`
- Dependencies: Task 5
- Acceptance: `/^[a-z]+$/i` returns `re.compile("^[a-z]+$", re.IGNORECASE)`. JS-only flags silently dropped. `b"SGVsbG8="` returns `b"Hello"`. `x"48656C6C6F"` returns `b"Hello"`. Invalid base64/hex raises errors. Empty binary works.

### Task 8: Implement parser -- Arrays, Tuples, Brace disambiguation
Add `_parse_array`, `_parse_tuple`, `_parse_brace`, `_finish_object`, `_finish_map`, `_finish_set`, `_parse_explicit_map`, `_parse_explicit_set`. Implement depth tracking.

- Files:
  - `packages/rdn-python/src/rdn/_parser.py`
  - `packages/rdn-python/tests/test_parse.py`
- Dependencies: Task 7
- Acceptance: Arrays, tuples, objects, maps, sets all parse. Brace disambiguation works (`{}` -> empty dict, `{"a": 1}` -> dict, `{"a" => 1}` -> dict, `{"a", "b"}` -> set, `{"a"}` -> frozenset). `Map{}` and `Set{}` work. Depth limit enforced at 128. `pytest tests/test_parse.py` fully passes.

### Task 9: Wire up public parse API with hooks
Implement `parse()` function in `_parser.py`. Wire up `loads()`, `load()` in `__init__.py`. Implement all parse hooks (`parse_float`, `parse_int`, `parse_bigint`, `parse_datetime`, `parse_timeonly`, `parse_duration`, `parse_regexp`, `parse_binary`, `object_hook`, `object_pairs_hook`).

- Files:
  - `packages/rdn-python/src/rdn/_parser.py`
  - `packages/rdn-python/src/rdn/__init__.py`
  - `packages/rdn-python/tests/test_parse.py`
  - `packages/rdn-python/tests/test_file_io.py` (create)
- Dependencies: Task 8
- Acceptance: `rdn.loads(text)` works. `rdn.load(fp)` reads from file-like object. All hooks are called correctly. `bytes`/`bytearray` input is decoded as UTF-8.

### Task 10: Implement serializer -- primitives and strings
Implement `_stringify_value` for `None`, `bool`, `int`, `float`, `str`. Implement `_escape_string` with fast/slow path and `ensure_ascii` support.

- Files:
  - `packages/rdn-python/src/rdn/_serializer.py`
  - `packages/rdn-python/tests/test_stringify.py` (create)
- Dependencies: Task 3
- Acceptance: Primitives serialize correctly. String escaping handles all control chars, `"`, `\`, unicode. `ensure_ascii=True` escapes non-ASCII. `ensure_ascii=False` passes UTF-8 through.

### Task 11: Implement serializer -- extended types (int auto-promote, datetime, time, timedelta, re.Pattern, bytes)
Add serialization for `int` (auto-promote > MAX_SAFE_INTEGER), `datetime`, `time` (TimeOnly), `timedelta` (Duration), `str` starting with `"P"` (Duration fallback), `re.Pattern`, `bytes`.

- Files:
  - `packages/rdn-python/src/rdn/_serializer.py`
  - `packages/rdn-python/tests/test_stringify.py`
- Dependencies: Task 10
- Acceptance: BigInt auto-promote works (>2^53-1 gets `n` suffix). Dates format as 24-char ISO. `re.Pattern` uses `/pattern/flags` with flags reconstructed from `pattern.flags`. Binary uses base64. `time` formats as `@HH:MM:SS[.mmm]`. `timedelta` formats as `@PnDTnHnMnS`.

### Task 12: Implement serializer -- containers, cycle detection, indent
Add serialization for `list`, `tuple`, `dict`, `set`/`frozenset`. Implement cycle detection with `set[int]` of `id()`. Implement `indent` and `sort_keys` support.

- Files:
  - `packages/rdn-python/src/rdn/_serializer.py`
  - `packages/rdn-python/tests/test_stringify.py`
- Dependencies: Task 11
- Acceptance: Containers serialize correctly. Circular references raise `ValueError`. `indent=2` produces pretty output. `sort_keys=True` sorts dict keys. `tuple` serializes as `(...)`, list as `[...]`.

### Task 13: Wire up public stringify API
Implement `stringify()` function in `_serializer.py`. Wire up `dumps()`, `dump()` in `__init__.py`. Implement `default` function support.

- Files:
  - `packages/rdn-python/src/rdn/_serializer.py`
  - `packages/rdn-python/src/rdn/__init__.py`
  - `packages/rdn-python/tests/test_stringify.py`
  - `packages/rdn-python/tests/test_file_io.py`
- Dependencies: Task 12
- Acceptance: `rdn.dumps(obj)` works. `rdn.dump(obj, fp)` writes to file-like object. `default` function is called for unsupported types. All `dumps` parameters work.

### Task 14: Implement RDNDecoder and RDNEncoder classes
Implement the class-based API: `RDNDecoder` (with `decode`, `raw_decode`) and `RDNEncoder` (with `encode`, `iterencode`, `default`). Wire up `cls` parameter in `loads`/`dumps`.

- Files:
  - `packages/rdn-python/src/rdn/decoder.py`
  - `packages/rdn-python/src/rdn/encoder.py`
  - `packages/rdn-python/src/rdn/__init__.py`
  - `packages/rdn-python/tests/test_decoder.py` (create)
  - `packages/rdn-python/tests/test_encoder.py` (create)
- Dependencies: Tasks 9, 13
- Acceptance: `RDNDecoder().decode(text)` works. `RDNDecoder(parse_bigint=lambda s: Decimal(s)).decode("42n")` returns `Decimal(42)`. `RDNEncoder(indent=2).encode(obj)` works. `RDNEncoder().iterencode(obj)` yields chunks. Custom subclasses with overridden `default()` work.

### Task 15: Conformance test runner and edge case tests
Implement the conformance test runner that consumes `test-suite/`. Add edge case tests.

- Files:
  - `packages/rdn-python/tests/test_conformance.py` (create)
  - `packages/rdn-python/tests/test_edge_cases.py` (create)
- Dependencies: Tasks 9, 13
- Acceptance: All 11 valid tests pass. All 10 invalid tests pass. Both roundtrip tests pass. Edge cases (max depth, empty input, surrogate pairs, large numbers) pass.

### Task 16: Update __init__.py exports and README
Finalize the public API surface in `__init__.py`. Update the package README with installation, usage examples, and API reference.

- Files:
  - `packages/rdn-python/src/rdn/__init__.py`
  - `packages/rdn-python/README.md` (rewrite)
- Dependencies: Tasks 14, 15
- Acceptance: `from rdn import loads, dumps, load, dump, RDNDecoder, RDNEncoder, RDNDecodeError` all work. README has complete examples. `__all__` is defined.

### Task 17: Create rdn-pydantic package
Set up package structure. Implement all Pydantic custom types and the `RDNModel` mixin with `model_dump_rdn()` and `model_validate_rdn()`.

- Files:
  - `packages/rdn-pydantic/pyproject.toml` (create)
  - `packages/rdn-pydantic/README.md` (create)
  - `packages/rdn-pydantic/src/rdn_pydantic/__init__.py` (create)
  - `packages/rdn-pydantic/src/rdn_pydantic/types.py` (create)
  - `packages/rdn-pydantic/src/rdn_pydantic/model.py` (create)
  - `packages/rdn-pydantic/tests/__init__.py` (create)
  - `packages/rdn-pydantic/tests/test_types.py` (create)
  - `packages/rdn-pydantic/tests/test_model.py` (create)
- Dependencies: Task 16
- Acceptance: All Pydantic types validate and serialize correctly. `model_dump_rdn()` produces valid RDN. `model_validate_rdn()` parses RDN into validated model. All tests pass.

### Task 18: Create rdn-fastapi package
Set up package structure. Implement `RDNResponse`, `RDNRoute`, and `RDNMiddleware`.

- Files:
  - `packages/rdn-fastapi/pyproject.toml` (create)
  - `packages/rdn-fastapi/README.md` (create)
  - `packages/rdn-fastapi/src/rdn_fastapi/__init__.py` (create)
  - `packages/rdn-fastapi/src/rdn_fastapi/response.py` (create)
  - `packages/rdn-fastapi/src/rdn_fastapi/routing.py` (create)
  - `packages/rdn-fastapi/src/rdn_fastapi/middleware.py` (create)
  - `packages/rdn-fastapi/tests/__init__.py` (create)
  - `packages/rdn-fastapi/tests/test_response.py` (create)
  - `packages/rdn-fastapi/tests/test_routing.py` (create)
  - `packages/rdn-fastapi/tests/test_middleware.py` (create)
  - `packages/rdn-fastapi/tests/test_integration.py` (create)
- Dependencies: Task 16 (Task 17 optional for Pydantic integration tests)
- Acceptance: `RDNResponse` serializes content as RDN. `RDNRoute` parses RDN request bodies. Middleware handles content negotiation. Integration tests with `TestClient` pass.

### Task 19: Update monorepo documentation and CI
Update `CLAUDE.md` build commands, repo root `README.md`, and CI workflow for Python packages.

- Files:
  - `CLAUDE.md`
  - `README.md`
  - `.github/workflows/ci.yml`
- Dependencies: Tasks 16, 17, 18
- Acceptance: `CLAUDE.md` documents Python build/test commands. `README.md` lists Python implementation. CI workflow runs Python tests on push.
