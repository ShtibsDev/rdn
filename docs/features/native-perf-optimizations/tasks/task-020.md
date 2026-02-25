# Task 020: Create buffer.rs with WriteBuffer

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
Create a new `buffer.rs` module containing the `WriteBuffer` struct -- a byte buffer for serializer output that accumulates UTF-8 bytes and converts to a Python unicode string at the end via `PyUnicode_FromStringAndSize`. Implement all buffer write methods and add Rust unit tests.

## Files to Modify
- `packages/rdn-native/src/buffer.rs` — new file with `WriteBuffer` struct
- `packages/rdn-native/src/lib.rs` — add `mod buffer`

## Implementation Details
**WriteBuffer struct** (from tech design Section 5.2):

```rust
/// Byte buffer for serializer output. Accumulates UTF-8 bytes
/// and converts to a Python unicode string at the end.
struct WriteBuffer {
    buf: Vec<u8>,
}

impl WriteBuffer {
    fn with_capacity(cap: usize) -> Self {
        Self { buf: Vec::with_capacity(cap) }
    }

    fn write_byte(&mut self, b: u8) {
        self.buf.push(b);
    }

    fn write_bytes(&mut self, bytes: &[u8]) {
        self.buf.extend_from_slice(bytes);
    }

    fn write_str(&mut self, s: &str) {
        self.buf.extend_from_slice(s.as_bytes());
    }

    /// Write a u32 value formatted as decimal (using itoa)
    fn write_u32(&mut self, v: u32) {
        let mut buf = itoa::Buffer::new();
        let formatted = buf.format(v);
        self.buf.extend_from_slice(formatted.as_bytes());
    }

    /// Write an i64 value formatted as decimal (using itoa)
    fn write_i64(&mut self, v: i64) {
        let mut buf = itoa::Buffer::new();
        let formatted = buf.format(v);
        self.buf.extend_from_slice(formatted.as_bytes());
    }

    /// Write an f64 value formatted with ryu
    fn write_f64(&mut self, v: f64) {
        let mut buf = ryu::Buffer::new();
        let formatted = buf.format(v);
        self.buf.extend_from_slice(formatted.as_bytes());
    }

    /// Consume the buffer and create a PyString via PyUnicode_FromStringAndSize
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
}
```

**Key design points**:
- `Vec<u8>` backing avoids the overhead of `String`'s UTF-8 validation on each push
- `into_py_string()` uses `PyUnicode_FromStringAndSize` which creates a Python unicode object directly from the byte buffer, eliminating the intermediate Rust `String` -> `PyString` copy
- `itoa` and `ryu` integration allows writing formatted numbers directly to the buffer without intermediate allocations

**Rust unit tests**:
- Verify `write_byte()`, `write_bytes()`, `write_str()` produce correct output
- Verify `write_i64()` formats integers correctly (positive, negative, zero, min/max)
- Verify `write_f64()` formats floats correctly (including edge cases like 0.0, -0.0, very large, very small)
- Verify `into_py_string()` produces correct Python strings (requires `#[test]` with PyO3 test harness)
- Verify buffer capacity growth works for large outputs

## Dependencies
- Depends on: 13
- Blocks: 21

## Acceptance Criteria
- [ ] `buffer.rs` exists with `WriteBuffer` struct
- [ ] All write methods implemented (`write_byte`, `write_bytes`, `write_str`, `write_u32`, `write_i64`, `write_f64`)
- [ ] `into_py_string()` uses `PyUnicode_FromStringAndSize`
- [ ] `mod buffer` is declared in `lib.rs`
- [ ] Rust `#[test]` functions verify all write operations
- [ ] `into_py_string()` produces correct Python strings

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 5.2, Section 6.3.3, Section 12 (Task 20)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
