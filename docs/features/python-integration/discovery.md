# Discovery: Python RDN Integration

## 1. Feature Overview

Build a full Python ecosystem for RDN (Rich Data Notation) consisting of three packages:

1. **`rdn-python`** (core) -- A pure Python parser/serializer with an optional C extension for performance. API modeled after Python's built-in `json` module (`loads`, `dumps`, `load`, `dump`), supporting all RDN types: dates, BigInts, regular expressions, binary data, Maps, Sets, tuples, TimeOnly, Duration, and special numeric values (NaN, Infinity).

2. **`rdn-pydantic`** -- Pydantic v2 integration providing custom types, validators, and serializers so Pydantic models can natively consume and produce RDN.

3. **`rdn-fastapi`** -- FastAPI integration with custom request parsing and response serialization for the `application/x-rdn` content type.

---

## 2. Current State

### 2.1 Existing Python Code

The `packages/rdn-python/` directory exists as a **minimal placeholder** with only two files:

**`packages/rdn-python/README.md`** (10 lines):
```markdown
# RDN Python Implementation
Placeholder for the Python RDN parser/serializer.

## Planned
- Pure Python implementation
- `rdn.parse(text)` / `rdn.stringify(value)`
- Type mappings: `datetime`, `Decimal` (BigInt), `re.Pattern`, `bytes`, `dict` (ordered), `set`, `tuple`
```

**`packages/rdn-python/pyproject.toml`** (10 lines):
```toml
[project]
name = "rdn"
version = "0.1.0"
description = "RDN (Rich Data Notation) parser and serializer for Python"
license = "MIT"
requires-python = ">=3.10"

[build-system]
requires = ["setuptools>=68.0"]
build-backend = "setuptools.backends._legacy:_Backend"
```

There is **no source code** -- no `src/` directory, no `rdn/` package, no tests. The `pyproject.toml` uses an older setuptools backend. There are no entries for `rdn-pydantic` or `rdn-fastapi` anywhere in the monorepo.

The `implementations/python/` path referenced in `CLAUDE.md` does **not exist** -- the Python code lives under `packages/rdn-python/`.

### 2.2 RDN Specification Summary

Source: `spec/rdn-spec.md` (Version 1.0)

RDN is a **strict superset of JSON**. Every valid JSON document is valid RDN. No comments, no trailing commas, no unquoted keys.

#### All Supported Types

| Category | Type | RDN Syntax | Example |
|----------|------|-----------|---------|
| JSON | null | `null` | `null` |
| JSON | boolean | `true` / `false` | `true` |
| JSON | number | Standard JSON numbers | `42`, `3.14`, `1e10` |
| JSON | string | Double-quoted with escapes | `"hello"` |
| JSON | array | `[...]` | `[1, "two", true]` |
| JSON | object | `{key: val}` | `{"a": 1}` |
| RDN | NaN | `NaN` | `NaN` |
| RDN | Infinity | `Infinity` / `-Infinity` | `-Infinity` |
| RDN | BigInt | Integer + `n` suffix | `42n`, `-123n` |
| RDN | DateTime | `@` prefix, 4 formats | `@2024-01-15T10:30:00.000Z` |
| RDN | TimeOnly | `@HH:MM:SS[.mmm]` | `@14:30:00.500` |
| RDN | Duration | `@P...` (ISO 8601) | `@P1Y2M3DT4H5M6S` |
| RDN | RegExp | `/pattern/flags` | `/^[a-z]+$/i` |
| RDN | Binary (base64) | `b"..."` | `b"SGVsbG8="` |
| RDN | Binary (hex) | `x"..."` | `x"48656C6C6F"` |
| RDN | Map | `Map{k => v}` or `{k => v}` | `Map{"a" => 1}` |
| RDN | Set | `Set{v, ...}` or `{v, v}` | `Set{1, 2, 3}` |
| RDN | Tuple | `(v, v, ...)` | `(1, "two", true)` |

#### DateTime Formats (spec section 4.3)

| Format | Example | Detection |
|--------|---------|-----------|
| Full ISO 8601 (24 chars) | `@2024-01-15T10:30:00.123Z` | Contains `T` and `.` |
| ISO without millis (20 chars) | `@2024-01-15T10:30:00Z` | Contains `T` but no `.` |
| Date only (10 chars) | `@2024-01-15` | No `T` after date |
| Unix timestamp (variable) | `@1705312200` | All digits; <=10 digits = seconds, >10 = milliseconds |

#### `@` Disambiguation (spec section 4.4-4.5)

After `@`, the parser distinguishes:
- `P` as 2nd char -> Duration
- Digit at pos 0, `:` at pos 2 -> TimeOnly
- Digit at pos 0, `-` at pos 4 -> DateTime
- All digits -> Unix timestamp

#### Brace Disambiguation (spec section 5)

| After first value | Separator | Result |
|-------------------|-----------|--------|
| Any | `:` | Object (first value must be string) |
| Any | `=>` | Map |
| Any | `,` | Set |
| Any | `}` | Set (single-element) |
| (nothing) | `}` immediately | Object (empty `{}`) |

#### Serialization Rules (spec section 6.2)

