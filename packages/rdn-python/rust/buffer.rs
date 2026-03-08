/// Byte buffer for serializer output. Accumulates UTF-8 bytes
/// and converts to a Python unicode string at the end.
///
/// This avoids repeated `String` allocations in the serializer by writing
/// directly into a single `Vec<u8>` and producing the final `PyString` via
/// `PyUnicode_FromStringAndSize` — the same pattern used by orjson.
use pyo3::ffi;
use pyo3::prelude::*;
use std::os::raw::c_char;

pub struct WriteBuffer {
    buf: Vec<u8>,
}

impl WriteBuffer {
    /// Create a new buffer with the given initial capacity.
    pub fn with_capacity(cap: usize) -> Self {
        Self { buf: Vec::with_capacity(cap) }
    }

    /// Write a single byte.
    #[inline(always)]
    pub fn write_byte(&mut self, b: u8) {
        self.buf.push(b);
    }

    /// Write a byte slice.
    #[inline(always)]
    pub fn write_bytes(&mut self, bytes: &[u8]) {
        self.buf.extend_from_slice(bytes);
    }

    /// Write a UTF-8 string.
    #[inline(always)]
    pub fn write_str(&mut self, s: &str) {
        self.buf.extend_from_slice(s.as_bytes());
    }

    /// Write a u32 formatted as decimal (using itoa).
    #[inline]
    pub fn write_u32(&mut self, v: u32) {
        let mut buf = itoa::Buffer::new();
        let s = buf.format(v);
        self.buf.extend_from_slice(s.as_bytes());
    }

    /// Write an i64 formatted as decimal (using itoa).
    #[inline]
    pub fn write_i64(&mut self, v: i64) {
        let mut buf = itoa::Buffer::new();
        let s = buf.format(v);
        self.buf.extend_from_slice(s.as_bytes());
    }

    /// Write an f64 formatted with ryu (shortest representation).
    #[inline]
    pub fn write_f64(&mut self, v: f64) {
        let mut buf = ryu::Buffer::new();
        let s = buf.format(v);
        self.buf.extend_from_slice(s.as_bytes());
    }

    /// Get the current length.
    #[inline]
    pub fn len(&self) -> usize {
        self.buf.len()
    }

    /// Check if the buffer is empty.
    #[inline]
    pub fn is_empty(&self) -> bool {
        self.buf.is_empty()
    }

    /// Consume the buffer and create a PyString via PyUnicode_FromStringAndSize.
    /// The buffer contents MUST be valid UTF-8 (guaranteed by our serializer).
    pub fn into_py_string(self, py: Python<'_>) -> PyResult<PyObject> {
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_write_byte() {
        let mut buf = WriteBuffer::with_capacity(16);
        buf.write_byte(b'H');
        buf.write_byte(b'i');
        assert_eq!(&buf.buf, b"Hi");
    }

    #[test]
    fn test_write_str() {
        let mut buf = WriteBuffer::with_capacity(32);
        buf.write_str("hello");
        buf.write_str(" world");
        assert_eq!(&buf.buf, b"hello world");
    }

    #[test]
    fn test_write_bytes() {
        let mut buf = WriteBuffer::with_capacity(16);
        buf.write_bytes(b"abc");
        buf.write_bytes(b"def");
        assert_eq!(&buf.buf, b"abcdef");
    }

    #[test]
    fn test_write_i64() {
        let mut buf = WriteBuffer::with_capacity(32);
        buf.write_i64(42);
        assert_eq!(&buf.buf, b"42");

        let mut buf = WriteBuffer::with_capacity(32);
        buf.write_i64(-9007199254740991);
        assert_eq!(&buf.buf, b"-9007199254740991");

        let mut buf = WriteBuffer::with_capacity(32);
        buf.write_i64(0);
        assert_eq!(&buf.buf, b"0");
    }

    #[test]
    fn test_write_u32() {
        let mut buf = WriteBuffer::with_capacity(16);
        buf.write_u32(12345);
        assert_eq!(&buf.buf, b"12345");

        let mut buf = WriteBuffer::with_capacity(16);
        buf.write_u32(0);
        assert_eq!(&buf.buf, b"0");
    }

    #[test]
    fn test_write_f64() {
        let mut buf = WriteBuffer::with_capacity(32);
        buf.write_f64(3.14);
        let s = std::str::from_utf8(&buf.buf).unwrap();
        let parsed: f64 = s.parse().unwrap();
        assert_eq!(parsed, 3.14);

        let mut buf = WriteBuffer::with_capacity(32);
        buf.write_f64(0.0);
        assert_eq!(&buf.buf, b"0.0");

        let mut buf = WriteBuffer::with_capacity(32);
        buf.write_f64(-1.5);
        assert_eq!(&buf.buf, b"-1.5");
    }

    #[test]
    fn test_with_capacity() {
        let buf = WriteBuffer::with_capacity(1024);
        assert!(buf.buf.capacity() >= 1024);
        assert_eq!(buf.len(), 0);
    }

    #[test]
    fn test_empty() {
        let buf = WriteBuffer::with_capacity(16);
        assert!(buf.is_empty());
        assert_eq!(buf.len(), 0);

        let mut buf = WriteBuffer::with_capacity(16);
        buf.write_byte(b'x');
        assert!(!buf.is_empty());
        assert_eq!(buf.len(), 1);
    }

    #[test]
    fn test_mixed_writes() {
        let mut buf = WriteBuffer::with_capacity(64);
        buf.write_byte(b'[');
        buf.write_i64(42);
        buf.write_str(",");
        buf.write_f64(3.14);
        buf.write_byte(b']');

        let s = std::str::from_utf8(&buf.buf).unwrap();
        assert!(s.starts_with("[42,"));
        assert!(s.ends_with(']'));
    }
}
