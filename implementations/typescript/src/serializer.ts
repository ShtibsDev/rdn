import type { RDNValue, RDNReplacer } from "./types.js";

/**
 * Serialize a JavaScript value to an RDN string.
 *
 * Serialization rules (from the spec):
 * - null → null
 * - Boolean → true / false
 * - Number (finite) → numeric literal
 * - Number (NaN) → NaN
 * - Number (±Infinity) → Infinity / -Infinity
 * - BigInt → 42n
 * - String → "escaped"
 * - Date (valid) → @YYYY-MM-DDTHH:mm:ss.sssZ
 * - Date (invalid) → null
 * - RegExp → /pattern/flags
 * - Uint8Array / ArrayBuffer → b"base64..."
 * - Array → [...]
 * - Object → {...}
 * - Map (non-empty) → Map{k => v, ...}
 * - Map (empty) → Map{}
 * - Set (non-empty) → Set{v, ...}
 * - Set (empty) → Set{}
 * - undefined, Function, Symbol → omitted from objects; null in arrays
 *
 * @param value - JavaScript value to serialize
 * @param replacer - Optional replacer function
 * @returns RDN string, or undefined for non-serializable root values
 */
export function stringify(value: RDNValue, replacer?: RDNReplacer): string | undefined {
  // TODO: Implement serializer with cycle detection
  throw new Error("Not implemented");
}
