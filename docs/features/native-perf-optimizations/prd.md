# PRD: rdn-native Performance Optimizations

## Overview
Apply performance optimizations inspired by orjson's Rust implementation to `rdn-native` (packages/rdn-native), the Rust+PyO3 native extension for RDN parsing and serialization in Python.

## Goals
1. **Minimize parse latency** — reduce per-value overhead in the recursive-descent parser
2. **Minimize serialize latency** — reduce allocation and formatting overhead in the serializer
3. **Maintain correctness** — all existing conformance tests must continue to pass
4. **Maintain API compatibility** — no changes to the public `rdn.parse()` / `rdn.stringify()` signatures

## Non-Goals
- Switching away from PyO3 to raw `pyo3-ffi` (too large a rewrite; incremental FFI use is in-scope)
- Adding new RDN types or features
- Changing the pure-Python fallback implementation

## Success Metrics
- **Parse throughput**: ≥30% improvement on medium/large payloads (measured via existing `bench.py`)
- **Serialize throughput**: ≥30% improvement on medium/large payloads
- **No regressions**: 100% conformance test pass rate

## Optimizations (Priority Order)

### Tier 1 — Build & Low-Hanging Fruit
1. **Cargo release profile**: `codegen-units = 1`, `lto = "fat"`, `panic = "abort"`, `opt-level = 3`
2. **Integer formatting**: Replace `i64.to_string()` with `itoa` crate (direct-to-buffer, zero alloc)
3. **Float formatting**: Replace Python `repr()` call with `ryu` crate (shortest round-trip, zero alloc)
4. **Hot/cold path separation**: `#[cold]` on rare serializer types (datetime, regex, binary), `#[inline(never)]` on error paths
5. **Empty collection fast-paths**: Detect `[]`, `{}`, `()` immediately without entering container logic

### Tier 2 — Type Dispatch & Caching
6. **Cached type pointers**: Cache `ob_type` pointers at module init for str, int, bool, float, list, dict, tuple, set, frozenset, bytes, None — use pointer comparison instead of `is_instance_of`
7. **Dictionary key caching**: Hash-based cache (≤2048 entries) for repeated dict keys during parsing — reuse `PyString` objects via `Py_INCREF`
8. **Bit-packed serializer state**: Pack options + depth into a single `u32`

### Tier 3 — SIMD & Buffer
9. **SIMD string scanning (parse)**: SSE2 16-byte stride for finding closing `"` and detecting backslash escapes
10. **SIMD string escaping (serialize)**: SSE2 16-byte stride for detecting characters needing escaping
11. **Direct PyBytes output buffer**: Write serializer output directly into a `PyBytesObject` to avoid final copy
12. **Stack buffers for formatting**: 64-byte stack buffer for datetime/duration/regex formatting instead of `format!()`

## Constraints
- Must support Python 3.10+ (no Python 3.12-only features required)
- Must compile on macOS (ARM64/Apple Silicon) and Linux (x86_64) — SIMD tiers with fallback
- Must not break the hot-path routing in `rdn.__init__` (native calls without hooks → native, with hooks → pure Python)

## Testing
- All existing `pytest` tests must pass
- Run `bench.py` before and after each tier for regression detection
- Add a micro-benchmark comparing before/after on string-heavy, number-heavy, and nested-object payloads
