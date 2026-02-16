import { describe, it, expect } from "vitest";
import { parse, stringify } from "./index.js";
import type { RDNValue, RDNTimeOnly, RDNDuration } from "./types.js";
import { readFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const SUITE_DIR = join(__dirname, "../../../test-suite");

// ── Normalize parsed RDN values to $type-tagged JSON for comparison ─────

function normalizeForComparison(value: RDNValue): unknown {
  if (value === null) return null;
  if (typeof value === "string" || typeof value === "boolean") return value;

  if (typeof value === "number") {
    if (Number.isNaN(value)) return { $type: "Number", value: "NaN" };
    if (value === Infinity) return { $type: "Number", value: "Infinity" };
    if (value === -Infinity) return { $type: "Number", value: "-Infinity" };
    return value;
  }

  if (typeof value === "bigint") {
    return { $type: "BigInt", value: String(value) };
  }

  if (value instanceof Date) {
    return { $type: "Date", value: value.toISOString() };
  }

  if (value instanceof RegExp) {
    return { $type: "RegExp", value: { source: value.source, flags: value.flags } };
  }

  if (value instanceof Uint8Array) {
    // Encode to base64 for comparison
    return { $type: "Binary", value: encodeBase64(value) };
  }

  if (value instanceof Map) {
    const entries: unknown[] = [];
    for (const [k, v] of value) {
      entries.push([normalizeForComparison(k as RDNValue), normalizeForComparison(v as RDNValue)]);
    }
    return { $type: "Map", value: entries };
  }

  if (value instanceof Set) {
    const elements: unknown[] = [];
    for (const v of value) {
      elements.push(normalizeForComparison(v as RDNValue));
    }
    return { $type: "Set", value: elements };
  }

  if (typeof value === "object" && "__type__" in value) {
    if (value.__type__ === "TimeOnly") {
      const t = value as RDNTimeOnly;
      return { $type: "TimeOnly", value: { hours: t.hours, minutes: t.minutes, seconds: t.seconds, milliseconds: t.milliseconds } };
    }
    if (value.__type__ === "Duration") {
      return { $type: "Duration", value: (value as RDNDuration).iso };
    }
  }

  if (Array.isArray(value)) {
    return value.map(v => normalizeForComparison(v as RDNValue));
  }

  // Plain object
  const obj = value as { [key: string]: RDNValue };
  const result: Record<string, unknown> = {};
  for (const key of Object.keys(obj)) {
    result[key] = normalizeForComparison(obj[key]!);
  }
  return result;
}

function encodeBase64(bytes: Uint8Array): string {
  const charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
  let result = "";
  const len = bytes.length;
  let i = 0;
  for (; i + 2 < len; i += 3) {
    const a = bytes[i]!, b = bytes[i + 1]!, c = bytes[i + 2]!;
    result += charset[a >> 2]! + charset[((a & 0x03) << 4) | (b >> 4)]! + charset[((b & 0x0F) << 2) | (c >> 6)]! + charset[c & 0x3F]!;
  }
  if (i < len) {
    const a = bytes[i]!;
    if (i + 1 < len) {
      const b = bytes[i + 1]!;
      result += charset[a >> 2]! + charset[((a & 0x03) << 4) | (b >> 4)]! + charset[(b & 0x0F) << 2]! + "=";
    } else {
      result += charset[a >> 2]! + charset[(a & 0x03) << 4]! + "==";
    }
  }
  return result;
}

function deepEqual(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (a === null || b === null) return false;
  if (typeof a !== typeof b) return false;
  if (typeof a !== "object") return false;

  if (Array.isArray(a) && Array.isArray(b)) {
    if (a.length !== b.length) return false;
    return a.every((v, i) => deepEqual(v, b[i]));
  }

  const aKeys = Object.keys(a as object);
  const bKeys = Object.keys(b as object);
  if (aKeys.length !== bKeys.length) return false;
  return aKeys.every(k => deepEqual((a as Record<string, unknown>)[k], (b as Record<string, unknown>)[k]));
}

// ── Valid tests ─────────────────────────────────────────────────────────

describe("Conformance: valid", () => {
  const validDir = join(SUITE_DIR, "valid");
  let files: string[];
  try {
    files = readdirSync(validDir).filter((f: string) => f.endsWith(".rdn"));
  } catch {
    files = [];
  }

  for (const file of files) {
    const name = file.replace(".rdn", "");
    it(`valid/${name}`, () => {
      const input = readFileSync(join(validDir, file), "utf-8");
      const expectedJson = readFileSync(join(validDir, `${name}.expected.json`), "utf-8");
      const expected = JSON.parse(expectedJson);
      const parsed = parse(input);
      const normalized = normalizeForComparison(parsed);
      expect(deepEqual(normalized, expected)).toBe(true);
    });
  }
});

// ── Invalid tests ───────────────────────────────────────────────────────

describe("Conformance: invalid", () => {
  const invalidDir = join(SUITE_DIR, "invalid");
  let files: string[];
  try {
    files = readdirSync(invalidDir).filter((f: string) => f.endsWith(".rdn"));
  } catch {
    files = [];
  }

  for (const file of files) {
    const name = file.replace(".rdn", "");
    it(`invalid/${name}`, () => {
      const input = readFileSync(join(invalidDir, file), "utf-8");
      expect(() => parse(input)).toThrow();
    });
  }
});

// ── Roundtrip tests ─────────────────────────────────────────────────────

describe("Conformance: roundtrip", () => {
  const roundtripDir = join(SUITE_DIR, "roundtrip");
  let files: string[];
  try {
    files = readdirSync(roundtripDir).filter((f: string) => f.endsWith(".rdn"));
  } catch {
    files = [];
  }

  for (const file of files) {
    const name = file.replace(".rdn", "");
    it(`roundtrip/${name}`, () => {
      const input = readFileSync(join(roundtripDir, file), "utf-8");
      const parsed1 = parse(input);
      const serialized = stringify(parsed1);
      expect(serialized).toBeDefined();
      const parsed2 = parse(serialized!);
      const norm1 = normalizeForComparison(parsed1);
      const norm2 = normalizeForComparison(parsed2);
      expect(deepEqual(norm1, norm2)).toBe(true);
    });
  }
});
