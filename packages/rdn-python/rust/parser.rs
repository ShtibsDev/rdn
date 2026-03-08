/// Recursive-descent RDN parser that produces Python objects directly via PyO3.
///
/// Works on `&[u8]` (UTF-8 bytes) for O(1) indexing — all structural RDN tokens
/// are ASCII. Produces `PyObject` values by calling Python constructors for
/// extended types (datetime, time, timedelta, re.Pattern, bytes).
use pyo3::prelude::*;
use pyo3::types::{PyBytes, PyDict, PyFloat, PyFrozenSet, PyList, PyString, PyTuple};

use crate::cache::KeyCache;
use crate::error::raise_decode_error;
use crate::tables::*;

const MAX_DEPTH: usize = 128;
const MAX_BINARY_SIZE: usize = 100 * 1024 * 1024; // 100 MB

/// Cached Python module/type references for constructing extended-type objects.
struct PyCaches<'py> {
    re_mod: Bound<'py, PyAny>,
    datetime_cls: Bound<'py, PyAny>,
    time_cls: Bound<'py, PyAny>,
    timedelta_cls: Bound<'py, PyAny>,
    timezone_utc: Bound<'py, PyAny>,
}

impl<'py> PyCaches<'py> {
    fn new(py: Python<'py>) -> PyResult<Self> {
        let datetime_mod = py.import("datetime")?;
        let re_mod = py.import("re")?;
        let datetime_cls = datetime_mod.getattr("datetime")?.into_any();
        let time_cls = datetime_mod.getattr("time")?.into_any();
        let timedelta_cls = datetime_mod.getattr("timedelta")?.into_any();
        let timezone = datetime_mod.getattr("timezone")?;
        let timezone_utc = timezone.getattr("utc")?.into_any();
        let _ = &datetime_mod; // keep module alive
        Ok(PyCaches { re_mod: re_mod.into_any(), datetime_cls, time_cls, timedelta_cls, timezone_utc })
    }
}

pub struct Parser<'py> {
    py: Python<'py>,
    source: &'py str,
    bytes: &'py [u8],
    pos: usize,
    len: usize,
    depth: usize,
    caches: PyCaches<'py>,
    pub key_cache: KeyCache,
}

impl<'py> Parser<'py> {
    pub fn new(py: Python<'py>, source: &'py str, key_cache: KeyCache) -> PyResult<Self> {
        let bytes = source.as_bytes();
        let len = bytes.len();
        let caches = PyCaches::new(py)?;
        Ok(Parser { py, source, bytes, pos: 0, len, depth: 0, caches, key_cache })
    }

    // -----------------------------------------------------------------------
    // Utility helpers
    // -----------------------------------------------------------------------

    #[inline(always)]
    fn skip_ws(&mut self) {
        while self.pos < self.len {
            match self.bytes[self.pos] {
                0x20 | 0x09 | 0x0A | 0x0D => self.pos += 1,
                _ => break,
            }
        }
    }

    #[cold]
    #[inline(never)]
    fn error(&self, msg: &str) -> PyErr {
        raise_decode_error(self.py, msg, self.source, self.pos)
    }

    fn expect(&mut self, ch: u8) -> PyResult<()> {
        if self.pos >= self.len || self.bytes[self.pos] != ch {
            return Err(self.error(&format!("Expected '{}'", ch as char)));
        }
        self.pos += 1;
        Ok(())
    }

    fn parse_literal(&mut self, expected: &[u8]) -> PyResult<()> {
        for &ch in expected {
            if self.pos >= self.len || self.bytes[self.pos] != ch {
                let s = std::str::from_utf8(expected).unwrap_or("?");
                return Err(self.error(&format!("Expected '{}'", s)));
            }
            self.pos += 1;
        }
        Ok(())
    }

    fn enter_container(&mut self) -> PyResult<()> {
        self.depth += 1;
        if self.depth > MAX_DEPTH {
            return Err(self.error("Maximum nesting depth exceeded (128)"));
        }
        Ok(())
    }

    fn exit_container(&mut self) {
        self.depth -= 1;
    }

    // -----------------------------------------------------------------------
    // String parsing with deferred materialization
    // -----------------------------------------------------------------------

    /// Check for unescaped control characters (< 0x20) in a byte range,
    /// skipping over escape sequences (backslash + next char).
    #[inline]
    fn check_control_chars(&self, start: usize, end: usize) -> PyResult<()> {
        let mut i = start;
        while i < end {
            let c = self.bytes[i];
            if c == b'\\' {
                // Skip the escape sequence — the escaped char is not a real control char
                i += 2;
                continue;
            }
            if c < 0x20 {
                // Temporarily set error position to the control char location
                return Err(raise_decode_error(self.py, "Unescaped control character in string", self.source, i));
            }
            i += 1;
        }
        Ok(())
    }

    fn parse_string(&mut self) -> PyResult<Bound<'py, PyString>> {
        self.pos += 1; // skip opening "
        let start = self.pos;

        let (end_pos, has_escape) = crate::simd::find_string_end(self.bytes, start);

