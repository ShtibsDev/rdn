use std::fmt;

/// Represents any RDN value.
#[derive(Debug, Clone, PartialEq)]
pub enum RdnValue {
    Null,
    Bool(bool),
    Number(f64),
    BigInt(BigInt),
    String(String),
    Array(Vec<RdnValue>),
    Object(Vec<(String, RdnValue)>),
    Date(RdnDate),
    TimeOnly(RdnTimeOnly),
    Duration(RdnDuration),
    RegExp(RdnRegExp),
    Binary(Vec<u8>),
    Map(Vec<(RdnValue, RdnValue)>),
    Set(Vec<RdnValue>),
}

/// Arbitrary-precision integer (stored as string for now).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BigInt {
    value: String,
}

impl BigInt {
    /// Creates a new `BigInt` from a string value.
    ///
    /// The value must be non-empty, with an optional leading `-`, followed by one or more ASCII digits.
    pub fn new(value: &str) -> Result<Self, String> {
        if value.is_empty() {
            return Err("BigInt value must not be empty".to_string());
        }
        let digits = if let Some(rest) = value.strip_prefix('-') { rest } else { value };
        if digits.is_empty() {
            return Err("BigInt value must contain digits after optional sign".to_string());
        }
        if !digits.chars().all(|c| c.is_ascii_digit()) {
            return Err(format!("BigInt value contains non-digit characters: {value}"));
        }
        Ok(BigInt { value: value.to_string() })
    }

    /// Returns the string representation of this BigInt.
    pub fn value(&self) -> &str {
        &self.value
    }
}

/// A date/time value (milliseconds since Unix epoch).
#[derive(Debug, Clone, PartialEq)]
pub struct RdnDate {
    pub millis: f64,
}

/// A time-of-day value.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RdnTimeOnly {
    hours: u8,
    minutes: u8,
    seconds: u8,
    milliseconds: u16,
}

impl RdnTimeOnly {
    /// Creates a new `RdnTimeOnly` with validation.
    ///
    /// - `hours`: 0..=23
    /// - `minutes`: 0..=59
    /// - `seconds`: 0..=59
    /// - `milliseconds`: 0..=999
    pub fn new(hours: u8, minutes: u8, seconds: u8, milliseconds: u16) -> Result<Self, String> {
        if hours > 23 {
            return Err(format!("hours must be 0-23, got {hours}"));
        }
        if minutes > 59 {
            return Err(format!("minutes must be 0-59, got {minutes}"));
        }
        if seconds > 59 {
            return Err(format!("seconds must be 0-59, got {seconds}"));
        }
        if milliseconds > 999 {
            return Err(format!("milliseconds must be 0-999, got {milliseconds}"));
        }
        Ok(RdnTimeOnly { hours, minutes, seconds, milliseconds })
    }

    pub fn hours(&self) -> u8 { self.hours }
    pub fn minutes(&self) -> u8 { self.minutes }
    pub fn seconds(&self) -> u8 { self.seconds }
    pub fn milliseconds(&self) -> u16 { self.milliseconds }
}

/// An ISO 8601 duration.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RdnDuration {
    pub iso: String,
}

/// A regular expression with pattern and flags.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RdnRegExp {
    source: String,
    flags: String,
}

impl RdnRegExp {
    /// Creates a new `RdnRegExp` with flag validation.
    ///
    /// Flags must only contain characters from `d`, `g`, `i`, `m`, `s`, `u`, `v`, `y`,
    /// and each flag may appear at most once.
    pub fn new(source: &str, flags: &str) -> Result<Self, String> {
        const VALID_FLAGS: &[char] = &['d', 'g', 'i', 'm', 's', 'u', 'v', 'y'];
        let mut seen = [false; 8];
        for ch in flags.chars() {
            match VALID_FLAGS.iter().position(|&f| f == ch) {
                Some(idx) => {
                    if seen[idx] {
                        return Err(format!("duplicate regex flag: {ch}"));
                    }
                    seen[idx] = true;
                }
                None => return Err(format!("invalid regex flag: {ch}")),
            }
        }
        Ok(RdnRegExp { source: source.to_string(), flags: flags.to_string() })
    }

    pub fn source(&self) -> &str { &self.source }
    pub fn flags(&self) -> &str { &self.flags }
}

/// Writes `s` to the formatter as a properly escaped RDN/JSON string
/// (including the surrounding double quotes).
fn write_escaped_string(f: &mut fmt::Formatter<'_>, s: &str) -> fmt::Result {
    f.write_str("\"")?;
    for ch in s.chars() {
        match ch {
            '"' => f.write_str("\\\"")?,
            '\\' => f.write_str("\\\\")?,
            '\n' => f.write_str("\\n")?,
            '\r' => f.write_str("\\r")?,
            '\t' => f.write_str("\\t")?,
            '\u{08}' => f.write_str("\\b")?,
            '\u{0C}' => f.write_str("\\f")?,
            c if (c as u32) < 0x20 => write!(f, "\\u{:04x}", c as u32)?,
            c => f.write_str(&c.to_string())?,
        }
    }
    f.write_str("\"")
}