| Value | RDN Output |
|-------|-----------|
| `null` | `null` |
| `Boolean` | `true` / `false` |
| `Number` (finite) | Numeric literal |
| `Number` (NaN) | `NaN` |
| `Number` (+/-Infinity) | `Infinity` / `-Infinity` |
| `BigInt` | `42n` |
| `String` | `"escaped"` |
| `Date` (valid) | `@YYYY-MM-DDTHH:mm:ss.sssZ` (always 24-char format) |
| `Date` (invalid) | `null` |
| `RegExp` | `/pattern/flags` |
| `Binary` | `b"base64..."` (always base64, even if parsed from hex) |
| `Array` | `[...]` |
| `Object` | `{...}` |
| `Map` (non-empty) | `Map{k => v, ...}` (explicit prefix) |
| `Map` (empty) | `Map{}` |
| `Set` (non-empty) | `Set{v, ...}` (explicit prefix) |
| `Set` (empty) | `Set{}` |
| Non-serializable (undefined, function, symbol) | Omitted from objects; `null` in arrays |

#### Escape Sequences (spec section 7)

Identical to JSON (RFC 8259 section 7): `\"`, `\\`, `\/`, `\b`, `\t`, `\n`, `\f`, `\r`, `\uXXXX`. All control characters below U+0020 **must** be escaped.

#### Error Handling (spec section 12)

All parse errors throw `SyntaxError` with format: `<description> in RDN at position <offset>`.

#### Security Considerations (spec section 13)

- **RegExp/ReDoS**: Parsed patterns can cause catastrophic backtracking at execution time.
- **Prototype pollution**: Not vulnerable -- keys like `__proto__` are treated as ordinary properties.
- **Stack depth**: Protected by stack guard; throws `RangeError` for deeply nested structures.
- **Binary validation**: Strict RFC 4648 base64 and even-length hex.
- **Circular reference detection**: Throws `TypeError` (consistent with `JSON.stringify`).

### 2.3 Grammar Summary

Source: `spec/grammar.ebnf` (97 lines, ISO/IEC 14977 EBNF notation)

Key grammar productions:

```ebnf
rdn_text = ws, value, ws ;

value = null | boolean | number | bigint | string
      | array | tuple | object | map | set
      | datetime | time_only | duration | regexp | binary ;

bigint       = [ "-" ], digit, { digit }, "n" ;
datetime     = "@", datetime_body ;
time_only    = "@", hh, ":", mi, ":", ss, [ ".", mmm ] ;
duration     = "@", "P", duration_body ;
regexp       = "/", regexp_body, "/", regexp_flags ;
binary       = binary_b64 | binary_hex ;
binary_b64   = "b", '"', { base64_char }, [ "=" | "==" ], '"' ;
binary_hex   = "x", '"', { hex_digit }, '"' ;
map          = explicit_map | implicit_map ;
explicit_map = "Map{", ws, "}" | "Map{", ws, map_entry, ... ;
set          = explicit_set | implicit_set ;
explicit_set = "Set{", ws, "}" | "Set{", ws, value, ... ;
tuple        = "(", ws, ")" | "(", ws, value, ... ;
```

The grammar confirms that the parser is unambiguous once you know the first 1-4 characters of each value.

---

## 3. Reference Implementations

### 3.1 TypeScript Implementation

**Location**: `packages/rdn-js/` (package name: `@rdn/typescript`, version 0.1.0)

**Module structure** (6 source files in `packages/rdn-js/src/`):

| File | Lines | Purpose |
|------|-------|---------|
| `index.ts` | 5 | Re-exports: `parse`, `stringify`, types, helpers |
| `types.ts` | 57 | Type definitions: `RDNValue`, `RDNTimeOnly`, `RDNDuration`, `RDNReviver`, `RDNReplacer`, helper functions `timeOnly()`, `duration()` |
| `parser.ts` | 827 | Recursive-descent parser with 256-entry dispatch table |
| `serializer.ts` | 229 | Serializer with cycle detection and base64 encoding |
| `tables.ts` | 108 | Lookup tables: `TOKEN_TABLE` (256-entry dispatch), `B64_DECODE`, `B64_ENCODE`, `HEX_DECODE`, `DIGIT_PAIRS`, `ESCAPE_TABLE` |
| `conformance.test.ts` | 187 | Shared test-suite runner |

**Public API surface**:
```typescript
// Functions
parse(text: string, reviver?: RDNReviver): RDNValue
stringify(value: RDNValue, replacer?: RDNReplacer): string | undefined

// Helpers
timeOnly(hours, minutes, seconds, milliseconds?): RDNTimeOnly
duration(iso: string): RDNDuration

// Constants
MAX_BINARY_SIZE: number  // 100 MB default
```

**Type representations** (`types.ts:27-41`):
```typescript
type RDNValue =
  | null | boolean | number | bigint | string
  | Date | RegExp | Uint8Array
  | RDNTimeOnly     // { __type__: "TimeOnly", hours, minutes, seconds, milliseconds }
  | RDNDuration     // { __type__: "Duration", iso: string }
  | RDNValue[]
  | Map<RDNValue, RDNValue>
  | Set<RDNValue>
  | { [key: string]: RDNValue };
```

