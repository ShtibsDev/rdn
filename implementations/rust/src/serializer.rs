use crate::types::*;

/// Serialize an `RdnValue` to an RDN string.
///
/// # Serialization rules (from the spec):
///
/// - `Null` → `null`
/// - `Bool` → `true` / `false`
/// - `Number` (finite) → numeric literal
/// - `Number` (NaN) → `NaN`
/// - `Number` (±Infinity) → `Infinity` / `-Infinity`
/// - `BigInt` → `42n`
/// - `String` → `"escaped"`
/// - `Date` → `@YYYY-MM-DDTHH:mm:ss.sssZ`
/// - `RegExp` → `/pattern/flags`
/// - `Binary` → `b"base64..."`
/// - `Array` → `[...]`
/// - `Object` → `{...}`
/// - `Map` (non-empty) → `Map{k => v, ...}`
/// - `Map` (empty) → `Map{}`
/// - `Set` (non-empty) → `Set{v, ...}`
/// - `Set` (empty) → `Set{}`
pub fn stringify(value: &RdnValue) -> String {
    // TODO: Implement serializer with cycle detection
    todo!("Not implemented")
}
