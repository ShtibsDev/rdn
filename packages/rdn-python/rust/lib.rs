mod buffer;
mod cache;
mod error;
mod parser;
mod serializer;
pub(crate) mod simd;
mod tables;

use pyo3::prelude::*;
use pyo3::types::PyString;

use parser::Parser;
use serializer::Serializer;

/// Parse an RDN string and return the corresponding Python value.
///
/// This is the native hot-path equivalent of `rdn._parser.parse()`.
/// Only handles the no-hooks case — when hooks are provided, the caller
/// should fall through to the pure Python implementation.
#[pyfunction]
fn parse(py: Python<'_>, text: &str) -> PyResult<PyObject> {
    let key_cache = cache::take_key_cache().unwrap_or_else(|| cache::KeyCache::new());
    let mut parser = Parser::new(py, text, key_cache)?;
    let result = parser.parse();
    // Always return the cache to the global pool, even on error
    cache::return_key_cache(parser.take_key_cache());
    result
}

/// Serialize a Python value to an RDN-formatted string.
///
/// This is the native hot-path equivalent of `rdn._serializer.stringify()`.
/// Only handles the no-hooks case (no `default` callback).
#[pyfunction]
#[pyo3(signature = (value, *, skipkeys=false, ensure_ascii=true, check_circular=true, allow_nan=true, sort_keys=false, indent=None, separators=None))]
fn stringify(
    py: Python<'_>,
    value: &Bound<'_, PyAny>,
    skipkeys: bool,
    ensure_ascii: bool,
    check_circular: bool,
    allow_nan: bool,
    sort_keys: bool,
    indent: Option<&Bound<'_, PyAny>>,
    separators: Option<&Bound<'_, PyAny>>,
) -> PyResult<PyObject> {
    // Process indent: int → spaces, str → verbatim, None → compact
    let indent_str: Option<String> = match indent {
        Some(v) => {
            if v.is_instance_of::<pyo3::types::PyInt>() {
                let n: usize = v.extract()?;
                Some(" ".repeat(n))
            } else if v.is_instance_of::<PyString>() {
                let s: &str = v.extract()?;
                Some(s.to_string())
            } else if v.is_none() {
                None
            } else {
                None
            }
        }
        None => None,
    };

    // Process separators
    let seps: Option<(String, String)> = match separators {
        Some(v) => {
            if v.is_none() {
                None
            } else {
                let tup = v.downcast::<pyo3::types::PyTuple>()?;
                let is: String = tup.get_item(0)?.extract()?;
                let ks: String = tup.get_item(1)?.extract()?;
                Some((is, ks))
            }
        }
        None => None,
    };

    let seps_ref = seps.as_ref().map(|(a, b)| (a.as_str(), b.as_str()));
    let indent_ref = indent_str.as_deref();

    let mut ser = Serializer::new(py, skipkeys, ensure_ascii, check_circular, allow_nan, sort_keys, indent_ref, seps_ref)?;
    ser.stringify(value)
}

#[pymodule]
fn _native(m: &Bound<'_, PyModule>) -> PyResult<()> {
    m.add_function(wrap_pyfunction!(parse, m)?)?;
    m.add_function(wrap_pyfunction!(stringify, m)?)?;

    // Initialize the global TypeCache (must happen once, under the GIL)
    cache::init_type_cache(m.py())?;

    Ok(())
}
