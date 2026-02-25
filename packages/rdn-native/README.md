# rdn-native

Native Rust extension for the [RDN](https://github.com/nicholasgasior/rdn) Python package, built with [PyO3](https://pyo3.rs) and [maturin](https://www.maturin.rs).

## Installation

```bash
pip install rdn-native
```

When installed alongside `rdn`, hot-path calls (without hooks/callbacks) are automatically routed to the native implementation for significantly better performance.

## How it works

- `pip install rdn` = pure Python (works everywhere)
- `pip install rdn-native` = adds the compiled native extension

The native extension handles calls **without hooks/callbacks**. When hooks are provided (`parse_int`, `object_hook`, `default`, etc.), execution falls through to the pure Python implementation automatically.

## Performance Optimizations

The native extension applies three tiers of optimization beyond the baseline PyO3 implementation:

### Tier 1: Build and Low-Hanging Fruit
- **Release profile**: `opt-level = 3`, `lto = "fat"`, `codegen-units = 1`, `panic = "abort"` for maximum compiler optimization
- **itoa**: Fast integer-to-string conversion (replaces `format!`)
- **ryu**: Fast float-to-string conversion (replaces Python `repr()`-style formatting)
- **Hot/cold path annotations**: `#[inline(always)]` / `#[cold]` on critical functions
- **Empty collection fast-paths**: Short-circuit serialization of `[]`, `{}`, `()`, `Set{}`

### Tier 2: Type Dispatch and Caching
- **TypeCache** (`cache.rs`): Stores raw `*mut PyTypeObject` pointers for 16 Python types used in the serializer's type-dispatch loop, avoiding repeated `isinstance()` calls
- **KeyCache** (`cache.rs`): xxhash-based, fixed-size (2048-slot) string-interning cache that reuses `PyObject` string keys during parsing instead of allocating new ones for every object key
- **Bit-packed serializer state**: Compact state representation for the serializer

### Tier 3: SIMD and Buffer
- **SIMD string scanning** (`simd.rs`): Accelerated `find_string_end()` (parser) and `needs_escape()` (serializer) using platform-specific SIMD intrinsics:
  - **SSE2** on x86_64: 16-byte vectorized scanning with `_mm_cmpeq_epi8` / `_mm_movemask_epi8`
  - **NEON** on aarch64: 16-byte vectorized scanning with `vceqq_u8` / `vmaxvq_u8`
  - **Scalar fallback** on all other architectures
- **WriteBuffer** (`buffer.rs`): Accumulates UTF-8 bytes directly into a `Vec<u8>` and produces the final `PyString` via `PyUnicode_FromStringAndSize`, avoiding repeated `String` allocations (same pattern used by orjson)

### Modules

| Module | Purpose |
|--------|---------|
| `lib.rs` | PyO3 module entry point, `parse()` / `stringify()` exports |
| `parser.rs` | Recursive-descent parser with O(1) dispatch table |
| `serializer.rs` | Type-dispatch serializer with cycle detection |
| `simd.rs` | SIMD-accelerated string scanning (SSE2, NEON, scalar fallback) |
| `cache.rs` | TypeCache (type pointer caching) and KeyCache (string interning) |
| `buffer.rs` | WriteBuffer for direct-to-buffer serialization output |
| `tables.rs` | Lookup tables for parser dispatch |
| `error.rs` | Error types and formatting |

### Dependencies

| Crate | Purpose |
|-------|---------|
| `pyo3` | Python-Rust bindings |
| `itoa` | Fast integer formatting |
| `ryu` | Fast float formatting |
| `xxhash-rust` | xxh3 hashing for KeyCache |
| `smallvec` | Stack-allocated small vectors for TypeCache refs |

### Float Formatting Note

The native extension uses `ryu` for float-to-string conversion, which may produce slightly different string representations compared to Python's `repr()` in edge cases (e.g., trailing digit differences). All outputs are mathematically equivalent -- they parse back to the identical IEEE 754 value.

## Building from source

Requires a Rust toolchain:

```bash
cd packages/rdn-native
maturin develop
```

For release builds with full optimizations:

```bash
cd packages/rdn-native
maturin develop --release
```

## Testing

```bash
# Build the native extension
cd packages/rdn-native && maturin develop

# Run all existing tests (automatically uses native path)
cd packages/rdn-python && python -m pytest tests/ -x -v

# Verify native is active
python -c "import rdn; print('Native:', rdn._USE_NATIVE)"
```
