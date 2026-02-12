import type { RDNValue, RDNReviver } from "./types.js";
import { timeOnly, duration } from "./types.js";
import { Token, TOKEN_TABLE, B64_DECODE, HEX_DECODE } from "./tables.js";

// Module-scoped cursor state — set on entry, cleared in finally
let source: string;
let pos: number;
let len: number;

function error(msg: string): never {
  throw new SyntaxError(`${msg} in RDN at position ${pos}`);
}

function skipWs(): void {
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if (c === 0x20 || c === 0x09 || c === 0x0A || c === 0x0D) { pos++; } else { break; }
  }
}

function expect(ch: number): void {
  if (pos >= len || source.charCodeAt(pos) !== ch) {
    error(`Expected '${String.fromCharCode(ch)}'`);
  }
  pos++;
}

// ── String parsing with deferred materialization ────────────────────────

function parseString(): string {
  pos++; // skip opening "
  const start = pos;
  let hasEscape = false;
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if (c === 0x22) { // closing "
      if (!hasEscape) {
        const result = source.slice(start, pos);
        pos++; // skip closing "
        return result;
      }
      // Slow path: materialize with escapes
      const result = materializeString(start, pos);
      pos++; // skip closing "
      return result;
    }
    if (c === 0x5C) { // backslash
      hasEscape = true;
      pos++; // skip the backslash
      if (pos >= len) break;
      if (source.charCodeAt(pos) === 0x75) { // \uXXXX
        pos += 5; // u + 4 hex digits
      } else {
        pos++;
      }
      continue;
    }
    if (c < 0x20) error("Unescaped control character in string");
    pos++;
  }
  error("Unterminated string");
}

function materializeString(start: number, end: number): string {
  const parts: string[] = [];
  let i = start;
  while (i < end) {
    const c = source.charCodeAt(i);
    if (c === 0x5C) { // backslash
      i++;
      const esc = source.charCodeAt(i);
      switch (esc) {
        case 0x22: parts.push('"'); i++; break;
        case 0x5C: parts.push('\\'); i++; break;
        case 0x2F: parts.push('/'); i++; break;
        case 0x62: parts.push('\b'); i++; break;
        case 0x66: parts.push('\f'); i++; break;
        case 0x6E: parts.push('\n'); i++; break;
        case 0x72: parts.push('\r'); i++; break;
        case 0x74: parts.push('\t'); i++; break;
        case 0x75: { // \uXXXX
          const hex = source.slice(i + 1, i + 5);
          if (hex.length < 4) error("Invalid unicode escape");
          const code = parseInt(hex, 16);
          if (Number.isNaN(code)) error("Invalid unicode escape");
          parts.push(String.fromCharCode(code));
          i += 5;
          break;
        }
        default: error(`Invalid escape sequence '\\${String.fromCharCode(esc)}'`);
      }
    } else {
      // Find the next backslash or end for bulk copy
      let j = i + 1;
      while (j < end && source.charCodeAt(j) !== 0x5C) j++;
      parts.push(source.slice(i, j));
      i = j;
    }
  }
  return parts.join('');
}

// ── Number parsing ──────────────────────────────────────────────────────

