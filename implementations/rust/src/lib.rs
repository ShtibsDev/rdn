//! RDN (Rich Data Notation) — A JSON superset with native extended types.
//!
//! Supported types beyond JSON:
//! - `Date`: `@2024-01-15T10:30:00.000Z`
//! - `BigInt`: `42n`
//! - `RegExp`: `/pattern/flags`
//! - `Binary`: `b"base64..."` or `x"hex..."`
//! - `Map`: `Map{"a" => 1}` or `{"a" => 1}`
//! - `Set`: `Set{1, 2}` or `{"a", "b"}`
//! - `Tuple`: `(1, 2, 3)` (parses to Vec)
//! - `TimeOnly`: `@14:30:00`
//! - `Duration`: `@P1Y2M3DT4H5M6S`
//! - Special numbers: `NaN`, `Infinity`, `-Infinity`

mod types;
mod parser;
mod serializer;

pub use types::*;
pub use parser::parse;
pub use serializer::stringify;
