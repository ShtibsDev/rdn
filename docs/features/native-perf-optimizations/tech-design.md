# Tech Design: native-perf-optimizations

## 1. Overview

This document details the technical design for applying orjson-inspired performance optimizations to `packages/rdn-native/`, the Rust+PyO3 native extension that accelerates RDN parsing and serialization for Python. The work is organized into three tiers -- build and low-hanging fruit, type dispatch and caching, and SIMD/buffer optimizations -- each measured independently. The goal is at least 30% throughput improvement on medium/large payloads while maintaining 100% conformance test pass rate, full API compatibility, and output parity with the pure-Python implementation.

## 2. Current State Summary

The discovery document (`discovery.md`) identified the following key bottlenecks in the current implementation:

- **Build configuration**: No `[profile.release]` section -- default `codegen-units = 16`, no LTO, no `panic = "abort"`.
- **Number formatting**: Integers use `i64.to_string()` (heap alloc). Floats call Python's `repr()` through the GIL on every value.
- **Type dispatch**: The serializer uses a mix of `is_instance_of`, `downcast`, `is_instance()` (Python-level isinstance), and string comparison (`"bytearray"`). Extended types (datetime, time, timedelta, Pattern) all go through Python isinstance.
- **String scanning**: Byte-by-byte loops in both parser (finding `"` and `\`) and serializer (detecting chars needing escaping). No SIMD.
- **Output assembly**: The serializer builds intermediate `String` objects for every child value, then concatenates them. No direct-to-buffer writing.
- **Key handling**: `parse_string_as_rust()` creates a `PyString`, immediately converts to Rust `String`, then uses it as a dict key. No key caching across calls.
- **Formatting**: All datetime/duration/regex formatting uses `format!()` which heap-allocates per call.

See [discovery.md](./discovery.md) for full line-by-line analysis.

## 3. To-Be Behavior

From the user's perspective:

- **API**: No changes. `rdn.parse(text)` and `rdn.stringify(obj, ...)` signatures remain identical.
- **Performance**: Significant throughput improvements on all payload sizes, with medium/large payloads seeing the largest gains.
- **Float formatting**: Minor differences vs Python `repr()` are possible. Both `ryu` and Python `repr()` produce shortest round-trip representations, but the exact digit sequences may differ (e.g., `0.1` vs `0.1` -- typically identical, but edge cases like `1e20` vs `1e+20` exist). All representations are mathematically equivalent.
- **Error messages**: No changes. `RDNDecodeError` with `msg`, `doc`, `pos`, `lineno`, `colno` attributes.
- **Hot-path routing**: Unchanged. Native handles no-hooks/no-cls paths; hooks fall through to pure Python.

## 4. Design Decisions

| # | Decision | Chosen Approach | Rationale |
|---|----------|----------------|-----------|
| 1 | Build profile | `codegen-units = 1`, `lto = "fat"`, `panic = "abort"`, `opt-level = 3` | Matches orjson. All `unwrap()` calls audited as safe. LTO increases compile time but is one-time for release. |
| 2 | Integer formatting | `itoa` crate, direct-to-buffer | Zero-alloc, ~3-5x faster than `i64.to_string()`. Widely used (serde_json, orjson). |
| 3 | Float formatting | `ryu` crate, direct-to-buffer | Eliminates Python `repr()` GIL call per float. Accept minor formatting differences. |
| 4 | Hot/cold path separation | `#[cold]` + `#[inline(never)]` on rare serializer types | Keeps common-type code in the instruction cache. datetime, regex, binary are rare paths. |
| 5 | Empty collection fast-paths | Early return before container logic on `[]`, `{}`, `()` | Avoids `Vec` allocation and container formatting for empty collections. Already partially done for `[]`/`{}`/`()` in parser; add to serializer. |
| 6 | Cached type pointers | Cache `ob_type` raw pointers for all Python types at module init | Pointer comparison is O(1) vs isinstance which crosses the Python/C boundary. Fall through to isinstance for subclass correctness. |
| 7 | Dictionary key cache | Module-level direct-mapped cache, 2048 entries, round-robin eviction | Reuses `PyString` objects across `parse()` calls. GIL protects access. Round-robin is simpler than LRU and matches orjson. |
| 8 | Bit-packed serializer state | Pack `ensure_ascii`, `check_circular`, `sort_keys`, and `depth` into a single `u32` | Fewer struct fields, fits in a register, faster branching. |
| 9 | SIMD string scanning | SSE2 (x86_64) + NEON (aarch64) + scalar fallback | Full coverage from day one. SSE2 is baseline on x86_64; NEON is baseline on Apple Silicon. Scalar fallback for other architectures. |
| 10 | Output buffer | Write to `Vec<u8>` buffer, final `PyUnicode_FromStringAndSize` | Eliminates intermediate `String` allocations in the serializer. Single copy at the end. |
| 11 | Stack buffers for formatting | `write!()` to `[u8; 64]` stack buffer instead of `format!()` | Eliminates heap allocation for datetime, timeonly, duration, regexp formatting. |
| 12 | Tier ordering | Tier 1 -> measure -> Tier 2 -> measure -> Tier 3 -> measure | Captures per-tier improvement. Earlier tiers have lower risk and deliver value independently. |

## 5. Architecture & Interfaces

### 5.1 New Module Structure

```
packages/rdn-native/src/
  lib.rs          # Module init: add TypeCache initialization, key cache static
  parser.rs       # Tier 2: key cache integration in parse_string()/finish_object()
                  # Tier 3: SIMD integration in parse_string() scanning loop
  serializer.rs   # Tier 1: itoa/ryu formatting, hot/cold separation
                  # Tier 2: cached type pointers, bit-packed state
                  # Tier 3: WriteBuffer integration (major refactor)
  tables.rs       # Unchanged (escape tables, token table, base64/hex tables)
  error.rs        # Unchanged
  simd.rs         # NEW — Tier 3: SIMD string scanning and escape detection
  cache.rs        # NEW — Tier 2: KeyCache struct and TypeCache struct
  buffer.rs       # NEW — Tier 3: WriteBuffer struct for direct-to-buffer serialization
```

### 5.2 Key Data Structures

**TypeCache** (Tier 2, `cache.rs`):

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

Thread-safety: The GIL protects all access in CPython. The `TypeCache` is initialized under the GIL in `_native` module init and stored in a `static`. All reads happen while the GIL is held (PyO3 enforces this via `Python<'py>`).

**KeyCache** (Tier 2, `cache.rs`):

```rust
/// Direct-mapped hash cache for dictionary keys during parsing.
/// Stores PyString objects keyed by their raw bytes, enabling
/// reuse of the same PyString across parse() calls for repeated keys.
struct KeyCache {
    entries: Box<[KeyCacheEntry; 2048]>,
    /// Number of entries currently occupied (for diagnostics only)
    count: usize,
}

struct KeyCacheEntry {
    /// Hash of the key bytes (xxh3 64-bit)
    hash: u64,
    /// The cached PyString object (Py_INCREF'd)
    value: Option<PyObject>,
    /// Raw key bytes for collision detection
    key_bytes: SmallVec<[u8; 32]>,
}
```

Eviction: Round-robin replacement -- on collision, the existing entry is overwritten (its `PyObject` is `Py_DECREF`'d). This is simple and matches orjson's approach.

Storage: Module-level `static Mutex<Option<KeyCache>>`, initialized lazily on first `parse()` call. The `Mutex` is only locked briefly to swap the cache in/out; all actual cache operations happen on a local `KeyCache` reference while the GIL is held.

**WriteBuffer** (Tier 3, `buffer.rs`):

```rust
/// Byte buffer for serializer output. Accumulates UTF-8 bytes
/// and converts to a Python unicode string at the end.
struct WriteBuffer {
    buf: Vec<u8>,
}

impl WriteBuffer {
    fn with_capacity(cap: usize) -> Self;
    fn write_byte(&mut self, b: u8);
    fn write_bytes(&mut self, bytes: &[u8]);
    fn write_str(&mut self, s: &str);
    /// Write a u8 value formatted as decimal (using itoa)
    fn write_u32(&mut self, v: u32);
    /// Write an i64 value formatted as decimal (using itoa)
    fn write_i64(&mut self, v: i64);
    /// Write an f64 value formatted with ryu
    fn write_f64(&mut self, v: f64);
    /// Consume the buffer and create a PyString via PyUnicode_FromStringAndSize
    fn into_py_string(self, py: Python<'_>) -> PyResult<PyObject>;
}
```

**SIMD string scanner** (Tier 3, `simd.rs`):

```rust
/// Scan forward from `start` looking for `"` or `\` in the byte slice.
/// Returns (end_pos, has_escape) where end_pos is the position of the
/// closing `"` and has_escape indicates whether any `\` was found.
/// If no closing `"` is found, returns (bytes.len(), _).
fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool);

/// Scan a byte slice and return the position of the first byte that
/// needs escaping (< 0x20, `"`, `\`, or > 0x7F if ensure_ascii).
/// Returns None if no bytes need escaping.
fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize>;
```

**Bit-packed serializer state** (Tier 2):

```rust
/// Bit layout of the state u32:
///   bits  0..6  : depth (0-128, 7 bits)
///   bit   7     : ensure_ascii
///   bit   8     : check_circular
///   bit   9     : sort_keys
///   bits 10..31 : reserved
const STATE_DEPTH_MASK: u32    = 0b0000_0000_0000_0000_0000_0000_0111_1111;
const STATE_ASCII_BIT: u32     = 0b0000_0000_0000_0000_0000_0000_1000_0000;
const STATE_CIRCULAR_BIT: u32  = 0b0000_0000_0000_0000_0000_0001_0000_0000;
const STATE_SORT_BIT: u32      = 0b0000_0000_0000_0000_0000_0010_0000_0000;
```

### 5.3 New Dependencies

| Crate | Version | Purpose | Size Impact |
|-------|---------|---------|-------------|
| `itoa` | `1` | Zero-alloc integer-to-string formatting | ~15KB, no transitive deps |
| `ryu` | `1` | Zero-alloc shortest-representation float formatting | ~30KB, no transitive deps |
| `xxhash-rust` | `0.8` | Fast hashing for key cache (xxh3 variant) | ~10KB, no transitive deps |
| `smallvec` | `1` | Inline storage for short key bytes in cache entries | ~20KB, no transitive deps |

All dependencies are mature, widely-used, and have zero transitive dependencies.

## 6. Implementation Details

### 6.1 Tier 1: Build & Low-Hanging Fruit

#### 6.1.1 Cargo Release Profile

Add to `Cargo.toml` after the `[dependencies]` section:

```toml
[profile.release]
opt-level = 3
lto = "fat"
codegen-units = 1
panic = "abort"
```

No code changes required. All `unwrap()` calls in the codebase have been audited (see discovery.md Section 8) and are provably safe.

#### 6.1.2 Integer Formatting with itoa

**What changes**: `serializer.rs` lines 347-361 (the integer branch of `stringify_value`).

**Current pattern**:
```rust
// line 354: small int
return Ok(v.to_string());
// line 352-353: BigInt from i64
return Ok(format!("{}n", v));
// line 358-359: very large int, Python str()
return Ok(format!("{}n", s));
```

**New pattern**:
```rust
// small int: use itoa to write directly to a stack buffer
let mut buf = itoa::Buffer::new();
let formatted = buf.format(v);
return Ok(formatted.to_string());
// BigInt from i64: itoa + "n" suffix
let mut buf = itoa::Buffer::new();
let formatted = buf.format(v);
return Ok(format!("{}n", formatted));
// very large int: unchanged (still needs Python str() for arbitrary precision)
```

Note: `itoa::Buffer::new()` allocates on the stack (a `[u8; 20]`). The `to_string()` on the result still allocates, but in Tier 3 we eliminate that by writing directly to the `WriteBuffer`.

BigInt formatting for values that overflow `i64` still uses `value.str()?.to_string()` + `"n"` suffix, since `itoa` only handles fixed-width integers.

#### 6.1.3 Float Formatting with ryu

**What changes**: `serializer.rs` lines 365-373 (the float branch of `stringify_value`).

**Current pattern**:
```rust
// line 371: calls Python repr() through the GIL
let repr = value.repr()?.to_string();
return Ok(repr);
```

**New pattern**:
```rust
// Use ryu for shortest round-trip representation
let mut buf = ryu::Buffer::new();
let formatted = buf.format(f);
return Ok(formatted.to_string());
```

**Special values** (lines 367-369) remain unchanged -- they already return static strings (`"NaN"`, `"Infinity"`, `"-Infinity"`).

**Formatting differences**: `ryu` produces shortest round-trip representations per the Ryu algorithm. Differences from Python `repr()`:
- `ryu` always includes a decimal point: `1.0` -> `"1.0"` (matches Python).
- `ryu` uses lowercase `e`: `1e20` (Python may produce `1e+20` with explicit `+`).
- Integer-valued floats: `ryu` produces `1.0`, Python produces `1.0` (same).
- Edge cases are extremely rare and all representations are mathematically equivalent.

Tests that compare float string output against hardcoded expectations may need updating.

#### 6.1.4 Hot/Cold Path Separation

The serializer's `stringify_value()` method (`serializer.rs` lines 328-505) checks 16 types in sequence. Common types (None, str, bool, int, float, list, dict) should remain inline. Rare types should be extracted to cold functions.

**Functions to annotate with `#[cold]` and `#[inline(never)]`**:
- `format_datetime()` (serializer.rs line 185)
- `format_timeonly()` (serializer.rs line 216)
- `format_duration()` (serializer.rs line 230)
- `format_regexp()` (serializer.rs line 275)
- `format_binary()` (serializer.rs line 287)

**Error paths to annotate with `#[cold]` and `#[inline(never)]`**:
- `Parser::error()` (parser.rs line 70)
- `raise_decode_error()` (error.rs line 19)

**Restructuring `stringify_value()`**: Extract the type dispatch for items 6-16 (datetime through set) into a separate `#[cold]` function `stringify_extended_value()`. The main `stringify_value()` handles None, str, bool, int, float, list, tuple, dict inline, and calls `stringify_extended_value()` as the final fallback.

#### 6.1.5 Empty Collection Fast-Paths

**Parser** -- already has fast-paths for empty `[]` (parser.rs line 752-757), `{}` (line 811-816), and `()` (line 781-786). No changes needed.

**Serializer** -- add fast-paths at the top of each container branch:

- `list` branch (serializer.rs line 406-418): Before calling `check_cycle`, check `list.len() == 0` and return `"[]"` immediately.
- `tuple` branch (serializer.rs line 421-427): Check `tup.len() == 0` and return `"()"` immediately.
- `dict` branch (serializer.rs line 430-468): Check `dict.len() == 0` and return `"{}"` immediately.
- `frozenset` branch (serializer.rs line 471-480): Already has a `len() == 0` check (line 472). No change needed.
- `set` branch (serializer.rs line 482-500): Already has a `len() == 0` check (line 486). No change needed.

These fast-paths avoid entering the `check_cycle` / `format_container` machinery for empty collections.

### 6.2 Tier 2: Type Dispatch & Caching

#### 6.2.1 Cached Type Pointers

**New struct**: `TypeCache` in `cache.rs` (defined in Section 5.2).

**Initialization**: In the `_native` module init function (`lib.rs` line 80-84), after adding the parse/stringify functions, initialize the `TypeCache` by importing the required modules and extracting `ob_type` pointers via `ffi::Py_TYPE(obj.as_ptr())` for built-in singletons and by getting the type objects for datetime/time/timedelta/Pattern.

**Serializer type dispatch changes** (`serializer.rs` `stringify_value()`):

Current chain (lines 330-500) uses `is_instance_of`, `downcast`, `is_instance()`, and string comparison. Replace with pointer comparison first, isinstance fallback for subclasses:

```
// Pseudo-code for the new dispatch
let obj_type = ffi::Py_TYPE(value.as_ptr());
if obj_type == type_cache.none_type {
    return "null"
} else if obj_type == type_cache.str_type {
    // fast string path
} else if obj_type == type_cache.bool_type {
    // fast bool path
} else if obj_type == type_cache.int_type {
    // fast int path
} else if obj_type == type_cache.float_type {
    // fast float path
} else if obj_type == type_cache.list_type {
    // fast list path
} else if obj_type == type_cache.dict_type {
    // fast dict path
} else if obj_type == type_cache.tuple_type {
    // fast tuple path
} else if obj_type == type_cache.frozenset_type {
    // fast frozenset path
} else if obj_type == type_cache.set_type {
    // fast set path
} else if obj_type == type_cache.bytes_type {
    // fast bytes path
} else if obj_type == type_cache.bytearray_type {
    // fast bytearray path (replaces string comparison)
} else if obj_type == type_cache.datetime_type {
    // fast datetime path (replaces isinstance call)
} else if obj_type == type_cache.time_type {
    // fast time path
} else if obj_type == type_cache.timedelta_type {
    // fast timedelta path
} else if obj_type == type_cache.pattern_type {
    // fast pattern path
} else {
    // Fallback: use isinstance for subclass support
    stringify_extended_value_slow(value, level)
}
```

The fallback path uses the existing `is_instance()` / `downcast` chain for subclass correctness. The common case (exact type match) takes O(1) pointer comparisons.

**Thread-safety**: All `ffi::Py_TYPE()` calls and pointer comparisons happen while the GIL is held. Built-in type pointers are immortal singletons. Module-level type pointers (datetime, etc.) are stable once their modules are imported.

#### 6.2.2 Dictionary Key Cache

**Cache struct**: Direct-mapped, 2048 entries, xxh3 hash function (defined in Section 5.2).

**Storage**: Module-level static with GIL-protected access:
```rust
static KEY_CACHE: Mutex<Option<KeyCache>> = Mutex::new(None);
```

On `parse()` entry, the parser takes ownership of the cache (swaps `None` in). On `parse()` exit (success or error), the parser puts it back. This avoids holding the mutex during parsing.

**Cache lookup flow in object key parsing**:

Currently, `finish_object()` (parser.rs line 858-882) calls `parse_string_as_rust()` (line 870) which creates a `PyString`, converts to Rust `String`, then uses it as a dict key via `dict.set_item(&key, val)`.

New flow:
1. In `parse_string()`, when the caller is `finish_object()` (or a new `parse_object_key()` method), after determining the key's byte range `start..end`:
2. Compute `xxh3(bytes[start..end])` -> `hash`.
3. Compute `slot = hash % 2048`.
4. Check `cache.entries[slot]`: if `hash` matches and `key_bytes` matches the source bytes, return `cache.entries[slot].value` (with `Py_INCREF`).
5. On miss: create `PyString::new(py, &source[start..end])`, store in cache slot (replacing any existing entry), return the new `PyString`.
6. Use the `PyString` directly as the dict key via `dict.set_item(py_string, val)`, eliminating the intermediate Rust `String`.

**Memory budget**: 2048 entries * ~(8 hash + 8 ptr + 32 inline bytes + overhead) = ~100-150KB. Negligible.

**Cleanup**: The cache is never explicitly cleared -- entries are evicted via round-robin replacement. The `PyObject` references keep Python strings alive. On interpreter shutdown, Python's finalizer handles cleanup. If memory pressure becomes a concern (unlikely), a `cache_clear()` function could be exposed, but this is out of scope.

#### 6.2.3 Bit-Packed Serializer State

**New state `u32` layout** (defined in Section 5.2):
- Bits 0-6: depth (7 bits, range 0-128)
- Bit 7: `ensure_ascii`
- Bit 8: `check_circular`
- Bit 9: `sort_keys`
- Bits 10-31: reserved

**Replaces**: The current `Serializer` struct fields `ensure_ascii: bool`, `check_circular: bool`, `sort_keys: bool` (serializer.rs lines 38-40). The `depth` parameter currently passed as `level: usize` through `stringify_value()` is embedded in the state.

**Access pattern**:
```rust
// Read depth
let depth = (self.state & STATE_DEPTH_MASK) as usize;
// Increment depth
self.state += 1;
// Check ensure_ascii
if self.state & STATE_ASCII_BIT != 0 { ... }
// Check check_circular
if self.state & STATE_CIRCULAR_BIT != 0 { ... }
```

This change is modest -- the primary benefit is reducing struct size and enabling the compiler to keep the state in a register during recursive calls.

### 6.3 Tier 3: SIMD & Buffer

#### 6.3.1 SIMD String Scanning (Parser)

**File**: New `src/simd.rs` module.

**Interface**:
```rust
/// Scan for the end of a JSON/RDN string starting at `start`.
/// The byte at `start - 1` should be the opening `"`.
/// Returns (pos, has_escape) where `pos` is the index of the closing `"`
/// (or bytes.len() if unterminated) and `has_escape` is true if any `\` was seen.
pub fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool);
```

**SSE2 implementation outline** (`#[cfg(target_arch = "x86_64")]`):

