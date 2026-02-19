import { describe, it, expect, beforeEach, vi } from "vitest";

// ─── Mock vscode module ──────────────────────────────────────────────────────

const { MockPosition, MockRange, MockMarkdownString, MockHover, mockConfigValues } = vi.hoisted(() => {
  class MockPosition {
    constructor(public line: number, public character: number) {}
  }

  class MockRange {
    constructor(public start: MockPosition, public end: MockPosition) {}
    static create(startLine: number, startChar: number, endLine: number, endChar: number): MockRange {
      return new MockRange(new MockPosition(startLine, startChar), new MockPosition(endLine, endChar));
    }
  }

  class MockMarkdownString {
    value = "";
    supportHtml = false;
    appendMarkdown(s: string) { this.value += s; }
  }

  class MockHover {
    constructor(public contents: MockMarkdownString, public range?: MockRange) {}
  }

  const mockConfigValues: Record<string, unknown> = {};

  return { MockPosition, MockRange, MockMarkdownString, MockHover, mockConfigValues };
});

vi.mock("vscode", () => ({
  Position: function (line: number, char: number) { return new MockPosition(line, char); },
  Range: function (...args: number[]) {
    if (args.length === 4) return MockRange.create(args[0]!, args[1]!, args[2]!, args[3]!);
    return new MockRange(args[0] as unknown as MockPosition, args[1] as unknown as MockPosition);
  },
  MarkdownString: MockMarkdownString,
  Hover: MockHover,
  workspace: {
    getConfiguration: (_section: string) => ({
      get: <T>(key: string, defaultValue: T): T => {
        const fullKey = key;
        return (fullKey in mockConfigValues ? mockConfigValues[fullKey] : defaultValue) as T;
      },
    }),
  },
}));

// ─── Import modules under test (after mock) ─────────────────────────────────

import { formatDate, expandDuration, groupDigits, formatByteSize, _resetWarnedFormats } from "../src/format";
import { detectToken, countCollectionElements, RdnHoverProvider, detectImageFromBytes, decodeBase64ToBytes, decodeHexToBytes, bytesToBase64, type TokenInfo } from "../src/hover";
import { _setHoverConfig, invalidateHoverConfig, type RdnHoverConfig } from "../src/config";
import { scanBinaryErrors, scanUnquotedKeys } from "../src/scanner";
import { formatSorted } from "../src/formatter";

// ─── Helper: create mock document ───────────────────────────────────────────

function mockDocument(text: string) {
  const lines = text.split("\n");
  return {
    getText: (range?: MockRange) => {
      if (!range) return text;
      const startOff = offsetOf(text, range.start.line, range.start.character);
      const endOff = offsetOf(text, range.end.line, range.end.character);
      return text.slice(startOff, endOff);
    },
    lineAt: (line: number) => ({ text: lines[line] || "" }),
    positionAt: (offset: number) => {
      let line = 0, col = 0;
      for (let i = 0; i < offset && i < text.length; i++) {
        if (text[i] === "\n") { line++; col = 0; } else { col++; }
      }
      return new MockPosition(line, col);
    },
    offsetAt: (pos: MockPosition) => offsetOf(text, pos.line, pos.character),
    languageId: "rdn",
  } as unknown as import("vscode").TextDocument;
}

function offsetOf(text: string, line: number, char: number): number {
  let offset = 0;
  let l = 0;
  while (l < line && offset < text.length) {
    if (text[offset] === "\n") l++;
    offset++;
  }
  return offset + char;
}

// ─── Default config for tests ────────────────────────────────────────────────

function defaultConfig(): RdnHoverConfig {
  return {
    enabled: true,
    dateTime: { enabled: true, fullFormat: "YYYY-MM-DD HH:mm:ss.SSS [UTC]", dateOnlyFormat: "MMMM D, YYYY", noMillisFormat: "YYYY-MM-DD HH:mm:ss [UTC]", unixFormat: "YYYY-MM-DD HH:mm:ss [UTC]" },
    timeOnly: { enabled: true, format: "HH:mm:ss" },
    duration: { enabled: true },
    bigint: { enabled: true, showBitLength: true },
    binary: { enabled: true, showPreview: true },
    regexp: { enabled: true },
    specialNumbers: { enabled: true },
    collections: { enabled: true },
    diagnostics: { enabled: true },
  };
}

