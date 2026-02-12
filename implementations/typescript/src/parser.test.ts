import { describe, it, expect } from "vitest";
import { parse } from "./parser.js";
import { timeOnly, duration } from "./types.js";

describe("RDN.parse", () => {
  describe("JSON-compatible types", () => {
    it("parses null", () => {
      expect(parse("null")).toBe(null);
    });
    it("parses booleans", () => {
      expect(parse("true")).toBe(true);
      expect(parse("false")).toBe(false);
    });
    it("parses integers", () => {
      expect(parse("0")).toBe(0);
      expect(parse("42")).toBe(42);
      expect(parse("-1")).toBe(-1);
      expect(parse("1234567890")).toBe(1234567890);
    });
    it("parses floats", () => {
      expect(parse("3.14")).toBe(3.14);
      expect(parse("-0.5")).toBe(-0.5);
      expect(parse("1e10")).toBe(1e10);
      expect(parse("1.5e-3")).toBe(1.5e-3);
      expect(parse("2E+10")).toBe(2e10);
    });
    it("parses strings with escapes", () => {
      expect(parse('"hello"')).toBe("hello");
      expect(parse('"hello\\nworld"')).toBe("hello\nworld");
      expect(parse('"tab\\there"')).toBe("tab\there");
      expect(parse('"quote\\"inside"')).toBe('quote"inside');
      expect(parse('"back\\\\slash"')).toBe("back\\slash");
      expect(parse('"\\u0041"')).toBe("A");
      expect(parse('"\\/slash"')).toBe("/slash");
      expect(parse('"\\b\\f\\r"')).toBe("\b\f\r");
    });
    it("parses empty string", () => {
      expect(parse('""')).toBe("");
    });
    it("parses arrays", () => {
      expect(parse("[]")).toEqual([]);
      expect(parse("[1,2,3]")).toEqual([1, 2, 3]);
      expect(parse('[ 1 , "two" , true ]')).toEqual([1, "two", true]);
    });
    it("parses objects", () => {
      expect(parse("{}")).toEqual({});
      expect(parse('{"a":1}')).toEqual({ a: 1 });
      expect(parse('{"a": 1, "b": "two"}')).toEqual({ a: 1, b: "two" });
    });
    it("parses nested structures", () => {
      expect(parse('{"a": [1, {"b": 2}]}')).toEqual({ a: [1, { b: 2 }] });
      expect(parse("[[[]]]")).toEqual([[[]]]);
    });
  });

  describe("special numbers", () => {
    it("parses NaN", () => {
      expect(parse("NaN")).toBeNaN();
    });
    it("parses Infinity", () => {
      expect(parse("Infinity")).toBe(Infinity);
    });
    it("parses -Infinity", () => {
      expect(parse("-Infinity")).toBe(-Infinity);
    });
  });

  describe("BigInt", () => {
    it("parses 0n", () => {
      expect(parse("0n")).toBe(0n);
    });
    it("parses positive bigint", () => {
      expect(parse("42n")).toBe(42n);
      expect(parse("999999999999999999n")).toBe(999999999999999999n);
    });
    it("parses negative bigint", () => {
      expect(parse("-99n")).toBe(-99n);
      expect(parse("-123456789012345678901234567890n")).toBe(-123456789012345678901234567890n);
    });
  });

  describe("DateTime", () => {
    it("parses full ISO datetime", () => {
      const d = parse("@2024-01-15T10:30:00.123Z") as Date;
      expect(d).toBeInstanceOf(Date);
      expect(d.toISOString()).toBe("2024-01-15T10:30:00.123Z");
    });
    it("parses ISO without milliseconds", () => {
      const d = parse("@2024-01-15T10:30:00Z") as Date;
      expect(d.toISOString()).toBe("2024-01-15T10:30:00.000Z");
    });
    it("parses date-only", () => {
      const d = parse("@2024-01-15") as Date;
      expect(d.toISOString()).toBe("2024-01-15T00:00:00.000Z");
    });
    it("parses unix timestamp in seconds", () => {
      const d = parse("@1705312200") as Date;
      expect(d).toBeInstanceOf(Date);
      expect(d.getTime()).toBe(1705312200000);
    });
    it("parses unix timestamp in milliseconds", () => {
      const d = parse("@1705312200000") as Date;
      expect(d).toBeInstanceOf(Date);
      expect(d.getTime()).toBe(1705312200000);
    });
  });

  describe("TimeOnly", () => {
    it("parses time without milliseconds", () => {
      expect(parse("@14:30:00")).toEqual(timeOnly(14, 30, 0, 0));
    });
    it("parses time with milliseconds", () => {
      expect(parse("@23:59:59.999")).toEqual(timeOnly(23, 59, 59, 999));
    });
    it("parses midnight", () => {
      expect(parse("@00:00:00")).toEqual(timeOnly(0, 0, 0, 0));
    });
  });

  describe("Duration", () => {
    it("parses full duration", () => {
      expect(parse("@P1Y2M3DT4H5M6S")).toEqual(duration("P1Y2M3DT4H5M6S"));
    });
    it("parses time-only duration", () => {
      expect(parse("@PT1H")).toEqual(duration("PT1H"));
    });
    it("parses day-only duration", () => {
      expect(parse("@P1D")).toEqual(duration("P1D"));
    });
    it("parses duration with fractional seconds", () => {
      expect(parse("@PT1H30M")).toEqual(duration("PT1H30M"));
    });
  });

  describe("RegExp", () => {
    it("parses simple pattern", () => {
      const re = parse("/test/") as RegExp;
      expect(re).toBeInstanceOf(RegExp);
      expect(re.source).toBe("test");
      expect(re.flags).toBe("");
    });
    it("parses pattern with flags", () => {
      const re = parse("/^[a-z]+$/gi") as RegExp;
      expect(re.source).toBe("^[a-z]+$");
      expect(re.flags).toBe("gi");
    });
    it("parses pattern with escaped slash", () => {
      const re = parse("/a\\/b/") as RegExp;
      expect(re.source).toBe("a\\/b");
    });
  });

  describe("Binary", () => {
    it("parses base64 binary", () => {
      const buf = parse('b"SGVsbG8="') as Uint8Array;
      expect(buf).toBeInstanceOf(Uint8Array);
      expect(Array.from(buf)).toEqual([72, 101, 108, 108, 111]); // "Hello"
    });
    it("parses hex binary", () => {
      const buf = parse('x"48656C6C6F"') as Uint8Array;
      expect(buf).toBeInstanceOf(Uint8Array);
      expect(Array.from(buf)).toEqual([72, 101, 108, 108, 111]);
    });
    it("parses empty binary", () => {
      expect(Array.from(parse('b""') as Uint8Array)).toEqual([]);
      expect(Array.from(parse('x""') as Uint8Array)).toEqual([]);
    });
    it("parses base64 without padding", () => {
      const buf = parse('b"AQID"') as Uint8Array;
      expect(Array.from(buf)).toEqual([1, 2, 3]);
    });
  });

  describe("Map", () => {
    it("parses explicit empty map", () => {
      const m = parse("Map{}") as Map<unknown, unknown>;
      expect(m).toBeInstanceOf(Map);
      expect(m.size).toBe(0);
    });
    it("parses explicit map", () => {
      const m = parse('Map{"a" => 1, "b" => 2}') as Map<string, number>;
      expect(m).toBeInstanceOf(Map);
      expect(m.get("a")).toBe(1);
      expect(m.get("b")).toBe(2);
    });
    it("parses implicit map (brace disambiguation)", () => {
      const m = parse('{"a" => 1, "b" => 2}') as Map<string, number>;
      expect(m).toBeInstanceOf(Map);
      expect(m.get("a")).toBe(1);
      expect(m.get("b")).toBe(2);
    });
    it("parses map with non-string keys", () => {
      const m = parse("Map{1 => \"one\", 2 => \"two\"}") as Map<number, string>;
      expect(m.get(1)).toBe("one");
      expect(m.get(2)).toBe("two");
    });
  });

  describe("Set", () => {
    it("parses explicit empty set", () => {
      const s = parse("Set{}") as Set<unknown>;
      expect(s).toBeInstanceOf(Set);
      expect(s.size).toBe(0);
    });
    it("parses explicit set", () => {
      const s = parse("Set{1, 2, 3}") as Set<number>;
      expect(s).toBeInstanceOf(Set);
      expect(s.has(1)).toBe(true);
      expect(s.has(2)).toBe(true);
      expect(s.has(3)).toBe(true);
    });
    it("parses implicit set (brace disambiguation)", () => {
      const s = parse('{"a", "b", "c"}') as Set<string>;
      expect(s).toBeInstanceOf(Set);
      expect(s.size).toBe(3);
    });
    it("parses single-element implicit set", () => {
      const s = parse('{"only"}') as Set<string>;
      expect(s).toBeInstanceOf(Set);
      expect(s.size).toBe(1);
      expect(s.has("only")).toBe(true);
    });
  });

  describe("Tuple", () => {
    it("parses empty tuple", () => {
      expect(parse("()")).toEqual([]);
    });
    it("parses tuple as array", () => {
      expect(parse('(1, "two", true)')).toEqual([1, "two", true]);
    });
  });

  describe("brace disambiguation", () => {
    it("empty braces → Object", () => {
      const result = parse("{}");
      expect(result).toEqual({});
      expect(result).not.toBeInstanceOf(Map);
      expect(result).not.toBeInstanceOf(Set);
    });
    it("colon separator → Object", () => {
      const result = parse('{"key": "value"}');
      expect(result).toEqual({ key: "value" });
    });
    it("arrow separator → Map", () => {
      const result = parse('{"key" => "value"}');
      expect(result).toBeInstanceOf(Map);
    });
    it("comma separator → Set", () => {
      const result = parse('{"a", "b"}');
      expect(result).toBeInstanceOf(Set);
    });
    it("single value + close brace → Set", () => {
      const result = parse('{"a"}');
      expect(result).toBeInstanceOf(Set);
      expect((result as Set<string>).has("a")).toBe(true);
    });
  });

  describe("whitespace handling", () => {
    it("handles leading and trailing whitespace", () => {
      expect(parse("  42  ")).toBe(42);
      expect(parse("\n\ttrue\n\t")).toBe(true);
    });
    it("handles whitespace in collections", () => {
      expect(parse("[ 1 , 2 , 3 ]")).toEqual([1, 2, 3]);
    });
  });

  describe("prototype pollution resistance", () => {
    it("handles __proto__ as ordinary key", () => {
      const obj = parse('{"__proto__": "safe"}') as Record<string, unknown>;
      expect(obj["__proto__"]).toBe("safe");
      expect(Object.getPrototypeOf(obj)).toBeNull();
    });
  });

  describe("reviver", () => {
    it("applies reviver to values", () => {
      const result = parse('{"a": 1, "b": 2}', (_key, value) => {
        return typeof value === "number" ? value * 2 : value;
      });
      expect(result).toEqual({ a: 2, b: 4 });
    });
  });

  describe("errors", () => {
    it("throws on trailing comma", () => {
      expect(() => parse('{"a": 1,}')).toThrow(SyntaxError);
    });
    it("throws on unquoted keys", () => {
      expect(() => parse('{key: "value"}')).toThrow(SyntaxError);
    });
    it("throws on invalid input", () => {
      expect(() => parse("")).toThrow(SyntaxError);
      expect(() => parse("undefined")).toThrow(SyntaxError);
    });
    it("throws on trailing data", () => {
      expect(() => parse("42 extra")).toThrow(SyntaxError);
    });
    it("throws on invalid bigint with decimal", () => {
      expect(() => parse("3.14n")).toThrow(SyntaxError);
    });
    it("throws on invalid bigint with exponent", () => {
      expect(() => parse("1e10n")).toThrow(SyntaxError);
    });
    it("throws on invalid base64", () => {
      expect(() => parse('b"not base64!!"')).toThrow(SyntaxError);
    });
    it("throws on invalid hex", () => {
      expect(() => parse('x"GHIJKL"')).toThrow(SyntaxError);
    });
    it("throws on unclosed string", () => {
      expect(() => parse('"hello')).toThrow(SyntaxError);
    });
    it("throws on unclosed regexp", () => {
      expect(() => parse("/unclosed")).toThrow(SyntaxError);
    });
    it("throws on single quotes", () => {
      expect(() => parse("'hello'")).toThrow(SyntaxError);
    });
    it("includes position in error message", () => {
      try {
        parse("   !");
        expect.unreachable("should have thrown");
      } catch (e) {
        expect((e as SyntaxError).message).toContain("at position 3");
      }
    });
    it("throws on leading zeros", () => {
      expect(() => parse("01")).toThrow(SyntaxError);
    });
  });
});