1. Load two 128-bit constants: `quote_mask` = 16 copies of `"` (0x22), `backslash_mask` = 16 copies of `\` (0x5C).
2. Process 16 bytes per iteration:
   - `_mm_loadu_si128` to load 16 bytes.
   - `_mm_cmpeq_epi8` against `quote_mask` -> `quote_hits`.
   - `_mm_cmpeq_epi8` against `backslash_mask` -> `backslash_hits`.
   - `_mm_or_si128(quote_hits, backslash_hits)` -> `combined`.
   - `_mm_movemask_epi8(combined)` -> `mask` (16-bit bitmask).
   - If `mask != 0`: find lowest set bit via `mask.trailing_zeros()`. Determine if it's a quote or backslash. If quote, return position. If backslash, set `has_escape = true`, skip 2 bytes (backslash + escaped char), resume.
3. Also check for control characters (`< 0x20`) to detect invalid unescaped control chars.
4. Handle the tail (< 16 remaining bytes) with the scalar fallback.

**NEON implementation outline** (`#[cfg(target_arch = "aarch64")]`):

Same algorithm, different intrinsics:
- `vld1q_u8` instead of `_mm_loadu_si128`.
- `vceqq_u8` instead of `_mm_cmpeq_epi8`.
- `vorrq_u8` instead of `_mm_or_si128`.
- Bitmask extraction: NEON lacks a direct `movemask` equivalent. Use `vshrn_n_u16` + `vget_lane_u64` to pack comparison results into a bitmask, then `trailing_zeros()`.