**Parser architecture** (`parser.ts`):
- Module-scoped cursor state: `source`, `pos`, `len`, `depth` (`parser.ts:7-9`)
- 256-entry `TOKEN_TABLE` dispatch for O(1) first-character branching (`tables.ts:30-60`)
- 23 token types defined as const enum (`tables.ts:2-27`)
- Main dispatch in `parseValue()` via switch statement (`parser.ts:709-743`)
- String parsing uses deferred materialization: fast scan for unescaped strings, slow `materializeString()` path for escaped content (`parser.ts:37-108`)
- Number parsing: accumulates integer digits, detects bigint suffix `n`, handles fraction/exponent (`parser.ts:112-185`)
- `@` disambiguation: checks for `P` (duration), `:` at pos+2 (time), `-` at pos+4 (datetime), else unix timestamp (`parser.ts:219-249`)
- Brace disambiguation in `parseBrace()`: parses first value, inspects separator (`:`, `=>`, `,`, `}`) (`parser.ts:513-561`)
- Max nesting depth: 128 (`parser.ts:11`)
- Max binary size: 100 MB (`parser.ts:12`)
- Reviver applied bottom-up via `applyReviver()`, handling Arrays, Maps, Sets, Objects, and tagged types (`parser.ts:748-798`)

**Stringifier architecture** (`serializer.ts`):
- Cycle detection via `WeakSet<object>` (`serializer.ts:5-13`)
- String escaping with fast-path scan + slow-path build (`serializer.ts:18-44`)
- Date formatting using pre-computed `DIGIT_PAIRS` table (`serializer.ts:48-54`)
- Base64 encoding with `B64_ENCODE` lookup (`serializer.ts:58-84`)
- Type dispatch via `typeof` checks then `instanceof` checks (`serializer.ts:98-215`)
- Serialization always uses explicit `Map{...}` and `Set{...}` prefixes (`serializer.ts:160-192`)
- Tagged types (`__type__: "TimeOnly"` / `"Duration"`) detected via `"__type__" in obj` (`serializer.ts:195-199`)
- Replacer applied at the start of each `stringifyValue()` call (`serializer.ts:100-103`)

### 3.2 Rust Implementation

**Location**: `packages/rdn-rust/` (crate name: `rdn`, version 0.1.0)

**Module structure** (4 source files):

| File | Lines | Purpose |
|------|-------|---------|
| `lib.rs` | 22 | Module declarations; re-exports `types::*`, `parser::parse`, `serializer::stringify` |
| `types.rs` | 353 | `RdnValue` enum, helper types, `Display` impl, tests |
| `parser.rs` | 33 | **Stub** -- returns `Err("Not implemented")` |
| `serializer.rs` | 27 | **Stub** -- `todo!("Not implemented")` |

**Type representations** (`types.rs:4-20`):
```rust
pub enum RdnValue {
    Null,
    Bool(bool),
    Number(f64),
    BigInt(BigInt),         // String-backed arbitrary precision
    String(String),
    Array(Vec<RdnValue>),
    Object(Vec<(String, RdnValue)>),  // Ordered key-value pairs
    Date(RdnDate),          // millis: f64
    TimeOnly(RdnTimeOnly),  // hours, minutes, seconds, milliseconds
    Duration(RdnDuration),  // iso: String
    RegExp(RdnRegExp),      // source, flags (validated)
    Binary(Vec<u8>),
    Map(Vec<(RdnValue, RdnValue)>),   // Ordered entries
    Set(Vec<RdnValue>),               // Ordered elements
}
```

Key design decisions:
- `BigInt` stored as `String` with validation on construction (`types.rs:24-49`)
- `Object` is `Vec<(String, RdnValue)>` preserving insertion order (not `HashMap`)
- `Map` is `Vec<(RdnValue, RdnValue)>` -- ordered, not hashed
- `Set` is `Vec<RdnValue>` -- ordered, not hashed
- `RdnTimeOnly` has validation on construction (`hours: 0-23`, `minutes: 0-59`, `seconds: 0-59`, `milliseconds: 0-999`) (`types.rs:67-94`)
- `RdnRegExp` validates flags (`d g i m s u v y`, no duplicates) (`types.rs:114-133`)
- `Display` trait implements serialization for primitive types (`types.rs:155-178`)
- Extensive unit tests for type validation (`types.ts:180-352`)

**Note**: The Rust parser and serializer are **not implemented** -- only type definitions are complete.

### 3.3 V8 Implementation Patterns

**Location**: `v8-integration/` (documentation and benchmarks only; actual code lives in `~/v8/v8/`)

**Key V8 source files** (referenced in `v8-integration/README.md:64-69`):
- `src/json/rdn-parser.h` / `rdn-parser.cc` -- Recursive-descent parser
- `src/json/rdn-stringifier.cc` -- Serializer with SWAR escape detection
- `src/runtime/runtime-rdn.cc` -- Runtime builtins (`RDN.parse`, `RDN.stringify`)
- `src/init/bootstrapper.cc` -- Installs the `RDN` global object

**C-level patterns relevant to Python C extension**:
1. **256-entry dispatch table**: O(1) character branching -- directly applicable to a C extension parser
2. **SWAR string escape detection**: Scan 4/8 bytes at a time using bitwise ops to detect chars needing escaping -- applicable to C extension stringifier
3. **Deferred string materialization**: Scan first, measure length, then allocate and copy -- reduces Python object allocations
4. **Pre-computed digit pair table**: 100-entry table for 2-digit formatting -- trivially portable to C
5. **Template on char width**: Parser templated on UTF-8 vs UTF-16 -- for Python, UTF-8 is sufficient
6. **LRU map cache for repeated shapes**: When parsing arrays of same-shape objects, cache the dict key layout -- advanced optimization for later

**Benchmark fixtures** (`v8-integration/benchmarks/parse-bench.js`):
- Simple object, with Date, with BigInt, with collections (Set/Map), complex nested document
- These fixtures should be replicated for Python benchmarks

---

## 4. Conformance Test Suite

