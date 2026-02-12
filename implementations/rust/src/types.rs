use std::collections::BTreeMap;
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
    pub value: String,
}

/// A date/time value (milliseconds since Unix epoch).
#[derive(Debug, Clone, PartialEq)]
pub struct RdnDate {
    pub millis: f64,
}

/// A time-of-day value.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RdnTimeOnly {
    pub hours: u8,
    pub minutes: u8,
    pub seconds: u8,
    pub milliseconds: u16,
}

/// An ISO 8601 duration.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RdnDuration {
    pub iso: String,
}

/// A regular expression with pattern and flags.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RdnRegExp {
    pub source: String,
    pub flags: String,
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
            RdnValue::BigInt(bi) => write!(f, "{}n", bi.value),
            RdnValue::String(s) => write!(f, "\"{s}\""),
            _ => write!(f, "[RdnValue]"),
        }
    }
}
