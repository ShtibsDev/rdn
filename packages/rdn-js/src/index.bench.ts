import { bench, describe } from "vitest";
import { parse, stringify } from "./index.js";

const simple = '{"name": "Alice", "age": 30, "active": true, "tags": ["a", "b", "c"]}';
const withDate = '{"name": "Alice", "created": @2024-01-15T10:30:00.000Z, "updated": @2024-06-01T00:00:00.000Z}';
const withBigInt = '{"id": 999999999999999999n, "balance": 12345678901234567890n}';
const withCollections = 'Map{"a" => Set{1, 2, 3}, "b" => Set{4, 5, 6}}';
const complex = `{
  "user": {
    "id": 42n,
    "name": "Alice",
    "email": "alice@example.com",
    "created": @2024-01-15T10:30:00.000Z,
    "lastLogin": @2024-06-01T12:00:00.000Z,
    "tags": Set{"admin", "editor"},
    "sessions": Map{@2024-01-14 => @PT2H, @2024-01-15 => @PT1H30M},
    "avatar": b"SGVsbG8gV29ybGQ=",
    "namePattern": /^[A-Za-z]+$/i,
    "preferences": {"theme": "dark", "notifications": true},
    "scores": [100, 95, 88, 92, NaN]
  }
}`;

const jsonOnly = '{"name": "Alice", "age": 30, "active": true, "items": [1, 2, 3], "nested": {"key": "value"}}';

describe("RDN.parse", () => {
  bench("simple", () => { parse(simple); });
  bench("withDate", () => { parse(withDate); });
  bench("withBigInt", () => { parse(withBigInt); });
  bench("withCollections", () => { parse(withCollections); });
  bench("complex", () => { parse(complex); });
});

describe("RDN.stringify", () => {
  const simpleVal = parse(simple);
  const withDateVal = parse(withDate);
  const withBigIntVal = parse(withBigInt);
  const withCollectionsVal = parse(withCollections);
  const complexVal = parse(complex);

  bench("simple", () => { stringify(simpleVal); });
  bench("withDate", () => { stringify(withDateVal); });
  bench("withBigInt", () => { stringify(withBigIntVal); });
  bench("withCollections", () => { stringify(withCollectionsVal); });
  bench("complex", () => { stringify(complexVal); });
});

describe("Baseline: JSON vs RDN (JSON-only input)", () => {
  bench("JSON.parse", () => { JSON.parse(jsonOnly); });
  bench("RDN.parse", () => { parse(jsonOnly); });

  const jsonVal = JSON.parse(jsonOnly);
  bench("JSON.stringify", () => { JSON.stringify(jsonVal); });
  bench("RDN.stringify", () => { stringify(jsonVal); });
});