**Scalar fallback** (`#[cfg(not(any(...)))]` and also used for tail bytes):

Extract the current byte-by-byte loop from `parse_string()` (parser.rs lines 114-149) into the same `find_string_end` interface. This is a direct extraction with no behavioral change.

**cfg gating**:
```rust
#[cfg(target_arch = "x86_64")]
mod sse2 { ... }
#[cfg(target_arch = "aarch64")]
mod neon { ... }
mod scalar { ... }

pub fn find_string_end(bytes: &[u8], start: usize) -> (usize, bool) {
    #[cfg(target_arch = "x86_64")]
    { return sse2::find_string_end(bytes, start); }
    #[cfg(target_arch = "aarch64")]
    { return neon::find_string_end(bytes, start); }
    #[cfg(not(any(target_arch = "x86_64", target_arch = "aarch64")))]
    { return scalar::find_string_end(bytes, start); }
}
```

**Integration point**: Replace the scanning loop in `parse_string()` (parser.rs lines 114-149). Instead of the byte-by-byte `while self.pos < self.len` loop, call `find_string_end(self.bytes, self.pos)`, then branch on `has_escape` to fast-path (slice) or slow-path (materialize).

#### 6.3.2 SIMD String Escaping (Serializer)

**Same module** (`simd.rs`), different function:

```rust
/// Scan `bytes` and return the index of the first byte that needs escaping,
/// or None if no escaping is needed.
/// A byte needs escaping if: b < 0x20 || b == '"' || b == '\\' || (ensure_ascii && b > 0x7F)
pub fn needs_escape(bytes: &[u8], ensure_ascii: bool) -> Option<usize>;
```

**SSE2/NEON implementations**: Same pattern as string scanning -- load 16 bytes, compare against `"`, `\`, and range-check for `< 0x20` (using `_mm_cmplt_epi8` with a zeroed vector, after XOR with 0x80 to handle signed comparison). If `ensure_ascii`, also check `> 0x7F`.

**Scalar fallback**: Extract the current escape detection loop from `escape_string()` (serializer.rs lines 82-91) into this interface.

**Integration point**: Replace the first-pass loop in `escape_string()` (serializer.rs lines 82-91). Call `needs_escape(s.as_bytes(), self.ensure_ascii)`. If `None`, take the fast path (no escaping needed). If `Some(pos)`, bulk-copy `s[..pos]` to the output, then enter the character-by-character escape loop starting at `pos`.

#### 6.3.3 Direct PyUnicode Output Buffer

**File**: New `src/buffer.rs` module (defined in Section 5.2).

**How the serializer changes**: This is the largest refactor in the entire effort. Currently, every method in the serializer returns a `String`:
- `stringify_value() -> PyResult<String>`
- `escape_string() -> String`
- `format_container() -> String`
- `format_datetime() -> PyResult<String>`
- etc.

After this change, all methods write directly to `self.buf: WriteBuffer`:
- `stringify_value(&mut self, value, level) -> PyResult<()>` -- writes to `self.buf`
- `escape_string(&mut self, s: &str)` -- writes to `self.buf`
- `format_container_open/close(&mut self, ...)` -- writes delimiters to `self.buf`
- `format_datetime(&mut self, obj) -> PyResult<()>` -- writes to `self.buf`
- etc.

The public entry point `stringify()` creates the buffer, calls `stringify_value()`, then calls `buf.into_py_string(py)` to produce the final `PyString`.

**Change pattern for each method**:

Before:
```rust
fn escape_string(&self, s: &str) -> String {
    let mut result = String::with_capacity(...);
    result.push('"');
    result.push_str(s);
    result.push('"');
    result
}
```

After:
```rust
fn escape_string(&mut self, s: &str) {
    self.buf.write_byte(b'"');
    self.buf.write_str(s);
    self.buf.write_byte(b'"');
}
```

**Final conversion**: `WriteBuffer::into_py_string()` uses `unsafe`:
```rust
fn into_py_string(self, py: Python<'_>) -> PyResult<PyObject> {
    unsafe {
        let ptr = ffi::PyUnicode_FromStringAndSize(
            self.buf.as_ptr() as *const c_char,
            self.buf.len() as ffi::Py_ssize_t,
        );
        if ptr.is_null() {
            return Err(PyErr::fetch(py));
        }
        Ok(PyObject::from_owned_ptr(py, ptr))
    }
}
```

This eliminates the intermediate Rust `String` -> `PyString` copy that happens in the current `lib.rs` line 76 (`PyString::new(py, &result)`).

#### 6.3.4 Stack Buffers for Formatting

Replace `format!()` calls with `write!()` to a `[u8; 64]` stack buffer in these functions:

**`format_datetime()`** (serializer.rs line 213):
- Current: `format!("@{:04}-{:02}-{:02}T{:02}:{:02}:{:02}.{:03}Z", year, month, day, hour, minute, second, ms)`
- After: `write!(&mut buf[..], "@{:04}-{:02}-{:02}T{:02}:{:02}:{:02}.{:03}Z", ...)` then `self.buf.write_bytes(&buf[..len])`. Maximum output is 28 bytes (`@YYYY-MM-DDTHH:MM:SS.mmmZ`), well within 64.

**`format_timeonly()`** (serializer.rs lines 224, 226):
- Maximum output: 13 bytes (`@HH:MM:SS.mmm`).

**`format_duration()`** (serializer.rs lines 252, 257, 260, 263):
- Multiple `format!()` calls for each component. Replace with sequential `write!()` calls to the same stack buffer, or manual digit writing.
- Maximum output: ~30 bytes (`@-P999DT23H59M59S`).

**`format_regexp()`** (serializer.rs line 284):
- Current: `format!("/{}/{}", pattern, flag_str)`.
- After: Write `"/"`, then `pattern` bytes, then `"/"`, then flag chars directly to `self.buf`. No stack buffer needed -- write directly.

**`format_binary()`** (serializer.rs line 287-322):
- Currently builds a `String`. After Tier 3, write base64 characters directly to `self.buf`. No intermediate allocation needed.

### 6.4 Cross-Cutting: Benchmark Infrastructure

#### 6.4.1 pytest-benchmark Integration

**New file**: `packages/rdn-python/tests/test_benchmark.py`

**Benchmarked operations** (using `pytest-benchmark`):
- Parse: small JSON, medium JSON, large JSON, medium RDN, large RDN
- Stringify: small object, medium object, large object, medium RDN object, large RDN object
- Micro: string-heavy parse, number-heavy parse, nested-object parse

**Fixture data**: Reuse the same fixtures from `bench.py`.

**CI configuration**: Add `pytest-benchmark` to test dependencies. In CI, run benchmarks with `--benchmark-only --benchmark-json=benchmark.json`. Store the JSON artifact. Set minimum threshold via `--benchmark-min-rounds=100`.

For regression detection, use `pytest-benchmark compare` against a stored baseline. Initially, no hard threshold -- just track and report. After Tier 3 is complete, establish minimum ops/sec thresholds based on the final numbers.

## 7. Migration & Compatibility

- **API**: No changes. `rdn.parse()` and `rdn.stringify()` signatures are identical.
- **Float formatting**: Minor differences documented. `ryu` and Python `repr()` both produce shortest round-trip representations, but exact digit sequences may differ in edge cases. All representations are mathematically equivalent and round-trip correctly.
- **Error messages**: No changes. `RDNDecodeError` attributes are preserved.
- **Pure Python fallback**: Unchanged. The `_USE_NATIVE` flag and routing logic in `rdn/__init__.py` remain as-is.
- **Wheel compatibility**: The native extension remains a standard maturin-built cdylib. No changes to the wheel format or installation process.

## 8. Testing Strategy

- **Existing tests**: All ~400 tests across `test_parse.py`, `test_stringify.py`, `test_native.py`, `test_conformance.py`, `test_edge_cases.py`, `test_encoder.py`, `test_decoder.py`, `test_file_io.py` must pass after each tier. Run the full suite between tiers.
- **Conformance suite**: All 11 valid, 10 invalid, and 2 roundtrip files must pass after each tier.
- **Float parity tests**: After Tier 1, identify and update any tests that compare float string output against hardcoded expectations (if `ryu` output differs from `repr()`). Document each change.
- **New tests -- SIMD correctness** (Tier 3):
  - Strings shorter than 16 bytes (SIMD register width).
  - Strings exactly 16, 32, 48 bytes (aligned to register width).
  - Strings with `"` or `\` at every possible position within a 16-byte window.
  - Strings ending mid-register (e.g., 17 bytes -- one full SIMD pass + 1 byte tail).
  - UTF-8 multi-byte sequences spanning SIMD boundaries.
  - Empty strings, single-character strings.
