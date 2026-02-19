import type { DocumentNode, RdnCstNode, StringLiteralNode, ObjectPropertyNode, MapEntryNode } from "./cst.js";
import { Token, TOKEN_TABLE } from "./tables.js";

// Module-scoped cursor state — set on entry, cleared in finally
let source: string;
let pos: number;
let len: number;
let depth: number;

const MAX_DEPTH = 128;

function error(msg: string): never {
  throw new SyntaxError(`${msg} in RDN at position ${pos}`);
}

function skipWs(): void {
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if (c === 0x20 || c === 0x09 || c === 0x0a || c === 0x0d) { pos++; } else { break; }
  }
}

function expect(ch: number): void {
  if (pos >= len || source.charCodeAt(pos) !== ch) {
    error(`Expected '${String.fromCharCode(ch)}'`);
  }
  pos++;
}

// ── String parsing ────────────────────────────────────────────────────

function parseString(): StringLiteralNode {
  const start = pos;
  pos++; // skip opening "
  let hasEscape = false;
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if (c === 0x22) { // closing "
      pos++; // skip closing "
      const raw = source.slice(start, pos);
      const value = hasEscape ? materializeString(start + 1, pos - 1) : source.slice(start + 1, pos - 1);
      return { type: "StringLiteral", start, end: pos, value, raw };
    }
    if (c === 0x5c) { // backslash
      hasEscape = true;
      pos++; // skip backslash
      if (pos >= len) break;
      if (source.charCodeAt(pos) === 0x75) { // \uXXXX
        pos += 5;
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
    if (c === 0x5c) { // backslash
      i++;
      const esc = source.charCodeAt(i);
      switch (esc) {
        case 0x22: parts.push('"'); i++; break;
        case 0x5c: parts.push('\\'); i++; break;
        case 0x2f: parts.push('/'); i++; break;
        case 0x62: parts.push('\b'); i++; break;
        case 0x66: parts.push('\f'); i++; break;
        case 0x6e: parts.push('\n'); i++; break;
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
      let j = i + 1;
      while (j < end && source.charCodeAt(j) !== 0x5c) j++;
      parts.push(source.slice(i, j));
      i = j;
    }
  }
  return parts.join("");
}

// ── Number / BigInt parsing ───────────────────────────────────────────

function parseNumber(negative: boolean): RdnCstNode {
  const start = negative ? pos - 1 : pos;
  let digitCount = 0;
  while (pos < len) {
    const d = source.charCodeAt(pos) - 0x30;
    if (d < 0 || d > 9) break;
    digitCount++;
    pos++;
  }
  if (digitCount === 0) error("Expected digit");

  // Leading zero check
  if (digitCount > 1 && source.charCodeAt(start + (negative ? 1 : 0)) === 0x30) {
    error("Leading zeros not allowed");
  }

  // BigInt suffix 'n'
  if (pos < len && source.charCodeAt(pos) === 0x6e) {
    pos++;
    return { type: "BigIntLiteral", start, end: pos, raw: source.slice(start, pos) };
  }

  let isFloat = false;

  // Fraction
  if (pos < len && source.charCodeAt(pos) === 0x2e) { // '.'
    isFloat = true;
    pos++;
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
        if (sign === 0x2b || sign === 0x2d) pos++;
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

  // Invalid bigint after float
  if (pos < len && source.charCodeAt(pos) === 0x6e) {
    if (isFloat) error("BigInt cannot have decimal point or exponent");
  }

  return { type: "NumberLiteral", start, end: pos, raw: source.slice(start, pos) };
}

// ── Date/Time/Duration parsing ────────────────────────────────────────

function parseAt(): RdnCstNode {
  const start = pos;
  pos++; // skip @

  if (pos >= len) error("Unexpected end after @");
  const ch = source.charCodeAt(pos);

  // Duration: @P...
  if (ch === 0x50) { // 'P'
    return parseDuration(start);
  }

  // Distinguish time vs date vs unix
  if (pos + 2 < len && ch >= 0x30 && ch <= 0x39) {
    const ch2 = source.charCodeAt(pos + 2);

    if (ch2 === 0x3a) { // ':' at position 2 → TimeOnly
      return parseTimeOnly(start);
    }

    if (pos + 4 < len && source.charCodeAt(pos + 4) === 0x2d) { // '-' at position 4 → DateTime
      return parseDateTime(start);
    }

    // Unix timestamp
    return parseUnixTimestamp(start);
  }

  error("Invalid @ literal");
}

function skipDigits(count: number): void {
  for (let i = 0; i < count; i++) {
    if (pos >= len) error("Unexpected end of input");
    const d = source.charCodeAt(pos) - 0x30;
    if (d < 0 || d > 9) error("Expected digit");
    pos++;
  }
}

function parseDateTime(start: number): RdnCstNode {
  skipDigits(4); // year
  expect(0x2d); // -
  skipDigits(2); // month
  expect(0x2d); // -
  skipDigits(2); // day

  // Date only or DateTime?
  if (pos < len && source.charCodeAt(pos) === 0x54) { // 'T'
    pos++;
    skipDigits(2); // hours
    expect(0x3a); // :
    skipDigits(2); // minutes
    expect(0x3a); // :
    skipDigits(2); // seconds

    if (pos < len && source.charCodeAt(pos) === 0x2e) { // '.'
      pos++;
      skipDigits(3); // milliseconds
    }

    expect(0x5a); // 'Z'
  }

  return { type: "DateTimeLiteral", start, end: pos, raw: source.slice(start, pos) };
}

function parseTimeOnly(start: number): RdnCstNode {
  skipDigits(2); // hours
  expect(0x3a);
  skipDigits(2); // minutes
  expect(0x3a);
  skipDigits(2); // seconds

  if (pos < len && source.charCodeAt(pos) === 0x2e) { // '.'
    pos++;
    skipDigits(3); // milliseconds
  }

  return { type: "TimeOnlyLiteral", start, end: pos, raw: source.slice(start, pos) };
}

function parseDuration(start: number): RdnCstNode {
  pos++; // skip 'P'
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if ((c >= 0x30 && c <= 0x39) || c === 0x59 || c === 0x4d || c === 0x44 || c === 0x54 || c === 0x48 || c === 0x53 || c === 0x2e) {
      pos++;
    } else {
      break;
    }
  }
  const raw = source.slice(start, pos);
  if (raw.length < 3) error("Invalid duration"); // @P + at least one component
  return { type: "DurationLiteral", start, end: pos, raw };
}

function parseUnixTimestamp(start: number): RdnCstNode {
  while (pos < len) {
    const d = source.charCodeAt(pos) - 0x30;
    if (d < 0 || d > 9) break;
    pos++;
  }
  return { type: "DateTimeLiteral", start, end: pos, raw: source.slice(start, pos) };
}

// ── RegExp parsing ────────────────────────────────────────────────────

function parseRegExp(): RdnCstNode {
  const start = pos;
  pos++; // skip opening /
  let escaped = false;
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if (escaped) { escaped = false; pos++; continue; }
    if (c === 0x5c) { escaped = true; pos++; continue; }
    if (c === 0x2f) break; // closing /
    pos++;
  }
  if (pos >= len) error("Unterminated regular expression");
  pos++; // skip closing /

  // Read flags
  while (pos < len) {
    const c = source.charCodeAt(pos);
    if (c === 0x64 || c === 0x67 || c === 0x69 || c === 0x6d || c === 0x73 || c === 0x75 || c === 0x76 || c === 0x79) {
      pos++;
    } else {
      break;
    }
  }

  return { type: "RegExpLiteral", start, end: pos, raw: source.slice(start, pos) };
}

// ── Binary parsing ────────────────────────────────────────────────────

function parseBinary(encoding: "base64" | "hex"): RdnCstNode {
  const start = pos;
  pos++; // skip 'b' or 'x'
  if (pos >= len || source.charCodeAt(pos) !== 0x22) error(`Expected '"' after '${encoding === "base64" ? "b" : "x"}'`);
  pos++; // skip opening "
  while (pos < len && source.charCodeAt(pos) !== 0x22) pos++;
  if (pos >= len) error("Unterminated binary literal");
  pos++; // skip closing "
  return { type: "BinaryLiteral", start, end: pos, encoding, raw: source.slice(start, pos) };
}

// ── Collection parsing ────────────────────────────────────────────────

function enterContainer(): void {
  if (++depth > MAX_DEPTH) throw new RangeError(`Maximum nesting depth exceeded (${MAX_DEPTH})`);
}

function parseArray(): RdnCstNode {
  const start = pos;
  enterContainer();
  pos++; // skip [
  skipWs();
  if (pos < len && source.charCodeAt(pos) === 0x5d) { // ]
    pos++;
    depth--;
    return { type: "Array", start, end: pos, elements: [] };
  }
  const elements: RdnCstNode[] = [];
  elements.push(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2c) { // ,
    pos++;
    skipWs();
    elements.push(parseValue());
    skipWs();
  }
  expect(0x5d); // ]
  depth--;
  return { type: "Array", start, end: pos, elements };
}

function parseTuple(): RdnCstNode {
  const start = pos;
  enterContainer();
  pos++; // skip (
  skipWs();
  if (pos < len && source.charCodeAt(pos) === 0x29) { // )
    pos++;
    depth--;
    return { type: "Tuple", start, end: pos, elements: [] };
  }
  const elements: RdnCstNode[] = [];
  elements.push(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2c) { // ,
    pos++;
    skipWs();
    elements.push(parseValue());
    skipWs();
  }
  expect(0x29); // )
  depth--;
  return { type: "Tuple", start, end: pos, elements };
}

function parseBrace(): RdnCstNode {
  const start = pos;
  enterContainer();
  pos++; // skip {
  skipWs();

  // Empty braces → Object
  if (pos < len && source.charCodeAt(pos) === 0x7d) { // }
    pos++;
    depth--;
    return { type: "Object", start, end: pos, properties: [] };
  }

  // Parse first value
  const firstValue = parseValue();
  skipWs();

  if (pos >= len) error("Unterminated brace expression");
  const sep = source.charCodeAt(pos);

  // : → Object
  if (sep === 0x3a) {
    if (firstValue.type !== "StringLiteral") error("Object key must be a string");
    return finishObject(start, firstValue);
  }

  // => → Map
  if (sep === 0x3d) {
    if (pos + 1 < len && source.charCodeAt(pos + 1) === 0x3e) {
      return finishMap(start, firstValue);
    }
    error("Expected '=>'");
  }

  // , → Set
  if (sep === 0x2c) {
    return finishSet(start, firstValue);
  }

  // } → single-element Set
  if (sep === 0x7d) {
    pos++;
    depth--;
    return { type: "Set", start, end: pos, elements: [firstValue], explicit: false };
  }

  error("Expected ':', '=>', ',' or '}' after value in brace expression");
}

function finishObject(start: number, firstKey: StringLiteralNode): RdnCstNode {
  const properties: ObjectPropertyNode[] = [];
  pos++; // skip :
  skipWs();
  const firstVal = parseValue();
  properties.push({ type: "ObjectProperty", start: firstKey.start, end: firstVal.end, key: firstKey, value: firstVal });
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2c) { // ,
    pos++;
    skipWs();
    const key = parseString();
    skipWs();
    expect(0x3a); // :
    skipWs();
    const val = parseValue();
    properties.push({ type: "ObjectProperty", start: key.start, end: val.end, key, value: val });
    skipWs();
  }
  expect(0x7d); // }
  depth--;
  return { type: "Object", start, end: pos, properties };
}

