import type { RDNValue, RDNReplacer, RDNTimeOnly, RDNDuration } from "./types.js";
import { DIGIT_PAIRS, ESCAPE_TABLE, B64_ENCODE } from "./tables.js";

let depth = 0;
let seen: WeakSet<object> | null = null;

function checkCycle(obj: object): void {
  if (depth > 10) {
    if (!seen) seen = new WeakSet();
    if (seen.has(obj)) throw new TypeError("Converting circular structure to RDN");
    seen.add(obj);
  }
}

function removeCycle(obj: object): void {
  if (seen) seen.delete(obj);
}

// ── String escaping ─────────────────────────────────────────────────────

function escapeString(s: string): string {
  // Fast scan: check if any char needs escaping
  let needsEscape = false;
  for (let i = 0; i < s.length; i++) {
    const c = s.charCodeAt(i);
    if (c < 0x20 || c === 0x22 || c === 0x5C) {
      needsEscape = true;
      break;
    }
  }
  if (!needsEscape) return '"' + s + '"';

  // Slow path: build escaped string
  const parts: string[] = ['"'];
  let start = 0;
  for (let i = 0; i < s.length; i++) {
    const c = s.charCodeAt(i);
    if (c < 0x20 || c === 0x22 || c === 0x5C) {
      if (i > start) parts.push(s.slice(start, i));
      parts.push(c < 256 ? ESCAPE_TABLE[c]! : "\\u" + c.toString(16).padStart(4, "0"));
      start = i + 1;
    }
  }
  if (start < s.length) parts.push(s.slice(start));
  parts.push('"');
  return parts.join('');
}

// ── Date formatting with digit pair table ───────────────────────────────

function formatDate(d: Date): string {
  if (isNaN(d.getTime())) return "null";

  const y = d.getUTCFullYear();
  const year = y < 0 ? "-" + String(-y).padStart(4, "0") : String(y).padStart(4, "0");
  return "@" + year + "-" + DIGIT_PAIRS[d.getUTCMonth() + 1]! + "-" + DIGIT_PAIRS[d.getUTCDate()]! + "T" + DIGIT_PAIRS[d.getUTCHours()]! + ":" + DIGIT_PAIRS[d.getUTCMinutes()]! + ":" + DIGIT_PAIRS[d.getUTCSeconds()]! + "." + String(d.getUTCMilliseconds()).padStart(3, "0") + "Z";
}

// ── Base64 encoding ─────────────────────────────────────────────────────

function encodeBase64(bytes: Uint8Array): string {
  const parts: string[] = [];
  const len = bytes.length;
  let i = 0;

  // Process 3 bytes at a time → 4 chars
  for (; i + 2 < len; i += 3) {
    const a = bytes[i]!;
    const b = bytes[i + 1]!;
    const c = bytes[i + 2]!;
    parts.push(B64_ENCODE[(a >> 2)]!, B64_ENCODE[((a & 0x03) << 4) | (b >> 4)]!, B64_ENCODE[((b & 0x0F) << 2) | (c >> 6)]!, B64_ENCODE[(c & 0x3F)]!);
  }

  // Handle remaining bytes
  if (i < len) {
    const a = bytes[i]!;
    parts.push(B64_ENCODE[(a >> 2)]!);
    if (i + 1 < len) {
      const b = bytes[i + 1]!;
      parts.push(B64_ENCODE[((a & 0x03) << 4) | (b >> 4)]!, B64_ENCODE[((b & 0x0F) << 2)]!, "=");
    } else {
      parts.push(B64_ENCODE[((a & 0x03) << 4)]!, "==");
    }
  }

  return parts.join('');
}

// ── TimeOnly formatting ─────────────────────────────────────────────────

function formatTimeOnly(t: RDNTimeOnly): string {
  const parts: string[] = ["@", DIGIT_PAIRS[t.hours]!, ":", DIGIT_PAIRS[t.minutes]!, ":", DIGIT_PAIRS[t.seconds]!];
  if (t.milliseconds > 0) {
    parts.push(".", String(t.milliseconds).padStart(3, "0"));
  }
  return parts.join('');
}