// ─── Tests: formatDate ───────────────────────────────────────────────────────

describe("formatDate", () => {
  beforeEach(() => _resetWarnedFormats());

  it("formats full ISO date with default format", () => {
    const d = new Date("2024-01-15T10:30:45.123Z");
    expect(formatDate(d, "YYYY-MM-DD HH:mm:ss.SSS [UTC]", "")).toBe("2024-01-15 10:30:45.123 UTC");
  });

  it("formats date-only", () => {
    const d = new Date("2024-06-15T00:00:00.000Z");
    expect(formatDate(d, "MMMM D, YYYY", "")).toBe("June 15, 2024");
  });

  it("supports 12-hour format", () => {
    const d = new Date("2024-01-15T14:30:00.000Z");
    expect(formatDate(d, "h:mm A", "")).toBe("2:30 PM");
  });

  it("supports short month names", () => {
    const d = new Date("2024-03-05T00:00:00.000Z");
    expect(formatDate(d, "MMM DD, YYYY", "")).toBe("Mar 05, 2024");
  });

  it("handles [literal] escapes", () => {
    const d = new Date("2024-01-01T00:00:00.000Z");
    expect(formatDate(d, "[Date:] YYYY-MM-DD", "")).toBe("Date: 2024-01-01");
  });

  it("falls back to default on invalid format", () => {
    const d = new Date("2024-01-15T10:30:00.000Z");
    // "QQQ" is not a valid token
    const result = formatDate(d, "QQQ", "YYYY-MM-DD");
    expect(result).toBe("2024-01-15");
  });

  it("uses YY for 2-digit year", () => {
    const d = new Date("2024-06-15T00:00:00.000Z");
    expect(formatDate(d, "YY", "")).toBe("24");
  });

  it("uses single-digit tokens M, D, H, m, s", () => {
    const d = new Date("2024-01-05T03:07:09.000Z");
    expect(formatDate(d, "M/D H:m:s", "")).toBe("1/5 3:7:9");
  });
});

// ─── Tests: expandDuration ───────────────────────────────────────────────────

describe("expandDuration", () => {
  it("expands full duration", () => {
    expect(expandDuration("P1Y2M3DT4H5M6S")).toBe("1 year, 2 months, 3 days, 4 hours, 5 minutes, 6 seconds");
  });

  it("handles singular units", () => {
    expect(expandDuration("P1YT1H1M1S")).toBe("1 year, 1 hour, 1 minute, 1 second");
  });

  it("handles date-only duration", () => {
    expect(expandDuration("P30D")).toBe("30 days");
  });

  it("handles time-only duration", () => {
    expect(expandDuration("PT2H30M")).toBe("2 hours, 30 minutes");
  });

  it("handles months vs minutes disambiguation", () => {
    expect(expandDuration("P6MT30M")).toBe("6 months, 30 minutes");
  });

  it("returns original on invalid input", () => {
    expect(expandDuration("invalid")).toBe("invalid");
  });

  it("handles fractional seconds", () => {
    expect(expandDuration("PT1.5S")).toBe("1.5 seconds");
  });
});

// ─── Tests: groupDigits ──────────────────────────────────────────────────────

describe("groupDigits", () => {
  it("groups large numbers", () => {
    expect(groupDigits("1234567890")).toBe("1,234,567,890");
  });

  it("leaves small numbers unchanged", () => {
    expect(groupDigits("42")).toBe("42");
  });

  it("handles negative numbers", () => {
    expect(groupDigits("-9876543210")).toBe("-9,876,543,210");
  });

  it("handles zero", () => {
    expect(groupDigits("0")).toBe("0");
  });
});

// ─── Tests: detectToken ──────────────────────────────────────────────────────