function finishMap(start: number, firstKey: RdnCstNode): RdnCstNode {
  const entries: MapEntryNode[] = [];
  pos += 2; // skip =>
  skipWs();
  const firstVal = parseValue();
  entries.push({ type: "MapEntry", start: firstKey.start, end: firstVal.end, key: firstKey, value: firstVal });
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2c) { // ,
    pos++;
    skipWs();
    const key = parseValue();
    skipWs();
    if (pos + 1 >= len || source.charCodeAt(pos) !== 0x3d || source.charCodeAt(pos + 1) !== 0x3e) {
      error("Expected '=>' in map entry");
    }
    pos += 2;
    skipWs();
    const val = parseValue();
    entries.push({ type: "MapEntry", start: key.start, end: val.end, key, value: val });
    skipWs();
  }
  expect(0x7d); // }
  depth--;
  return { type: "Map", start, end: pos, entries, explicit: false };
}

function finishSet(start: number, firstValue: RdnCstNode): RdnCstNode {
  const elements: RdnCstNode[] = [firstValue];
  pos++; // skip ,
  skipWs();
  elements.push(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2c) { // ,
    pos++;
    skipWs();
    elements.push(parseValue());
    skipWs();
  }
  expect(0x7d); // }
  depth--;
  return { type: "Set", start, end: pos, elements, explicit: false };
}