**Location**: `test-suite/` with `README.md` documenting the convention.

### Test Structure and Categories

| Category | Directory | File Pattern | Count |
|----------|-----------|-------------|-------|
| Valid parse tests | `test-suite/valid/` | `*.rdn` + `*.expected.json` | 11 pairs |
| Invalid parse tests | `test-suite/invalid/` | `*.rdn` | 10 files |
| Roundtrip tests | `test-suite/roundtrip/` | `*.rdn` | 2 files |

### Valid Tests (11 pairs)

| Test Name | Types Covered |
|-----------|---------------|
| `primitives` | null, boolean, integer, float, string |
| `special-numbers` | NaN, Infinity, -Infinity |
| `bigint` | 0n, positive, negative, large |
| `datetime` | Full ISO, no-ms ISO, date-only |
| `time-and-duration` | TimeOnly (with/without ms), Duration (full, short) |
| `regexp` | Simple pattern with flags, anchored pattern |
| `binary` | Base64, hex, empty binary |
| `map` | Explicit Map with string keys |
| `set` | Explicit Set with integers |
| `tuple` | Mixed-type tuple (parses to array) |
| `nested` | Object with BigInt, Date, implicit Set, Map with Date keys and Duration values |

### Invalid Tests (10 files)

| Test Name | Error Condition |
|-----------|-----------------|
| `bigint-decimal` | `3.14n` -- BigInt with decimal point |
| `bigint-exponent` | `1e10n` -- BigInt with exponent |
| `invalid-binary` | `b"not base64!!"` -- Invalid base64 characters |
| `invalid-date` | `@not-a-date` -- Invalid @ literal |
| `invalid-hex` | `x"GHIJKL"` -- Invalid hex characters |
| `invalid-regexp` | `/unclosed` -- Unterminated regex |
| `single-quotes` | `{'key': 'value'}` -- Single quotes not allowed |
| `trailing-comma` | `{"a": 1,}` -- Trailing commas not allowed |
| `unclosed-map` | `Map{"a" => 1` -- Missing closing brace |
| `unquoted-key` | `{key: "value"}` -- Unquoted object keys |

### Roundtrip Tests (2 files)

| Test Name | Content |
|-----------|---------|
| `all-types` | All 14 RDN types in a single object |
| `empty-containers` | Empty array, object, Map, Set, tuple |

### `$type` Tagged Convention (from `test-suite/README.md`)

Since JSON cannot represent RDN extended types, expected outputs use:

```json
{"$type": "TypeName", "value": ...}
```

| RDN Type | `$type` | `value` Format |
|----------|---------|----------------|
| Date | `"Date"` | ISO 8601 string (e.g., `"2024-01-15T10:30:00.123Z"`) |
| BigInt | `"BigInt"` | String of digits (e.g., `"42"`) |
| RegExp | `"RegExp"` | `{"source": "...", "flags": "..."}` |
| Binary | `"Binary"` | Base64 string |
| Map | `"Map"` | Array of `[key, value]` pairs |
| Set | `"Set"` | Array of values |
| NaN | `"Number"` | `"NaN"` |
| Infinity | `"Number"` | `"Infinity"` or `"-Infinity"` |
| TimeOnly | `"TimeOnly"` | `{"hours", "minutes", "seconds", "milliseconds"}` |
| Duration | `"Duration"` | ISO 8601 duration string |

### How the TypeScript Runner Consumes Tests (`conformance.test.ts`)

The TS conformance test runner:
1. Reads each `*.rdn` file from `valid/`, parses with `parse()`
2. Normalizes the parsed result to `$type`-tagged JSON using `normalizeForComparison()`
3. Reads the corresponding `*.expected.json`, parses as regular JSON
4. Deep-compares the normalized result with the expected JSON
5. For invalid tests: asserts that `parse()` throws
6. For roundtrip tests: parse -> stringify -> parse, normalize both, deep-compare

### How a Python Runner Should Consume Them

The Python conformance runner should:
1. Read `.rdn` files and parse with `rdn.loads()`
2. Implement a `normalize_for_comparison(value)` function that converts native Python types to `$type`-tagged dicts
3. Read `.expected.json` files and parse with `json.loads()`
4. Compare using a custom deep-equal function (since Python dicts maintain insertion order in 3.7+)
5. For invalid tests: assert that `rdn.loads()` raises `rdn.RDNDecodeError`
6. For roundtrip tests: loads -> dumps -> loads, normalize both, deep-compare

---

## 5. Python `json` Module API Reference

The `json` module is the standard interface that `rdn-python` should mirror.

### Functions

#### `json.loads(s, *, cls=None, object_hook=None, parse_float=None, parse_int=None, parse_constant=None, object_pairs_hook=None, **kw)`

Deserialize a string to a Python object.

| Parameter | Type | Purpose |
|-----------|------|---------|
| `s` | `str` / `bytes` / `bytearray` | Input to parse |
| `cls` | `type` | Custom `JSONDecoder` subclass |
| `object_hook` | `callable` | Called with every decoded object (dict); return value replaces the dict |
| `parse_float` | `callable` | Called with string of every float; default `float()` |
| `parse_int` | `callable` | Called with string of every int; default `int()` |
| `parse_constant` | `callable` | Called with string `-Infinity`, `Infinity`, `NaN` |
| `object_pairs_hook` | `callable` | Called with ordered list of `(key, value)` pairs; takes priority over `object_hook` |

