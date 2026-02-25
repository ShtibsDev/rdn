/// Error handling for the RDN native extension.
///
/// Raises `rdn.exceptions.RDNDecodeError` (imported from the pure-Python package)
/// so that error types are identical regardless of which code path is used.
use pyo3::prelude::*;
use pyo3::types::PyString;

/// Convert a byte offset in a UTF-8 string to a character (code point) offset.
/// This is needed because the Python `RDNDecodeError` reports character positions,
/// but the Rust parser works on byte offsets.
#[inline]
pub fn byte_offset_to_char_offset(source: &str, byte_pos: usize) -> usize {
    // Count chars up to byte_pos
    source[..byte_pos.min(source.len())].chars().count()
}

/// Raise an `RDNDecodeError` with the given message, source document, and byte position.
/// The byte position is converted to a character offset for parity with the pure Python parser.
#[cold]
#[inline(never)]
pub fn raise_decode_error(py: Python<'_>, msg: &str, source: &str, byte_pos: usize) -> PyErr {
    let char_pos = byte_offset_to_char_offset(source, byte_pos);

    // Import and call RDNDecodeError(msg, doc, pos)
    match py.import("rdn.exceptions") {
        Ok(module) => {
            match module.getattr("RDNDecodeError") {
                Ok(cls) => {
                    let py_msg = PyString::new(py, msg);
                    let py_doc = PyString::new(py, source);
                    match cls.call1((py_msg, py_doc, char_pos)) {
                        Ok(exc) => PyErr::from_value(exc.into_any()),
                        Err(e) => e,
                    }
                }
                Err(e) => e,
            }
        }
        Err(_) => {
            // Fallback: if we can't import RDNDecodeError, raise a plain ValueError
            PyErr::new::<pyo3::exceptions::PyValueError, _>(format!(
                "{} in RDN at position {}",
                msg, char_pos
            ))
        }
    }
}