impl fmt::Display for RdnValue {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            RdnValue::Null => write!(f, "null"),
            RdnValue::Bool(b) => write!(f, "{b}"),
            RdnValue::Number(n) => {
                if n.is_nan() {
                    write!(f, "NaN")
                } else if n.is_infinite() {
                    if n.is_sign_positive() {
                        write!(f, "Infinity")
                    } else {
                        write!(f, "-Infinity")
                    }
                } else {
                    write!(f, "{n}")
                }
            }
            RdnValue::BigInt(bi) => write!(f, "{}n", bi.value()),
            RdnValue::String(s) => write_escaped_string(f, s),
            _ => write!(f, "[RdnValue]"),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn str_val(s: &str) -> RdnValue {
        RdnValue::String(s.to_string())
    }

    #[test]
    fn display_normal_string() {
        assert_eq!(str_val("hello").to_string(), r#""hello""#);
    }

    #[test]
    fn display_string_with_quote() {
        assert_eq!(str_val("say \"hi\"").to_string(), r#""say \"hi\"""#);
    }

    #[test]
    fn display_string_with_backslash() {
        assert_eq!(str_val("a\\b").to_string(), r#""a\\b""#);
    }

    #[test]
    fn display_string_with_newline() {
        assert_eq!(str_val("line1\nline2").to_string(), r#""line1\nline2""#);
    }

    #[test]
    fn display_string_with_tab() {
        assert_eq!(str_val("col1\tcol2").to_string(), r#""col1\tcol2""#);
    }

    #[test]
    fn display_string_with_control_char() {
        assert_eq!(str_val("\x01").to_string(), r#""\u0001""#);
    }

    #[test]
    fn display_string_with_multiple_special_chars() {
        // backslash, newline, tab, quote, control char \x02, carriage return
        assert_eq!(str_val("a\\b\nc\t\"d\x02\re").to_string(), r#""a\\b\nc\t\"d\u0002\re""#);
    }

    #[test]
    fn display_string_with_backspace_and_formfeed() {
        assert_eq!(str_val("\u{08}\u{0C}").to_string(), r#""\b\f""#);
    }

    // --- BigInt validation tests ---

    #[test]
    fn bigint_valid_positive() {
        let bi = BigInt::new("12345").unwrap();
        assert_eq!(bi.value(), "12345");
    }

    #[test]
    fn bigint_valid_negative() {
        let bi = BigInt::new("-42").unwrap();
        assert_eq!(bi.value(), "-42");
    }

    #[test]
    fn bigint_valid_zero() {
        let bi = BigInt::new("0").unwrap();
        assert_eq!(bi.value(), "0");
    }

    #[test]
    fn bigint_display() {
        let bi = BigInt::new("999").unwrap();
        let val = RdnValue::BigInt(bi);
        assert_eq!(val.to_string(), "999n");
    }

    #[test]
    fn bigint_empty_is_err() {
        assert!(BigInt::new("").is_err());
    }

    #[test]
    fn bigint_just_minus_is_err() {
        assert!(BigInt::new("-").is_err());
    }

    #[test]
    fn bigint_non_digit_is_err() {
        assert!(BigInt::new("12a3").is_err());
    }

    #[test]
    fn bigint_float_is_err() {
        assert!(BigInt::new("1.5").is_err());
    }

    // --- RdnTimeOnly validation tests ---

    #[test]
    fn time_only_valid() {
        let t = RdnTimeOnly::new(14, 30, 0, 500).unwrap();
        assert_eq!(t.hours(), 14);
        assert_eq!(t.minutes(), 30);
        assert_eq!(t.seconds(), 0);
        assert_eq!(t.milliseconds(), 500);
    }

    #[test]
    fn time_only_boundary_values() {
        let t = RdnTimeOnly::new(23, 59, 59, 999).unwrap();
        assert_eq!(t.hours(), 23);
        assert_eq!(t.milliseconds(), 999);
    }

    #[test]
    fn time_only_midnight() {
        assert!(RdnTimeOnly::new(0, 0, 0, 0).is_ok());
    }

    #[test]
    fn time_only_hours_out_of_range() {
        assert!(RdnTimeOnly::new(24, 0, 0, 0).is_err());
        assert!(RdnTimeOnly::new(255, 0, 0, 0).is_err());
    }

    #[test]
    fn time_only_minutes_out_of_range() {
        assert!(RdnTimeOnly::new(12, 60, 0, 0).is_err());
    }

    #[test]
    fn time_only_seconds_out_of_range() {
        assert!(RdnTimeOnly::new(12, 30, 60, 0).is_err());
    }

    #[test]
    fn time_only_milliseconds_out_of_range() {
        assert!(RdnTimeOnly::new(12, 30, 0, 1000).is_err());
    }

    // --- RdnRegExp validation tests ---

    #[test]
    fn regexp_valid_no_flags() {
        let re = RdnRegExp::new("abc", "").unwrap();
        assert_eq!(re.source(), "abc");
        assert_eq!(re.flags(), "");
    }

    #[test]
    fn regexp_valid_with_flags() {
        let re = RdnRegExp::new("\\d+", "gi").unwrap();
        assert_eq!(re.source(), "\\d+");
        assert_eq!(re.flags(), "gi");
    }

    #[test]
    fn regexp_all_valid_flags() {
        assert!(RdnRegExp::new(".", "dgimsuyv").is_ok());
    }

    #[test]
    fn regexp_invalid_flag() {
        assert!(RdnRegExp::new(".", "x").is_err());
        assert!(RdnRegExp::new(".", "giz").is_err());
    }

    #[test]
    fn regexp_duplicate_flag() {
        assert!(RdnRegExp::new(".", "gg").is_err());
        assert!(RdnRegExp::new(".", "gig").is_err());
    }
}