**RDN equivalents needed**: `object_hook` maps to reviver for objects. Additional hooks needed for RDN-specific types: `parse_bigint`, `parse_datetime`, `parse_regexp`, `parse_binary`, `parse_timeonly`, `parse_duration`.

#### `json.dumps(obj, *, skipkeys=False, ensure_ascii=True, check_circular=True, allow_nan=True, cls=None, indent=None, separators=None, default=None, sort_keys=False, **kw)`

Serialize a Python object to a string.

| Parameter | Type | Default | Purpose |
|-----------|------|---------|---------|
| `obj` | `any` | -- | Value to serialize |
| `skipkeys` | `bool` | `False` | Skip non-string dict keys without raising |
| `ensure_ascii` | `bool` | `True` | Escape non-ASCII characters |
| `check_circular` | `bool` | `True` | Enable cycle detection |
| `allow_nan` | `bool` | `True` | Allow NaN/Infinity (always true for RDN) |
| `cls` | `type` | -- | Custom `JSONEncoder` subclass |
| `indent` | `int/str/None` | `None` | Pretty-print indentation |
| `separators` | `tuple` | -- | `(item_sep, key_sep)` override |
| `default` | `callable` | -- | Called for non-serializable objects; return serializable replacement |
| `sort_keys` | `bool` | `False` | Sort dict keys alphabetically |

**RDN-specific considerations**: `allow_nan` is always true for RDN (NaN/Infinity are first-class). `indent` is not supported by the RDN spec (no `space` parameter in `RDN.stringify`), but is a nice-to-have for pretty-printing. `default` maps to the replacer pattern.

#### `json.load(fp, *, cls=None, object_hook=None, parse_float=None, parse_int=None, parse_constant=None, object_pairs_hook=None, **kw)`

Like `loads()` but reads from a file-like object (`.read()`).

#### `json.dump(obj, fp, *, skipkeys=False, ensure_ascii=True, check_circular=True, allow_nan=True, cls=None, indent=None, separators=None, default=None, sort_keys=False, **kw)`

Like `dumps()` but writes to a file-like object (`.write()`).

### Classes

#### `json.JSONDecoder`

```python
class JSONDecoder:
    def __init__(self, *, object_hook=None, parse_float=None,
                 parse_int=None, parse_constant=None,
                 strict=True, object_pairs_hook=None):
        ...
    def decode(self, s: str) -> Any: ...
    def raw_decode(self, s: str, idx: int = 0) -> tuple[Any, int]: ...
```

**RDN equivalent**: `RDNDecoder` with additional hooks for RDN-specific types.

#### `json.JSONEncoder`

```python
class JSONEncoder:
    def __init__(self, *, skipkeys=False, ensure_ascii=True,
                 check_circular=True, allow_nan=True,
                 sort_keys=False, indent=None, separators=None,
                 default=None):
        ...
    def encode(self, o: Any) -> str: ...
    def iterencode(self, o: Any) -> Iterator[str]: ...
    def default(self, o: Any) -> Any: ...  # Override for custom types
```

**RDN equivalent**: `RDNEncoder` with `default()` for custom type serialization. `iterencode()` is useful for streaming large outputs.

### Exception

```python
class json.JSONDecodeError(ValueError):
    msg: str
    doc: str
    pos: int
    lineno: int
    colno: int
```