function parseExplicitMap(): RdnCstNode {
  const start = pos;
  enterContainer();
  if (pos + 3 >= len || source.charCodeAt(pos + 1) !== 0x61 || source.charCodeAt(pos + 2) !== 0x70 || source.charCodeAt(pos + 3) !== 0x7b) {
    error("Expected 'Map{'");
  }
  pos += 4; // skip 'Map{'
  skipWs();
  const entries: MapEntryNode[] = [];
  if (pos < len && source.charCodeAt(pos) === 0x7d) { // }
    pos++;
    depth--;
    return { type: "Map", start, end: pos, entries, explicit: true };
  }
  // Parse entries
  const key = parseValue();
  skipWs();
  if (pos + 1 >= len || source.charCodeAt(pos) !== 0x3d || source.charCodeAt(pos + 1) !== 0x3e) {
    error("Expected '=>' in map entry");
  }
  pos += 2;
  skipWs();
  const val = parseValue();
  entries.push({ type: "MapEntry", start: key.start, end: val.end, key, value: val });
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2c) { // ,
    pos++;
    skipWs();
    const k = parseValue();
    skipWs();
    if (pos + 1 >= len || source.charCodeAt(pos) !== 0x3d || source.charCodeAt(pos + 1) !== 0x3e) {
      error("Expected '=>' in map entry");
    }
    pos += 2;
    skipWs();
    const v = parseValue();
    entries.push({ type: "MapEntry", start: k.start, end: v.end, key: k, value: v });
    skipWs();
  }
  expect(0x7d); // }
  depth--;
  return { type: "Map", start, end: pos, entries, explicit: true };
}