- **New tests -- Key cache behavior** (Tier 2):
  - Parse a payload with many repeated keys, verify correct values.
  - Parse payloads with > 2048 unique keys, verify no corruption.
  - Parse multiple payloads sequentially, verify cross-call cache reuse works correctly.
- **New tests -- Buffer edge cases** (Tier 3):
  - Very large payloads (>1MB) to test buffer growth.
  - Deeply nested structures (128 levels) to test buffer capacity.
  - Empty output (serialize `None` -> `"null"`).
- **Benchmark regression**: `pytest-benchmark` in CI tracks throughput over time.

## 9. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| `ryu` float formatting differs from Python `repr()` | Test failures, user-visible output changes | Accept minor diffs; update affected tests with comments; both representations are mathematically equivalent |
| SIMD off-by-one errors in boundary handling | Incorrect parse results, potential memory safety issues | Run full conformance suite with SIMD on/off; add boundary-specific tests; fuzz test the scanner |
| `panic = "abort"` turns any future `unwrap()` into process termination | Python process crashes instead of raising exception | All current `unwrap()` calls audited as safe; establish code review rule that new `unwrap()` must be justified |
| Key cache memory leak if entries accumulate indefinitely | Gradual memory growth over long-running processes | Fixed-size (2048 entries) with round-robin eviction; memory is bounded at ~150KB |
| Module-level `TypeCache` statics and `KeyCache` mutex | Complexity, potential for subtle bugs | GIL protects all access; static initialization is once-only; extensive test coverage |
| `WriteBuffer` refactor touches all serializer methods | High risk of introducing bugs during refactor | Implement behind a feature flag initially; run full test suite at every method conversion; diff output against pure-Python for a large corpus |
| NEON intrinsics are less well-tested than SSE2 in the Rust ecosystem | Potential correctness issues on ARM64 | Run CI on both x86_64 and ARM64; scalar fallback is always available |

