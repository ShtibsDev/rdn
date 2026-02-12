import { describe, it, expect } from "vitest";
import { parse } from "./parser.js";

describe("RDN.parse", () => {
  describe("JSON-compatible types", () => {
    it.todo("parses null");
    it.todo("parses booleans");
    it.todo("parses integers");
    it.todo("parses floats");
    it.todo("parses strings with escapes");
    it.todo("parses arrays");
    it.todo("parses objects");
    it.todo("parses nested structures");
  });

  describe("special numbers", () => {
    it.todo("parses NaN");
    it.todo("parses Infinity");
    it.todo("parses -Infinity");
  });

  describe("BigInt", () => {
    it.todo("parses 0n");
    it.todo("parses positive bigint");
    it.todo("parses negative bigint");
  });

  describe("DateTime", () => {
    it.todo("parses full ISO datetime");
    it.todo("parses ISO without milliseconds");
    it.todo("parses date-only");
    it.todo("parses unix timestamp in seconds");
    it.todo("parses unix timestamp in milliseconds");
  });

  describe("TimeOnly", () => {
    it.todo("parses time without milliseconds");
    it.todo("parses time with milliseconds");
  });

  describe("Duration", () => {
    it.todo("parses full duration");
    it.todo("parses time-only duration");
  });

  describe("RegExp", () => {
    it.todo("parses simple pattern");
    it.todo("parses pattern with flags");
    it.todo("parses pattern with escaped slash");
  });

  describe("Binary", () => {
    it.todo("parses base64 binary");
    it.todo("parses hex binary");
    it.todo("parses empty binary");
  });

  describe("Map", () => {
    it.todo("parses explicit empty map");
    it.todo("parses explicit map");
    it.todo("parses implicit map (brace disambiguation)");
    it.todo("parses map with non-string keys");
  });

  describe("Set", () => {
    it.todo("parses explicit empty set");
    it.todo("parses explicit set");
    it.todo("parses implicit set (brace disambiguation)");
    it.todo("parses single-element implicit set");
  });

  describe("Tuple", () => {
    it.todo("parses empty tuple");
    it.todo("parses tuple as array");
  });

  describe("brace disambiguation", () => {
    it.todo("empty braces → Object");
    it.todo("colon separator → Object");
    it.todo("arrow separator → Map");
    it.todo("comma separator → Set");
    it.todo("single value + close brace → Set");
  });

  describe("errors", () => {
    it.todo("throws on trailing comma");
    it.todo("throws on unquoted keys");
    it.todo("throws on invalid input");
  });
});
