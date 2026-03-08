# Discovery: python-json-parity

## 1. Overview

Make the `rdn` Python package a complete drop-in replacement for Python's `json` module by closing 6 identified API gaps: `py.typed` marker, `__version__` attribute, CLI tool, `skipkeys` parameter, `allow_nan` parameter, and `parse`/`stringify` aliases.

---

## 2. Current Behavior

### 2.1 Package Structure

```
packages/rdn-python/
├── pyproject.toml              # Build config (maturin backend)
├── Cargo.toml                  # Rust native extension manifest
├── README.md
├── src/rdn/
│   ├── __init__.py             # Main module — exports loads, load, dumps, dump, RDNDecoder, RDNEncoder
│   ├── encoder.py              # RDNEncoder (mirrors json.JSONEncoder)
│   ├── decoder.py              # RDNDecoder (mirrors json.JSONDecoder)
│   ├── exceptions.py           # RDNDecodeError, MAX_SAFE_INTEGER
│   ├── _serializer.py          # Pure Python serializer
│   ├── _parser.py              # Pure Python recursive-descent parser
│   └── _tables.py              # Lookup tables
├── rust/                       # Native extension (PyO3 + maturin)
│   ├── lib.rs, parser.rs, serializer.rs, cache.rs, simd.rs, buffer.rs, tables.rs, error.rs
└── tests/
    ├── test_parse.py           # ~800 lines — parser tests
    ├── test_stringify.py       # ~900 lines — serializer tests
    ├── test_decoder.py         # ~350 lines — RDNDecoder tests
    ├── test_encoder.py         # ~350 lines — RDNEncoder tests
    ├── test_edge_cases.py      # ~600 lines — boundary conditions
    ├── test_file_io.py         # ~250 lines — load/dump file I/O
    ├── test_conformance.py     # ~400 lines — shared test suite runner
    ├── test_native.py          # ~350 lines — native extension tests
    ├── test_benchmark.py       # ~350 lines — benchmarks
    └── bench_compare.py        # ~800 lines — vs json/orjson
```

**Missing files:**
- `src/rdn/py.typed` — PEP 561 marker
- `src/rdn/__main__.py` — CLI tool
- No `__version__` in `__init__.py`

### 2.2 Public API Surface

**`__all__`:** `["loads", "load", "dumps", "dump", "RDNDecoder", "RDNEncoder", "RDNDecodeError", "MAX_SAFE_INTEGER"]`

| Function | Signature | Notes |
|----------|-----------|-------|
| `dumps` | `(obj, *, cls, ensure_ascii, check_circular, indent, separators, default, sort_keys)` | Missing: `skipkeys`, `allow_nan` |
| `dump` | `(obj, fp, **kwargs)` | Delegates to `dumps` |
| `loads` | `(s, *, cls, object_hook, parse_float, parse_int, parse_bigint, parse_datetime, parse_timeonly, parse_duration, parse_regexp, parse_binary, object_pairs_hook)` | Full hook support |
| `load` | `(fp, **kwargs)` | Delegates to `loads` |

**Classes:** `RDNEncoder` (encode, iterencode, default), `RDNDecoder` (decode, raw_decode)

### 2.3 Serializer Details (`_serializer.py`)

**Dict key handling (lines 318-338):**
- Only `str` keys allowed; raises `TypeError: "Object key must be a string, got {type}"` for non-string keys
- No `skipkeys` support — always errors on non-string keys

**NaN/Infinity handling (lines 267-273):**
- Always serializes: `float('nan')` → `NaN`, `float('inf')` → `Infinity`, `float('-inf')` → `-Infinity`
- No `allow_nan` parameter — NaN/Infinity are native RDN types, always serialized

**Other features:** `ensure_ascii`, `check_circular` (cycle detection via `set[int]` of object IDs), `indent` (int→spaces, str→verbatim), `separators`, `sort_keys`, `default` callback — all working.

### 2.4 Parser Details (`_parser.py`)

- Recursive-descent parser with O(1) dispatch table
- `MAX_DEPTH = 128`
- `raw_parse(s, idx=0, ...)` → `(value, end_position)` for partial parsing
- Hook routing: all hooks passed as parameters, called inline during parsing
- Global state for cursor (`_source`, `_pos`, `_len`, `_depth`) — thread-unsafe