function parseNumber(negative: boolean): RDNValue {
  const start = negative ? pos - 1 : pos;
  // Accumulate integer digits
  let intValue = 0;
  let digitCount = 0;
  while (pos < len) {
    const d = source.charCodeAt(pos) - 0x30;
    if (d < 0 || d > 9) break;
    intValue = intValue * 10 + d;
    digitCount++;
    pos++;
  }
  if (digitCount === 0) error("Expected digit");

  // Leading zero check: "01" is invalid, "0" alone is ok, "0." and "0e" are ok
  if (digitCount > 1 && source.charCodeAt(start + (negative ? 1 : 0)) === 0x30) {
    error("Leading zeros not allowed");
  }

  // Check for bigint suffix 'n'
  if (pos < len && source.charCodeAt(pos) === 0x6E) { // 'n'
    pos++;
    return BigInt(source.slice(start, pos - 1));
  }

  let isFloat = false;

  // Fraction
  if (pos < len && source.charCodeAt(pos) === 0x2E) { // '.'
    isFloat = true;
    pos++; // skip '.'
    let fracDigits = 0;
    while (pos < len) {
      const d = source.charCodeAt(pos) - 0x30;
      if (d < 0 || d > 9) break;
      fracDigits++;
      pos++;
    }
    if (fracDigits === 0) error("Expected digit after decimal point");
  }

  // Exponent
  if (pos < len) {
    const e = source.charCodeAt(pos);
    if (e === 0x65 || e === 0x45) { // 'e' or 'E'
      isFloat = true;
      pos++;
      if (pos < len) {
        const sign = source.charCodeAt(pos);
        if (sign === 0x2B || sign === 0x2D) pos++; // + or -
      }
      let expDigits = 0;
      while (pos < len) {
        const d = source.charCodeAt(pos) - 0x30;
        if (d < 0 || d > 9) break;
        expDigits++;
        pos++;
      }
      if (expDigits === 0) error("Expected digit in exponent");
    }
  }

  // Check for invalid bigint suffix after float
  if (pos < len && source.charCodeAt(pos) === 0x6E) { // 'n'
    if (isFloat) error("BigInt cannot have decimal point or exponent");
  }

  // Fast path: small integers (≤15 digits, no float)
  if (!isFloat && digitCount <= 15) {
    return negative ? -intValue : intValue;
  }

  return Number(source.slice(start, pos));
}

// ── Date/Time parsing ───────────────────────────────────────────────────

function readDigits2(): number {
  const d1 = source.charCodeAt(pos) - 0x30;
  const d2 = source.charCodeAt(pos + 1) - 0x30;
  if (d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9) error("Expected 2-digit number");
  pos += 2;
  return d1 * 10 + d2;
}

function readDigits3(): number {
  const d1 = source.charCodeAt(pos) - 0x30;
  const d2 = source.charCodeAt(pos + 1) - 0x30;
  const d3 = source.charCodeAt(pos + 2) - 0x30;
  if (d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 || d3 < 0 || d3 > 9) error("Expected 3-digit number");
  pos += 3;
  return d1 * 100 + d2 * 10 + d3;
}

function readDigits4(): number {
  const d1 = source.charCodeAt(pos) - 0x30;
  const d2 = source.charCodeAt(pos + 1) - 0x30;
  const d3 = source.charCodeAt(pos + 2) - 0x30;
  const d4 = source.charCodeAt(pos + 3) - 0x30;
  if (d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 || d3 < 0 || d3 > 9 || d4 < 0 || d4 > 9) error("Expected 4-digit year");
  pos += 4;
  return d1 * 1000 + d2 * 100 + d3 * 10 + d4;
}

function parseAt(): RDNValue {
  pos++; // skip @

  if (pos >= len) error("Unexpected end after @");

  const ch = source.charCodeAt(pos);

  // Duration: @P...
  if (ch === 0x50) { // 'P'
    return parseDuration();
  }

  // Check if this looks like a time (digit digit colon) vs date (digit digit digit digit dash)
  // We need at least 2 chars to distinguish
  if (pos + 2 < len && ch >= 0x30 && ch <= 0x39) {
    const ch2 = source.charCodeAt(pos + 2);

    if (ch2 === 0x3A) { // ':' at position 2 → TimeOnly
      return parseTimeOnly();
    }

    if (pos + 4 < len && source.charCodeAt(pos + 4) === 0x2D) { // '-' at position 4 → DateTime
      return parseDateTime();
    }

    // Must be unix timestamp (digits only)
    return parseUnixTimestamp();
  }

  error("Invalid @ literal");
}

function parseDateTime(): Date {
  const year = readDigits4();
  expect(0x2D); // -
  const month = readDigits2();
  expect(0x2D); // -
  const day = readDigits2();

  // Date only: @YYYY-MM-DD (10 chars after @)
  if (pos >= len || source.charCodeAt(pos) !== 0x54) { // not 'T'
    return new Date(Date.UTC(year, month - 1, day));
  }

  pos++; // skip 'T'
  const hours = readDigits2();
  expect(0x3A); // :
  const minutes = readDigits2();
  expect(0x3A); // :
  const seconds = readDigits2();

  let ms = 0;
  if (pos < len && source.charCodeAt(pos) === 0x2E) { // '.'
    pos++; // skip '.'
    ms = readDigits3();
  }

  expect(0x5A); // 'Z'
  return new Date(Date.UTC(year, month - 1, day, hours, minutes, seconds, ms));
}