## 10. Out of Scope

- Switching from PyO3 to raw `pyo3-ffi` entirely (too large a rewrite; targeted FFI use for `PyUnicode_FromStringAndSize` is in-scope)
- Adding new RDN types or features
- Changing the pure-Python fallback implementation
- WASM compilation support
- AVX2 or AVX-512 SIMD (SSE2 + NEON provide the bulk of the benefit)
- `target-cpu=native` in distributed wheels (would break cross-compilation; benchmark-only use is acceptable)
- Parser container pre-allocation heuristics (low impact relative to effort)

## 11. Dependencies & Blockers

- **No external blockers**: All required crates are stable and published on crates.io.
- **Tier ordering**: Tier 1 must be completed and measured before Tier 2 begins. Tier 2 must be completed and measured before Tier 3 begins. This is a delivery constraint, not a technical dependency -- later tiers build on earlier tiers' code changes.
- **CI**: pytest-benchmark should be added to CI early (Task 1) so that all tiers can be measured consistently.

## 12. Ordered Task List

### Task 1: Establish baseline benchmarks
**Files**: `packages/rdn-python/tests/test_benchmark.py`, `packages/rdn-python/pyproject.toml` (add `pytest-benchmark` dev dependency)
**Description**: Create a `test_benchmark.py` file with pytest-benchmark tests covering all the payload categories from `bench.py`. Add `pytest-benchmark` to the dev dependencies. Run the benchmarks and record the baseline numbers as a JSON artifact. This provides the baseline against which all tiers are measured.
**Acceptance criteria**: `pytest -k benchmark --benchmark-only` runs successfully, produces JSON output, and covers parse + stringify for small/medium/large JSON and medium/large RDN payloads.

---

### Task 2: Add Cargo release profile
**Files**: `packages/rdn-native/Cargo.toml`
**Description**: Add `[profile.release]` section with `opt-level = 3`, `lto = "fat"`, `codegen-units = 1`, `panic = "abort"`. Rebuild the extension with `maturin develop --release` and verify all tests pass.
**Acceptance criteria**: `Cargo.toml` has the release profile. `maturin develop --release` succeeds. All pytest tests pass. No functional changes.

### Task 3: Replace integer formatting with itoa
**Files**: `packages/rdn-native/Cargo.toml` (add `itoa = "1"`), `packages/rdn-native/src/serializer.rs`
**Description**: Add `itoa` dependency. In `stringify_value()`, replace `v.to_string()` (line 354) with `itoa::Buffer::new().format(v).to_string()`. Replace `format!("{}n", v)` (line 352) with itoa-formatted string + "n". Leave the very-large-int path (line 358-359) unchanged since it needs Python `str()` for arbitrary precision.
**Acceptance criteria**: All tests pass. `itoa` is used for i64 integer formatting in the serializer.

### Task 4: Replace float formatting with ryu
**Files**: `packages/rdn-native/Cargo.toml` (add `ryu = "1"`), `packages/rdn-native/src/serializer.rs`
**Description**: Add `ryu` dependency. In `stringify_value()`, replace the `value.repr()?.to_string()` call (line 371) with `ryu::Buffer::new().format(f).to_string()`. Special float handling (NaN, Infinity, -Infinity) remains unchanged. Identify and update any tests that compare float string output against hardcoded expectations if the ryu output differs.
**Acceptance criteria**: All tests pass (with any necessary float-format test updates documented). `ryu` is used for f64 formatting. No Python `repr()` calls for floats.

