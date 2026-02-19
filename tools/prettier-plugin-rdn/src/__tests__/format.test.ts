import { describe, it, expect } from "vitest";
import * as prettier from "prettier";
import * as plugin from "../index.js";

async function format(
  input: string,
  options?: prettier.Options,
): Promise<string> {
  return prettier.format(input, {
    parser: "rdn",
    plugins: [plugin],
    ...options,
  });
}

describe("prettier-plugin-rdn formatting", () => {
  describe("atomic literals", () => {
    it("formats null", async () => {
      expect(await format("null")).toBe("null\n");
    });

    it("formats booleans", async () => {
      expect(await format("true")).toBe("true\n");
      expect(await format("false")).toBe("false\n");
    });

    it("formats numbers", async () => {
      expect(await format("42")).toBe("42\n");
      expect(await format("-3.14")).toBe("-3.14\n");
      expect(await format("1e10")).toBe("1e10\n");
    });

    it("formats bigints", async () => {
      expect(await format("42n")).toBe("42n\n");
      expect(await format("-100n")).toBe("-100n\n");
    });

    it("formats NaN", async () => {
      expect(await format("NaN")).toBe("NaN\n");
    });

    it("formats Infinity", async () => {
      expect(await format("Infinity")).toBe("Infinity\n");
      expect(await format("-Infinity")).toBe("-Infinity\n");
    });
  });

  describe("strings", () => {
    it("formats simple string", async () => {
      expect(await format('"hello"')).toBe('"hello"\n');
    });

    it("normalizes escape sequences", async () => {
      expect(await format('"hello\\nworld"')).toBe('"hello\\nworld"\n');
    });

    it("formats empty string", async () => {
      expect(await format('""')).toBe('""\n');
    });

    it("re-escapes strings with special chars", async () => {
      expect(await format('"tab\\there"')).toBe('"tab\\there"\n');
    });
  });

  describe("dates and times", () => {
    it("formats date", async () => {
      expect(await format("@2024-01-15")).toBe("@2024-01-15\n");
    });

    it("formats datetime", async () => {
      expect(await format("@2024-01-15T10:30:00.000Z")).toBe(
        "@2024-01-15T10:30:00.000Z\n",
      );
    });

    it("formats time-only", async () => {
      expect(await format("@14:30:00")).toBe("@14:30:00\n");
    });

    it("formats duration", async () => {
      expect(await format("@PT2H30M")).toBe("@PT2H30M\n");
    });

    it("formats unix timestamp", async () => {
      expect(await format("@1700000000")).toBe("@1700000000\n");
    });
  });

  describe("regexp", () => {
    it("formats regexp", async () => {
      expect(await format("/abc/g")).toBe("/abc/g\n");
    });
  });

  describe("binary", () => {
    it("formats base64", async () => {
      expect(await format('b"SGVsbG8="')).toBe('b"SGVsbG8="\n');
    });

    it("formats hex", async () => {
      expect(await format('x"48656C6C6F"')).toBe('x"48656C6C6F"\n');
    });
  });

  describe("arrays", () => {
    it("formats empty array", async () => {
      expect(await format("[]")).toBe("[]\n");
    });

    it("formats short array on one line", async () => {
      expect(await format("[1, 2, 3]")).toBe("[1, 2, 3]\n");
    });

    it("formats short array from compact input", async () => {
      expect(await format("[1,2,3]")).toBe("[1, 2, 3]\n");
    });

    it("breaks long array into multiple lines", async () => {
      const longArray =
        "[" +
        Array.from({ length: 20 }, (_, i) => `"item-${i}"`).join(", ") +
        "]";
      const result = await format(longArray);
      expect(result).toContain("\n");
      // Should have proper indentation
      expect(result).toMatch(/^\[\n  "item-0"/);
    });
  });

  describe("tuples", () => {
    it("formats empty tuple", async () => {
      expect(await format("()")).toBe("()\n");
    });

    it("formats short tuple on one line", async () => {
      expect(await format("(1, 2, 3)")).toBe("(1, 2, 3)\n");
    });
  });

  describe("objects", () => {
    it("formats empty object", async () => {
      expect(await format("{}")).toBe("{}\n");
    });

    it("formats short object on one line with bracket spacing", async () => {
      expect(await format('{"a": 1, "b": 2}')).toBe('{ "a": 1, "b": 2 }\n');
    });

    it("formats without bracket spacing when option is false", async () => {
      expect(await format('{"a": 1}', { bracketSpacing: false })).toBe(
        '{"a": 1}\n',
      );
    });

    it("breaks long object into multiple lines", async () => {
      const obj =
        '{"longKeyName1": "value1", "longKeyName2": "value2", "longKeyName3": "value3", "longKeyName4": "value4"}';
      const result = await format(obj, { printWidth: 40 });
      expect(result).toContain("\n");
      expect(result).toMatch(/^\{\n/);
    });

    it("formats nested objects", async () => {
      const result = await format('{"a": {"b": 1}}');
      expect(result).toBe('{ "a": { "b": 1 } }\n');
    });
  });

  describe("maps", () => {
    it("formats empty explicit map", async () => {
      expect(await format("Map{}")).toBe("Map{}\n");
    });

    it("strips Map keyword from non-empty explicit map by default", async () => {
      expect(await format('Map{"a" => 1, "b" => 2}')).toBe(
        '{ "a" => 1, "b" => 2 }\n',
      );
    });

    it("keeps Map keyword when useExplicitMapKeyword is true", async () => {
      expect(
        await format('Map{"a" => 1, "b" => 2}', {
          useExplicitMapKeyword: true,
        } as any),
      ).toBe('Map{ "a" => 1, "b" => 2 }\n');
    });

    it("formats implicit map", async () => {
      expect(await format('{"a" => 1}')).toBe('{ "a" => 1 }\n');
    });

    it("strips Map keyword from non-string key map by default", async () => {
      expect(await format("Map{1 => 2, 3 => 4}")).toBe("{ 1 => 2, 3 => 4 }\n");
    });

    it("keeps Map keyword on non-string key map when flag is true", async () => {
      expect(
        await format("Map{1 => 2, 3 => 4}", {
          useExplicitMapKeyword: true,
        } as any),
      ).toBe("Map{ 1 => 2, 3 => 4 }\n");
    });

    it("always keeps Map keyword on empty map regardless of flag", async () => {
      expect(
        await format("Map{}", { useExplicitMapKeyword: false } as any),
      ).toBe("Map{}\n");
      expect(
        await format("Map{}", { useExplicitMapKeyword: true } as any),
      ).toBe("Map{}\n");
    });
  });

  describe("sets", () => {
    it("formats empty explicit set", async () => {
      expect(await format("Set{}")).toBe("Set{}\n");
    });

    it("strips Set keyword from non-empty explicit set by default", async () => {
      expect(await format("Set{1, 2, 3}")).toBe("{ 1, 2, 3 }\n");
    });

    it("keeps Set keyword when useExplicitSetKeyword is true", async () => {
      expect(
        await format("Set{1, 2, 3}", { useExplicitSetKeyword: true } as any),
      ).toBe("Set{ 1, 2, 3 }\n");
    });

    it("formats implicit set", async () => {
      expect(await format("{1, 2, 3}")).toBe("{ 1, 2, 3 }\n");
    });

    it("formats single-element implicit set", async () => {
      expect(await format('{"only"}')).toBe('{ "only" }\n');
    });

    it("always keeps Set keyword on empty set regardless of flag", async () => {
      expect(
        await format("Set{}", { useExplicitSetKeyword: false } as any),
      ).toBe("Set{}\n");
      expect(
        await format("Set{}", { useExplicitSetKeyword: true } as any),
      ).toBe("Set{}\n");
    });
  });

  describe("idempotency", () => {
    const samples = [
      "null",
      "42",
      "-3.14",
      "42n",
      '"hello"',
      "true",
      "NaN",
      "Infinity",
      "-Infinity",
      "@2024-01-15T10:30:00.000Z",
      "@14:30:00",
      "@PT2H30M",
      "/abc/gi",
      'b"SGVsbG8="',
      'x"4F"',
      "[1, 2, 3]",
      "(1, 2, 3)",
      '{"a": 1, "b": 2}',
      '{"a" => 1}',
      "{1, 2, 3}",
      '{"x" => "y"}',
      '{"a", "b", "c"}',
      '{"only"}',
      "[]",
      "()",
      "{}",
      "Map{}",
      "Set{}",
    ];

    for (const input of samples) {
      it(`format(format(${JSON.stringify(input)})) === format(${JSON.stringify(input)})`, async () => {
        const first = await format(input);
        const second = await format(first);
        expect(second).toBe(first);
      });
    }
  });

  describe("whitespace normalization", () => {
    it("normalizes extra whitespace in arrays", async () => {
      expect(await format("[  1  ,  2  ,  3  ]")).toBe("[1, 2, 3]\n");
    });

    it("normalizes extra whitespace in objects", async () => {
      expect(await format('{  "a"  :  1  }')).toBe('{ "a": 1 }\n');
    });

    it("normalizes leading/trailing whitespace", async () => {
      expect(await format("  42  ")).toBe("42\n");
    });
  });

  describe("sortKeys", () => {
    it("sorts object keys alphabetically", async () => {
      expect(
        await format('{"c": 3, "a": 1, "b": 2}', { sortKeys: true } as any),
      ).toBe('{ "a": 1, "b": 2, "c": 3 }\n');
    });

    it("sorts nested object keys recursively", async () => {
      const input = '{"z": {"b": 2, "a": 1}, "a": 0}';
      const result = await format(input, { sortKeys: true } as any);
      expect(result).toBe('{ "a": 0, "z": { "a": 1, "b": 2 } }\n');
    });

    it("does not sort when sortKeys is false (default)", async () => {
      expect(await format('{"c": 3, "a": 1, "b": 2}')).toBe(
        '{ "c": 3, "a": 1, "b": 2 }\n',
      );
    });

    it("sorts keys in objects nested inside arrays", async () => {
      const input = '[{"b": 2, "a": 1}]';
      const result = await format(input, { sortKeys: true } as any);
      expect(result).toBe('[{ "a": 1, "b": 2 }]\n');
    });

    it("leaves empty object unchanged", async () => {
      expect(await format("{}", { sortKeys: true } as any)).toBe("{}\n");
    });
  });

  describe("complex document", () => {
    it("formats a realistic document", async () => {
      const input = `{"meta":{"version":"1.0","timestamp":@2024-01-15T10:30:00.000Z},"data":[1,2,3],"tags":Set{"a","b"}}`;
      const result = await format(input);
      expect(result).toContain('"meta"');
      expect(result).toContain('"version"');
      expect(result).toContain("@2024-01-15T10:30:00.000Z");
      // Set keyword is stripped by default for non-empty sets
      expect(result).not.toContain("Set{");
      // Should be idempotent
      expect(await format(result)).toBe(result);
    });
  });
});
