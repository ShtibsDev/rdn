# Discovery: native-perf-optimizations

## 1. Feature Summary

This effort applies orjson-inspired performance optimizations to `packages/rdn-native/`, the Rust+PyO3 native extension that accelerates RDN parsing and serialization for Python. The optimizations span three tiers: build configuration and low-hanging fruit (Tier 1), type dispatch and caching (Tier 2), and SIMD/buffer optimizations (Tier 3). The goal is at least 30% throughput improvement on medium/large payloads while maintaining 100% conformance test pass rate and full API compatibility.

## 2. Current Implementation Analysis

### 2.1 Build Configuration

**Cargo.toml** (`packages/rdn-native/Cargo.toml`):
- `edition = "2021"`
- `crate-type = ["cdylib"]`
- Single dependency: `pyo3 = { version = "0.23", features = ["extension-module"] }`
- **No `[profile.release]` section exists at all** -- uses Cargo defaults:
  - `opt-level = 3` (default for release)
  - `codegen-units = 16` (default -- prevents whole-program optimization)
  - `lto = false` (default -- no link-time optimization)
  - `panic = "unwind"` (default -- carries unwind table overhead)
- No `itoa` or `ryu` crates for number formatting
- No SIMD-related dependencies

**pyproject.toml** (`packages/rdn-native/pyproject.toml`):
- Build system: `maturin>=1.0,<2.0`
- `module-name = "rdn._native"`
- `features = ["pyo3/extension-module"]`
- No maturin strip, profile, or target-specific settings
- No `[tool.maturin.profile]` overrides

**What's missing vs orjson's profile**: orjson uses `codegen-units = 1`, `lto = "fat"`, `panic = "abort"`, `opt-level = 3`, and `target-cpu=native`. All of these are absent from the current configuration.

### 2.2 Parser Hot Paths

