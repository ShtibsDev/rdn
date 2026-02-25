/// RDN serializer that reads Python objects and produces RDN text.
///
/// Type dispatch uses cached `*mut ffi::PyTypeObject` pointer comparison via
/// `cache::get_type_cache()` for O(1) type checks on the hot path.
/// Bool-before-int order is critical since `isinstance(True, int)` is True.
/// Cycle detection via `HashSet<usize>` of `obj.as_ptr()`.
///
/// Output is accumulated in a `WriteBuffer` (single `Vec<u8>`) and converted
/// to a Python unicode string at the end via `PyUnicode_FromStringAndSize`.
use std::collections::HashSet;

use pyo3::ffi;
use pyo3::prelude::*;
use pyo3::types::{PyBool, PyByteArray, PyBytes, PyDict, PyFloat, PyFrozenSet, PyInt, PyList, PySet, PyString, PyTuple};

use crate::buffer::WriteBuffer;
use crate::cache;
use crate::tables::{B64_ENCODE, escape_byte};

/// 2^53 - 1 — JavaScript Number.MAX_SAFE_INTEGER
const MAX_SAFE_INTEGER: i64 = 9007199254740991;

// Bit-packed serializer state:
//   bits 0-6  = recursion depth (0-127)
//   bit  7    = ensure_ascii flag
//   bit  8    = check_circular flag
//   bit  9    = sort_keys flag
const STATE_DEPTH_MASK: u32 = 0x7F;
const STATE_ASCII_BIT: u32 = 0x80;
const STATE_CIRCULAR_BIT: u32 = 0x100;
const STATE_SORT_BIT: u32 = 0x200;

pub struct Serializer<'py> {
    py: Python<'py>,
    state: u32,
    indent_str: Option<String>,
    item_sep: String,
    key_sep: String,
    seen: HashSet<usize>,
    buf: WriteBuffer,
}

impl<'py> Serializer<'py> {
    pub fn new(
        py: Python<'py>,
        ensure_ascii: bool,
        check_circular: bool,
        sort_keys: bool,
        indent: Option<&str>,
        separators: Option<(&str, &str)>,
    ) -> PyResult<Self> {
        let indent_str = indent.map(|s| s.to_string());

        let (item_sep, key_sep) = match separators {
            Some((is, ks)) => (is.to_string(), ks.to_string()),
            None => {
                if indent.is_some() {
                    (",".to_string(), ": ".to_string())
                } else {
                    (",".to_string(), ":".to_string())
                }
            }
        };

        let mut state: u32 = 0;
        if ensure_ascii { state |= STATE_ASCII_BIT; }
        if check_circular { state |= STATE_CIRCULAR_BIT; }
        if sort_keys { state |= STATE_SORT_BIT; }

        Ok(Serializer { py, state, indent_str, item_sep, key_sep, seen: HashSet::new(), buf: WriteBuffer::with_capacity(256) })
    }

    // -----------------------------------------------------------------------
    // String escaping — writes directly to self.buf
    // -----------------------------------------------------------------------