function parseTimeOnly(): RDNValue {
  const hours = readDigits2();
  expect(0x3A); // :
  const minutes = readDigits2();
  expect(0x3A); // :
  const seconds = readDigits2();

  let ms = 0;
  if (pos < len && source.charCodeAt(pos) === 0x2E) { // '.'
    pos++; // skip '.'
    ms = readDigits3();
  }

  return timeOnly(hours, minutes, seconds, ms);
}

function parseDuration(): RDNValue {
  const start = pos;
  pos++; // skip 'P'
  // Scan until we hit a non-duration character
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if ((c >= 0x30 && c <= 0x39) || c === 0x59 || c === 0x4D || c === 0x44 || c === 0x54 || c === 0x48 || c === 0x53 || c === 0x2E) {
      // 0-9, Y, M, D, T, H, S, .
      pos++;
    } else {
      break;
    }
  }
  const iso = source.slice(start, pos);
  if (iso.length < 2) error("Invalid duration");
  return duration(iso);
}

function parseUnixTimestamp(): Date {
  const start = pos;
  while (pos < len) {
    const d = source.charCodeAt(pos) - 0x30;
    if (d < 0 || d > 9) break;
    pos++;
  }
  const digits = source.slice(start, pos);
  const num = Number(digits);
  // ≤10 digits = seconds, >10 = milliseconds
  if (digits.length <= 10) {
    return new Date(num * 1000);
  }
  return new Date(num);
}

// ── RegExp parsing ──────────────────────────────────────────────────────

function parseRegExp(): RegExp {
  pos++; // skip opening /
  const patternStart = pos;
  let escaped = false;

  while (pos < len) {
    const c = source.charCodeAt(pos);
    if (escaped) {
      escaped = false;
      pos++;
      continue;
    }
    if (c === 0x5C) { // backslash
      escaped = true;
      pos++;
      continue;
    }
    if (c === 0x2F) { // closing /
      break;
    }
    pos++;
  }

  if (pos >= len) error("Unterminated regular expression");
  const pattern = source.slice(patternStart, pos);
  pos++; // skip closing /

  // Read flags
  const flagStart = pos;
  while (pos < len) {
    const c = source.charCodeAt(pos);
    // Valid flags: d g i m s u v y
    if (c === 0x64 || c === 0x67 || c === 0x69 || c === 0x6D || c === 0x73 || c === 0x75 || c === 0x76 || c === 0x79) {
      pos++;
    } else {
      break;
    }
  }
  const flags = source.slice(flagStart, pos);
  return new RegExp(pattern, flags);
}

// ── Binary parsing ──────────────────────────────────────────────────────

