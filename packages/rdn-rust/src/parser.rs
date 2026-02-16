use crate::types::*;

/// Parse an RDN string into an `RdnValue`.
///
/// # Errors
///
/// Returns an error string if the input is malformed.
///
/// # Examples
///
/// ```
/// use rdn::parse;
///
/// let value = parse(r#"{"name": "RDN", "version": 42n}"#).unwrap();
/// ```
pub fn parse(input: &str) -> Result<RdnValue, String> {
    // TODO: Implement recursive-descent parser
    // The parser should handle:
    // 1. All JSON types (null, boolean, number, string, array, object)
    // 2. Special numbers: NaN, Infinity, -Infinity
    // 3. BigInt: 42n, -123n
    // 4. DateTime: @2024-01-15T10:30:00.000Z, @2024-01-15, @1705312200
    // 5. TimeOnly: @14:30:00, @23:59:59.999
    // 6. Duration: @P1Y2M3DT4H5M6S
    // 7. RegExp: /pattern/flags
    // 8. Binary: b"base64...", x"hex..."
    // 9. Map: Map{k => v}, {k => v}
    // 10. Set: Set{1, 2}, {"a", "b"}
    // 11. Tuple: (1, 2, 3)
    // 12. Brace disambiguation: { → Object vs Map vs Set
    Err("Not implemented".to_string())
}