    fn escape_string(&mut self, s: &str) {
        let ensure_ascii = self.state & STATE_ASCII_BIT != 0;
        match crate::simd::needs_escape(s.as_bytes(), ensure_ascii) {
            None => {
                // Fast path — no escaping needed
                self.buf.write_byte(b'"');
                self.buf.write_str(s);
                self.buf.write_byte(b'"');
            }
            Some(first_escape_pos) => {
                // Slow path — we know exactly where the first escape is
                self.buf.write_byte(b'"');
                // Copy the safe prefix in bulk
                self.buf.write_str(&s[..first_escape_pos]);
                // Process the rest character by character
                for ch in s[first_escape_pos..].chars() {
                    let cp = ch as u32;

                    if cp < 0x100 {
                        let esc = escape_byte(cp as u8);
                        if !esc.is_empty() {
                            self.buf.write_bytes(esc);
                            continue;
                        }
                    }

                    if ensure_ascii && cp > 0x7F {
                        if cp <= 0xFFFF {
                            let mut ubuf = [0u8; 6];
                            let len = {
                                use std::io::Write;
                                let mut c = std::io::Cursor::new(&mut ubuf[..]);
                                write!(c, "\\u{:04x}", cp).unwrap();
                                c.position() as usize
                            };
                            self.buf.write_bytes(&ubuf[..len]);
                        } else {
                            // UTF-16 surrogate pair
                            let high = 0xD800 + ((cp - 0x10000) >> 10);
                            let low = 0xDC00 + ((cp - 0x10000) & 0x3FF);
                            let mut ubuf = [0u8; 12];
                            let len = {
                                use std::io::Write;
                                let mut c = std::io::Cursor::new(&mut ubuf[..]);
                                write!(c, "\\u{:04x}\\u{:04x}", high, low).unwrap();
                                c.position() as usize
                            };
                            self.buf.write_bytes(&ubuf[..len]);
                        }
                        continue;
                    }

                    // Write the char's UTF-8 bytes directly
                    let mut char_buf = [0u8; 4];
                    let encoded = ch.encode_utf8(&mut char_buf);
                    self.buf.write_bytes(encoded.as_bytes());
                }
                self.buf.write_byte(b'"');
            }
        }
    }

    // -----------------------------------------------------------------------
    // Cycle detection
    // -----------------------------------------------------------------------

    fn check_cycle(&mut self, obj: &Bound<'py, PyAny>) -> PyResult<()> {
        let ptr = obj.as_ptr() as usize;
        if self.seen.contains(&ptr) {
            return Err(pyo3::exceptions::PyValueError::new_err("Converting circular structure to RDN"));
        }
        self.seen.insert(ptr);
        Ok(())
    }

    fn remove_cycle(&mut self, obj: &Bound<'py, PyAny>) {
        self.seen.remove(&(obj.as_ptr() as usize));
    }

    // -----------------------------------------------------------------------
    // Indentation helper
    // -----------------------------------------------------------------------