function parseExplicitSet(): RdnCstNode {
  const start = pos;
  enterContainer();
  if (pos + 3 >= len || source.charCodeAt(pos + 1) !== 0x65 || source.charCodeAt(pos + 2) !== 0x74 || source.charCodeAt(pos + 3) !== 0x7b) {
    error("Expected 'Set{'");
  }
  pos += 4; // skip 'Set{'
  skipWs();
  const elements: RdnCstNode[] = [];
  if (pos < len && source.charCodeAt(pos) === 0x7d) { // }
    pos++;
    depth--;
    return { type: "Set", start, end: pos, elements, explicit: true };
  }
  elements.push(parseValue());
  skipWs();
  while (pos < len && source.charCodeAt(pos) === 0x2c) { // ,
    pos++;
    skipWs();
    elements.push(parseValue());
    skipWs();
  }
  expect(0x7d); // }
  depth--;
  return { type: "Set", start, end: pos, elements, explicit: true };
}

// ── Literal parsing ───────────────────────────────────────────────────

function expectLiteral(expected: string): void {
  for (let i = 0; i < expected.length; i++) {
    if (pos >= len || source.charCodeAt(pos) !== expected.charCodeAt(i)) {
      error(`Expected '${expected}'`);
    }
    pos++;
  }
}

// ── Main value dispatch ───────────────────────────────────────────────

function parseValue(): RdnCstNode {
  skipWs();
  if (pos >= len) error("Unexpected end of input");

  const ch = source.charCodeAt(pos);
  const token = TOKEN_TABLE[ch]!;

  switch (token) {
    case Token.STRING: return parseString();
    case Token.NUMBER: return parseNumber(false);
    case Token.MINUS: {
      const start = pos;
      pos++; // skip -
      if (pos < len && source.charCodeAt(pos) === 0x49) { // 'I' → -Infinity
        expectLiteral("Infinity");
        return { type: "InfinityLiteral", start, end: pos, negative: true };
      }
      return parseNumber(true);
    }
    case Token.OPEN_BRACE: return parseBrace();
    case Token.OPEN_BRACKET: return parseArray();
    case Token.OPEN_PAREN: return parseTuple();
    case Token.TRUE: {
      const start = pos;
      expectLiteral("true");
      return { type: "BooleanLiteral", start, end: pos, value: true };
    }
    case Token.FALSE: {
      const start = pos;
      expectLiteral("false");
      return { type: "BooleanLiteral", start, end: pos, value: false };
    }
    case Token.NULL: {
      const start = pos;
      expectLiteral("null");
      return { type: "NullLiteral", start, end: pos };
    }
    case Token.AT: return parseAt();
    case Token.SLASH: return parseRegExp();
    case Token.B64: return parseBinary("base64");
    case Token.HEX: return parseBinary("hex");
    case Token.INFINITY: {
      const start = pos;
      expectLiteral("Infinity");
      return { type: "InfinityLiteral", start, end: pos, negative: false };
    }
    case Token.NAN: {
      const start = pos;
      expectLiteral("NaN");
      return { type: "NaNLiteral", start, end: pos };
    }
    case Token.MAP: return parseExplicitMap();
    case Token.SET: return parseExplicitSet();
    default: error(`Unexpected character '${String.fromCharCode(ch)}'`);
  }
}

// ── Public API ────────────────────────────────────────────────────────

export function parse(text: string): DocumentNode {
  if (typeof text !== "string") {
    throw new TypeError("First argument must be a string");
  }
  source = text;
  pos = 0;
  len = text.length;
  depth = 0;
  try {
    const body = parseValue();
    skipWs();
    if (pos < len) error("Unexpected data after value");
    return { type: "Document", start: 0, end: len, body };
  } finally {
    source = "";
    pos = 0;
    len = 0;
    depth = 0;
  }
}