### Task 5: Add hot/cold path annotations
**Files**: `packages/rdn-native/src/serializer.rs`, `packages/rdn-native/src/parser.rs`, `packages/rdn-native/src/error.rs`
**Description**: Add `#[cold]` and `#[inline(never)]` to `format_datetime()`, `format_timeonly()`, `format_duration()`, `format_regexp()`, `format_binary()`, `Parser::error()`, and `raise_decode_error()`. Extract the extended-type dispatch (datetime through set) from `stringify_value()` into a new `#[cold] fn stringify_extended_value()` method. The main `stringify_value()` handles None, str, bool, int, float, list, tuple, dict inline, then calls the cold path.
**Acceptance criteria**: All tests pass. Cold annotations are in place. `stringify_value()` is split into hot and cold paths.

### Task 6: Add empty collection fast-paths in serializer
**Files**: `packages/rdn-native/src/serializer.rs`
**Description**: Add `len() == 0` early-return checks before cycle detection for list (return `"[]"`), tuple (return `"()"`), and dict (return `"{}"`). These checks should be at the very top of each branch, before `check_cycle()`.
**Acceptance criteria**: All tests pass. Empty collections bypass cycle detection and container formatting.

### Task 7: Run post-Tier-1 benchmarks
**Files**: None (benchmark run only)
**Description**: Run the pytest-benchmark suite and `bench.py`. Record results and compare against the Task 1 baseline. Document the per-optimization improvement.
**Acceptance criteria**: Benchmark results are recorded. Improvement (or lack thereof) is documented.

---

### Task 8: Create cache.rs with TypeCache struct
**Files**: `packages/rdn-native/src/cache.rs` (new), `packages/rdn-native/src/lib.rs` (add `mod cache`)
**Description**: Create the `TypeCache` struct that holds raw `*mut ffi::PyTypeObject` pointers for all 16 types (str, int, bool, float, list, dict, tuple, set, frozenset, bytes, NoneType, bytearray, datetime, time, timedelta, Pattern). Include `PyObject` refs to keep module-level types alive. Implement `TypeCache::new(py: Python) -> PyResult<Self>` that imports the needed modules and extracts the type pointers. Store the TypeCache in a module-level `static`. Initialize it in the `_native` module init function.
**Acceptance criteria**: `cache.rs` compiles. Module init succeeds. TypeCache is populated. All existing tests pass.

### Task 9: Refactor serializer to use cached type pointers
**Files**: `packages/rdn-native/src/serializer.rs`, `packages/rdn-native/src/cache.rs`
**Description**: Refactor `stringify_value()` to use `ffi::Py_TYPE(value.as_ptr())` pointer comparison against the cached type pointers for all 16 types. The existing isinstance/downcast calls become the fallback path for subclasses (the cold `stringify_extended_value()` method). Remove the `TypeCaches` struct from `serializer.rs` (currently at lines 16-34) since it's replaced by the module-level `TypeCache`. Remove the `bytearray` string comparison (line 400).
**Acceptance criteria**: All tests pass. No `is_instance()` Python calls for exact type matches. No string comparison for bytearray. Subclasses still work via fallback.

### Task 10: Create cache.rs KeyCache struct
**Files**: `packages/rdn-native/src/cache.rs`, `packages/rdn-native/Cargo.toml` (add `xxhash-rust = { version = "0.8", features = ["xxh3"] }`, `smallvec = "1"`)
**Description**: Implement the `KeyCache` struct with 2048 direct-mapped entries, xxh3 hashing, and round-robin eviction. Implement `lookup(&self, bytes: &[u8]) -> Option<PyObject>` and `insert(&mut self, bytes: &[u8], value: PyObject)`. Store the cache in a module-level `static Mutex<Option<KeyCache>>`.
**Acceptance criteria**: `KeyCache` compiles. Unit tests (Rust `#[test]`) verify lookup/insert/eviction behavior.

### Task 11: Integrate KeyCache into parser
**Files**: `packages/rdn-native/src/parser.rs`, `packages/rdn-native/src/lib.rs`
**Description**: In `parse()` entry point, acquire the key cache from the module-level static. Add a new `parse_object_key()` method that computes the key's byte range, checks the cache, and either returns the cached `PyString` or creates a new one and inserts it. Replace `parse_string_as_rust()` calls in `finish_object()` (line 870) and `parse_brace()` (lines 831-833) with `parse_object_key()` that returns a `Bound<'py, PyString>` directly. Use the `PyString` as the dict key (eliminating the intermediate Rust `String`). On `parse()` exit, return the cache to the module-level static.
**Acceptance criteria**: All tests pass. Object keys are cached. Repeated keys across `parse()` calls reuse the same `PyString` objects.

### Task 12: Implement bit-packed serializer state
**Files**: `packages/rdn-native/src/serializer.rs`
**Description**: Replace `ensure_ascii: bool`, `check_circular: bool`, `sort_keys: bool` fields with a single `state: u32`. Replace the `level: usize` parameter in `stringify_value()` with depth embedded in the state. Update all reads of these fields to use bitmask operations. Update the `Serializer::new()` constructor to pack the initial state.
**Acceptance criteria**: All tests pass. The `Serializer` struct has a `state: u32` field instead of three bools. The `stringify_value` method no longer takes a `level` parameter.

### Task 13: Run post-Tier-2 benchmarks
**Files**: None (benchmark run only)
**Description**: Run the pytest-benchmark suite and `bench.py`. Record results and compare against the post-Tier-1 numbers. Document the per-optimization improvement.
**Acceptance criteria**: Benchmark results are recorded. Cumulative and per-tier improvement is documented.

---

### Task 14: Create simd.rs with scalar fallback
**Files**: `packages/rdn-native/src/simd.rs` (new), `packages/rdn-native/src/lib.rs` (add `mod simd`)
**Description**: Create the `simd.rs` module with the `find_string_end()` and `needs_escape()` functions. Initially, implement only the scalar fallback versions -- extract the byte-by-byte scanning logic from `parse_string()` (parser.rs lines 114-149) and `escape_string()` (serializer.rs lines 82-91) into the new functions. This establishes the interface and integration points without SIMD.
**Acceptance criteria**: `simd.rs` compiles with scalar implementations. All tests pass. No behavioral change.

### Task 15: Integrate SIMD scanner into parser
**Files**: `packages/rdn-native/src/parser.rs`
**Description**: Replace the string scanning loop in `parse_string()` (lines 114-149) with a call to `simd::find_string_end()`. The function returns `(end_pos, has_escape)`. If `!has_escape`, slice directly from source (fast path). If `has_escape`, call `materialize_string(start, end_pos)` (slow path). Ensure control character detection (`< 0x20`) is handled correctly.
**Acceptance criteria**: All tests pass. `parse_string()` uses `find_string_end()` from `simd.rs`. Behavior is identical (scalar fallback).

### Task 16: Integrate SIMD escape detection into serializer
**Files**: `packages/rdn-native/src/serializer.rs`
**Description**: Replace the first-pass escape detection loop in `escape_string()` (lines 82-91) with a call to `simd::needs_escape()`. If `None`, take the no-escape fast path. If `Some(pos)`, enter the character-by-character escape loop.
**Acceptance criteria**: All tests pass. `escape_string()` uses `needs_escape()` from `simd.rs`. Behavior is identical (scalar fallback).

### Task 17: Implement SSE2 SIMD for find_string_end
**Files**: `packages/rdn-native/src/simd.rs`
**Description**: Add the SSE2 implementation of `find_string_end()` behind `#[cfg(target_arch = "x86_64")]`. Use `_mm_loadu_si128`, `_mm_cmpeq_epi8`, `_mm_or_si128`, `_mm_movemask_epi8` for 16-byte stride scanning. Handle tail bytes with the scalar fallback. Add SIMD-specific tests for boundary conditions.
**Acceptance criteria**: All tests pass on x86_64. SIMD-specific boundary tests pass. `#[cfg]` correctly gates the implementation.

