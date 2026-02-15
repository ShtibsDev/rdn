import { describe, it, expect } from "vitest";
import { stringify } from "./serializer.js";
import { timeOnly, duration } from "./types.js";

describe("RDN.stringify", () => {
  describe("JSON-compatible types", () => {
    it("serializes null", () => {
      expect(stringify(null)).toBe("null");
    });
    it("serializes booleans", () => {
      expect(stringify(true as never)).toBe("true");
      expect(stringify(false as never)).toBe("false");
    });
    it("serializes numbers", () => {
      expect(stringify(0 as never)).toBe("0");
      expect(stringify(42 as never)).toBe("42");
      expect(stringify(3.14 as never)).toBe("3.14");
      expect(stringify(-1 as never)).toBe("-1");
    });
    it("serializes strings", () => {
      expect(stringify("hello")).toBe('"hello"');
      expect(stringify("a\"b")).toBe('"a\\"b"');
      expect(stringify("a\\b")).toBe('"a\\\\b"');
      expect(stringify("line\nnew")).toBe('"line\\nnew"');
      expect(stringify("tab\there")).toBe('"tab\\there"');
    });
    it("serializes strings with control characters", () => {
      expect(stringify("\x00")).toBe('"\\u0000"');
      expect(stringify("\x1f")).toBe('"\\u001f"');
    });
    it("serializes arrays", () => {
      expect(stringify([])).toBe("[]");
      expect(stringify([1, 2, 3] as never)).toBe("[1,2,3]");
      expect(stringify([1, "two", true] as never)).toBe('[1,"two",true]');
    });
    it("serializes objects", () => {
      expect(stringify({} as never)).toBe("{}");
      expect(stringify({ a: 1 } as never)).toBe('{"a":1}');
      expect(stringify({ a: 1, b: "two" } as never)).toBe('{"a":1,"b":"two"}');
    });
    it("serializes nested structures", () => {
      expect(stringify({ a: [1, { b: 2 }] } as never)).toBe('{"a":[1,{"b":2}]}');
    });
  });

  describe("special numbers", () => {
    it("serializes NaN", () => {
      expect(stringify(NaN as never)).toBe("NaN");
    });
    it("serializes Infinity", () => {
      expect(stringify(Infinity as never)).toBe("Infinity");
    });
    it("serializes -Infinity", () => {
      expect(stringify(-Infinity as never)).toBe("-Infinity");
    });
  });

  describe("BigInt", () => {
    it("serializes bigint with n suffix", () => {
      expect(stringify(42n)).toBe("42n");
      expect(stringify(0n)).toBe("0n");
      expect(stringify(-99n)).toBe("-99n");
      expect(stringify(999999999999999999n)).toBe("999999999999999999n");
    });
  });

  describe("Date", () => {
    it("serializes valid date as @ISO", () => {
      const d = new Date("2024-01-15T10:30:00.123Z");
      expect(stringify(d as never)).toBe("@2024-01-15T10:30:00.123Z");
    });
    it("serializes date with zero milliseconds", () => {
      const d = new Date("2024-01-15T10:30:00.000Z");
      expect(stringify(d as never)).toBe("@2024-01-15T10:30:00.000Z");
    });
    it("serializes invalid date as null", () => {
      expect(stringify(new Date("invalid") as never)).toBe("null");
    });
  });

  describe("RegExp", () => {
    it("serializes regexp with /pattern/flags", () => {
      expect(stringify(/^[a-z]+$/i as never)).toBe("/^[a-z]+$/i");
      expect(stringify(/test/gi as never)).toBe("/test/gi");
    });
    it("serializes regexp without flags", () => {
      expect(stringify(/test/ as never)).toBe("/test/");
    });
  });

  describe("Binary", () => {
    it('serializes Uint8Array as b"base64"', () => {
      const buf = new Uint8Array([72, 101, 108, 108, 111]); // "Hello"
      expect(stringify(buf as never)).toBe('b"SGVsbG8="');
    });
    it("serializes empty Uint8Array", () => {
      expect(stringify(new Uint8Array(0) as never)).toBe('b""');
    });
    it("serializes ArrayBuffer", () => {
      const buf = new Uint8Array([1, 2, 3]).buffer;
      expect(stringify(buf as never)).toBe('b"AQID"');
    });
  });

  describe("Map", () => {
    it("serializes non-empty map as Map{k => v}", () => {
      const m = new Map<string, number>([["a", 1], ["b", 2]]);
      expect(stringify(m as never)).toBe('Map{"a"=>1,"b"=>2}');
    });
    it("serializes empty map as Map{}", () => {
      expect(stringify(new Map() as never)).toBe("Map{}");
    });
  });

  describe("Set", () => {
    it("serializes non-empty set as Set{v, ...}", () => {
      const s = new Set([1, 2, 3]);
      expect(stringify(s as never)).toBe("Set{1,2,3}");
    });
    it("serializes empty set as Set{}", () => {
      expect(stringify(new Set() as never)).toBe("Set{}");
    });
  });

  describe("TimeOnly", () => {
    it("serializes time without milliseconds", () => {
      expect(stringify(timeOnly(14, 30, 0))).toBe("@14:30:00");
    });
    it("serializes time with milliseconds", () => {
      expect(stringify(timeOnly(23, 59, 59, 999))).toBe("@23:59:59.999");
    });
  });

  describe("Duration", () => {
    it("serializes duration", () => {
      expect(stringify(duration("P1Y2M3DT4H5M6S"))).toBe("@P1Y2M3DT4H5M6S");
      expect(stringify(duration("PT1H"))).toBe("@PT1H");
    });
  });

  describe("special values", () => {
    it("omits undefined from objects", () => {
      expect(stringify({ a: 1, b: undefined } as never)).toBe('{"a":1}');
    });
    it("serializes undefined as null in arrays", () => {
      expect(stringify([1, undefined, 3] as never)).toBe("[1,null,3]");
    });
    it("returns undefined for non-serializable root", () => {
      expect(stringify(undefined as never)).toBeUndefined();
    });
  });

  describe("replacer", () => {
    it("applies replacer to values", () => {
      const result = stringify({ a: 1, b: 2 } as never, (_key, value) => {
        return typeof value === "number" ? (value as number) * 2 : value;
      });
      expect(result).toBe('{"a":2,"b":4}');
    });
    it("omits properties when replacer returns undefined", () => {
      const result = stringify({ a: 1, b: 2 } as never, (_key, value) => {
        return value === 2 ? undefined : value;
      });
      expect(result).toBe('{"a":1}');
    });
  });

  describe("cycle detection", () => {
    it("throws TypeError on circular object reference at depth 1", () => {
      const a: any = {};
      a.self = a;
      expect(() => stringify(a as never)).toThrow(TypeError);
    });
    it("throws TypeError on circular reference through arrays at depth 2", () => {
      const a: any = {};
      const arr: any[] = [a];
      a.list = arr;
      expect(() => stringify(a as never)).toThrow(TypeError);
    });
    it("throws TypeError on deep circular reference", () => {
      let current: Record<string, unknown> = { root: true };
      const root = current;
      for (let i = 0; i < 12; i++) {
        const next: Record<string, unknown> = {};
        current["child"] = next;
        current = next;
      }
      current["circular"] = root;
      expect(() => stringify(root as never)).toThrow(TypeError);
    });
  });
});
