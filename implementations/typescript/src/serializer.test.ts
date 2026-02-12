import { describe, it, expect } from "vitest";
import { stringify } from "./serializer.js";

describe("RDN.stringify", () => {
  describe("JSON-compatible types", () => {
    it.todo("serializes null");
    it.todo("serializes booleans");
    it.todo("serializes numbers");
    it.todo("serializes strings");
    it.todo("serializes arrays");
    it.todo("serializes objects");
  });

  describe("special numbers", () => {
    it.todo("serializes NaN");
    it.todo("serializes Infinity");
    it.todo("serializes -Infinity");
  });

  describe("BigInt", () => {
    it.todo("serializes bigint with n suffix");
  });

  describe("Date", () => {
    it.todo("serializes valid date as @ISO");
    it.todo("serializes invalid date as null");
  });

  describe("RegExp", () => {
    it.todo("serializes regexp with /pattern/flags");
  });

  describe("Binary", () => {
    it.todo("serializes Uint8Array as b\"base64\"");
  });

  describe("Map", () => {
    it.todo("serializes non-empty map as Map{k => v}");
    it.todo("serializes empty map as Map{}");
  });

  describe("Set", () => {
    it.todo("serializes non-empty set as Set{v, ...}");
    it.todo("serializes empty set as Set{}");
  });

  describe("special values", () => {
    it.todo("omits undefined from objects");
    it.todo("serializes undefined as null in arrays");
    it.todo("returns undefined for non-serializable root");
  });

  describe("cycle detection", () => {
    it.todo("throws TypeError on circular reference");
  });
});