function parseBinaryB64(): Uint8Array {
  pos++; // skip 'b'
  if (pos >= len || source.charCodeAt(pos) !== 0x22) error("Expected '\"' after 'b'");
  pos++; // skip opening "

  const start = pos;
  // Scan to closing "
  while (pos < len && source.charCodeAt(pos) !== 0x22) pos++;
  if (pos >= len) error("Unterminated binary literal");
  const content = source.slice(start, pos);
  pos++; // skip closing "

  if (content.length === 0) return new Uint8Array(0);

  // Validate and decode base64
  if (content.length % 4 !== 0) error("Invalid base64: length must be a multiple of 4");

  // Count padding
  let padding = 0;
  if (content.charCodeAt(content.length - 1) === 0x3D) padding++;
  if (content.length > 1 && content.charCodeAt(content.length - 2) === 0x3D) padding++;

  const outLen = (content.length / 4) * 3 - padding;
  const out = new Uint8Array(outLen);

  let outPos = 0;
  for (let i = 0; i < content.length; i += 4) {
    const a = B64_DECODE[content.charCodeAt(i)]!;
    const b = B64_DECODE[content.charCodeAt(i + 1)]!;
    const c = B64_DECODE[content.charCodeAt(i + 2)]!;
    const d = B64_DECODE[content.charCodeAt(i + 3)]!;

    // Validate: 0xFF = invalid char, 0xFE = padding
    if (a === 0xFF || a === 0xFE || b === 0xFF || b === 0xFE) error("Invalid base64 character");
    if (c === 0xFF || d === 0xFF) error("Invalid base64 character");

    // Padding can only appear at end
    if (c === 0xFE) {
      // c and d must both be padding
      if (d !== 0xFE) error("Invalid base64 padding");
      // Check for non-zero padding bits
      if (b & 0x0F) error("Invalid base64: non-zero padding bits");
      out[outPos++] = (a << 2) | (b >> 4);
    } else if (d === 0xFE) {
      // Only d is padding
      // Check for non-zero padding bits
      if (c & 0x03) error("Invalid base64: non-zero padding bits");
      out[outPos++] = (a << 2) | (b >> 4);
      out[outPos++] = ((b & 0x0F) << 4) | (c >> 2);
    } else {
      out[outPos++] = (a << 2) | (b >> 4);
      out[outPos++] = ((b & 0x0F) << 4) | (c >> 2);
      out[outPos++] = ((c & 0x03) << 6) | d;
    }
  }

  return out;
}

function parseBinaryHex(): Uint8Array {
  pos++; // skip 'x'
  if (pos >= len || source.charCodeAt(pos) !== 0x22) error("Expected '\"' after 'x'");
  pos++; // skip opening "

  const start = pos;
  while (pos < len && source.charCodeAt(pos) !== 0x22) pos++;
  if (pos >= len) error("Unterminated hex literal");
  const content = source.slice(start, pos);
  pos++; // skip closing "

  if (content.length === 0) return new Uint8Array(0);
  if (content.length % 2 !== 0) error("Invalid hex: odd length");

  const out = new Uint8Array(content.length / 2);
  for (let i = 0; i < content.length; i += 2) {
    const hi = HEX_DECODE[content.charCodeAt(i)]!;
    const lo = HEX_DECODE[content.charCodeAt(i + 1)]!;
    if (hi === 0xFF || lo === 0xFF) error("Invalid hex character");
    out[i / 2] = (hi << 4) | lo;
  }
  return out;
}

// ── Collection parsing ──────────────────────────────────────────────────

function parseArray(): RDNValue[] {
  pos++; // skip [
  skipWs();
  if (pos < len && source.charCodeAt(pos) === 0x5D) { // ]
    pos++;
    return [];
  }
  const arr: RDNValue[] = [];
  arr.push(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2C) { // ,
    pos++;
    skipWs();
    arr.push(parseValue());
    skipWs();
  }
  expect(0x5D); // ]
  return arr;
}

function parseTuple(): RDNValue[] {
  pos++; // skip (
  skipWs();
  if (pos < len && source.charCodeAt(pos) === 0x29) { // )
    pos++;
    return [];
  }
  const arr: RDNValue[] = [];
  arr.push(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2C) { // ,
    pos++;
    skipWs();
    arr.push(parseValue());
    skipWs();
  }
  expect(0x29); // )
  return arr;
}

function parseBrace(): RDNValue {
  pos++; // skip {
  skipWs();
  // Empty braces → Object
  if (pos < len && source.charCodeAt(pos) === 0x7D) { // }
    pos++;
    return {};
  }

  // Parse first value
  const firstValue = parseValue();
  skipWs();

  if (pos >= len) error("Unterminated brace expression");

  const sep = source.charCodeAt(pos);

  // : → Object
  if (sep === 0x3A) {
    if (typeof firstValue !== "string") error("Object key must be a string");
    return finishObject(firstValue);
  }

  // = → check for => (Map)
  if (sep === 0x3D) {
    if (pos + 1 < len && source.charCodeAt(pos + 1) === 0x3E) { // =>
      return finishMap(firstValue);
    }
    error("Expected '=>'");
  }

  // , → Set
  if (sep === 0x2C) {
    return finishSet(firstValue);
  }

  // } → single-element Set
  if (sep === 0x7D) {
    pos++;
    const set = new Set<RDNValue>();
    set.add(firstValue);
    return set;
  }

  error("Expected ':', '=>', ',' or '}' after value in brace expression");
}

