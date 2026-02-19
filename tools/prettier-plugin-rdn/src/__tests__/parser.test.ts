import { describe, it, expect } from "vitest";
import { parse } from "../parser.js";

describe("CST parser", () => {
  describe("atomic literals", () => {
    it("parses null", () => {
      const doc = parse("null");
      expect(doc.body).toEqual({ type: "NullLiteral", start: 0, end: 4 });
    });

    it("parses true", () => {
      const doc = parse("true");
      expect(doc.body).toEqual({ type: "BooleanLiteral", start: 0, end: 4, value: true });
    });

    it("parses false", () => {
      const doc = parse("false");
      expect(doc.body).toEqual({ type: "BooleanLiteral", start: 0, end: 5, value: false });
    });

    it("parses NaN", () => {
      const doc = parse("NaN");
      expect(doc.body).toEqual({ type: "NaNLiteral", start: 0, end: 3 });
    });

    it("parses Infinity", () => {
      const doc = parse("Infinity");
      expect(doc.body).toEqual({ type: "InfinityLiteral", start: 0, end: 8, negative: false });
    });

    it("parses -Infinity", () => {
      const doc = parse("-Infinity");
      expect(doc.body).toEqual({ type: "InfinityLiteral", start: 0, end: 9, negative: true });
    });
  });

  describe("numbers", () => {
    it("parses integer", () => {
      const doc = parse("42");
      expect(doc.body).toEqual({ type: "NumberLiteral", start: 0, end: 2, raw: "42" });
    });

    it("parses negative number", () => {
      const doc = parse("-3.14");
      expect(doc.body).toEqual({ type: "NumberLiteral", start: 0, end: 5, raw: "-3.14" });
    });

    it("parses exponent", () => {
      const doc = parse("1e10");
      expect(doc.body).toEqual({ type: "NumberLiteral", start: 0, end: 4, raw: "1e10" });
    });

    it("parses zero", () => {
      const doc = parse("0");
      expect(doc.body).toEqual({ type: "NumberLiteral", start: 0, end: 1, raw: "0" });
    });
  });

  describe("bigints", () => {
    it("parses bigint", () => {
      const doc = parse("42n");
      expect(doc.body).toEqual({ type: "BigIntLiteral", start: 0, end: 3, raw: "42n" });
    });

    it("parses negative bigint", () => {
      const doc = parse("-100n");
      expect(doc.body).toEqual({ type: "BigIntLiteral", start: 0, end: 5, raw: "-100n" });
    });
  });

  describe("strings", () => {
    it("parses simple string", () => {
      const doc = parse('"hello"');
      expect(doc.body).toEqual({ type: "StringLiteral", start: 0, end: 7, value: "hello", raw: '"hello"' });
    });

    it("parses string with escapes", () => {
      const doc = parse('"hello\\nworld"');
      expect(doc.body).toMatchObject({ type: "StringLiteral", value: "hello\nworld", raw: '"hello\\nworld"' });
    });

    it("parses string with unicode escape", () => {
      const doc = parse('"\\u0041"');
      expect(doc.body).toMatchObject({ type: "StringLiteral", value: "A" });
    });

    it("parses empty string", () => {
      const doc = parse('""');
      expect(doc.body).toMatchObject({ type: "StringLiteral", value: "", raw: '""' });
    });
  });

  describe("dates and times", () => {
    it("parses date-only", () => {
      const doc = parse("@2024-01-15");
      expect(doc.body).toEqual({ type: "DateTimeLiteral", start: 0, end: 11, raw: "@2024-01-15" });
    });

    it("parses datetime", () => {
      const doc = parse("@2024-01-15T10:30:00.000Z");
      expect(doc.body).toEqual({ type: "DateTimeLiteral", start: 0, end: 25, raw: "@2024-01-15T10:30:00.000Z" });
    });

    it("parses datetime without millis", () => {
      const doc = parse("@2024-01-15T10:30:00Z");
      expect(doc.body).toEqual({ type: "DateTimeLiteral", start: 0, end: 21, raw: "@2024-01-15T10:30:00Z" });
    });

    it("parses unix timestamp", () => {
      const doc = parse("@1700000000");
      expect(doc.body).toEqual({ type: "DateTimeLiteral", start: 0, end: 11, raw: "@1700000000" });
    });

    it("parses time-only", () => {
      const doc = parse("@14:30:00");
      expect(doc.body).toEqual({ type: "TimeOnlyLiteral", start: 0, end: 9, raw: "@14:30:00" });
    });

    it("parses time-only with millis", () => {
      const doc = parse("@14:30:00.123");
      expect(doc.body).toEqual({ type: "TimeOnlyLiteral", start: 0, end: 13, raw: "@14:30:00.123" });
    });

    it("parses duration", () => {
      const doc = parse("@PT2H30M");
      expect(doc.body).toEqual({ type: "DurationLiteral", start: 0, end: 8, raw: "@PT2H30M" });
    });

    it("parses complex duration", () => {
      const doc = parse("@P1Y2M3DT4H5M6S");
      expect(doc.body).toEqual({ type: "DurationLiteral", start: 0, end: 15, raw: "@P1Y2M3DT4H5M6S" });
    });
  });

  describe("regexp", () => {
    it("parses simple regexp", () => {
      const doc = parse("/abc/g");
      expect(doc.body).toEqual({ type: "RegExpLiteral", start: 0, end: 6, raw: "/abc/g" });
    });

    it("parses regexp with escapes", () => {
      const doc = parse("/a\\.b/i");
      expect(doc.body).toEqual({ type: "RegExpLiteral", start: 0, end: 7, raw: "/a\\.b/i" });
    });
  });

  describe("binary", () => {
    it("parses base64", () => {
      const doc = parse('b"SGVsbG8="');
      expect(doc.body).toEqual({ type: "BinaryLiteral", start: 0, end: 11, encoding: "base64", raw: 'b"SGVsbG8="' });
    });

    it("parses hex", () => {
      const doc = parse('x"48656C6C6F"');
      expect(doc.body).toEqual({ type: "BinaryLiteral", start: 0, end: 13, encoding: "hex", raw: 'x"48656C6C6F"' });
    });

    it("parses empty base64", () => {
      const doc = parse('b""');
      expect(doc.body).toEqual({ type: "BinaryLiteral", start: 0, end: 3, encoding: "base64", raw: 'b""' });
    });
  });

  describe("arrays", () => {
    it("parses empty array", () => {
      const doc = parse("[]");
      expect(doc.body).toEqual({ type: "Array", start: 0, end: 2, elements: [] });
    });

    it("parses array with elements", () => {
      const doc = parse("[1, 2, 3]");
      const body = doc.body;
      expect(body.type).toBe("Array");
      if (body.type === "Array") {
        expect(body.elements).toHaveLength(3);
        expect(body.elements[0]).toMatchObject({ type: "NumberLiteral", raw: "1" });
        expect(body.elements[1]).toMatchObject({ type: "NumberLiteral", raw: "2" });
        expect(body.elements[2]).toMatchObject({ type: "NumberLiteral", raw: "3" });
      }
    });

    it("tracks element positions", () => {
      const doc = parse("[1, 2]");
      if (doc.body.type === "Array") {
        expect(doc.body.elements[0]).toMatchObject({ start: 1, end: 2 });
        expect(doc.body.elements[1]).toMatchObject({ start: 4, end: 5 });
      }
    });
  });

  describe("tuples", () => {
    it("parses empty tuple", () => {
      const doc = parse("()");
      expect(doc.body).toEqual({ type: "Tuple", start: 0, end: 2, elements: [] });
    });

    it("parses tuple with elements", () => {
      const doc = parse('(1, "a", true)');
      if (doc.body.type === "Tuple") {
        expect(doc.body.elements).toHaveLength(3);
        expect(doc.body.elements[0]).toMatchObject({ type: "NumberLiteral" });
        expect(doc.body.elements[1]).toMatchObject({ type: "StringLiteral" });
        expect(doc.body.elements[2]).toMatchObject({ type: "BooleanLiteral" });
      }
    });
  });

  describe("objects", () => {
    it("parses empty object", () => {
      const doc = parse("{}");
      expect(doc.body).toEqual({ type: "Object", start: 0, end: 2, properties: [] });
    });

    it("parses object with properties", () => {
      const doc = parse('{"a": 1, "b": 2}');
      if (doc.body.type === "Object") {
        expect(doc.body.properties).toHaveLength(2);
        expect(doc.body.properties[0]!.key).toMatchObject({ type: "StringLiteral", value: "a" });
        expect(doc.body.properties[0]!.value).toMatchObject({ type: "NumberLiteral", raw: "1" });
        expect(doc.body.properties[1]!.key).toMatchObject({ type: "StringLiteral", value: "b" });
        expect(doc.body.properties[1]!.value).toMatchObject({ type: "NumberLiteral", raw: "2" });
      }
    });
  });

  describe("brace disambiguation", () => {
    it("empty braces → Object", () => {
      const doc = parse("{}");
      expect(doc.body.type).toBe("Object");
    });

    it("colon → Object", () => {
      const doc = parse('{"x": 1}');
      expect(doc.body.type).toBe("Object");
    });

    it("arrow → implicit Map", () => {
      const doc = parse('{"x" => 1}');
      if (doc.body.type === "Map") {
        expect(doc.body.explicit).toBe(false);
        expect(doc.body.entries).toHaveLength(1);
      }
    });

    it("comma → implicit Set", () => {
      const doc = parse("{1, 2, 3}");
      if (doc.body.type === "Set") {
        expect(doc.body.explicit).toBe(false);
        expect(doc.body.elements).toHaveLength(3);
      }
    });

    it("single element + close brace → single-element Set", () => {
      const doc = parse('{"only"}');
      if (doc.body.type === "Set") {
        expect(doc.body.explicit).toBe(false);
        expect(doc.body.elements).toHaveLength(1);
      }
    });
  });

  describe("explicit Map/Set", () => {
    it("parses Map{}", () => {
      const doc = parse("Map{}");
      expect(doc.body).toMatchObject({ type: "Map", explicit: true, entries: [] });
    });

    it("parses Map with entries", () => {
      const doc = parse('Map{"a" => 1, "b" => 2}');
      if (doc.body.type === "Map") {
        expect(doc.body.explicit).toBe(true);
        expect(doc.body.entries).toHaveLength(2);
      }
    });

    it("parses Set{}", () => {
      const doc = parse("Set{}");
      expect(doc.body).toMatchObject({ type: "Set", explicit: true, elements: [] });
    });

    it("parses Set with elements", () => {
      const doc = parse("Set{1, 2, 3}");
      if (doc.body.type === "Set") {
        expect(doc.body.explicit).toBe(true);
        expect(doc.body.elements).toHaveLength(3);
      }
    });
  });

  describe("document positions", () => {
    it("wraps body in Document node", () => {
      const doc = parse("  42  ");
      expect(doc.type).toBe("Document");
      expect(doc.start).toBe(0);
      expect(doc.end).toBe(6);
      expect(doc.body).toMatchObject({ type: "NumberLiteral", start: 2, end: 4, raw: "42" });
    });
  });

  describe("errors", () => {
    it("rejects trailing data", () => {
      expect(() => parse("1 2")).toThrow("Unexpected data after value");
    });

    it("rejects empty input", () => {
      expect(() => parse("")).toThrow("Unexpected end of input");
    });

    it("rejects unterminated string", () => {
      expect(() => parse('"hello')).toThrow("Unterminated string");
    });

    it("rejects leading zeros", () => {
      expect(() => parse("01")).toThrow("Leading zeros");
    });

    it("rejects non-string object key", () => {
      expect(() => parse("{1: 2}")).toThrow();
    });
  });

  describe("nested structures", () => {
    it("parses nested objects", () => {
      const doc = parse('{"a": {"b": 1}}');
      if (doc.body.type === "Object") {
        const inner = doc.body.properties[0]!.value;
        expect(inner.type).toBe("Object");
      }
    });

    it("parses array of objects", () => {
      const doc = parse('[{"a": 1}, {"b": 2}]');
      if (doc.body.type === "Array") {
        expect(doc.body.elements).toHaveLength(2);
        expect(doc.body.elements[0]!.type).toBe("Object");
        expect(doc.body.elements[1]!.type).toBe("Object");
      }
    });
  });
});