**RDN equivalent**: `rdn.RDNDecodeError(ValueError)` with `msg`, `doc`, `pos` (byte offset, matching the spec's error format).

---

## 6. Python Type Mapping

| RDN Type | Python Type | Notes |
|----------|-------------|-------|
| `null` | `None` | Direct mapping |
| `true` / `false` | `bool` | Direct mapping |
| Number (integer) | `int` | Python int is arbitrary precision but RDN integers are JSON-compatible (float64 range) |
| Number (float) | `float` | Direct mapping |
| `NaN` | `float('nan')` | `math.isnan()` for detection |
| `Infinity` | `float('inf')` | `math.isinf()` for detection |
| `-Infinity` | `float('-inf')` | `math.isinf()` for detection |
| BigInt | `int` | Python `int` is already arbitrary precision. Needs a wrapper or marker to distinguish from regular int during serialization. Options: (a) use a sentinel subclass `class RDNBigInt(int)`, (b) serialize all `int` as bigint when value exceeds float64 safe range, (c) provide explicit `bigint()` helper |
| String | `str` | Direct mapping |
| Array | `list` | Direct mapping |
| Object | `dict` | Python 3.7+ dicts preserve insertion order |
| DateTime | `datetime.datetime` (UTC, tzinfo=timezone.utc) | Always UTC per spec. Parsing date-only produces midnight UTC. |
| TimeOnly | `datetime.time` or custom `RDNTimeOnly` dataclass | `datetime.time` has hours/minutes/seconds/microseconds (not milliseconds). Could use `time(h, m, s, ms * 1000)` or custom class. |
| Duration | `datetime.timedelta` or custom `RDNDuration` dataclass | `timedelta` cannot represent years/months (variable length). ISO 8601 durations with Y/M components need a custom class. `timedelta` works for D/H/M/S only. |
| RegExp | `re.Pattern` (compiled regex) | `re.compile(pattern, flags)`. JS flags `d g i m s u v y` need mapping to Python `re` flags. Not all JS flags exist in Python (`d`, `g`, `v`, `y` have no equivalent). |
| Binary (base64) | `bytes` | `base64.b64decode()` for parsing, `base64.b64encode()` for serialization |
| Binary (hex) | `bytes` | `bytes.fromhex()` for parsing |
| Map | Custom `RDNMap` (ordered, any-key) or `dict` | Python `dict` only supports hashable keys. RDN Maps can have any-type keys (including lists, dicts). Need a custom ordered map type. |
| Set | `frozenset` or custom `RDNSet` | Python `set` only supports hashable elements. RDN Sets can contain lists, dicts, etc. Need a custom type or `frozenset` (immutable). |
| Tuple | `tuple` | Natural mapping since tuples are immutable in Python |

### Challenges

1. **BigInt vs int ambiguity**: Python `int` is already arbitrary precision, so `42` and `42n` both map to `int`. Need a way to distinguish for serialization. Best approach: serialize as `42n` only when the value exceeds JavaScript's `Number.MAX_SAFE_INTEGER` (2^53-1) or when explicitly wrapped. Provide `rdn.bigint(42)` helper.

2. **Map with non-hashable keys**: Python `dict` requires hashable keys. RDN Maps can have list/dict/set keys. Options: (a) custom `RDNMap` class backed by a list of pairs, (b) raise an error for non-hashable keys.

3. **Set with non-hashable elements**: Same problem. `frozenset` requires hashable elements.

4. **Duration with years/months**: `datetime.timedelta` cannot represent ISO 8601 durations with Y or M components. Need a custom `RDNDuration` dataclass that stores the raw ISO string (matching the TS/Rust approach).

5. **RegExp flag mapping**: JS flags `d` (indices), `g` (global), `v` (unicodeSets), `y` (sticky) have no Python equivalents. Options: (a) store the pattern+flags as strings, compile lazily, (b) provide `RDNRegExp` wrapper that preserves original flags and exposes a `compiled` property with mapped flags.

| JS Flag | Python Equivalent | Notes |
|---------|-------------------|-------|
| `i` | `re.IGNORECASE` | Direct |
| `m` | `re.MULTILINE` | Direct |
| `s` | `re.DOTALL` | Direct |
| `u` | No direct equivalent | Python 3 strings are already Unicode |
| `d` | No equivalent | JS-specific "hasIndices" |
| `g` | No equivalent | JS-specific "global" |
| `v` | No equivalent | JS-specific "unicodeSets" |
| `y` | No equivalent | JS-specific "sticky" |

---

## 7. C Extension Architecture Patterns

### How Existing Python JSON Libraries Handle C/Python Fallback

**`orjson`** (Rust-backed via PyO3/maturin):
- Written entirely in Rust using `pyo3` bindings
- No pure Python fallback -- wheels are prebuilt for common platforms
- Build system: `maturin` (Rust-to-Python bridge)
- Extremely fast -- 2-10x faster than `json`
- Relevant pattern: Use Rust (already have Rust types defined) with `maturin` to build Python extension

**`ujson`** (C extension via setuptools):
- C source in `lib/` directory
- `setup.py` with `ext_modules = [Extension("ujson", ...)]`
- No pure Python fallback -- compilation required
- Uses CPython C API directly (`PyObject*`, `PyDict_SetItem`, etc.)

**`simplejson`** (C extension with pure Python fallback):
- Most relevant pattern for our needs
- Structure:
  ```
  simplejson/
    __init__.py          # Public API, tries C import
    decoder.py           # Pure Python decoder
    encoder.py           # Pure Python encoder
    _speedups.c          # Optional C extension
  ```
- `__init__.py` does:
  ```python
  try:
      from simplejson._speedups import ...
  except ImportError:
      pass  # Fall back to pure Python
  ```
- C extension provides `scanstring()` and `encode_basestring_ascii()` -- the hot paths
- Rest of the logic stays in Python

**`python-rapidjson`** (C++ extension via pybind11):
- Wraps RapidJSON C++ library
- Build with `pybind11` and `setuptools`
- No pure Python fallback

### Recommended Approach for `rdn-python`

**Phase 1**: Pure Python implementation (correctness first)
- `rdn/` package with `parser.py`, `serializer.py`, `types.py`
- All logic in Python, thoroughly tested against conformance suite

**Phase 2**: C extension for hot paths (performance)
- Option A: **Rust + maturin** (leverage existing `rdn-rust` types)
  - Complete the Rust parser/serializer
  - Add `pyo3` bindings to expose `parse()` and `stringify()` to Python
  - Build with `maturin develop` / `maturin build`
  - Pro: Reuses Rust code, memory-safe, excellent performance
  - Con: Requires Rust toolchain for building from source

- Option B: **C extension with CPython API** (like `simplejson`)
  - Write C parser/serializer using CPython API
  - Port the dispatch-table pattern from V8
  - Pro: Minimal dependencies, smaller binary
  - Con: Memory safety concerns, more code to maintain

- Option C: **cffi** (C Foreign Function Interface)
  - Write core parser in C, use cffi to bridge
  - Pro: Works with PyPy
  - Con: Extra layer of indirection

**Recommended**: Option A (Rust + maturin) for the C extension, with pure Python as fallback.

### Build System Considerations

For a package with optional C extension and pure Python fallback:

```toml
# pyproject.toml
[build-system]
requires = ["maturin>=1.0,<2.0"]
build-backend = "maturin"

[tool.maturin]
features = ["pyo3/extension-module"]
```

The package structure would be:
```
packages/rdn-python/
  pyproject.toml
  src/
    rdn/
      __init__.py          # Public API, tries native import
      _native.pyd/.so      # Built by maturin (optional)
      _parser.py           # Pure Python parser
      _serializer.py       # Pure Python serializer
      types.py             # RDN type definitions
      exceptions.py        # RDNDecodeError
  rust/
    src/lib.rs             # PyO3 bindings (optional)
    Cargo.toml
  tests/
    test_parse.py
    test_stringify.py
    test_conformance.py
```

---

## 8. Pydantic Integration Patterns

### Pydantic v2 Custom Type System

Pydantic v2 (pydantic >= 2.0) uses `Annotated` types and custom validators/serializers.

#### Custom Type with `__get_pydantic_core_schema__`

```python
from pydantic import GetCoreSchemaHandler
from pydantic_core import core_schema

class RDNDateTime:
    @classmethod
    def __get_pydantic_core_schema__(cls, source_type, handler: GetCoreSchemaHandler):
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize),
        )

    @classmethod
    def _validate(cls, value):
        if isinstance(value, datetime):
            return value
        if isinstance(value, str) and value.startswith("@"):
            return rdn_parse_datetime(value)
        raise ValueError("Invalid RDN DateTime")

    @classmethod
    def _serialize(cls, value):
        return f"@{value.isoformat()}Z"
```

#### Custom Serializers/Validators via `Annotated`

```python
from pydantic import field_validator, field_serializer
from typing import Annotated

RDNBigInt = Annotated[int, AfterValidator(lambda v: v)]  # marker
```

### How Other Libraries Integrate with Pydantic

**`pydantic-extra-types`**: Provides types like `Color`, `PhoneNumber`, `PaymentCardNumber`. Each type implements `__get_pydantic_core_schema__`.

**`pydantic-settings`**: Custom sources for loading model fields from env, files, etc.

**Pattern for `rdn-pydantic`**:
1. Export annotated types: `RDNBigInt`, `RDNDateTime`, `RDNTimeOnly`, `RDNDuration`, `RDNRegExp`, `RDNBinary`, `RDNMap`, `RDNSet`, `RDNTuple`
2. Provide a `model_dump_rdn()` method (similar to `model_dump_json()`)
3. Provide a `model_validate_rdn()` class method
4. Custom `RDNSerializer` for model-level RDN serialization

---

## 9. FastAPI Integration Patterns

### Custom Request/Response Classes

FastAPI allows custom request parsing and response serialization via `Request` and `Response` subclasses.

#### Custom Response Class

```python
from fastapi.responses import Response

class RDNResponse(Response):
    media_type = "application/x-rdn"

    def render(self, content: Any) -> bytes:
        return rdn.dumps(content).encode("utf-8")
```

#### Custom Request Parsing

```python
from fastapi import Request

async def parse_rdn_body(request: Request):
    body = await request.body()
    return rdn.loads(body.decode("utf-8"))
```

#### Content-Type Negotiation

```python
from fastapi import APIRouter

router = APIRouter()

@router.post("/data", response_class=RDNResponse)
async def create_data(data: MyModel = Depends(parse_rdn_body)):
    return data
```

### How Other Libraries Integrate with FastAPI

**`msgpack-asgi`**: ASGI middleware that transparently converts MessagePack request/response bodies.

**`fastapi-orjson`**: Provides `ORJSONResponse` as a drop-in replacement for `JSONResponse`.

**Pattern for `rdn-fastapi`**:
1. `RDNResponse` -- custom response class (media type `application/x-rdn`)
2. `RDNRoute` -- custom `APIRoute` that auto-parses RDN request bodies when `Content-Type: application/x-rdn`
3. `RDNMiddleware` -- optional ASGI middleware for transparent RDN negotiation
4. Depends on `rdn-python` for parsing/serialization and optionally `rdn-pydantic` for model integration

---

## 10. Blast Radius

### Existing Code/Tests Affected

| Area | Impact | Details |
|------|--------|---------|
| `packages/rdn-python/` | **Major** -- full rewrite | Replace placeholder with complete implementation |
| `packages/rdn-python/pyproject.toml` | **Major** -- rewrite | New build system, dependencies, entry points |
| `pnpm-workspace.yaml` | **None** | Python packages are not managed by pnpm |
| `test-suite/` | **None** | Consumed read-only; no changes needed |
| `spec/` | **None** | Read-only reference |
| `packages/rdn-rust/` | **Possible** | If using Rust + maturin for C extension, may need to complete the Rust parser/serializer and add PyO3 bindings |

### New Files to Create

| Path | Purpose |
|------|---------|
| `packages/rdn-python/src/rdn/` | Core Python package |
| `packages/rdn-python/tests/` | Tests including conformance suite runner |
| `packages/rdn-pydantic/` | New package -- Pydantic integration |
| `packages/rdn-fastapi/` | New package -- FastAPI integration |

### CI/CD Changes Needed

| File | Change |
|------|--------|
| `.github/workflows/ci.yml` | Add `python` job: setup Python, `pip install -e`, `pytest` |
| `.github/workflows/ci.yml` | Optionally add jobs for `rdn-pydantic` and `rdn-fastapi` |
| `.github/workflows/release.yml` | Add PyPI publish step (e.g., `twine upload` or `maturin publish`) |

Currently, CI has `js` and `dotnet` jobs. The Rust job is commented out. A `python` job needs to be added.

### Documentation Updates Needed

| File | Change |
|------|--------|
| `CLAUDE.md` | Update build/test commands for Python packages |
| `packages/rdn-python/README.md` | Full rewrite with API documentation, examples, installation |
| `README.md` (repo root) | Add Python to the list of implementations |
| `docs/` | This discovery document + tech design + implementation docs |

---

## 11. Edge Cases & Risks

### Unicode Handling Differences

- Python 3 strings are Unicode by default (like JS), so UTF-8/UTF-16 distinction is less relevant
- `\uXXXX` escape handling is straightforward in Python
- Surrogate pairs (`\uD800-\uDFFF`) need care -- Python uses UCS-4 internally, so astral characters are single code points, not surrogate pairs
- The `ensure_ascii` parameter in `json.dumps` controls whether non-ASCII chars are escaped -- RDN should support this too

### BigInt Representation

- Python `int` is already arbitrary precision, so no separate BigInt type is needed for storage
- **Serialization ambiguity**: `42` (JSON number) vs `42n` (BigInt) -- need a policy:
  - Option A: All `int` serialize as BigInt -- breaks JSON compatibility
  - Option B: Only `int` outside safe float64 range serialize as BigInt -- most practical
  - Option C: Require explicit wrapping with `rdn.bigint(42)` -- most explicit
  - **Recommendation**: Option B by default, with `rdn.bigint()` wrapper for explicit control

### Binary Data Handling

- Python `bytes` is the natural type for binary data
- Base64 encoding/decoding: use `base64.b64encode()` / `base64.b64decode()`
- Hex encoding/decoding: use `bytes.fromhex()` / `bytes.hex()`
- The spec requires strict RFC 4648 validation -- Python's `base64.b64decode()` with `validate=True` handles this

### Circular Reference Detection

- Python's `json` module uses `check_circular=True` by default
- Implementation: maintain a set of `id()` values during serialization
- Must handle all container types: dict, list, Map, Set

### Performance Considerations for Pure Python Fallback

- Pure Python parser will be 10-50x slower than C/Rust extension
- String scanning is the main bottleneck -- character-by-character iteration in Python is slow
- Base64 decode in pure Python is slow -- use `base64` module (C-backed)
- Regex compilation during parsing adds overhead
- **Mitigation**: The pure Python version is for correctness and portability; the C extension is for production use

### Date/Time Edge Cases

- Unix timestamp detection: <=10 digits = seconds, >10 digits = milliseconds (spec section 4.3)
- `datetime.datetime` in Python requires explicit timezone -- always use `timezone.utc`
- Date-only (`@2024-01-15`) parses to midnight UTC
- Invalid dates: The spec says invalid `Date` serializes to `null`. Python `datetime` raises `ValueError` for invalid dates during construction.

### Nesting Depth

- TypeScript implementation limits to 128 levels (`MAX_DEPTH = 128`)
- Python has a default recursion limit of 1000 (`sys.getrecursionlimit()`)
- Should implement explicit depth tracking (like TS) rather than relying on Python's recursion limit

### Map Key Ordering

- RDN Maps preserve insertion order
- Python `dict` preserves insertion order (3.7+)
- For Maps with non-hashable keys, need a list-of-tuples or custom ordered container

---

## 12. Resolved Decisions

| # | Question | Decision |
|---|----------|----------|
| 1 | **BigInt serialization policy** | **Auto-promote large ints.** Serialize `int` as JSON number normally; auto-promote to BigInt (`42n`) when value exceeds `Number.MAX_SAFE_INTEGER` (2^53-1). Provide `rdn.bigint()` wrapper for explicit control. |
| 2 | **Map/Set with non-hashable elements** | **Use native `dict`/`set`, error on unhashable.** Use Python's native `dict` for Maps and `set`/`frozenset` for Sets. Raise error if non-hashable keys/elements are encountered. |
| 3 | **RegExp flag handling** | **Just `re.Pattern`, drop JS-only flags.** Parse into `re.Pattern` using only compatible flags (`i`→`IGNORECASE`, `m`→`MULTILINE`, `s`→`DOTALL`). JS-only flags (`d`, `g`, `v`, `y`) silently dropped. |
| 4 | **Pretty-printing / indent** | **Yes, support indent.** `rdn.dumps(indent=2)` supported for developer convenience. Matches `json.dumps()` API. |
| 5 | **Package naming** | **`rdn`** on PyPI. Short and clean. |
| 6 | **Build system for C extension** | **Rust + maturin.** Leverage existing Rust type definitions with PyO3 bindings. Pure Python fallback included. |
| 7 | **Minimum Python version** | **Python 3.10+.** Enables `match` statements, `TypeAlias`, `ParamSpec`. |
| 8 | **`rdn-pydantic` scope** | **Types + model methods.** Custom Pydantic types plus `model_dump_rdn()` / `model_validate_rdn()` for full model-level serialization. |
| 9 | **`rdn-fastapi` scope** | **Response + Route + Middleware.** `RDNResponse`, `RDNRoute` for auto-parsing, and optional ASGI middleware for content-type negotiation. |
| 10 | **Monorepo placement** | **Separate `packages/` dirs.** `packages/rdn-python/`, `packages/rdn-pydantic/`, `packages/rdn-fastapi/` — matches existing monorepo convention. |
| 11 | **`bytes` vs `str` input** | **Accept both `str` and `bytes`.** Also `bytearray`. Decode bytes as UTF-8 before parsing. Matches `json.loads()`. |
| 12 | **File I/O (load/dump)** | **Include from start.** `rdn.load(fp)` and `rdn.dump(obj, fp)` for file-like objects. Completes `json` module API parity. |