**String parsing** (`parser.rs`, lines 109-224):
- `parse_string()` (line 109): Two-pass approach -- first scans byte-by-byte for `"` or `\`, tracking `has_escape` flag. If no escape found, slices directly from `self.source` (fast path, line 119-122). If escapes present, calls `materialize_string()` (slow path, line 125).
- `materialize_string()` (line 154): Allocates a `String::with_capacity(end - start)`. Processes escapes character-by-character. Bulk copies non-escape segments via `push_str(&self.source[j_start..i])` (line 219). Surrogate pair handling is inline (lines 182-206).
- `parse_string_as_rust()` (line 227-229): Used for object keys -- calls `parse_string()` then `.to_string()`, **creating a PyString and immediately converting to Rust String**. This is wasteful for keys that are only used as dict keys.
- **Allocation pattern**: Every string becomes a `PyString::new()` which allocates a Python unicode object. No key caching exists.

**Number parsing** (`parser.rs`, lines 236-336):
- Integer fast path (line 240-255): Accumulates digits into `i64` with overflow detection via `checked_mul`/`checked_add`. Converts to PyObject via `val.into_pyobject()` (line 328).
- Float path (line 320-322): Parses the raw slice via `raw.parse::<f64>()` (Rust's standard library `from_str`), then wraps with `PyFloat::new()`.
- BigInt path (line 268-274): Imports `builtins` module each time, calls `int()` with the raw string. This import is not cached.
- Large integer fallback (line 332-335): Also imports `builtins` and calls `int()` on each invocation.
- **No `itoa`/`ryu` usage** for formatting; parsing uses stdlib which is reasonable.

**Container parsing** (`parser.rs`, lines 747-935):
- `parse_array()` (line 747): Uses `Vec::new()` with no capacity hint. Items are pushed one at a time. Final conversion via `PyList::new(self.py, &items)` (line 772).
- `parse_tuple()` (line 776): Same pattern -- `Vec::new()`, push items, `PyTuple::new()`.
- `parse_brace()` (line 805): Disambiguates `{` between Object, Map, and Set. Parses first value, then checks separator byte. Object keys are extracted via `downcast::<PyString>()?.to_string()` (line 831-833).
- `finish_object()` (line 858): Creates `PyDict::new()`, sets items one at a time via `dict.set_item(&key, val)`. Keys are Rust `String` objects.
- `finish_set()` (line 913): Collects into `Vec<PyObject>`, then converts each to `Bound<'py, PyAny>` via `.bind().clone()` (line 932), then creates `PyFrozenSet::new()`.
- **No pre-allocation**: None of the container parsers estimate capacity from the input size.

**Whitespace skipping** (`parser.rs`, lines 60-68):
- `skip_ws()`: Byte-by-byte loop matching `0x20 | 0x09 | 0x0A | 0x0D`. Marked `#[inline(always)]`. Simple and efficient for small gaps but could benefit from wider-word scanning for large whitespace blocks.

### 2.3 Serializer Hot Paths

**Type dispatch chain** (`serializer.rs`, lines 328-505):
The `stringify_value()` method checks types in this order:
1. `PyNone` -- `is_instance_of::<PyNone>()` (line 330)
2. `PyString` -- `downcast::<PyString>()` (line 335)
3. `PyBool` -- `is_instance_of::<PyBool>()` (line 341)
4. `PyInt` -- `is_instance_of::<PyInt>()` (line 347)
5. `PyFloat` -- `is_instance_of::<PyFloat>()` (line 365)
6. `datetime` -- `is_instance(&self.caches.datetime_type)` (line 376) -- Python isinstance call
7. `time` -- `is_instance(&self.caches.time_type)` (line 381) -- Python isinstance call
8. `timedelta` -- `is_instance(&self.caches.timedelta_type)` (line 387) -- Python isinstance call
9. `re.Pattern` -- `is_instance(&self.caches.pattern_type)` (line 391) -- Python isinstance call
10. `PyBytes` -- `downcast::<PyBytes>()` (line 396)
11. `bytearray` -- `value.get_type().name()? == "bytearray"` (line 400) -- **string comparison for type name**
12. `PyList` -- `downcast::<PyList>()` (line 406)
13. `PyTuple` -- `downcast::<PyTuple>()` (line 421)
14. `PyDict` -- `downcast::<PyDict>()` (line 430)
15. `PyFrozenSet` -- `downcast::<PyFrozenSet>()` (line 471)
16. `PySet` -- `downcast::<PySet>()` (line 482)

**Key observations**:
- Items 6-9 use `is_instance()` which makes a Python-level `isinstance()` call through the GIL -- this is significantly slower than pointer comparison against a cached `ob_type`.
- Item 11 (bytearray) uses string comparison on the type name -- very slow.
- Common types like `str`, `int`, `list`, `dict` use PyO3's `downcast` or `is_instance_of` which are C-level type checks, but `is_instance_of` still goes through PyO3's type checking infrastructure.
- No `#[cold]` or `#[inline(never)]` annotations on rare paths (datetime, regex, binary).

**String escaping** (`serializer.rs`, lines 79-133):
- Two-pass approach: First pass (lines 82-91) scans every byte to check if escaping is needed. Second pass (lines 105-129) processes character-by-character if escaping is needed.
- Fast path (lines 93-99): If no escaping needed and ASCII-only, allocates `String::with_capacity(s.len() + 2)`, pushes `"`, the string, and `"`.
- Slow path: Uses `escape_byte()` lookup (from `tables.rs`), then `format!("\\u{:04x}", cp)` for non-ASCII (line 118) and surrogate pairs (line 123). **`format!()` allocates a temporary String for each escape**.
- **No SIMD scanning** for detecting characters needing escaping.

**Number formatting** (`serializer.rs`, lines 347-373):
- Integer (line 354): Uses `v.to_string()` which allocates a `String` via Rust's `Display` trait. BigInt uses `format!("{}n", v)` or `format!("{}n", s)` (lines 355, 359).
- Float (line 371): Uses `value.repr()?.to_string()` -- **calls into Python's repr() function** through the GIL for every float. This is extremely expensive compared to using `ryu` crate directly.
- Special floats (lines 367-369): Return static strings, which is efficient.

**Container formatting** (`serializer.rs`, lines 156-179):
- `format_container()`: Collects all serialized parts as `Vec<String>`, then joins them. For indented output, uses `format!()` with string concatenation (line 168). For compact output, iterates and pushes.
- **Each child value is serialized into its own String**, then all strings are concatenated. This means the serializer makes many small allocations instead of writing to a single buffer.

**DateTime/Duration/Regex formatting** (`serializer.rs`, lines 185-285):
- `format_datetime()` (line 185): Extracts 7 Python attributes (`year`, `month`, `day`, `hour`, `minute`, `second`, `microsecond`) via `getattr().extract()` -- **7 Python attribute lookups per datetime**. Also checks and potentially converts timezone (lines 192-202). Uses `format!("@{:04}-{:02}-...")` (line 213).
- `format_timeonly()` (line 216): 4 attribute lookups + `format!()`.
- `format_duration()` (line 230): Calls `total_seconds()` Python method, then multiple `format!()` calls for each component (lines 252, 257, 260, 263).
- `format_regexp()` (line 275): 2 attribute lookups + `format!("/{}/{}", ...)` (line 284).
- **All of these use `format!()` which heap-allocates a String. A stack buffer of ~64 bytes would eliminate these allocations.**

### 2.4 PyO3 Usage Patterns

**Type checking approaches in use:**
1. `is_instance_of::<T>()` -- used for `PyNone`, `PyBool`, `PyInt`, `PyFloat` (serializer lines 330, 341, 347, 365). This is a C-level `ob_type` comparison for built-in types.
2. `downcast::<T>()` -- used for `PyString`, `PyBytes`, `PyList`, `PyTuple`, `PyDict`, `PyFrozenSet`, `PySet` (serializer lines 335, 396, 406, 421, 430, 471, 482). Returns `Result`, combines type check + cast.
3. `is_instance(&cached_type)` -- used for `datetime`, `time`, `timedelta`, `Pattern` (serializer lines 376, 381, 387, 391). This calls Python's `isinstance()` through PyO3.
4. `get_type().name()? == "bytearray"` -- string comparison (serializer line 400). Slowest approach.

**Python object creation:**
- `PyString::new(py, &s)` -- used everywhere for string creation (allocates Python unicode object)
- `PyFloat::new(py, f)` -- for floats
- `PyList::new(py, &items)` / `PyTuple::new(py, &items)` -- from collected Rust vecs
- `PyDict::new(py)` + `set_item()` -- incremental dict building
- `val.into_pyobject(py)` -- for integers and booleans
- `py.None()` -- for null

**Python calls that could be avoided:**
- `value.repr()?.to_string()` for float formatting (serializer line 371) -- replace with `ryu`
- `builtins.getattr("int")?.call1((raw,))` for BigInt parsing (parser lines 271-274, 332-334) -- import is not cached
- `obj.call_method0("total_seconds")` for duration formatting (serializer line 231)
- `obj.getattr("year")`, `obj.getattr("month")`, etc. for datetime formatting (7 calls per datetime)
- `self.caches.re_mod.getattr("compile")?.call1(...)` for regex parsing (parser lines 584-585)
- `self.caches.datetime_cls.call1(...)` for datetime construction (parser line 402, 421)

### 2.5 Hot-Path Routing

**`rdn/__init__.py`** (lines 19-26, 80, 198):

The module attempts to import `rdn._native` at load time. If successful, `_USE_NATIVE = True`.

**Parse routing** (lines 196-202):
```python
if _USE_NATIVE and all(x is None for x in [object_hook, parse_float, parse_int, ...]):
    return _native_parse(text)
```
- Native path: Only when **all hooks are None** and no `cls` is provided.
- Any single hook triggers fallback to pure Python.
- The `all(x is None for x in [...])` check evaluates 10 items on every call.

**Stringify routing** (lines 80-81):
```python
if _USE_NATIVE and default is None:
    return _native_stringify(obj, ...)
```
- Native path: Only when `default is None` and no `cls` is provided.
- If `cls` is provided, it bypasses native entirely (line 74-76).

**Implications for optimization:**
- The native extension only runs the no-hooks/no-cls path, so all optimization effort is correctly scoped.
- The routing logic itself (`all(x is None ...)`) adds a small per-call overhead on the Python side that we cannot change.
- The API surface (`parse(text) -> PyObject`, `stringify(value, ...) -> PyObject`) must remain identical.

## 3. Benchmark Baseline

**`bench.py`** (`packages/rdn-python/bench.py`):

Measures four categories:
1. **PARSE (JSON payloads)** -- json vs rdn-py vs rdn-native on small (~55B), medium (~1.6KB), large (~15KB) payloads
2. **PARSE (RDN-only payloads)** -- rdn-py vs rdn-native on payloads with datetime, BigInt, binary, Map, Set, regex
3. **STRINGIFY (JSON-compatible objects)** -- json vs rdn-py vs rdn-native
4. **STRINGIFY (RDN extended-type objects)** -- rdn-py vs rdn-native

Iteration counts: small=20,000, medium=5,000, large=1,000.

**Fixtures:**
- `SMALL_JSON`: 3-key object (~55 bytes)
- `MEDIUM_JSON`: 20-user array with nested objects (~1.6KB)
- `LARGE_JSON`: 50-user complex nested structure (~15KB)
- `MEDIUM_RDN`: 3 users with `@datetime` and sets (~350 bytes)
- `LARGE_RDN`: Complex payload with datetime, BigInt, binary, Map, Set, regex (~700 bytes)

No existing performance numbers are checked into the repository. The benchmark must be run manually to establish a baseline before optimization work begins.

## 4. Dependencies & Build System

**Current Cargo.toml dependencies:**
- `pyo3 = { version = "0.23", features = ["extension-module"] }` (resolved to 0.23.5)

**New dependencies needed:**

| Crate | Purpose | Size Impact |
|-------|---------|-------------|
| `itoa` | Zero-alloc integer-to-string formatting | ~15KB, no deps |
| `ryu` | Zero-alloc shortest-representation float formatting | ~30KB, no deps |
| `simdutf8` (optional) | SIMD-accelerated UTF-8 validation | ~20KB, potential for string scanning |
| `xxhash-rust` (optional) | Fast hashing for key cache | ~10KB, xxh3 variant |

**maturin build configuration implications:**
- Adding `[profile.release]` to `Cargo.toml` is straightforward and does not affect maturin.
- `panic = "abort"` is compatible with PyO3 cdylib builds. PyO3 catches Rust panics at the FFI boundary via `catch_unwind` by default, but with `panic = "abort"`, any uncaught panic will abort the process. Since all error paths in the current code return `PyResult`, this should be safe -- but any `unwrap()` calls become process-fatal. Current code has `.unwrap()` calls at: `parser.rs` lines 85, 193, 204, 328 (all on `char::from_u32` which is safe for valid codepoints), `serializer.rs` line 111 (`from_utf8` on known-valid escape bytes).
- `lto = "fat"` significantly increases compile time (potentially 2-5x) but is a one-time cost for release builds.
- `codegen-units = 1` also increases compile time but enables better optimization across the entire crate.

## 5. Test Coverage

### Test Files

| File | Coverage Area | Test Count (approx) |
|------|--------------|---------------------|
| `test_parse.py` | Parser: strings, escapes, unicode, surrogates, keywords, whitespace, errors, numbers, BigInt, special numbers, datetime, timeonly, duration, unix timestamp, regexp, binary (b64/hex), bytes input, all hooks (parse_int/float/bigint/datetime/timeonly/duration/regexp/binary), object_hook, object_pairs_hook | ~100 tests |
| `test_stringify.py` | Serializer: None, bools, ints, BigInt auto-promote, floats, strings, ensure_ascii, surrogate pairs, datetime, timeonly, duration, regexp, binary, lists, tuples, dicts, sets, cycle detection, indent, sort_keys, default function, separators, mixed structures, unsupported types, public API | ~90 tests |
| `test_native.py` | Native extension: availability, direct parse/stringify, parity with pure Python (30 parse cases, 30 stringify values), fallback behavior | ~50 tests |
| `test_conformance.py` | Shared test suite: 11 valid, 10 invalid, 2 roundtrip files | ~23 parametrized tests |
| `test_edge_cases.py` | Edge cases: empty/whitespace input, trailing whitespace, whitespace between tokens, surrogate pairs, nesting depth (128/129), large BigInts, long strings, nested containers, special literals, datetime/timeonly/duration/regexp/binary edge cases, error messages, input types, roundtrips | ~60 tests |
| `test_encoder.py` | RDNEncoder class: basic encode, extended types, settings, default method, iterencode, cls parameter | ~40 tests |
| `test_decoder.py` | RDNDecoder class: basic decode, extended types, all hooks, raw_decode, errors, cls parameter | ~30 tests |
| `test_file_io.py` | File I/O: dump to StringIO/files, load from StringIO/BytesIO/files, all kwargs | ~30 tests |

### Conformance Test Suite
- 11 valid `.rdn` files with `.expected.json` counterparts
- 10 invalid `.rdn` files that must raise `RDNDecodeError`
- 2 roundtrip `.rdn` files (parse -> stringify -> parse identity)

### What's covered vs what needs new tests
- **Well covered**: All RDN types, error handling, hooks, API surface, edge cases, parity between native and pure Python.
- **Not covered by current tests**: Performance regression tests (bench.py exists but is not in pytest), SIMD-specific correctness (once SIMD paths are added), key cache hit/miss behavior, stack buffer overflow edge cases for formatting.
- **Recommendation**: After optimization, add pytest-benchmark markers or a dedicated performance regression test that asserts minimum ops/sec thresholds. Also add tests for any new unsafe code paths.

## 6. Blast Radius Assessment

| File | Changes | Risk |
|------|---------|------|
| `Cargo.toml` | Add `[profile.release]`, new deps | **Low** -- build-only, no API change |
| `pyproject.toml` | Potentially add maturin profile settings | **Low** -- build-only |
| `src/lib.rs` | Module init for cached type pointers | **Low** -- initialization only |
| `src/serializer.rs` | Major refactor: type dispatch, number formatting, string escaping, buffer strategy | **High** -- core serialization logic, many code paths |
| `src/parser.rs` | String scanning optimization, key caching, container pre-allocation | **Medium** -- core parsing logic, but mostly additive |
| `src/tables.rs` | Potentially add SIMD lookup tables, escape tables | **Low** -- data-only |
| `src/error.rs` | No changes expected | **None** |

**Python API surface that must remain stable:**
- `rdn._native.parse(text: str) -> Any`
- `rdn._native.stringify(value, *, ensure_ascii, check_circular, sort_keys, indent, separators) -> str`
- `rdn.__init__._USE_NATIVE` flag behavior
- All exception types and messages (RDNDecodeError with msg, doc, pos, lineno, colno)
- Output parity between native and pure Python for all inputs

## 7. Platform Considerations

### macOS ARM64 (Apple Silicon)
- **No SSE2 or AVX2** -- x86-only instruction sets
- **Has NEON** (ARM's SIMD, 128-bit registers, roughly equivalent to SSE2)
- PyO3/maturin builds universal2 wheels by default on macOS
- `#[cfg(target_arch = "aarch64")]` for NEON paths
- `std::arch::aarch64::*` intrinsics available in Rust nightly; stable alternatives via the `std::simd` portable SIMD API (nightly) or the `packed_simd2` crate

### Linux x86_64
- **SSE2 guaranteed** on all x86_64 processors (baseline)
- **AVX2 available** on most modern processors (Haswell+, 2013)
- `#[cfg(target_arch = "x86_64")]` for SSE2/AVX2 paths
- `std::arch::x86_64::*` intrinsics for SIMD

### SIMD Feature Detection
- **Compile-time**: Use `#[cfg(target_arch = "...")]` for platform selection
- **Runtime**: Use `std::is_x86_feature_detected!("avx2")` for optional AVX2
- **Fallback**: Always provide a scalar fallback path
- For a first implementation, SSE2 (x86_64) + scalar fallback (everything else including ARM) is the pragmatic choice. NEON can be added later.
- The `simdutf8` crate handles cross-platform SIMD internally and could be leveraged.

### Cross-Compilation with maturin
- maturin handles cross-compilation for different platforms when building wheels
- SIMD intrinsics must be gated behind `#[cfg()]` to compile on all targets
- `target-cpu=native` in `.cargo/config.toml` would optimize for the build machine but break cross-compilation -- should only be used in benchmark-specific builds, not in distributed wheels

## 8. Edge Cases & Risks

### PyO3 Version Compatibility
- Current: PyO3 0.23.5
- PyO3 0.23.x uses the `Bound<'py, T>` API (introduced in 0.21, required since 0.22)
- Cached type pointer optimization requires accessing `ffi::PyObject` type pointers -- this is supported in PyO3 0.23.x via `obj.as_ptr()` and `ffi::Py_TYPE(ptr)`
- No compatibility risk as long as we stay on PyO3 0.23.x

### Python Version Support
- Must support Python 3.10+ (per pyproject.toml classifiers: 3.10, 3.11, 3.12, 3.13)
- `ob_type` pointer layout is stable across these versions
- No known issues with SIMD or FFI on any supported version

### Cached Type Pointer Invalidation
- Python type objects (`ob_type` pointers) for built-in types (`str`, `int`, `bool`, `float`, `list`, `dict`, `tuple`, `set`, `frozenset`, `bytes`, `NoneType`) are **immortal singletons** in CPython -- they never move or change address during a process lifetime.
- For module-level types (`datetime`, `time`, `timedelta`, `re.Pattern`), the type objects are also stable once the module is imported.
- **Risk**: If a Python subclass of `datetime` (etc.) is passed, pointer comparison would miss it. The current `is_instance()` check handles subclasses. **Mitigation**: Use pointer comparison as a fast check, fall through to `is_instance()` on miss. This gives O(1) for the common case and correct behavior for subclasses.

### SIMD Fallback Correctness
- SIMD string scanning must produce byte-identical results to the scalar path.
- Edge cases: strings shorter than 16 bytes (SIMD register width), strings ending mid-register, UTF-8 multi-byte sequences spanning SIMD boundaries.
- **Risk**: Off-by-one errors in SIMD boundary handling. **Mitigation**: Run the full conformance test suite with SIMD enabled and disabled; add fuzzing tests for string scanning.

### `panic = "abort"` Implications
- With `panic = "abort"`, any panic (including `unwrap()` on `None`/`Err`) terminates the entire Python process instead of unwinding.
- Current `unwrap()` locations in the codebase:
  - `parser.rs` line 85: `from_utf8(expected).unwrap()` -- safe, `expected` is always valid ASCII literal
  - `parser.rs` line 193: `char::from_u32(codepoint).unwrap()` -- safe, codepoint is computed from valid surrogate pair
  - `parser.rs` line 204: `char::from_u32(code as u32).unwrap()` -- safe, `code` is validated to not be a surrogate
  - `parser.rs` line 328: `into_pyobject(self.py).unwrap()` -- safe, integer conversion always succeeds
  - `serializer.rs` line 111: `from_utf8(esc).unwrap()` -- safe, escape sequences are ASCII
  - `serializer.rs` line 343: `into_pyobject(self.py).unwrap()` (twice, for `true`/`false`) -- safe
  - `serializer.rs` line 354/359: `format!` calls -- never panic
- **Assessment**: All current `unwrap()` calls are provably safe. `panic = "abort"` is acceptable.

### Key Cache Memory Pressure
- PRD specifies up to 2,048 cached entries.
- Each entry: a `PyString` key (Python object, ~50-100 bytes overhead) + hash (8 bytes) + pointer (8 bytes).
- Worst case: ~2,048 * ~120 bytes = ~240KB per cache instance.
- The cache lives for the duration of a single `parse()` call and is dropped after.
- **Risk**: Low. 240KB is negligible. If parsing streaming data, each call creates a fresh cache.

## 9. Implementation Constraints

### What must NOT change
- **Public API signatures**: `parse(text: str) -> Any` and `stringify(value, *, ensure_ascii, check_circular, sort_keys, indent, separators) -> str`
- **Test behavior**: All pytest tests and conformance tests must pass unchanged
- **Hook routing**: The `_USE_NATIVE` flag and the routing logic in `rdn/__init__.py` must remain as-is. Native handles no-hooks; hooks fall through to pure Python.
- **Exception types**: `RDNDecodeError` with `msg`, `doc`, `pos`, `lineno`, `colno` attributes
- **Output parity**: `rdn._native.stringify(x)` must produce identical output to `rdn._serializer.stringify(x)` for all inputs

### PyO3 API Boundaries
- Must use `Python<'py>` GIL token for all Python object creation
- Must return `PyResult<PyObject>` from parse, `PyResult<PyObject>` (wrapping `PyString`) from stringify
- Cannot hold `Python<'py>` across threads (GIL is single-threaded)
- `Bound<'py, T>` references are lifetime-scoped to GIL acquisition

### Where `unsafe` Code is Acceptable
- **SIMD intrinsics**: Inherently unsafe but well-audited patterns exist (orjson, simd-json)
- **FFI type pointer comparison**: `ffi::Py_TYPE(ptr)` is safe to call but returns a raw pointer -- comparison is safe, dereferencing requires care
- **`PyBytes` direct buffer writing**: Creating a `PyBytesObject` and writing directly to its buffer is unsafe but is a known pattern (orjson does this)
- **NOT acceptable**: Bypassing PyO3's GIL safety, unsound lifetime extensions, transmuting Python objects

## 10. Open Questions — Resolved

1. **SIMD scope for Tier 3**: **Decision: SSE2 + NEON + scalar fallback.** Full SIMD coverage from day one. Implement SSE2 for x86_64, NEON for ARM64 (macOS Apple Silicon), and scalar fallback for all other architectures.

2. **Float formatting precision**: **Decision: Use `ryu`, accept minor formatting differences.** Both representations are mathematically equivalent. Update tests if formatting differs from Python `repr()`.

3. **Key cache scope**: **Decision: Module-level cache** persisted across `parse()` calls with an eviction policy. Better amortization for repeated parses of similar schemas (e.g., API responses). Requires thread-safety consideration (the GIL protects us in CPython, but document the assumption).

4. **Benchmark methodology**: **Decision: Add pytest-benchmark to CI** with minimum throughput thresholds. Catches regressions automatically.

5. **PyBytes output buffer (Tier 3)**: **Decision: Write to a byte buffer, construct `PyUnicode` via `PyUnicode_FromStringAndSize`.** Avoids intermediate Rust `String` copy. Requires `unsafe` FFI but is a well-known pattern.

6. **Tier ordering**: **Decision: Measure per-tier.** Implement all of Tier 1, measure, then Tier 2, measure, then Tier 3. Good balance of delivery speed and insight.