        if end_pos >= self.len {
            self.pos = end_pos;
            return Err(self.error("Unterminated string"));
        }

        // Validate: no unescaped control characters
        self.check_control_chars(start, end_pos)?;

        if !has_escape {
            // Fast path: no escapes, slice directly from source
            let s = &self.source[start..end_pos];
            self.pos = end_pos + 1; // skip closing "
            return Ok(PyString::new(self.py, s));
        }

        // Slow path: materialize with escapes
        let result = self.materialize_string(start, end_pos)?;
        self.pos = end_pos + 1; // skip closing "
        Ok(result)
    }

    fn materialize_string(&self, start: usize, end: usize) -> PyResult<Bound<'py, PyString>> {
        let mut result = String::with_capacity(end - start);
        let mut i = start;

        while i < end {
            let c = self.bytes[i];

            if c == b'\\' {
                i += 1;
                let esc = self.bytes[i];
                match esc {
                    b'"' => { result.push('"'); i += 1; }
                    b'\\' => { result.push('\\'); i += 1; }
                    b'/' => { result.push('/'); i += 1; }
                    b'b' => { result.push('\u{0008}'); i += 1; }
                    b'f' => { result.push('\u{000C}'); i += 1; }
                    b'n' => { result.push('\n'); i += 1; }
                    b'r' => { result.push('\r'); i += 1; }
                    b't' => { result.push('\t'); i += 1; }
                    b'u' => {
                        let hex_str = &self.source[i + 1..i + 5];
                        if hex_str.len() < 4 {
                            return Err(raise_decode_error(self.py, "Invalid unicode escape", self.source, i));
                        }
                        let code = u16::from_str_radix(hex_str, 16)
                            .map_err(|_| raise_decode_error(self.py, "Invalid unicode escape", self.source, i))?;

                        // Handle surrogate pairs
                        if (0xD800..=0xDBFF).contains(&code) {
                            // High surrogate — expect \uXXXX low surrogate
                            if i + 5 < end && self.bytes[i + 5] == b'\\' && i + 6 < end && self.bytes[i + 6] == b'u' {
                                let low_hex = &self.source[i + 7..i + 11];
                                if low_hex.len() < 4 {
                                    return Err(raise_decode_error(self.py, "Invalid unicode escape", self.source, i));
                                }
                                let low = u16::from_str_radix(low_hex, 16)
                                    .map_err(|_| raise_decode_error(self.py, "Invalid unicode escape", self.source, i))?;
                                if (0xDC00..=0xDFFF).contains(&low) {
                                    let codepoint = 0x10000 + ((code as u32 - 0xD800) << 10) + (low as u32 - 0xDC00);
                                    result.push(char::from_u32(codepoint).unwrap());
                                    i += 11; // u + 4hex + \ + u + 4hex
                                } else {
                                    return Err(raise_decode_error(self.py, "Invalid surrogate pair", self.source, i));
                                }
                            } else {
                                return Err(raise_decode_error(self.py, "Invalid surrogate pair", self.source, i));
                            }
                        } else if (0xDC00..=0xDFFF).contains(&code) {
                            return Err(raise_decode_error(self.py, "Unexpected low surrogate", self.source, i));
                        } else {
                            result.push(char::from_u32(code as u32).unwrap());
                            i += 5; // u + 4 hex digits
                        }
                    }
                    _ => {
                        return Err(raise_decode_error(self.py, &format!("Invalid escape sequence '\\{}'", esc as char), self.source, i - 1));
                    }
                }
            } else {
                // Find the next backslash or end for bulk copy
                let j_start = i;
                i += 1;
                while i < end && self.bytes[i] != b'\\' {
                    i += 1;
                }
                result.push_str(&self.source[j_start..i]);
            }
        }

        Ok(PyString::new(self.py, &result))
    }

    /// Parse a string and return it as a Rust String (for internal use like object keys).
    #[allow(dead_code)]
    fn parse_string_as_rust(&mut self) -> PyResult<String> {
        let py_str = self.parse_string()?;
        Ok(py_str.to_string())
    }

    /// Parse an object key string with key-cache integration.
    ///
    /// Fast path (no escapes): looks up the raw key bytes in the cache; on hit returns
    /// the cached PyString, on miss creates a new one and inserts it into the cache.
    /// Slow path (escapes): materializes the string without caching (escaped keys are
    /// rare and the materialized bytes don't match the source bytes).
    fn parse_object_key(&mut self) -> PyResult<Bound<'py, PyString>> {
        self.pos += 1; // skip opening "
        let start = self.pos;

        let (end_pos, has_escape) = crate::simd::find_string_end(self.bytes, start);

        if end_pos >= self.len {
            self.pos = end_pos;
            return Err(self.error("Unterminated string"));
        }

        // Validate: no unescaped control characters
        self.check_control_chars(start, end_pos)?;

        if !has_escape {
            // Fast path: no escapes — use key cache
            let key_bytes = &self.bytes[start..end_pos];
            self.pos = end_pos + 1; // skip closing "
            if let Some(cached) = self.key_cache.lookup(self.py, key_bytes) {
                return Ok(cached.into_bound(self.py).downcast_into::<PyString>().unwrap());
            }
            let py_str = PyString::new(self.py, &self.source[start..end_pos]);
            self.key_cache.insert(self.py, key_bytes, py_str.clone().into_any().unbind());
            return Ok(py_str);
        }

        // Slow path: escapes present — materialize without caching
        let result = self.materialize_string(start, end_pos)?;
        self.pos = end_pos + 1; // skip closing "
        Ok(result)
    }

    // -----------------------------------------------------------------------
    // Number parsing
    // -----------------------------------------------------------------------

    fn parse_number(&mut self, negative: bool) -> PyResult<PyObject> {
        let start = if negative { self.pos - 1 } else { self.pos };

        // Accumulate integer digits
        let mut int_value: i64 = 0;
        let mut digit_count: usize = 0;
        let mut overflow = false;

        while self.pos < self.len {
            let d = self.bytes[self.pos].wrapping_sub(b'0');
            if d > 9 { break; }
            digit_count += 1;
            if !overflow {
                match int_value.checked_mul(10).and_then(|v| v.checked_add(d as i64)) {
                    Some(v) => int_value = v,
                    None => overflow = true,
                }
            }
            self.pos += 1;
        }

        if digit_count == 0 {
            return Err(self.error("Expected digit"));
        }

        // Leading-zero check
        let first_digit_pos = if negative { start + 1 } else { start };
        if digit_count > 1 && self.bytes[first_digit_pos] == b'0' {
            return Err(self.error("Leading zeros not allowed"));
        }

        // BigInt suffix 'n'
        if self.pos < self.len && self.bytes[self.pos] == b'n' {
            self.pos += 1;
            let raw = &self.source[start..self.pos - 1];
            let builtins = self.py.import("builtins")?;
            let py_int = builtins.getattr("int")?.call1((raw,))
                .map_err(|_| self.error("Invalid BigInt"))?;
            return Ok(py_int.into_any().unbind());
        }

        let mut is_float = false;

        // Fraction
        if self.pos < self.len && self.bytes[self.pos] == b'.' {
            is_float = true;
            self.pos += 1;
            let mut frac_digits = 0;
            while self.pos < self.len {
                let d = self.bytes[self.pos].wrapping_sub(b'0');
                if d > 9 { break; }
                frac_digits += 1;
                self.pos += 1;
            }
            if frac_digits == 0 {
                return Err(self.error("Expected digit after decimal point"));
            }
        }

        // Exponent
        if self.pos < self.len && (self.bytes[self.pos] == b'e' || self.bytes[self.pos] == b'E') {
            is_float = true;
            self.pos += 1;
            if self.pos < self.len && (self.bytes[self.pos] == b'+' || self.bytes[self.pos] == b'-') {
                self.pos += 1;
            }
            let mut exp_digits = 0;
            while self.pos < self.len {
                let d = self.bytes[self.pos].wrapping_sub(b'0');
                if d > 9 { break; }
                exp_digits += 1;
                self.pos += 1;
            }
            if exp_digits == 0 {
                return Err(self.error("Expected digit in exponent"));
            }
        }

        // Invalid BigInt after float
        if self.pos < self.len && self.bytes[self.pos] == b'n' && is_float {
            return Err(self.error("BigInt cannot have decimal point or exponent"));
        }

        let raw = &self.source[start..self.pos];
        if is_float {
            let f: f64 = raw.parse().map_err(|_| self.error("Invalid number"))?;
            return Ok(PyFloat::new(self.py, f).into_any().unbind());
        }

        // Integer fast path
        if !overflow && digit_count <= 15 {
            let val = if negative { -int_value } else { int_value };
            return Ok(val.into_pyobject(self.py).unwrap().into_any().unbind());
        }

        // Large integer — use Python's int()
        let builtins = self.py.import("builtins")?;
        let py_int = builtins.getattr("int")?.call1((raw,))
            .map_err(|_| self.error("Invalid number"))?;
        Ok(py_int.into_any().unbind())
    }

    // -----------------------------------------------------------------------
    // Digit-reading helper for date/time parsing
    // -----------------------------------------------------------------------

    fn read_digits(&mut self, n: usize) -> PyResult<u32> {
        if self.pos + n > self.len {
            return Err(self.error(&format!("Expected {}-digit number", n)));
        }
        let mut value: u32 = 0;
        for _ in 0..n {
            let d = self.bytes[self.pos].wrapping_sub(b'0');
            if d > 9 {
                return Err(self.error(&format!("Expected {}-digit number", n)));
            }
            value = value * 10 + d as u32;
            self.pos += 1;
        }
        Ok(value)
    }

    // -----------------------------------------------------------------------
    // @-prefixed type parsing
    // -----------------------------------------------------------------------

    fn parse_at(&mut self) -> PyResult<PyObject> {
        self.pos += 1; // skip '@'

        if self.pos >= self.len {
            return Err(self.error("Unexpected end after @"));
        }

        let ch = self.bytes[self.pos];

        // Duration: @P...
        if ch == b'P' {
            return self.parse_duration();
        }

        // Digit-based
        if ch.is_ascii_digit() {
            // TimeOnly: digit at +0, colon at +2
            if self.pos + 2 < self.len && self.bytes[self.pos + 2] == b':' {
                return self.parse_timeonly();
            }
            // DateTime: digit at +0, dash at +4
            if self.pos + 4 < self.len && self.bytes[self.pos + 4] == b'-' {
                return self.parse_datetime();
            }
            // Unix timestamp
            return self.parse_unix_timestamp();
        }

        Err(self.error("Invalid @ literal"))
    }

    fn parse_datetime(&mut self) -> PyResult<PyObject> {
        let year = self.read_digits(4)?;
        self.expect(b'-')?;
        let month = self.read_digits(2)?;
        self.expect(b'-')?;
        let day = self.read_digits(2)?;

        // Date-only: @YYYY-MM-DD
        if self.pos >= self.len || self.bytes[self.pos] != b'T' {
            let dt = self.caches.datetime_cls.call1((year, month, day, 0, 0, 0, 0, &self.caches.timezone_utc))?;
            return Ok(dt.unbind());
        }

        self.pos += 1; // skip 'T'
        let hours = self.read_digits(2)?;
        self.expect(b':')?;
        let minutes = self.read_digits(2)?;
        self.expect(b':')?;
        let seconds = self.read_digits(2)?;

        let mut microsecond: u32 = 0;
        if self.pos < self.len && self.bytes[self.pos] == b'.' {
            self.pos += 1; // skip '.'
            let ms = self.read_digits(3)?;
            microsecond = ms * 1000;
        }

        self.expect(b'Z')?;
        let dt = self.caches.datetime_cls.call1((year, month, day, hours, minutes, seconds, microsecond, &self.caches.timezone_utc))?;
        Ok(dt.unbind())
    }

    fn parse_timeonly(&mut self) -> PyResult<PyObject> {
        let hours = self.read_digits(2)?;
        self.expect(b':')?;
        let minutes = self.read_digits(2)?;
        self.expect(b':')?;
        let seconds = self.read_digits(2)?;

        let mut microsecond: u32 = 0;
        if self.pos < self.len && self.bytes[self.pos] == b'.' {
            self.pos += 1; // skip '.'
            let ms = self.read_digits(3)?;
            microsecond = ms * 1000;
        }

        let t = self.caches.time_cls.call1((hours, minutes, seconds, microsecond))?;
        Ok(t.unbind())
    }

    fn parse_duration(&mut self) -> PyResult<PyObject> {
        let start = self.pos;
        self.pos += 1; // skip 'P'

        while self.pos < self.len {
            let c = self.bytes[self.pos];
            if c.is_ascii_digit() || matches!(c, b'Y' | b'M' | b'D' | b'T' | b'H' | b'S' | b'.') {
                self.pos += 1;
            } else {
                break;
            }
        }

        let iso = &self.source[start..self.pos];
        if iso.len() < 2 {
            return Err(self.error("Invalid duration"));
        }

        // Split on 'T' to separate date-part and time-part
        let (date_part, time_part) = if let Some(t_idx) = iso[1..].find('T') {
            (&iso[1..t_idx + 1], &iso[t_idx + 2..])
        } else {
            (&iso[1..], "")
        };

        // If date_part contains Y or M (months), return as string
        if date_part.contains('Y') || date_part.contains('M') {
            return Ok(PyString::new(self.py, iso).into_any().unbind());
        }

        // Parse date_part for D
        let mut days: i64 = 0;
        if !date_part.is_empty() {
            if let Some(d_pos) = date_part.find('D') {
                days = date_part[..d_pos].parse::<i64>()
                    .map_err(|_| self.error("Invalid duration days"))?;
            }
        }

        // Parse time_part for H, M (minutes), S
        let mut total_hours: i64 = 0;
        let mut total_minutes: i64 = 0;
        let mut total_seconds: f64 = 0.0;
        if !time_part.is_empty() {
            let mut remaining = time_part;
            if let Some(h_idx) = remaining.find('H') {
                total_hours = remaining[..h_idx].parse::<i64>()
                    .map_err(|_| self.error("Invalid duration hours"))?;
                remaining = &remaining[h_idx + 1..];
            }
            if let Some(m_idx) = remaining.find('M') {
                total_minutes = remaining[..m_idx].parse::<i64>()
                    .map_err(|_| self.error("Invalid duration minutes"))?;
                remaining = &remaining[m_idx + 1..];
            }
            if let Some(s_idx) = remaining.find('S') {
                total_seconds = remaining[..s_idx].parse::<f64>()
                    .map_err(|_| self.error("Invalid duration seconds"))?;
            }
        }

        let td = self.caches.timedelta_cls.call((), Some(&pyo3::types::PyDict::from_sequence(
            PyList::new(self.py, [
                PyTuple::new(self.py, [PyString::new(self.py, "days").into_any(), days.into_pyobject(self.py).unwrap().into_any()])?,
                PyTuple::new(self.py, [PyString::new(self.py, "hours").into_any(), total_hours.into_pyobject(self.py).unwrap().into_any()])?,
                PyTuple::new(self.py, [PyString::new(self.py, "minutes").into_any(), total_minutes.into_pyobject(self.py).unwrap().into_any()])?,
                PyTuple::new(self.py, [PyString::new(self.py, "seconds").into_any(), total_seconds.into_pyobject(self.py).unwrap().into_any()])?,
            ])?.as_any()
        )?))?;
        Ok(td.unbind())
    }

    fn parse_unix_timestamp(&mut self) -> PyResult<PyObject> {
        let start = self.pos;
        while self.pos < self.len {
            let d = self.bytes[self.pos].wrapping_sub(b'0');
            if d > 9 { break; }
            self.pos += 1;
        }

        let digits = &self.source[start..self.pos];
        let value: i64 = digits.parse().map_err(|_| self.error("Invalid unix timestamp"))?;

        // <= 10 digits → seconds; > 10 digits → milliseconds
        let dt = if digits.len() <= 10 {
            self.caches.datetime_cls.call_method1("fromtimestamp", (value, &self.caches.timezone_utc))?
        } else {
            let secs = value as f64 / 1000.0;
            self.caches.datetime_cls.call_method1("fromtimestamp", (secs, &self.caches.timezone_utc))?
        };
        Ok(dt.unbind())
    }

    // -----------------------------------------------------------------------
    // RegExp parsing
    // -----------------------------------------------------------------------

    fn parse_regexp(&mut self) -> PyResult<PyObject> {
        self.pos += 1; // skip opening /

        let start = self.pos;
        let mut escaped = false;

        while self.pos < self.len {
            let c = self.bytes[self.pos];
            if escaped {
                escaped = false;
                self.pos += 1;
                continue;
            }
            if c == b'\\' {
                escaped = true;
                self.pos += 1;
                continue;
            }
            if c == b'/' {
                break;
            }
            self.pos += 1;
        }

        if self.pos >= self.len {
            return Err(self.error("Unterminated regular expression"));
        }

        let pattern = &self.source[start..self.pos];
        self.pos += 1; // skip closing /

        // Read flags
        let valid_flags = b"dgimsvy";
        let mut re_flags: u32 = 0;
        while self.pos < self.len && valid_flags.contains(&self.bytes[self.pos]) {
            match self.bytes[self.pos] {
                b'i' => re_flags |= 2,  // re.IGNORECASE
                b'm' => re_flags |= 8,  // re.MULTILINE
                b's' => re_flags |= 16, // re.DOTALL
                _ => {}                  // Other JS flags are silently ignored
            }
            self.pos += 1;
        }

        let compile = self.caches.re_mod.getattr("compile")?;
        let result = compile.call1((pattern, re_flags))?;
        Ok(result.unbind())
    }

    // -----------------------------------------------------------------------
    // Binary parsing — base64 and hex
    // -----------------------------------------------------------------------

    fn parse_binary_b64(&mut self) -> PyResult<PyObject> {
        self.pos += 1; // skip 'b'
        if self.pos >= self.len || self.bytes[self.pos] != b'"' {
            return Err(self.error("Expected '\"' after 'b'"));
        }
        self.pos += 1; // skip opening "

        let start = self.pos;
        while self.pos < self.len && self.bytes[self.pos] != b'"' {
            self.pos += 1;
        }
        if self.pos >= self.len {
            return Err(self.error("Unterminated binary literal"));
        }
        let content = &self.bytes[start..self.pos];
        self.pos += 1; // skip closing "

        if content.is_empty() {
            return Ok(PyBytes::new(self.py, b"").into_any().unbind());
        }

        // Validate length is multiple of 4
        if content.len() % 4 != 0 {
            return Err(self.error("Invalid base64: length must be a multiple of 4"));
        }

        // Validate all chars and count padding
        let mut padding = 0usize;
        for (i, &ch) in content.iter().enumerate() {
            if ch == b'=' {
                padding += 1;
                if i < content.len() - 2 {
                    return Err(self.error("Invalid base64 character"));
                }
            } else {
                if padding > 0 {
                    return Err(self.error("Invalid base64 character"));
                }
                if B64_DECODE[ch as usize] == 0xFF {
                    return Err(self.error("Invalid base64 character"));
                }
            }
        }

        // Check decoded size
        let decoded_size = (content.len() / 4) * 3 - padding;
        if decoded_size > MAX_BINARY_SIZE {
            return Err(self.error("Binary data too large"));
        }

        // Non-zero padding bit check
        if padding == 1 {
            let last_data_val = B64_DECODE[content[content.len() - 2] as usize];
            if last_data_val & 0x03 != 0 {
                return Err(self.error("Invalid base64: non-zero padding bits"));
            }
        } else if padding == 2 {
            let last_data_val = B64_DECODE[content[content.len() - 3] as usize];
            if last_data_val & 0x0F != 0 {
                return Err(self.error("Invalid base64: non-zero padding bits"));
            }
        }

        // Decode base64 in Rust
        let decoded = self.decode_b64(content)?;
        Ok(PyBytes::new(self.py, &decoded).into_any().unbind())
    }

    fn decode_b64(&self, input: &[u8]) -> PyResult<Vec<u8>> {
        let mut output = Vec::with_capacity((input.len() / 4) * 3);
        let mut i = 0;
        while i < input.len() {
            let a = B64_DECODE[input[i] as usize] as u32;
            let b = B64_DECODE[input[i + 1] as usize] as u32;

            if input[i + 2] == b'=' {
                // Last group with 2 padding chars
                output.push(((a << 2) | (b >> 4)) as u8);
                break;
            }
            let c = B64_DECODE[input[i + 2] as usize] as u32;

            if input[i + 3] == b'=' {
                // Last group with 1 padding char
                output.push(((a << 2) | (b >> 4)) as u8);
                output.push((((b & 0x0F) << 4) | (c >> 2)) as u8);
                break;
            }
            let d = B64_DECODE[input[i + 3] as usize] as u32;

            output.push(((a << 2) | (b >> 4)) as u8);
            output.push((((b & 0x0F) << 4) | (c >> 2)) as u8);
            output.push((((c & 0x03) << 6) | d) as u8);

            i += 4;
        }
        Ok(output)
    }

    fn parse_binary_hex(&mut self) -> PyResult<PyObject> {
        self.pos += 1; // skip 'x'
        if self.pos >= self.len || self.bytes[self.pos] != b'"' {
            return Err(self.error("Expected '\"' after 'x'"));
        }
        self.pos += 1; // skip opening "

        let start = self.pos;
        while self.pos < self.len && self.bytes[self.pos] != b'"' {
            self.pos += 1;
        }
        if self.pos >= self.len {
            return Err(self.error("Unterminated hex literal"));
        }
        let content = &self.bytes[start..self.pos];
        self.pos += 1; // skip closing "

        if content.is_empty() {
            return Ok(PyBytes::new(self.py, b"").into_any().unbind());
        }

        // Validate even length
        if content.len() % 2 != 0 {
            return Err(self.error("Invalid hex: odd length"));
        }

        // Validate all chars are hex digits
        for &ch in content {
            if HEX_DECODE[ch as usize] == 0xFF {
                return Err(self.error("Invalid hex character"));
            }
        }

        // Check decoded size
        if content.len() / 2 > MAX_BINARY_SIZE {
            return Err(self.error("Binary data too large"));
        }

        // Decode hex in Rust
        let mut decoded = Vec::with_capacity(content.len() / 2);
        let mut i = 0;
        while i < content.len() {
            let hi = HEX_DECODE[content[i] as usize] as u8;
            let lo = HEX_DECODE[content[i + 1] as usize] as u8;
            decoded.push((hi << 4) | lo);
            i += 2;
        }

        Ok(PyBytes::new(self.py, &decoded).into_any().unbind())
    }

    // -----------------------------------------------------------------------
    // Container parsing
    // -----------------------------------------------------------------------

    fn parse_array(&mut self) -> PyResult<PyObject> {
        self.enter_container()?;
        self.pos += 1; // skip [
        self.skip_ws();

        if self.pos < self.len && self.bytes[self.pos] == b']' {
            self.pos += 1;
            self.exit_container();
            let list = PyList::empty(self.py);
            return Ok(list.into_any().unbind());
        }

        let mut items: Vec<PyObject> = Vec::new();
        items.push(self.parse_value()?);
        self.skip_ws();

        while self.pos < self.len && self.bytes[self.pos] == b',' {
            self.pos += 1; // skip ,
            self.skip_ws();
            items.push(self.parse_value()?);
            self.skip_ws();
        }

        self.expect(b']')?;
        self.exit_container();
        let list = PyList::new(self.py, &items)?;
        Ok(list.into_any().unbind())
    }

    fn parse_tuple(&mut self) -> PyResult<PyObject> {
        self.enter_container()?;
        self.pos += 1; // skip (
        self.skip_ws();

        if self.pos < self.len && self.bytes[self.pos] == b')' {
            self.pos += 1;
            self.exit_container();
            let tup = PyTuple::empty(self.py);
            return Ok(tup.into_any().unbind());
        }

        let mut items: Vec<PyObject> = Vec::new();
        items.push(self.parse_value()?);
        self.skip_ws();

        while self.pos < self.len && self.bytes[self.pos] == b',' {
            self.pos += 1; // skip ,
            self.skip_ws();
            items.push(self.parse_value()?);
            self.skip_ws();
        }

        self.expect(b')')?;
        self.exit_container();
        let tup = PyTuple::new(self.py, &items)?;
        Ok(tup.into_any().unbind())
    }

    fn parse_brace(&mut self) -> PyResult<PyObject> {
        self.enter_container()?;
        self.pos += 1; // skip {
        self.skip_ws();

        // Empty braces → Object (empty dict)
        if self.pos < self.len && self.bytes[self.pos] == b'}' {
            self.pos += 1;
            self.exit_container();
            let dict = PyDict::new(self.py);
            return Ok(dict.into_any().unbind());
        }

        // Parse first value
        let first_value = self.parse_value()?;
        self.skip_ws();

        if self.pos >= self.len {
            return Err(self.error("Unterminated brace expression"));
        }

        let sep = self.bytes[self.pos];

        // : → Object
        if sep == b':' {
            // first_value must be a string
            let key: Bound<'py, PyString> = first_value.bind(self.py).downcast::<PyString>()
                .map_err(|_| self.error("Object key must be a string"))?
                .clone();
            return self.finish_object(key);
        }

        // => → Map
        if sep == b'=' && self.pos + 1 < self.len && self.bytes[self.pos + 1] == b'>' {
            return self.finish_map(first_value);
        }

        // , → Set
        if sep == b',' {
            return self.finish_set(first_value);
        }

        // } → single-element Set
        if sep == b'}' {
            self.pos += 1;
            self.exit_container();
            let set = PyFrozenSet::new(self.py, &[first_value.bind(self.py)])?;
            return Ok(set.into_any().unbind());
        }

        Err(self.error("Expected ':', '=>', ',' or '}' after value in brace expression"))
    }

    fn finish_object(&mut self, first_key: Bound<'py, PyString>) -> PyResult<PyObject> {
        self.pos += 1; // skip :
        self.skip_ws();

        let dict = PyDict::new(self.py);
        let first_val = self.parse_value()?;
        dict.set_item(&first_key, first_val)?;
        self.skip_ws();

        while self.pos < self.len && self.bytes[self.pos] == b',' {
            self.pos += 1; // skip ,
            self.skip_ws();
            let key = self.parse_object_key()?;
            self.skip_ws();
            self.expect(b':')?;
            self.skip_ws();
            let val = self.parse_value()?;
            dict.set_item(&key, val)?;
            self.skip_ws();
        }

        self.expect(b'}')?;
        self.exit_container();
        Ok(dict.into_any().unbind())
    }

    fn finish_map(&mut self, first_key: PyObject) -> PyResult<PyObject> {
        self.pos += 2; // skip =>
        self.skip_ws();

        let dict = PyDict::new(self.py);
        let first_val = self.parse_value()?;
        dict.set_item(first_key.bind(self.py), first_val)?;
        self.skip_ws();

        while self.pos < self.len && self.bytes[self.pos] == b',' {
            self.pos += 1; // skip ,
            self.skip_ws();
            let key = self.parse_value()?;
            self.skip_ws();
            if self.pos + 1 >= self.len || self.bytes[self.pos] != b'=' || self.bytes[self.pos + 1] != b'>' {
                return Err(self.error("Expected '=>' in map entry"));
            }
            self.pos += 2; // skip =>
            self.skip_ws();
            let val = self.parse_value()?;
            dict.set_item(key.bind(self.py), val)?;
            self.skip_ws();
        }

        self.expect(b'}')?;
        self.exit_container();
        Ok(dict.into_any().unbind())
    }

    fn finish_set(&mut self, first_value: PyObject) -> PyResult<PyObject> {
        let mut items: Vec<PyObject> = Vec::new();
        items.push(first_value);

        self.pos += 1; // skip ,
        self.skip_ws();
        items.push(self.parse_value()?);
        self.skip_ws();

        while self.pos < self.len && self.bytes[self.pos] == b',' {
            self.pos += 1; // skip ,
            self.skip_ws();
            items.push(self.parse_value()?);
            self.skip_ws();
        }

        self.expect(b'}')?;
        self.exit_container();

        let py_items: Vec<Bound<'py, PyAny>> = items.iter().map(|o| o.bind(self.py).clone()).collect();
        let set = PyFrozenSet::new(self.py, &py_items)?;
        Ok(set.into_any().unbind())
    }

    fn parse_explicit_map(&mut self) -> PyResult<PyObject> {
        // Verify and skip "Map{"
        if self.pos + 3 >= self.len
            || self.bytes[self.pos + 1] != b'a'
            || self.bytes[self.pos + 2] != b'p'
            || self.bytes[self.pos + 3] != b'{'
        {
            return Err(self.error("Expected 'Map{'"));
        }
        self.enter_container()?;
        self.pos += 4; // skip Map{
        self.skip_ws();

        let dict = PyDict::new(self.py);

        if self.pos < self.len && self.bytes[self.pos] == b'}' {
            self.pos += 1;
            self.exit_container();
            return Ok(dict.into_any().unbind());
        }

        // Parse first entry
        let key = self.parse_value()?;
        self.skip_ws();
        if self.pos + 1 >= self.len || self.bytes[self.pos] != b'=' || self.bytes[self.pos + 1] != b'>' {
            return Err(self.error("Expected '=>' in map entry"));
        }
        self.pos += 2; // skip =>
        self.skip_ws();
        let val = self.parse_value()?;
        dict.set_item(key.bind(self.py), val)?;
        self.skip_ws();

        while self.pos < self.len && self.bytes[self.pos] == b',' {
            self.pos += 1; // skip ,
            self.skip_ws();
            let key = self.parse_value()?;
            self.skip_ws();
            if self.pos + 1 >= self.len || self.bytes[self.pos] != b'=' || self.bytes[self.pos + 1] != b'>' {
                return Err(self.error("Expected '=>' in map entry"));
            }
            self.pos += 2; // skip =>
            self.skip_ws();
            let val = self.parse_value()?;
            dict.set_item(key.bind(self.py), val)?;
            self.skip_ws();
        }

        self.expect(b'}')?;
        self.exit_container();
        Ok(dict.into_any().unbind())
    }

    fn parse_explicit_set(&mut self) -> PyResult<PyObject> {
        // Verify and skip "Set{"
        if self.pos + 3 >= self.len
            || self.bytes[self.pos + 1] != b'e'
            || self.bytes[self.pos + 2] != b't'
            || self.bytes[self.pos + 3] != b'{'
        {
            return Err(self.error("Expected 'Set{'"));
        }
        self.enter_container()?;
        self.pos += 4; // skip Set{
        self.skip_ws();

        if self.pos < self.len && self.bytes[self.pos] == b'}' {
            self.pos += 1;
            self.exit_container();
            let set = PyFrozenSet::empty(self.py)?;
            return Ok(set.into_any().unbind());
        }

        let mut items: Vec<PyObject> = Vec::new();
        items.push(self.parse_value()?);
        self.skip_ws();

        while self.pos < self.len && self.bytes[self.pos] == b',' {
            self.pos += 1; // skip ,
            self.skip_ws();
            items.push(self.parse_value()?);
            self.skip_ws();
        }

        self.expect(b'}')?;
        self.exit_container();

        let py_items: Vec<Bound<'py, PyAny>> = items.iter().map(|o| o.bind(self.py).clone()).collect();
        let set = PyFrozenSet::new(self.py, &py_items)?;
        Ok(set.into_any().unbind())
    }

    // -----------------------------------------------------------------------
    // Value dispatch
    // -----------------------------------------------------------------------

    pub fn parse_value(&mut self) -> PyResult<PyObject> {
        self.skip_ws();

        if self.pos >= self.len {
            return Err(self.error("Unexpected end of input"));
        }

        let ch = self.bytes[self.pos];
        let token = TOKEN_TABLE[ch as usize];

        match token {
            TOKEN_STRING => {
                let s = self.parse_string()?;
                Ok(s.into_any().unbind())
            }
            TOKEN_NUMBER => self.parse_number(false),
            TOKEN_MINUS => {
                self.pos += 1; // consume '-'
                if self.pos < self.len && self.bytes[self.pos] == b'I' {
                    self.parse_literal(b"Infinity")?;
                    return Ok(PyFloat::new(self.py, f64::NEG_INFINITY).into_any().unbind());
                }
                self.parse_number(true)
            }
            TOKEN_INFINITY => {
                self.parse_literal(b"Infinity")?;
                Ok(PyFloat::new(self.py, f64::INFINITY).into_any().unbind())
            }
            TOKEN_NAN => {
                self.parse_literal(b"NaN")?;
                Ok(PyFloat::new(self.py, f64::NAN).into_any().unbind())
            }
            TOKEN_TRUE => {
                self.parse_literal(b"true")?;
                Ok(true.into_pyobject(self.py).unwrap().to_owned().into_any().unbind())
            }
            TOKEN_FALSE => {
                self.parse_literal(b"false")?;
                Ok(false.into_pyobject(self.py).unwrap().to_owned().into_any().unbind())
            }
            TOKEN_NULL => {
                self.parse_literal(b"null")?;
                Ok(self.py.None().into_pyobject(self.py).unwrap().unbind())
            }
            TOKEN_AT => self.parse_at(),
            TOKEN_SLASH => self.parse_regexp(),
            TOKEN_B64 => self.parse_binary_b64(),
            TOKEN_HEX => self.parse_binary_hex(),
            TOKEN_OPEN_BRACKET => self.parse_array(),
            TOKEN_OPEN_PAREN => self.parse_tuple(),
            TOKEN_OPEN_BRACE => self.parse_brace(),
            TOKEN_MAP => self.parse_explicit_map(),
            TOKEN_SET => self.parse_explicit_set(),
            _ => Err(self.error(&format!("Unexpected character '{}'", ch as char))),
        }
    }

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    pub fn parse(&mut self) -> PyResult<PyObject> {
        let result = self.parse_value()?;
        self.skip_ws();
        if self.pos < self.len {
            return Err(self.error("Unexpected data after value"));
        }
        Ok(result)
    }

    /// Take the key_cache out of this parser (for returning to the global pool).
    pub fn take_key_cache(&mut self) -> KeyCache {
        std::mem::replace(&mut self.key_cache, KeyCache::new())
    }
}