function finishObject(firstKey: string): { [key: string]: RDNValue } {
  const obj: { [key: string]: RDNValue } = Object.create(null);
  pos++; // skip :
  skipWs();
  obj[firstKey] = parseValue();
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2C) { // ,
    pos++;
    skipWs();
    const key = parseString();
    skipWs();
    expect(0x3A); // :
    skipWs();
    obj[key] = parseValue();
    skipWs();
  }
  expect(0x7D); // }
  return obj;
}

function finishMap(firstKey: RDNValue): Map<RDNValue, RDNValue> {
  const map = new Map<RDNValue, RDNValue>();
  pos += 2; // skip =>
  skipWs();
  map.set(firstKey, parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2C) { // ,
    pos++;
    skipWs();
    const key = parseValue();
    skipWs();
    if (pos + 1 >= len || source.charCodeAt(pos) !== 0x3D || source.charCodeAt(pos + 1) !== 0x3E) {
      error("Expected '=>' in map entry");
    }
    pos += 2; // skip =>
    skipWs();
    map.set(key, parseValue());
    skipWs();
  }
  expect(0x7D); // }
  return map;
}

function finishSet(firstValue: RDNValue): Set<RDNValue> {
  const set = new Set<RDNValue>();
  set.add(firstValue);
  pos++; // skip ,
  skipWs();
  set.add(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2C) { // ,
    pos++;
    skipWs();
    set.add(parseValue());
    skipWs();
  }
  expect(0x7D); // }
  return set;
}

function parseExplicitMap(): Map<RDNValue, RDNValue> {
  // pos is at 'M', check for 'Map{'
  if (pos + 3 >= len || source.charCodeAt(pos + 1) !== 0x61 || source.charCodeAt(pos + 2) !== 0x70 || source.charCodeAt(pos + 3) !== 0x7B) {
    error("Expected 'Map{'");
  }
  pos += 4; // skip 'Map{'
  skipWs();
  const map = new Map<RDNValue, RDNValue>();
  if (pos < len && source.charCodeAt(pos) === 0x7D) { // }
    pos++;
    return map;
  }
  // Parse first entry
  const key = parseValue();
  skipWs();
  if (pos + 1 >= len || source.charCodeAt(pos) !== 0x3D || source.charCodeAt(pos + 1) !== 0x3E) {
    error("Expected '=>' in map entry");
  }
  pos += 2; // skip =>
  skipWs();
  map.set(key, parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2C) { // ,
    pos++;
    skipWs();
    const k = parseValue();
    skipWs();
    if (pos + 1 >= len || source.charCodeAt(pos) !== 0x3D || source.charCodeAt(pos + 1) !== 0x3E) {
      error("Expected '=>' in map entry");
    }
    pos += 2;
    skipWs();
    map.set(k, parseValue());
    skipWs();
  }
  expect(0x7D); // }
  return map;
}

function parseExplicitSet(): Set<RDNValue> {
  // pos is at 'S', check for 'Set{'
  if (pos + 3 >= len || source.charCodeAt(pos + 1) !== 0x65 || source.charCodeAt(pos + 2) !== 0x74 || source.charCodeAt(pos + 3) !== 0x7B) {
    error("Expected 'Set{'");
  }
  pos += 4; // skip 'Set{'
  skipWs();
  const set = new Set<RDNValue>();
  if (pos < len && source.charCodeAt(pos) === 0x7D) { // }
    pos++;
    return set;
  }
  set.add(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2C) { // ,
    pos++;
    skipWs();
    set.add(parseValue());
    skipWs();
  }
  expect(0x7D); // }
  return set;
}

// ── Literal parsing ─────────────────────────────────────────────────────

function parseLiteral(expected: string, value: RDNValue): RDNValue {
  for (let i = 0; i < expected.length; i++) {
    if (pos >= len || source.charCodeAt(pos) !== expected.charCodeAt(i)) {
      error(`Expected '${expected}'`);
    }
    pos++;
  }
  return value;
}