describe("detectToken", () => {
  it("detects full DateTime", () => {
    const doc = mockDocument(`@2024-01-15T10:30:00.000Z`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("dateTimeFull");
    expect(token!.text).toBe("@2024-01-15T10:30:00.000Z");
  });

  it("detects DateTime without millis", () => {
    const doc = mockDocument(`@2024-01-15T10:30:00Z`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("dateTimeNoMillis");
  });

  it("detects date-only", () => {
    const doc = mockDocument(`@2024-01-15`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("dateOnly");
  });

  it("detects Unix timestamp", () => {
    const doc = mockDocument(`@1704067200`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("unixTimestamp");
  });

  it("detects TimeOnly", () => {
    const doc = mockDocument(`@14:30:00`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("timeOnly");
  });

  it("detects Duration", () => {
    const doc = mockDocument(`@P1Y2M3DT4H5M6S`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("duration");
  });

  it("detects BigInt", () => {
    const doc = mockDocument(`42n`);
    const token = detectToken(doc, new MockPosition(0, 1) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("bigint");
    expect(token!.text).toBe("42n");
  });

  it("detects negative BigInt", () => {
    const doc = mockDocument(`-999n`);
    const token = detectToken(doc, new MockPosition(0, 1) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("bigint");
    expect(token!.text).toBe("-999n");
  });

  it("detects base64 binary", () => {
    const doc = mockDocument(`b"SGVsbG8="`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("binaryBase64");
  });

  it("detects hex binary", () => {
    const doc = mockDocument(`x"48656C6C6F"`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("binaryHex");
  });

  it("detects base64 binary when hovering inside content", () => {
    const doc = mockDocument(`b"SGVsbG8="`);
    // col 4 = 'l' inside the base64 content
    const token = detectToken(doc, new MockPosition(0, 4) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("binaryBase64");
    expect(token!.text).toBe(`b"SGVsbG8="`);
  });

  it("detects hex binary when hovering inside content", () => {
    const doc = mockDocument(`x"48656C6C6F"`);
    // col 5 = '5' inside the hex content
    const token = detectToken(doc, new MockPosition(0, 5) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("binaryHex");
    expect(token!.text).toBe(`x"48656C6C6F"`);
  });

  it("detects RegExp", () => {
    const doc = mockDocument(`/^test$/gi`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("regexp");
    expect(token!.text).toBe("/^test$/gi");
  });

  it("detects NaN", () => {
    const doc = mockDocument(`NaN`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("nan");
  });

  it("detects Infinity", () => {
    const doc = mockDocument(`Infinity`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("infinity");
  });

  it("detects -Infinity", () => {
    const doc = mockDocument(`-Infinity`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("negInfinity");
  });

  it("detects Map keyword", () => {
    const doc = mockDocument(`Map{"a" => 1}`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("mapKeyword");
  });

  it("detects Set keyword", () => {
    const doc = mockDocument(`Set{1, 2, 3}`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("setKeyword");
  });

  it("detects => arrow", () => {
    const doc = mockDocument(`{"a" => 1}`);
    const token = detectToken(doc, new MockPosition(0, 5) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("mapArrow");
  });

  it("detects tuple (", () => {
    const doc = mockDocument(`(1, 2, 3)`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("tuple");
  });

  it("returns null inside strings", () => {
    const doc = mockDocument(`"@2024-01-15"`);
    const token = detectToken(doc, new MockPosition(0, 1) as import("vscode").Position);
    expect(token).toBeNull();
  });

  it("returns null for regular numbers", () => {
    const doc = mockDocument(`42`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).toBeNull();
  });

  it("returns null for boolean true", () => {
    const doc = mockDocument(`true`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).toBeNull();
  });

  it("detects @ literal at middle cursor position", () => {
    const doc = mockDocument(`@2024-01-15T10:30:00.000Z`);
    const token = detectToken(doc, new MockPosition(0, 10) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("dateTimeFull");
    expect(token!.text).toBe("@2024-01-15T10:30:00.000Z");
  });

  it("detects implicit set", () => {
    const doc = mockDocument(`{"a", "b", "c"}`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("implicitSet");
  });

  it("detects implicit map", () => {
    const doc = mockDocument(`{"a" => 1, "b" => 2}`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).not.toBeNull();
    expect(token!.kind).toBe("implicitMap");
  });

  it("returns null for empty object {}", () => {
    const doc = mockDocument(`{}`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).toBeNull();
  });

  it("returns null for regular object { key: value }", () => {
    const doc = mockDocument(`{"key": "value"}`);
    const token = detectToken(doc, new MockPosition(0, 0) as import("vscode").Position);
    expect(token).toBeNull();
  });
});

// ─── Tests: countCollectionElements ──────────────────────────────────────────

describe("countCollectionElements", () => {
  it("counts empty collection", () => {
    const doc = mockDocument(`()`);
    expect(countCollectionElements(doc, 0)).toBe(0);
  });

  it("counts single element", () => {
    const doc = mockDocument(`(1)`);
    expect(countCollectionElements(doc, 0)).toBe(1);
  });

  it("counts multiple elements", () => {
    const doc = mockDocument(`(1, 2, 3)`);
    expect(countCollectionElements(doc, 0)).toBe(3);
  });

  it("handles nested structures", () => {
    const doc = mockDocument(`(1, [2, 3], {"a": 4})`);
    expect(countCollectionElements(doc, 0)).toBe(3);
  });

  it("counts set elements", () => {
    const doc = mockDocument(`{"a", "b", "c"}`);
    expect(countCollectionElements(doc, 0)).toBe(3);
  });

  it("handles empty braces", () => {
    const doc = mockDocument(`{}`);
    expect(countCollectionElements(doc, 0)).toBe(0);
  });
});

// ─── Tests: Hover content ────────────────────────────────────────────────────

describe("RdnHoverProvider", () => {
  let provider: RdnHoverProvider;

  beforeEach(() => {
    provider = new RdnHoverProvider();
    _setHoverConfig(defaultConfig());
  });

  it("shows DateTime hover with formatted date", () => {
    const doc = mockDocument(`@2024-01-15T10:30:45.123Z`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**DateTime**");
    expect(md.value).toContain("2024-01-15 10:30:45.123 UTC");
  });

  it("shows date-only hover", () => {
    const doc = mockDocument(`@2024-06-15`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**DateTime**");
    expect(md.value).toContain("June 15, 2024");
  });

  it("shows Unix timestamp hover", () => {
    const doc = mockDocument(`@1704067200`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Unix Timestamp**");
    expect(md.value).toContain("seconds");
  });

  it("shows TimeOnly hover", () => {
    const doc = mockDocument(`@14:30:00`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**TimeOnly**");
    expect(md.value).toContain("14:30:00");
  });

  it("shows Duration hover with expansion", () => {
    const doc = mockDocument(`@P1Y2M3DT4H5M6S`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Duration**");
    expect(md.value).toContain("1 year, 2 months, 3 days, 4 hours, 5 minutes, 6 seconds");
  });

  it("shows BigInt hover with grouped digits and bit length", () => {
    const doc = mockDocument(`1234567890n`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**BigInt**");
    expect(md.value).toContain("1,234,567,890");
    expect(md.value).toContain("bit");
  });

  it("shows BigInt diagnostic when fits in Number", () => {
    const doc = mockDocument(`42n`);
    const hover = provider.provideHover(doc, new MockPosition(0, 1) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("fits in a regular Number");
  });

  it("shows base64 binary hover with byte count", () => {
    const doc = mockDocument(`b"SGVsbG8="`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Base64 Binary**");
    expect(md.value).toContain("5 bytes");
    expect(md.value).toContain("Hello");
  });

  it("shows hex binary hover with byte count", () => {
    const doc = mockDocument(`x"48656C6C6F"`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Hex Binary**");
    expect(md.value).toContain("5 bytes");
    expect(md.value).toContain("Hello");
  });

  it("shows hex binary odd-digit diagnostic", () => {
    const doc = mockDocument(`x"123"`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("Odd number of hex digits");
  });

  it("shows RegExp hover with flag names", () => {
    const doc = mockDocument(`/^test$/gi`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**RegExp**");
    expect(md.value).toContain("global");
    expect(md.value).toContain("ignoreCase");
  });

  it("shows NaN hover", () => {
    const doc = mockDocument(`NaN`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**NaN**");
    expect(md.value).toContain("IEEE 754");
  });

  it("shows Infinity hover", () => {
    const doc = mockDocument(`Infinity`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Infinity**");
  });

  it("shows -Infinity hover", () => {
    const doc = mockDocument(`-Infinity`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**-Infinity**");
  });

  it("shows Map keyword hover with element count", () => {
    const doc = mockDocument(`Map{"a" => 1, "b" => 2}`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Map**");
    expect(md.value).toContain("2 entries");
  });

  it("shows Set keyword hover with element count", () => {
    const doc = mockDocument(`Set{1, 2, 3}`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Set**");
    expect(md.value).toContain("3 elements");
  });

  it("shows Tuple hover with element count", () => {
    const doc = mockDocument(`(1, 2, 3)`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Tuple**");
    expect(md.value).toContain("3 elements");
  });

  it("shows => arrow hover", () => {
    const doc = mockDocument(`{"a" => 1}`);
    const hover = provider.provideHover(doc, new MockPosition(0, 5) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**=>**");
    expect(md.value).toContain("Map entry separator");
  });

  it("shows implicit map hover", () => {
    const doc = mockDocument(`{"a" => 1, "b" => 2}`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Map** _(implicit)_");
    expect(md.value).toContain("2 entries");
  });

  it("shows implicit set hover", () => {
    const doc = mockDocument(`{"a", "b", "c"}`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Set** _(implicit)_");
    expect(md.value).toContain("3 elements");
  });
});

// ─── Tests: Settings behavior ────────────────────────────────────────────────

describe("Settings behavior", () => {
  let provider: RdnHoverProvider;

  beforeEach(() => {
    provider = new RdnHoverProvider();
  });

  it("master toggle disables all hovers", () => {
    const cfg = defaultConfig();
    cfg.enabled = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`@2024-01-15`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).toBeNull();
  });

  it("per-type toggle disables DateTime hovers", () => {
    const cfg = defaultConfig();
    cfg.dateTime.enabled = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`@2024-01-15`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).toBeNull();
  });

  it("per-type toggle disables BigInt hovers", () => {
    const cfg = defaultConfig();
    cfg.bigint.enabled = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`42n`);
    const hover = provider.provideHover(doc, new MockPosition(0, 1) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).toBeNull();
  });

  it("per-type toggle disables binary hovers", () => {
    const cfg = defaultConfig();
    cfg.binary.enabled = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`b"SGVsbG8="`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).toBeNull();
  });

  it("showPreview=false hides binary ASCII preview", () => {
    const cfg = defaultConfig();
    cfg.binary.showPreview = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`b"SGVsbG8="`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).not.toContain("ASCII preview");
  });

  it("showBitLength=false hides bit length", () => {
    const cfg = defaultConfig();
    cfg.bigint.showBitLength = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`42n`);
    const hover = provider.provideHover(doc, new MockPosition(0, 1) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).not.toContain("bit");
  });

  it("diagnostics=false hides BigInt fits-in-Number hint", () => {
    const cfg = defaultConfig();
    cfg.diagnostics.enabled = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`42n`);
    const hover = provider.provideHover(doc, new MockPosition(0, 1) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).not.toContain("fits in a regular Number");
  });

  it("diagnostics=false hides hex odd-digit warning", () => {
    const cfg = defaultConfig();
    cfg.diagnostics.enabled = false;
    _setHoverConfig(cfg);
    const doc = mockDocument(`x"123"`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).not.toContain("Odd number");
  });

  it("custom date format is used", () => {
    const cfg = defaultConfig();
    cfg.dateTime.fullFormat = "DD/MM/YYYY";
    _setHoverConfig(cfg);
    const doc = mockDocument(`@2024-01-15T10:30:00.000Z`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("15/01/2024");
  });

  it("invalidateHoverConfig causes config reload", () => {
    const cfg = defaultConfig();
    _setHoverConfig(cfg);
    invalidateHoverConfig();
    // After invalidation, the next getHoverConfig() will read from vscode.workspace
    // We just verify it doesn't throw
    expect(() => {
      const doc = mockDocument(`@2024-01-15`);
      provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    }).not.toThrow();
  });
});

// ─── Tests: scanBinaryErrors ─────────────────────────────────────────────────

describe("scanBinaryErrors", () => {
  it("returns empty for valid base64", () => {
    expect(scanBinaryErrors(`b"SGVsbG8="`)).toHaveLength(0);
  });

  it("returns empty for valid hex", () => {
    expect(scanBinaryErrors(`x"48656C6C6F"`)).toHaveLength(0);
  });

  it("detects invalid base64 character", () => {
    const errors = scanBinaryErrors(`b"SG!sbG8="`);
    expect(errors.length).toBe(1);
    expect(errors[0]!.message).toContain("Invalid base64 character '!'");
    expect(errors[0]!.kind).toBe("base64");
  });

  it("detects multiple invalid base64 characters", () => {
    const errors = scanBinaryErrors(`b"A#B$C"`);
    expect(errors.length).toBe(2);
    expect(errors[0]!.message).toContain("#");
    expect(errors[1]!.message).toContain("$");
  });

  it("detects data after base64 padding", () => {
    const errors = scanBinaryErrors(`b"SGVs=bG8"`);
    expect(errors.length).toBeGreaterThan(0);
    expect(errors.some((e) => e.message.includes("after padding"))).toBe(true);
  });

  it("detects invalid hex character", () => {
    const errors = scanBinaryErrors(`x"48G56C"`);
    expect(errors.length).toBe(1);
    expect(errors[0]!.message).toContain("Invalid hex character 'G'");
    expect(errors[0]!.kind).toBe("hex");
  });

  it("detects space as invalid hex character", () => {
    const errors = scanBinaryErrors(`x"48 65"`);
    expect(errors.length).toBe(1);
    expect(errors[0]!.message).toContain("' '");
  });

  it("does not flag characters inside regular strings", () => {
    const errors = scanBinaryErrors(`"this has b and x and !@# in it"`);
    expect(errors).toHaveLength(0);
  });

  it("finds errors in multiple binary literals", () => {
    const errors = scanBinaryErrors(`[b"A!B", x"GG"]`);
    expect(errors.length).toBe(3); // ! in base64, two G's in hex
    expect(errors[0]!.kind).toBe("base64");
    expect(errors[1]!.kind).toBe("hex");
    expect(errors[2]!.kind).toBe("hex");
  });

  it("returns correct offsets", () => {
    // b"A!B" — the ! is at index 3 (b=0, "=1, A=2, !=3)
    const errors = scanBinaryErrors(`b"A!B"`);
    expect(errors.length).toBe(1);
    expect(errors[0]!.offset).toBe(3);
    expect(errors[0]!.length).toBe(1);
  });
});

// ─── Tests: formatByteSize ───────────────────────────────────────────────────

describe("formatByteSize", () => {
  it("formats bytes", () => {
    expect(formatByteSize(5)).toBe("5 bytes");
    expect(formatByteSize(1)).toBe("1 byte");
    expect(formatByteSize(0)).toBe("0 bytes");
  });

  it("formats kilobytes", () => {
    expect(formatByteSize(1024)).toBe("1.0 KB");
    expect(formatByteSize(2560)).toBe("2.5 KB");
  });

  it("formats megabytes", () => {
    expect(formatByteSize(1048576)).toBe("1.0 MB");
  });
});

// ─── Tests: Image detection ──────────────────────────────────────────────────

describe("detectImageFromBytes", () => {
  it("detects PNG", () => {
    const result = detectImageFromBytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    expect(result).not.toBeNull();
    expect(result!.label).toBe("PNG");
    expect(result!.mimeType).toBe("image/png");
  });

  it("detects JPEG", () => {
    const result = detectImageFromBytes([0xFF, 0xD8, 0xFF, 0xE0]);
    expect(result).not.toBeNull();
    expect(result!.label).toBe("JPEG");
  });

  it("detects GIF", () => {
    const result = detectImageFromBytes([0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);
    expect(result).not.toBeNull();
    expect(result!.label).toBe("GIF");
  });

  it("detects BMP", () => {
    const result = detectImageFromBytes([0x42, 0x4D, 0x00, 0x00]);
    expect(result).not.toBeNull();
    expect(result!.label).toBe("BMP");
  });

  it("detects WebP", () => {
    const result = detectImageFromBytes([0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]);
    expect(result).not.toBeNull();
    expect(result!.label).toBe("WebP");
  });

  it("detects ICO", () => {
    const result = detectImageFromBytes([0x00, 0x00, 0x01, 0x00]);
    expect(result).not.toBeNull();
    expect(result!.label).toBe("ICO");
  });

  it("returns null for non-image data", () => {
    expect(detectImageFromBytes([0x48, 0x65, 0x6C, 0x6C, 0x6F])).toBeNull(); // "Hello"
  });

  it("returns null for too few bytes", () => {
    expect(detectImageFromBytes([0x89])).toBeNull();
  });

  it("returns null for empty bytes", () => {
    expect(detectImageFromBytes([])).toBeNull();
  });
});

// ─── Tests: bytesToBase64 ────────────────────────────────────────────────────

describe("bytesToBase64", () => {
  it("encodes Hello", () => {
    // "Hello" = [72, 101, 108, 108, 111]
    expect(bytesToBase64([72, 101, 108, 108, 111])).toBe("SGVsbG8=");
  });

  it("encodes empty array", () => {
    expect(bytesToBase64([])).toBe("");
  });

  it("handles single byte", () => {
    expect(bytesToBase64([0])).toBe("AA==");
  });

  it("handles two bytes", () => {
    expect(bytesToBase64([0, 0])).toBe("AAA=");
  });
});

// ─── Tests: Image hover preview ──────────────────────────────────────────────

describe("Image hover preview", () => {
  let provider: RdnHoverProvider;

  beforeEach(() => {
    provider = new RdnHoverProvider();
    _setHoverConfig(defaultConfig());
  });

  it("shows image preview for base64 PNG", () => {
    // Minimal PNG header as base64: 89504E47 0D0A1A0A = iVBORw0KGgo=
    const pngB64 = "iVBORw0KGgo=";
    const doc = mockDocument(`b"${pngB64}"`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Base64 Binary** _(PNG)_");
    expect(md.value).toContain("<img src=");
    expect(md.value).toContain("data:image/png;base64,");
    expect(md.supportHtml).toBe(true);
  });

  it("shows image preview for hex JPEG", () => {
    // JPEG magic bytes: FFD8FFE0 + some padding
    const doc = mockDocument(`x"FFD8FFE000104A464946"`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Hex Binary** _(JPEG)_");
    expect(md.value).toContain("<img src=");
    expect(md.value).toContain("data:image/jpeg;base64,");
    expect(md.supportHtml).toBe(true);
  });

  it("shows text hover for non-image base64", () => {
    const doc = mockDocument(`b"SGVsbG8="`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Base64 Binary**");
    expect(md.value).not.toContain("_(PNG)_");
    expect(md.value).not.toContain("<img src=");
    expect(md.value).toContain("Hello");
  });

  it("shows text hover for non-image hex", () => {
    const doc = mockDocument(`x"48656C6C6F"`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    expect(hover).not.toBeNull();
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("**Hex Binary**");
    expect(md.value).not.toContain("<img src=");
    expect(md.value).toContain("Hello");
  });

  it("shows formatted byte size for image", () => {
    const pngB64 = "iVBORw0KGgo=";
    const doc = mockDocument(`b"${pngB64}"`);
    const hover = provider.provideHover(doc, new MockPosition(0, 0) as import("vscode").Position, {} as import("vscode").CancellationToken);
    const md = (hover as MockHover).contents as MockMarkdownString;
    expect(md.value).toContain("byte");
  });
});

// ─── Tests: scanUnquotedKeys ──────────────────────────────────────────────────

describe("scanUnquotedKeys", () => {
  it("returns empty for valid quoted keys", () => {
    expect(scanUnquotedKeys(`{"a": 1, "b": 2}`)).toHaveLength(0);
  });

  it("detects a single unquoted key", () => {
    const keys = scanUnquotedKeys(`{foo: 1}`);
    expect(keys).toHaveLength(1);
    expect(keys[0]!.name).toBe("foo");
  });

  it("detects multiple unquoted keys", () => {
    const keys = scanUnquotedKeys(`{foo: 1, bar: "test"}`);
    expect(keys).toHaveLength(2);
    expect(keys[0]!.name).toBe("foo");
    expect(keys[1]!.name).toBe("bar");
  });

  it("does not flag RDN keywords as unquoted keys", () => {
    expect(scanUnquotedKeys(`{"key": true}`)).toHaveLength(0);
    expect(scanUnquotedKeys(`{"key": false}`)).toHaveLength(0);
    expect(scanUnquotedKeys(`{"key": null}`)).toHaveLength(0);
    expect(scanUnquotedKeys(`{"key": NaN}`)).toHaveLength(0);
  });

  it("does not flag set values as unquoted keys", () => {
    expect(scanUnquotedKeys(`{"a", "b", "c"}`)).toHaveLength(0);
  });

  it("does not flag map keys as unquoted keys", () => {
    expect(scanUnquotedKeys(`{"a" => 1, "b" => 2}`)).toHaveLength(0);
  });

  it("does not flag Map/Set keywords", () => {
    expect(scanUnquotedKeys(`Map{"a" => 1}`)).toHaveLength(0);
    expect(scanUnquotedKeys(`Set{1, 2, 3}`)).toHaveLength(0);
  });

  it("handles nested objects", () => {
    const keys = scanUnquotedKeys(`{"a": {foo: 1}}`);
    expect(keys).toHaveLength(1);
    expect(keys[0]!.name).toBe("foo");
  });

  it("returns correct offsets", () => {
    const keys = scanUnquotedKeys(`{foo: 1}`);
    expect(keys[0]!.offset).toBe(1);
    expect(keys[0]!.length).toBe(3);
  });

  it("skips strings and regex", () => {
    expect(scanUnquotedKeys(`{"key": "foo: bar"}`)).toHaveLength(0);
    expect(scanUnquotedKeys(`{"key": /foo: bar/}`)).toHaveLength(0);
  });

  it("skips @ literals", () => {
    expect(scanUnquotedKeys(`{"key": @2024-01-15}`)).toHaveLength(0);
  });

  it("skips binary literals", () => {
    expect(scanUnquotedKeys(`{"key": b"SGVsbG8="}`)).toHaveLength(0);
    expect(scanUnquotedKeys(`{"key": x"48656C6C6F"}`)).toHaveLength(0);
  });
});

// ─── Tests: formatSorted ──────────────────────────────────────────────────────

describe("formatSorted", () => {
  it("sorts object keys alphabetically", () => {
    const input = `{"c": 3, "a": 1, "b": 2}`;
    const result = formatSorted(input, 2, true);
    expect(result).not.toBeNull();
    expect(result!.indexOf('"a"')).toBeLessThan(result!.indexOf('"b"'));
    expect(result!.indexOf('"b"')).toBeLessThan(result!.indexOf('"c"'));
  });

  it("sorts nested object keys", () => {
    const input = `{"z": {"b": 2, "a": 1}, "a": 0}`;
    const result = formatSorted(input, 2, true);
    expect(result).not.toBeNull();
    expect(result!.indexOf('"a": 0')).toBeLessThan(result!.indexOf('"z"'));
    // Inner keys should also be sorted
    const zBlock = result!.slice(result!.indexOf('"z"'));
    expect(zBlock.indexOf('"a"')).toBeLessThan(zBlock.indexOf('"b"'));
  });

  it("returns null on invalid RDN", () => {
    expect(formatSorted(`{invalid`, 2, true)).toBeNull();
  });

  it("preserves non-object values", () => {
    const input = `[3, 1, 2]`;
    const result = formatSorted(input, 2, true);
    expect(result).not.toBeNull();
    expect(result).toContain("3");
  });

  it("handles empty object", () => {
    const result = formatSorted(`{}`, 2, true);
    expect(result).not.toBeNull();
    expect(result!.trim()).toBe("{}");
  });
});
