# Task 021: Refactor serializer to use WriteBuffer

## Status: pending

## Tier: Tier 3: SIMD & Buffer

## Description
This is the largest refactor in the entire effort. Add a `buf: WriteBuffer` field to the `Serializer` struct and change all serializer methods from returning `String` to writing directly to `self.buf`. Update the public entry point to create the buffer, call `stringify_value()`, then call `buf.into_py_string(py)` to produce the final `PyString`. Update `lib.rs` to use the new `PyObject` return directly.

## Files to Modify
- `packages/rdn-native/src/serializer.rs` — major refactor of all serializer methods
- `packages/rdn-native/src/lib.rs` — update to use `PyObject` return from `stringify()` directly (remove `PyString::new(py, &result)` on line 76)

## Implementation Details
**How the serializer changes**: Currently, every method in the serializer returns a `String`:
- `stringify_value() -> PyResult<String>`
- `escape_string() -> String`
- `format_container() -> String`
- `format_datetime() -> PyResult<String>`
- etc.

After this change, all methods write directly to `self.buf: WriteBuffer`:
- `stringify_value(&mut self, value) -> PyResult<()>` -- writes to `self.buf`
- `escape_string(&mut self, s: &str)` -- writes to `self.buf`
- `format_container_open/close(&mut self, ...)` -- writes delimiters to `self.buf`
- `format_datetime(&mut self, obj) -> PyResult<()>` -- writes to `self.buf`
- etc.

The public entry point `stringify()` creates the buffer, calls `stringify_value()`, then calls `buf.into_py_string(py)` to produce the final `PyString`.

**Method-by-method refactor**:

1. **`escape_string()`**: `-> String` becomes `(&mut self, s: &str)` writing to `self.buf`.
   ```rust
   // Before:
   fn escape_string(&self, s: &str) -> String {
       let mut result = String::with_capacity(...);
       result.push('"');
       result.push_str(s);
       result.push('"');
       result
   }
   // After:
   fn escape_string(&mut self, s: &str) {
       self.buf.write_byte(b'"');
       self.buf.write_str(s);
       self.buf.write_byte(b'"');
   }
   ```

2. **`format_container()`**: Remove entirely; inline open/close/separator writing into the container branches of `stringify_value()`.

3. **`format_datetime()`**, **`format_timeonly()`**, **`format_duration()`**: Use `write!()` to `[u8; 64]` stack buffer, then `self.buf.write_bytes()`.
   - `format_datetime()`: Maximum output is 28 bytes (`@YYYY-MM-DDTHH:MM:SS.mmmZ`), well within 64.
   - `format_timeonly()`: Maximum output is 13 bytes (`@HH:MM:SS.mmm`).
   - `format_duration()`: Multiple `write!()` calls for each component. Maximum output ~30 bytes (`@-P999DT23H59M59S`).

4. **`format_regexp()`**: Write `"/"`, then pattern bytes, then `"/"`, then flag chars directly to `self.buf`. No stack buffer needed.

5. **`format_binary()`**: Write base64 characters directly to `self.buf`. No intermediate allocation.

6. **`stringify_value()`**: `-> PyResult<String>` becomes `-> PyResult<()>`.

7. **`stringify()`**: Create `WriteBuffer`, call `stringify_value()`, call `buf.into_py_string()`.

**Final conversion**: `WriteBuffer::into_py_string()` uses `PyUnicode_FromStringAndSize`:
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

**Update `lib.rs`**: Remove the `PyString::new(py, &result)` conversion on line 76. The `stringify()` now returns a `PyObject` directly.

**New tests -- Buffer edge cases** (from tech design Section 8):
- Very large payloads (>1MB) to test buffer growth
- Deeply nested structures (128 levels) to test buffer capacity
- Empty output (serialize `None` -> `"null"`)

## Dependencies
- Depends on: 17, 18, 19, 20
- Blocks: 22

## Acceptance Criteria
- [ ] `Serializer` struct has a `buf: WriteBuffer` field
- [ ] All serializer methods write to `self.buf` instead of returning `String`
- [ ] `escape_string()` writes directly to buffer
- [ ] `format_container()` is removed; delimiters are inlined
- [ ] `format_datetime/timeonly/duration()` use stack buffers + `write!()`
- [ ] `format_regexp()` writes directly to buffer
- [ ] `format_binary()` writes directly to buffer
- [ ] `stringify()` returns `PyObject` via `buf.into_py_string()`
- [ ] `lib.rs` uses the `PyObject` return directly (no `PyString::new()`)
- [ ] No intermediate `String` allocations in the serializer hot path
- [ ] Buffer edge case tests pass (large payloads, deep nesting, empty output)
- [ ] All existing tests pass

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.3.3, Section 6.3.4, Section 8, Section 12 (Task 21)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
