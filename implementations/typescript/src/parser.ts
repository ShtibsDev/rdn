import type { RDNValue, RDNReviver } from "./types.js";

/**
 * Parse an RDN string into a JavaScript value.
 *
 * RDN is a superset of JSON with native support for:
 * Date, BigInt, RegExp, Binary (Uint8Array), Map, Set, Tuple,
 * TimeOnly, Duration, NaN, Infinity, and -Infinity.
 *
 * @param text - RDN-formatted string
 * @param reviver - Optional reviver function
 * @returns Parsed JavaScript value
 * @throws SyntaxError on malformed input
 */
export function parse(text: string, reviver?: RDNReviver): RDNValue {
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
  throw new Error("Not implemented");
}