### 2.5 Native Extension

**Hot-path routing in `__init__.py`:**
- **Parse:** Native when `_USE_NATIVE=True` AND all hooks are `None`
- **Stringify:** Native when `_USE_NATIVE=True` AND `default is None`

**Rust serializer (`rust/serializer.rs`):**
- Supports: `ensure_ascii`, `check_circular`, `sort_keys`, `indent`, `separators`
- Does NOT support: `skipkeys`, `allow_nan`, `default` callback
- New parameters (`skipkeys`, `allow_nan`) must be added to both pure Python AND native Rust paths

---

## 3. Test Coverage

**Well tested:** All value types, hooks, encoder/decoder classes, circular refs, conformance suite, native extension, file I/O

**Not tested (gaps corresponding to PRD items):**
- `__version__` attribute
- `py.typed` marker existence
- CLI tool (`python -m rdn`)
- `skipkeys` parameter
- `allow_nan` parameter
- `parse`/`stringify` aliases

---

## 4. BI Events / Metrics

N/A — library package with no telemetry.

---

## 5. Configuration

- **Build system:** maturin (compiles Rust extension automatically during `pip install`)
- **Version:** Hardcoded `"0.1.0"` in `pyproject.toml` line 3 and `Cargo.toml`
- **Python support:** `>=3.10` (importlib.metadata available in all supported versions)
- **No runtime dependencies** — only dev deps: pytest, pytest-benchmark, mypy

---

## 6. Cross-Implementation Reference

**TypeScript (`packages/rdn-js/`):** Exports `parse`, `stringify` (NOT `loads`/`dumps`). No class-based API, no CLI tool, no `__version__`.

**Naming convention:** All non-Python RDN implementations use `parse`/`stringify`. Python uses `loads`/`dumps` for json-module parity.

---

## 7. Blast Radius

| Change | Risk | Impact |
|--------|------|--------|
| `py.typed` | None | Additive — new marker file |
| `__version__` | None | Additive — new attribute |
| CLI tool | None | Additive — new `__main__.py` |
| `skipkeys` | None | Additive — new parameter with `False` default (preserves current behavior) |
| `allow_nan` | Low | New parameter; if implemented as functional (raises when `False`), could surprise users who set it |
| `parse`/`stringify` aliases | None | Additive — new exports aliasing existing functions |

**Ecosystem packages:** `rdn-pydantic` and `rdn-fastapi` are unaffected (they use `loads`/`dumps`).

---

## 8. Edge Cases

1. **`skipkeys` with nested dicts:** Must recurse — skip non-string keys at every level, not just top-level
2. **`skipkeys` + empty dict after skipping:** If all keys are skipped, result should be `{}`
3. **`allow_nan=False` + nested NaN:** Must detect NaN/Infinity at any nesting depth (inside lists, dicts, etc.)
4. **`allow_nan` + native extension:** Rust serializer needs the parameter too, or must fall back to pure Python
5. **`__version__` in editable installs:** `importlib.metadata.version("rdn")` works with `pip install -e .` via maturin
6. **CLI stdin encoding:** Must handle UTF-8 BOM and binary stdin gracefully

---

## 9. Dependencies

No new runtime dependencies needed. All features use stdlib:
- `importlib.metadata` for `__version__`
- `argparse` + `sys` for CLI tool

---

## 10. Open Questions — RESOLVED

1. **`skipkeys` semantics:** Silently skip non-string keys (match `json` exactly). **DECIDED.**

2. **`allow_nan` semantics:** Raise `ValueError` when `allow_nan=False` and NaN/Infinity encountered (match `json`). **DECIDED.**

3. **`parse`/`stringify` aliases:** Add to `__all__` as public aliases. **DECIDED.**

4. **CLI tool scope:** Validate + pretty-print only (match `json.tool`). Extend later if needed. **DECIDED.**

5. **`skipkeys` + `allow_nan` in native extension:** Implement in Rust too for consistent performance. **DECIDED.**