### Task 18: Implement NEON SIMD for find_string_end
**Files**: `packages/rdn-native/src/simd.rs`
**Description**: Add the NEON implementation of `find_string_end()` behind `#[cfg(target_arch = "aarch64")]`. Use `vld1q_u8`, `vceqq_u8`, `vorrq_u8` for 16-byte stride scanning. Implement bitmask extraction via `vshrn_n_u16` + `vget_lane_u64`. Handle tail bytes with the scalar fallback.
**Acceptance criteria**: All tests pass on ARM64 (Apple Silicon). SIMD-specific boundary tests pass. `#[cfg]` correctly gates the implementation.

### Task 19: Implement SSE2 and NEON SIMD for needs_escape
**Files**: `packages/rdn-native/src/simd.rs`
**Description**: Add SSE2 and NEON implementations of `needs_escape()`. Use the same pattern as `find_string_end()` but check for `< 0x20`, `"`, `\`, and optionally `> 0x7F` (ensure_ascii). Return the first position needing escape.
**Acceptance criteria**: All tests pass on both x86_64 and ARM64. Escape detection behavior is identical to scalar.

### Task 20: Create buffer.rs with WriteBuffer
**Files**: `packages/rdn-native/src/buffer.rs` (new), `packages/rdn-native/src/lib.rs` (add `mod buffer`)
**Description**: Implement the `WriteBuffer` struct with `Vec<u8>` backing, `write_byte()`, `write_bytes()`, `write_str()`, `write_u32()` (using itoa), `write_i64()` (using itoa), `write_f64()` (using ryu), and `into_py_string()` (using `PyUnicode_FromStringAndSize`). Add Rust unit tests for the buffer.
**Acceptance criteria**: `buffer.rs` compiles. Unit tests verify write operations and `into_py_string()` produces correct Python strings.

### Task 21: Refactor serializer to use WriteBuffer
**Files**: `packages/rdn-native/src/serializer.rs`, `packages/rdn-native/src/lib.rs`
**Description**: Add a `buf: WriteBuffer` field to the `Serializer` struct. Change all serializer methods from returning `String` to writing directly to `self.buf`. This is a method-by-method refactor:
1. `escape_string()`: `-> String` becomes `(&mut self, s: &str)` writing to `self.buf`.
2. `format_container()`: Remove entirely; inline open/close/separator writing.
3. `format_datetime()`, `format_timeonly()`, `format_duration()`: Use `write!()` to `[u8; 64]` stack buffer, then `self.buf.write_bytes()`.
4. `format_regexp()`: Write `/`, pattern, `/`, flags directly to `self.buf`.
5. `format_binary()`: Write base64 characters directly to `self.buf`.
6. `stringify_value()`: `-> PyResult<String>` becomes `-> PyResult<()>`.
7. `stringify()`: Create `WriteBuffer`, call `stringify_value()`, call `buf.into_py_string()`.
Update `lib.rs` to use the new `PyObject` return from `stringify()` directly (remove the `PyString::new(py, &result)` conversion on line 76).
**Acceptance criteria**: All tests pass. No intermediate `String` allocations in the serializer hot path. The final output is produced via `PyUnicode_FromStringAndSize`.

### Task 22: Run post-Tier-3 benchmarks
**Files**: None (benchmark run only)
**Description**: Run the pytest-benchmark suite and `bench.py`. Record results and compare against the post-Tier-2 numbers and the original baseline. Document the per-tier and cumulative improvement. Verify the 30% improvement target is met on medium/large payloads.
**Acceptance criteria**: Benchmark results are recorded. Cumulative improvement is documented. Target: >= 30% improvement on medium/large payloads for both parse and stringify.

---

### Task 23: Update documentation
**Files**: `packages/rdn-native/README.md` (or create if absent), `packages/rdn-python/README.md`, `CLAUDE.md`
**Description**: Document the performance optimizations applied. Note any behavioral differences (float formatting). Update build instructions if needed. Update `CLAUDE.md` with notes about the new modules (`simd.rs`, `cache.rs`, `buffer.rs`), the release profile, and the SIMD architecture.
**Acceptance criteria**: Documentation reflects the new state of the codebase. Float formatting differences are noted.

### Task 24: Final validation
**Files**: None
**Description**: Run the full test suite (`pytest`), the conformance suite, and the benchmark suite one final time. Verify 100% test pass rate. Verify benchmark improvements meet the 30% target. Run on both x86_64 and ARM64 if CI supports it.
**Acceptance criteria**: All tests pass. Benchmarks meet targets. No regressions.