    fn write_indent(&mut self, depth: usize) {
        if let Some(ref indent) = self.indent_str {
            self.buf.write_byte(b'\n');
            for _ in 0..depth {
                self.buf.write_str(indent);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Extended type formatting — writes directly to self.buf
    // -----------------------------------------------------------------------

    #[cold]
    #[inline(never)]
    fn format_datetime(&mut self, obj: &Bound<'py, PyAny>) -> PyResult<()> {
        let tz = obj.getattr("tzinfo")?;
        let obj = if !tz.is_none() {
            // Check if it's already UTC
            let py_utc = self.py.import("datetime")?.getattr("timezone")?.getattr("utc")?;
            if !tz.eq(&py_utc)? {
                obj.call_method1("astimezone", (&py_utc,))?
            } else {
                obj.clone()
            }
        } else {
            obj.clone()
        };

        let year: i32 = obj.getattr("year")?.extract()?;
        let month: u32 = obj.getattr("month")?.extract()?;
        let day: u32 = obj.getattr("day")?.extract()?;
        let hour: u32 = obj.getattr("hour")?.extract()?;
        let minute: u32 = obj.getattr("minute")?.extract()?;
        let second: u32 = obj.getattr("second")?.extract()?;
        let microsecond: u32 = obj.getattr("microsecond")?.extract()?;
        let ms = microsecond / 1000;

        let mut stack_buf = [0u8; 64];
        let len = {
            use std::io::Write;
            let mut cursor = std::io::Cursor::new(&mut stack_buf[..]);
            write!(cursor, "@{:04}-{:02}-{:02}T{:02}:{:02}:{:02}.{:03}Z", year, month, day, hour, minute, second, ms).unwrap();
            cursor.position() as usize
        };
        self.buf.write_bytes(&stack_buf[..len]);
        Ok(())
    }

    #[cold]
    #[inline(never)]
    fn format_timeonly(&mut self, obj: &Bound<'py, PyAny>) -> PyResult<()> {
        let hour: u32 = obj.getattr("hour")?.extract()?;
        let minute: u32 = obj.getattr("minute")?.extract()?;
        let second: u32 = obj.getattr("second")?.extract()?;
        let microsecond: u32 = obj.getattr("microsecond")?.extract()?;
        let ms = microsecond / 1000;

        let mut stack_buf = [0u8; 64];
        let len = if ms > 0 {
            use std::io::Write;
            let mut cursor = std::io::Cursor::new(&mut stack_buf[..]);
            write!(cursor, "@{:02}:{:02}:{:02}.{:03}", hour, minute, second, ms).unwrap();
            cursor.position() as usize
        } else {
            use std::io::Write;
            let mut cursor = std::io::Cursor::new(&mut stack_buf[..]);
            write!(cursor, "@{:02}:{:02}:{:02}", hour, minute, second).unwrap();
            cursor.position() as usize
        };
        self.buf.write_bytes(&stack_buf[..len]);
        Ok(())
    }

    #[cold]
    #[inline(never)]
    fn format_duration(&mut self, obj: &Bound<'py, PyAny>) -> PyResult<()> {
        let total_seconds_f: f64 = obj.call_method0("total_seconds")?.extract()?;
        let mut total_seconds = total_seconds_f as i64;
        let negative = total_seconds < 0;
        if negative {
            total_seconds = -total_seconds;
        }

        let days = total_seconds / 86400;
        let remaining = total_seconds % 86400;
        let hours = remaining / 3600;
        let remaining = remaining % 3600;
        let minutes = remaining / 60;
        let seconds = remaining % 60;

        self.buf.write_byte(b'@');
        if negative {
            self.buf.write_byte(b'-');
        }
        self.buf.write_byte(b'P');

        let wrote_date = if days > 0 {
            let mut ibuf = itoa::Buffer::new();
            self.buf.write_str(ibuf.format(days));
            self.buf.write_byte(b'D');
            true
        } else {
            false
        };

        if hours > 0 || minutes > 0 || seconds > 0 {
            self.buf.write_byte(b'T');
            if hours > 0 {
                let mut ibuf = itoa::Buffer::new();
                self.buf.write_str(ibuf.format(hours));
                self.buf.write_byte(b'H');
            }
            if minutes > 0 {
                let mut ibuf = itoa::Buffer::new();
                self.buf.write_str(ibuf.format(minutes));
                self.buf.write_byte(b'M');
            }
            if seconds > 0 {
                let mut ibuf = itoa::Buffer::new();
                self.buf.write_str(ibuf.format(seconds));
                self.buf.write_byte(b'S');
            }
        } else if !wrote_date {
            // Zero duration
            self.buf.write_str("T0S");
        }

        Ok(())
    }

    #[cold]
    #[inline(never)]
    fn format_regexp(&mut self, obj: &Bound<'py, PyAny>) -> PyResult<()> {
        let pattern: String = obj.getattr("pattern")?.extract()?;
        let flags: u32 = obj.getattr("flags")?.extract()?;

        let mut flag_str = String::new();
        if flags & 2 != 0 { flag_str.push('i'); }  // re.IGNORECASE
        if flags & 8 != 0 { flag_str.push('m'); }  // re.MULTILINE
        if flags & 16 != 0 { flag_str.push('s'); } // re.DOTALL

        self.buf.write_byte(b'/');
        self.buf.write_str(&pattern);
        self.buf.write_byte(b'/');
        self.buf.write_str(&flag_str);
        Ok(())
    }

    #[cold]
    #[inline(never)]
    fn format_binary(&mut self, data: &[u8]) {
        // Base64 encode directly to buffer
        self.buf.write_str("b\"");

        let mut i = 0;
        let len = data.len();
        while i + 2 < len {
            let b0 = data[i] as usize;
            let b1 = data[i + 1] as usize;
            let b2 = data[i + 2] as usize;
            self.buf.write_byte(B64_ENCODE[b0 >> 2]);
            self.buf.write_byte(B64_ENCODE[((b0 & 0x03) << 4) | (b1 >> 4)]);
            self.buf.write_byte(B64_ENCODE[((b1 & 0x0F) << 2) | (b2 >> 6)]);
            self.buf.write_byte(B64_ENCODE[b2 & 0x3F]);
            i += 3;
        }

        let remaining = len - i;
        if remaining == 1 {
            let b0 = data[i] as usize;
            self.buf.write_byte(B64_ENCODE[b0 >> 2]);
            self.buf.write_byte(B64_ENCODE[(b0 & 0x03) << 4]);
            self.buf.write_byte(b'=');
            self.buf.write_byte(b'=');
        } else if remaining == 2 {
            let b0 = data[i] as usize;
            let b1 = data[i + 1] as usize;
            self.buf.write_byte(B64_ENCODE[b0 >> 2]);
            self.buf.write_byte(B64_ENCODE[((b0 & 0x03) << 4) | (b1 >> 4)]);
            self.buf.write_byte(B64_ENCODE[(b1 & 0x0F) << 2]);
            self.buf.write_byte(b'=');
        }

        self.buf.write_byte(b'"');
    }

    // -----------------------------------------------------------------------
    // Value serialization — hot path (pointer comparison)
    // -----------------------------------------------------------------------

    pub fn stringify_value(&mut self, value: &Bound<'py, PyAny>) -> PyResult<()> {
        let tc = cache::get_type_cache();
        let obj_type = unsafe { ffi::Py_TYPE(value.as_ptr()) };

        // 1. None → null
        if obj_type == tc.none_type {
            self.buf.write_str("null");
            return Ok(());
        }

        // 2. str
        if obj_type == tc.str_type {
            let s: &str = value.downcast::<PyString>().unwrap().to_str()?;
            self.escape_string(s);
            return Ok(());
        }

        // 3. bool — MUST come before int
        if obj_type == tc.bool_type {
            let b: bool = value.extract()?;
            self.buf.write_str(if b { "true" } else { "false" });
            return Ok(());
        }

        // 4. int — BigInt auto-promote when outside safe integer range
        if obj_type == tc.int_type {
            match value.extract::<i64>() {
                Ok(v) => {
                    if v.abs() > MAX_SAFE_INTEGER {
                        let mut ibuf = itoa::Buffer::new();
                        self.buf.write_str(ibuf.format(v));
                        self.buf.write_byte(b'n');
                    } else {
                        let mut ibuf = itoa::Buffer::new();
                        self.buf.write_str(ibuf.format(v));
                    }
                    return Ok(());
                }
                Err(_) => {
                    // Very large int — use Python str() + "n" suffix
                    let s: String = value.str()?.to_string();
                    self.buf.write_str(&s);
                    self.buf.write_byte(b'n');
                    return Ok(());
                }
            }
        }

        // 5. float — special values, then ryu for shortest round-trip
        if obj_type == tc.float_type {
            let f: f64 = value.extract()?;
            if f.is_nan() {
                self.buf.write_str("NaN");
            } else if f == f64::INFINITY {
                self.buf.write_str("Infinity");
            } else if f == f64::NEG_INFINITY {
                self.buf.write_str("-Infinity");
            } else {
                self.buf.write_f64(f);
            }
            return Ok(());
        }

        // 6. list
        if obj_type == tc.list_type {
            let list = value.downcast::<PyList>().unwrap();
            if list.len() == 0 {
                self.buf.write_str("[]");
                return Ok(());
            }
            let check_circular = self.state & STATE_CIRCULAR_BIT != 0;
            if check_circular {
                self.check_cycle(value)?;
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_byte(b'[');
            for (i, item) in list.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b']');
            self.state -= 1;
            if check_circular {
                self.remove_cycle(value);
            }
            return Ok(());
        }

        // 7. tuple
        if obj_type == tc.tuple_type {
            let tup = value.downcast::<PyTuple>().unwrap();
            if tup.len() == 0 {
                self.buf.write_str("()");
                return Ok(());
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_byte(b'(');
            for (i, item) in tup.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b')');
            self.state -= 1;
            return Ok(());
        }

        // 8. dict
        if obj_type == tc.dict_type {
            let dict = value.downcast::<PyDict>().unwrap();
            if dict.len() == 0 {
                self.buf.write_str("{}");
                return Ok(());
            }
            let check_circular = self.state & STATE_CIRCULAR_BIT != 0;
            if check_circular {
                self.check_cycle(value)?;
            }

            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_byte(b'{');

            if self.state & STATE_SORT_BIT != 0 {
                let mut keys: Vec<String> = Vec::new();
                for key in dict.keys() {
                    let k = key.downcast::<PyString>()
                        .map_err(|_| {
                            let type_name = key.get_type().name().map(|n| n.to_string()).unwrap_or_else(|_| "unknown".to_string());
                            pyo3::exceptions::PyTypeError::new_err(format!("Object key must be a string, got {}", type_name))
                        })?;
                    keys.push(k.to_string());
                }
                keys.sort();
                for (i, k) in keys.iter().enumerate() {
                    if i > 0 {
                        self.buf.write_str(&self.item_sep);
                    }
                    if indent {
                        self.write_indent(depth);
                    }
                    self.escape_string(k);
                    self.buf.write_str(&self.key_sep);
                    let val = dict.get_item(k)?.unwrap();
                    self.stringify_value(&val)?;
                }
            } else {
                for (i, (key, val)) in dict.iter().enumerate() {
                    if i > 0 {
                        self.buf.write_str(&self.item_sep);
                    }
                    if indent {
                        self.write_indent(depth);
                    }
                    let k = key.downcast::<PyString>()
                        .map_err(|_| {
                            let type_name = key.get_type().name().map(|n| n.to_string()).unwrap_or_else(|_| "unknown".to_string());
                            pyo3::exceptions::PyTypeError::new_err(format!("Object key must be a string, got {}", type_name))
                        })?;
                    self.escape_string(k.to_str()?);
                    self.buf.write_str(&self.key_sep);
                    self.stringify_value(&val)?;
                }
            }

            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b'}');
            self.state -= 1;

            if check_circular {
                self.remove_cycle(value);
            }
            return Ok(());
        }

        // Extended types — cold path
        self.stringify_extended_value(value)
    }

    // -----------------------------------------------------------------------
    // Extended type serialization (cold path — pointer comparison)
    // -----------------------------------------------------------------------

    #[cold]
    #[inline(never)]
    fn stringify_extended_value(&mut self, value: &Bound<'py, PyAny>) -> PyResult<()> {
        let tc = cache::get_type_cache();
        let obj_type = unsafe { ffi::Py_TYPE(value.as_ptr()) };

        // datetime (must check before time since datetime is a subclass of date)
        if obj_type == tc.datetime_type {
            return self.format_datetime(value);
        }

        // time
        if obj_type == tc.time_type {
            return self.format_timeonly(value);
        }

        // timedelta
        if obj_type == tc.timedelta_type {
            return self.format_duration(value);
        }

        // re.Pattern
        if obj_type == tc.pattern_type {
            return self.format_regexp(value);
        }

        // bytes
        if obj_type == tc.bytes_type {
            let b = value.downcast::<PyBytes>().unwrap();
            self.format_binary(b.as_bytes());
            return Ok(());
        }

        // bytearray
        if obj_type == tc.bytearray_type {
            let ba = value.downcast::<PyByteArray>().unwrap();
            let data: &[u8] = unsafe { ba.as_bytes() };
            self.format_binary(data);
            return Ok(());
        }

        // frozenset
        if obj_type == tc.frozenset_type {
            let fset = value.downcast::<PyFrozenSet>().unwrap();
            if fset.len() == 0 {
                self.buf.write_str("Set{}");
                return Ok(());
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_str("Set{");
            for (i, item) in fset.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b'}');
            self.state -= 1;
            return Ok(());
        }

        // set
        if obj_type == tc.set_type {
            let set = value.downcast::<PySet>().unwrap();
            let check_circular = self.state & STATE_CIRCULAR_BIT != 0;
            if check_circular {
                self.check_cycle(value)?;
            }
            if set.len() == 0 {
                if check_circular {
                    self.remove_cycle(value);
                }
                self.buf.write_str("Set{}");
                return Ok(());
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_str("Set{");
            for (i, item) in set.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b'}');
            self.state -= 1;
            if check_circular {
                self.remove_cycle(value);
            }
            return Ok(());
        }

        // Fallback for subclasses — use isinstance checks
        self.stringify_fallback(value)
    }

    // -----------------------------------------------------------------------
    // Fallback for subclasses — isinstance chain (rare path)
    // -----------------------------------------------------------------------

    #[cold]
    #[inline(never)]
    fn stringify_fallback(&mut self, value: &Bound<'py, PyAny>) -> PyResult<()> {
        // bool subclass — MUST come before int
        if value.is_instance_of::<PyBool>() {
            let b: bool = value.extract()?;
            self.buf.write_str(if b { "true" } else { "false" });
            return Ok(());
        }

        // int subclass
        if value.is_instance_of::<PyInt>() {
            match value.extract::<i64>() {
                Ok(v) => {
                    if v.abs() > MAX_SAFE_INTEGER {
                        let mut ibuf = itoa::Buffer::new();
                        self.buf.write_str(ibuf.format(v));
                        self.buf.write_byte(b'n');
                    } else {
                        let mut ibuf = itoa::Buffer::new();
                        self.buf.write_str(ibuf.format(v));
                    }
                    return Ok(());
                }
                Err(_) => {
                    let s: String = value.str()?.to_string();
                    self.buf.write_str(&s);
                    self.buf.write_byte(b'n');
                    return Ok(());
                }
            }
        }

        // str subclass
        if let Ok(s) = value.downcast::<PyString>() {
            let rust_str: &str = s.to_str()?;
            self.escape_string(rust_str);
            return Ok(());
        }

        // float subclass
        if value.is_instance_of::<PyFloat>() {
            let f: f64 = value.extract()?;
            if f.is_nan() {
                self.buf.write_str("NaN");
            } else if f == f64::INFINITY {
                self.buf.write_str("Infinity");
            } else if f == f64::NEG_INFINITY {
                self.buf.write_str("-Infinity");
            } else {
                self.buf.write_f64(f);
            }
            return Ok(());
        }

        // list subclass
        if let Ok(list) = value.downcast::<PyList>() {
            if list.len() == 0 {
                self.buf.write_str("[]");
                return Ok(());
            }
            let check_circular = self.state & STATE_CIRCULAR_BIT != 0;
            if check_circular {
                self.check_cycle(value)?;
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_byte(b'[');
            for (i, item) in list.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b']');
            self.state -= 1;
            if check_circular {
                self.remove_cycle(value);
            }
            return Ok(());
        }

        // tuple subclass
        if let Ok(tup) = value.downcast::<PyTuple>() {
            if tup.len() == 0 {
                self.buf.write_str("()");
                return Ok(());
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_byte(b'(');
            for (i, item) in tup.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b')');
            self.state -= 1;
            return Ok(());
        }

        // dict subclass
        if let Ok(dict) = value.downcast::<PyDict>() {
            if dict.len() == 0 {
                self.buf.write_str("{}");
                return Ok(());
            }
            let check_circular = self.state & STATE_CIRCULAR_BIT != 0;
            if check_circular {
                self.check_cycle(value)?;
            }

            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_byte(b'{');

            if self.state & STATE_SORT_BIT != 0 {
                let mut keys: Vec<String> = Vec::new();
                for key in dict.keys() {
                    let k = key.downcast::<PyString>()
                        .map_err(|_| {
                            let type_name = key.get_type().name().map(|n| n.to_string()).unwrap_or_else(|_| "unknown".to_string());
                            pyo3::exceptions::PyTypeError::new_err(format!("Object key must be a string, got {}", type_name))
                        })?;
                    keys.push(k.to_string());
                }
                keys.sort();
                for (i, k) in keys.iter().enumerate() {
                    if i > 0 {
                        self.buf.write_str(&self.item_sep);
                    }
                    if indent {
                        self.write_indent(depth);
                    }
                    self.escape_string(k);
                    self.buf.write_str(&self.key_sep);
                    let val = dict.get_item(k)?.unwrap();
                    self.stringify_value(&val)?;
                }
            } else {
                for (i, (key, val)) in dict.iter().enumerate() {
                    if i > 0 {
                        self.buf.write_str(&self.item_sep);
                    }
                    if indent {
                        self.write_indent(depth);
                    }
                    let k = key.downcast::<PyString>()
                        .map_err(|_| {
                            let type_name = key.get_type().name().map(|n| n.to_string()).unwrap_or_else(|_| "unknown".to_string());
                            pyo3::exceptions::PyTypeError::new_err(format!("Object key must be a string, got {}", type_name))
                        })?;
                    self.escape_string(k.to_str()?);
                    self.buf.write_str(&self.key_sep);
                    self.stringify_value(&val)?;
                }
            }

            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b'}');
            self.state -= 1;
            if check_circular {
                self.remove_cycle(value);
            }
            return Ok(());
        }

        // datetime subclass
        if value.is_instance(&self.py.import("datetime")?.getattr("datetime")?)? {
            return self.format_datetime(value);
        }

        // time subclass
        if value.is_instance(&self.py.import("datetime")?.getattr("time")?)? {
            return self.format_timeonly(value);
        }

        // timedelta subclass
        if value.is_instance(&self.py.import("datetime")?.getattr("timedelta")?)? {
            return self.format_duration(value);
        }

        // bytes subclass
        if let Ok(b) = value.downcast::<PyBytes>() {
            self.format_binary(b.as_bytes());
            return Ok(());
        }

        // bytearray subclass
        if let Ok(ba) = value.downcast::<PyByteArray>() {
            let data: &[u8] = unsafe { ba.as_bytes() };
            self.format_binary(data);
            return Ok(());
        }

        // frozenset subclass
        if let Ok(fset) = value.downcast::<PyFrozenSet>() {
            if fset.len() == 0 {
                self.buf.write_str("Set{}");
                return Ok(());
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_str("Set{");
            for (i, item) in fset.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b'}');
            self.state -= 1;
            return Ok(());
        }

        // set subclass
        if let Ok(set) = value.downcast::<PySet>() {
            let check_circular = self.state & STATE_CIRCULAR_BIT != 0;
            if check_circular {
                self.check_cycle(value)?;
            }
            if set.len() == 0 {
                if check_circular {
                    self.remove_cycle(value);
                }
                self.buf.write_str("Set{}");
                return Ok(());
            }
            let indent = self.indent_str.is_some();
            self.state += 1;
            let depth = (self.state & STATE_DEPTH_MASK) as usize;
            self.buf.write_str("Set{");
            for (i, item) in set.iter().enumerate() {
                if i > 0 {
                    self.buf.write_str(&self.item_sep);
                }
                if indent {
                    self.write_indent(depth);
                }
                self.stringify_value(&item)?;
            }
            if indent {
                self.write_indent(depth - 1);
            }
            self.buf.write_byte(b'}');
            self.state -= 1;
            if check_circular {
                self.remove_cycle(value);
            }
            return Ok(());
        }

        // Unsupported type
        let type_name = value.get_type().name()?.to_string();
        Err(pyo3::exceptions::PyTypeError::new_err(format!("Object of type {} is not RDN serializable", type_name)))
    }

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    pub fn stringify(&mut self, value: &Bound<'py, PyAny>) -> PyResult<PyObject> {
        self.stringify_value(value)?;
        // Move buf out of self and convert to PyString
        let buf = std::mem::replace(&mut self.buf, WriteBuffer::with_capacity(0));
        buf.into_py_string(self.py)
    }
}