// ── Main value dispatch ─────────────────────────────────────────────────

function parseValue(): RDNValue {
  skipWs();
  if (pos >= len) error("Unexpected end of input");

  const ch = source.charCodeAt(pos);
  const token = TOKEN_TABLE[ch]!;

  switch (token) {
    case Token.STRING: return parseString();
    case Token.NUMBER: return parseNumber(false);
    case Token.MINUS: {
      pos++; // skip -
      // -Infinity
      if (pos < len && source.charCodeAt(pos) === 0x49) { // 'I'
        parseLiteral("Infinity", null);
        return -Infinity;
      }
      return parseNumber(true);
    }
    case Token.OPEN_BRACE: return parseBrace();
    case Token.OPEN_BRACKET: return parseArray();
    case Token.OPEN_PAREN: return parseTuple();
    case Token.TRUE: return parseLiteral("true", true);
    case Token.FALSE: return parseLiteral("false", false);
    case Token.NULL: return parseLiteral("null", null);
    case Token.AT: return parseAt();
    case Token.SLASH: return parseRegExp();
    case Token.B64: return parseBinaryB64();
    case Token.HEX: return parseBinaryHex();
    case Token.INFINITY: return parseLiteral("Infinity", Infinity);
    case Token.NAN: return parseLiteral("NaN", NaN);
    case Token.MAP: return parseExplicitMap();
    case Token.SET: return parseExplicitSet();
    default: error(`Unexpected character '${String.fromCharCode(ch)}'`);
  }
}

// ── Reviver ─────────────────────────────────────────────────────────────

function applyReviver(holder: { [key: string]: RDNValue }, key: string, reviver: RDNReviver): RDNValue {
  let val = holder[key] as RDNValue;
  if (val !== null && typeof val === "object") {
    if (Array.isArray(val)) {
      for (let i = 0; i < val.length; i++) {
        const wrapper: { [key: string]: RDNValue } = { [String(i)]: val[i]! };
        const newVal = applyReviver(wrapper, String(i), reviver);
        if (newVal === undefined) {
          val.splice(i, 1);
          i--;
        } else {
          val[i] = newVal;
        }
      }
    } else if (val instanceof Map) {
      for (const [mk, mv] of val) {
        const wrapper: { [key: string]: RDNValue } = {};
        (wrapper as Record<string, RDNValue>)["value"] = mv;
        const newVal = reviver.call(holder, mk as string | RDNValue, mv);
        if (newVal === undefined) {
          val.delete(mk);
        } else {
          val.set(mk, newVal);
        }
      }
    } else if (val instanceof Set) {
      const entries = [...val];
      val.clear();
      for (const entry of entries) {
        const newVal = reviver.call(holder, entry as string | RDNValue, entry);
        if (newVal !== undefined) {
          val.add(newVal);
        }
      }
    } else if (!(val instanceof Date) && !(val instanceof RegExp) && !(val instanceof Uint8Array) && !("__type__" in val)) {
      const obj = val as { [key: string]: RDNValue };
      for (const k of Object.keys(obj)) {
        const wrapper: { [key: string]: RDNValue } = { [k]: obj[k]! };
        const newVal = applyReviver(wrapper, k, reviver);
        if (newVal === undefined) {
          delete obj[k];
        } else {
          obj[k] = newVal;
        }
      }
    }
  }
  return reviver.call(holder, key, val) as RDNValue;
}

// ── Public API ──────────────────────────────────────────────────────────

export function parse(text: string, reviver?: RDNReviver): RDNValue {
  if (typeof text !== "string") {
    throw new TypeError("First argument must be a string");
  }
  source = text;
  pos = 0;
  len = text.length;
  try {
    const result = parseValue();
    skipWs();
    if (pos < len) error("Unexpected data after value");
    if (reviver) {
      const root: { [key: string]: RDNValue } = { "": result };
      return applyReviver(root, "", reviver);
    }
    return result;
  } finally {
    source = "";
    pos = 0;
    len = 0;
  }
}