// ── Core stringification ────────────────────────────────────────────────

function stringifyValue(value: RDNValue, replacer: RDNReplacer | undefined, key: string | RDNValue): string | undefined {
  // Apply replacer
  if (replacer) {
    value = replacer(key, value) as RDNValue;
    if (value === undefined) return undefined;
  }

  // null
  if (value === null) return "null";

  const t = typeof value;

  // string — most common
  if (t === "string") return escapeString(value as string);

  // number
  if (t === "number") {
    const n = value as number;
    if (Number.isNaN(n)) return "NaN";
    if (!Number.isFinite(n)) return n > 0 ? "Infinity" : "-Infinity";
    return String(n);
  }

  // boolean
  if (t === "boolean") return value ? "true" : "false";

  // bigint
  if (t === "bigint") return String(value) + "n";

  // undefined, function, symbol → not serializable
  if (t === "undefined" || t === "function" || t === "symbol") return undefined;

  // Objects
  const obj = value as object;

  // Date
  if (obj instanceof Date) return formatDate(obj);

  // RegExp
  if (obj instanceof RegExp) return "/" + obj.source + "/" + obj.flags;

  // Uint8Array
  if (obj instanceof Uint8Array) return 'b"' + encodeBase64(obj) + '"';

  // ArrayBuffer → convert to Uint8Array
  if (obj instanceof ArrayBuffer) return 'b"' + encodeBase64(new Uint8Array(obj)) + '"';

  // Array
  if (Array.isArray(obj)) {
    checkCycle(obj);
    depth++;
    const parts: string[] = [];
    for (let i = 0; i < obj.length; i++) {
      const el = stringifyValue(obj[i] as RDNValue, replacer, String(i));
      parts.push(el === undefined ? "null" : el);
    }
    depth--;
    removeCycle(obj);
    return "[" + parts.join(",") + "]";
  }

  // Map
  if (obj instanceof Map) {
    checkCycle(obj);
    depth++;
    const map = obj as Map<RDNValue, RDNValue>;
    if (map.size === 0) { depth--; removeCycle(obj); return "Map{}"; }
    const parts: string[] = [];
    for (const [mk, mv] of map) {
      const sk = stringifyValue(mk, replacer, mk);
      const sv = stringifyValue(mv, replacer, mk);
      if (sk !== undefined && sv !== undefined) {
        parts.push(sk + "=>" + sv);
      }
    }
    depth--;
    removeCycle(obj);
    return "Map{" + parts.join(",") + "}";
  }

  // Set
  if (obj instanceof Set) {
    checkCycle(obj);
    depth++;
    const set = obj as Set<RDNValue>;
    if (set.size === 0) { depth--; removeCycle(obj); return "Set{}"; }
    const parts: string[] = [];
    for (const sv of set) {
      const s = stringifyValue(sv, replacer, sv);
      if (s !== undefined) parts.push(s);
    }
    depth--;
    removeCycle(obj);
    return "Set{" + parts.join(",") + "}";
  }

  // Tagged types: TimeOnly, Duration
  if ("__type__" in obj) {
    const tagged = obj as RDNTimeOnly | RDNDuration;
    if (tagged.__type__ === "TimeOnly") return formatTimeOnly(tagged as RDNTimeOnly);
    if (tagged.__type__ === "Duration") return "@" + (tagged as RDNDuration).iso;
  }

  // Plain object
  checkCycle(obj);
  depth++;
  const parts: string[] = [];
  const plain = obj as { [key: string]: RDNValue };
  for (const k of Object.keys(plain)) {
    const sv = stringifyValue(plain[k]!, replacer, k);
    if (sv !== undefined) {
      parts.push(escapeString(k) + ":" + sv);
    }
  }
  depth--;
  removeCycle(obj);
  return "{" + parts.join(",") + "}";
}

// ── Public API ──────────────────────────────────────────────────────────

export function stringify(value: RDNValue, replacer?: RDNReplacer): string | undefined {
  depth = 0;
  seen = null;
  try {
    return stringifyValue(value, replacer, "");
  } finally {
    depth = 0;
    seen = null;
  }
}
