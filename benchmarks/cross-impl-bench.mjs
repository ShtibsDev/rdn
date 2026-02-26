/**
 * Cross-implementation benchmark: TypeScript RDN vs JSON.parse/stringify
 * Uses the same payloads as Python bench_compare.py for direct comparison.
 */

import { parse, stringify } from '../packages/rdn-js/dist/index.js';

// ── Payloads (identical to Python bench_compare.py) ──────────────────────

const SMALL_JSON = '{"name": "test", "value": 42, "active": true}';

const MEDIUM_JSON = JSON.stringify({
  users: Array.from({ length: 20 }, (_, i) => ({
    id: i, name: `user_${i}`, email: `user${i}@example.com`, active: i % 2 === 0, score: i * 1.5, tags: ["a", "b", "c"]
  })),
  meta: { total: 20, page: 1, perPage: 20, hasMore: false },
});

const LARGE_JSON = JSON.stringify({
  apiVersion: "2.1.0",
  requestId: "c7d83aef-cf17-42e1-baef-00004539f5f8",
  data: {
    users: Array.from({ length: 50 }, (_, i) => ({
      id: `usr_${String(i).padStart(5, '0')}`,
      email: `user${i}@example.com`,
      profile: { firstName: `First${i}`, lastName: `Last${i}`, bio: `Bio text for user ${i} `.repeat(5) },
      preferences: { theme: i % 2 ? "dark" : "light", notifications: { email: true, push: false, sms: i % 3 === 0 } },
      roles: i % 5 === 0 ? ["admin", "editor"] : ["viewer"],
      scores: Array.from({ length: 10 }, (_, j) => Math.round((i * 1.1 + j * 0.7) * 100) / 100),
      metadata: Object.fromEntries(Array.from({ length: 5 }, (_, j) => [`key_${j}`, `value_${j}`])),
    })),
  },
  pagination: { total: 50, page: 1, perPage: 50, hasMore: false },
});

const MEDIUM_RDN = `{
  "users": [
    {"id": 1, "name": "Alice", "created": @2024-01-15T10:30:00.000Z, "tags": {"admin", "editor"}},
    {"id": 2, "name": "Bob", "created": @2024-02-20T08:00:00.000Z, "tags": {"viewer"}},
    {"id": 3, "name": "Charlie", "created": @2024-03-10T14:15:00.000Z, "tags": {"editor"}}
  ],
  "meta": {"total": 3, "generatedAt": @2024-06-01T00:00:00.000Z}
}`;

const REALISTIC_RDN = `{
  "apiVersion": "2.1.0",
  "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": @2025-08-15T14:32:07.123Z,
  "data": {
    "users": [
      {
        "id": 1,
        "externalId": 9007199254740993n,
        "name": "Alice Johnson",
        "email": "alice@example.com",
        "createdAt": @2024-01-15T10:30:00.000Z,
        "lastLogin": @2025-08-14T22:15:33.000Z,
        "score": 98.5,
        "active": true,
        "roles": ["admin", "editor"],
        "preferences": {"theme": "dark", "notifications": true, "language": "en"}
      },
      {
        "id": 2,
        "externalId": 9007199254740994n,
        "name": "Bob Smith",
        "email": "bob@example.com",
        "createdAt": @2024-02-20T08:00:00.000Z,
        "lastLogin": @2025-08-15T09:42:11.000Z,
        "score": 87.3,
        "active": true,
        "roles": ["viewer"],
        "preferences": {"theme": "light", "notifications": false, "language": "es"}
      },
      {
        "id": 3,
        "externalId": 9007199254740995n,
        "name": "Charlie Davis",
        "email": "charlie@example.com",
        "createdAt": @2024-03-10T14:15:00.000Z,
        "lastLogin": @2025-08-13T17:08:55.000Z,
        "score": 92.1,
        "active": false,
        "roles": ["editor"],
        "preferences": {"theme": "dark", "notifications": true, "language": "fr"}
      }
    ],
    "pagination": {"total": 3, "page": 1, "perPage": 20, "hasMore": false}
  }
}`;

// ── Benchmark helper ─────────────────────────────────────────────────────

function bench(fn, iterations) {
  // warmup
  for (let i = 0; i < Math.min(iterations, 1000); i++) fn();
  const start = performance.now();
  for (let i = 0; i < iterations; i++) fn();
  const elapsed = (performance.now() - start) / 1000; // seconds
  return iterations / elapsed; // ops/sec
}

function fmt(ops) {
  if (ops >= 1_000_000) return `${(ops / 1_000_000).toFixed(2)}M`;
  if (ops >= 1_000) return `${(ops / 1_000).toFixed(1)}K`;
  return `${ops.toFixed(0)}`;
}

function nsPerOp(ops) {
  return (1_000_000_000 / ops).toFixed(0);
}

// ── Iteration counts (same as Python) ────────────────────────────────────
const ITERS = { small: 200_000, medium: 50_000, large: 10_000, realistic: 50_000 };

// ── Run ──────────────────────────────────────────────────────────────────

const results = {};

console.log("=== Cross-Implementation Benchmark: TypeScript RDN ===");
console.log(`Node.js ${process.version}`);
console.log();
console.log("Payload sizes:");
console.log(`  Small JSON:    ${SMALL_JSON.length}B`);
console.log(`  Medium JSON:   ${MEDIUM_JSON.length}B`);
console.log(`  Large JSON:    ${LARGE_JSON.length}B`);
console.log(`  Medium RDN:    ${MEDIUM_RDN.length}B`);
console.log(`  Realistic RDN: ${REALISTIC_RDN.length}B`);
console.log();

// Pre-parse objects for stringify
const SMALL_OBJ = JSON.parse(SMALL_JSON);
const MEDIUM_OBJ = JSON.parse(MEDIUM_JSON);
const LARGE_OBJ = JSON.parse(LARGE_JSON);
const MEDIUM_RDN_OBJ = parse(MEDIUM_RDN);
const REALISTIC_RDN_OBJ = parse(REALISTIC_RDN);

// ── PARSE ────────────────────────────────────────────────────────────────

console.log("--- PARSE ---");

for (const [name, payload, iters] of [
  ["small_json", SMALL_JSON, ITERS.small],
  ["medium_json", MEDIUM_JSON, ITERS.medium],
  ["large_json", LARGE_JSON, ITERS.large],
  ["medium_rdn", MEDIUM_RDN, ITERS.medium],
  ["realistic_rdn", REALISTIC_RDN, ITERS.realistic],
]) {
  const jsonOps = name.includes("rdn") ? null : bench(() => JSON.parse(payload), iters);
  const rdnOps = bench(() => parse(payload), iters);

  results[`parse_${name}`] = { json: jsonOps, rdn: rdnOps };

  const jsonStr = jsonOps ? `JSON.parse: ${fmt(jsonOps)} ops/s (${nsPerOp(jsonOps)} ns)` : "JSON.parse: N/A";
  console.log(`  ${name.padEnd(16)} ${jsonStr}  |  RDN.parse: ${fmt(rdnOps)} ops/s (${nsPerOp(rdnOps)} ns)`);
}

console.log();

// ── STRINGIFY ─────────────────────────────────────────────────────────────

console.log("--- STRINGIFY ---");

for (const [name, obj, iters] of [
  ["small_json", SMALL_OBJ, ITERS.small],
  ["medium_json", MEDIUM_OBJ, ITERS.medium],
  ["large_json", LARGE_OBJ, ITERS.large],
  ["medium_rdn", MEDIUM_RDN_OBJ, ITERS.medium],
  ["realistic_rdn", REALISTIC_RDN_OBJ, ITERS.realistic],
]) {
  const jsonOps = name.includes("rdn") ? null : bench(() => JSON.stringify(obj), iters);
  const rdnOps = bench(() => stringify(obj), iters);

  results[`stringify_${name}`] = { json: jsonOps, rdn: rdnOps };

  const jsonStr = jsonOps ? `JSON.stringify: ${fmt(jsonOps)} ops/s (${nsPerOp(jsonOps)} ns)` : "JSON.stringify: N/A";
  console.log(`  ${name.padEnd(16)} ${jsonStr}  |  RDN.stringify: ${fmt(rdnOps)} ops/s (${nsPerOp(rdnOps)} ns)`);
}

console.log();

// ── JSON output for machine-readable comparison ──────────────────────────
console.log("--- JSON_RESULTS_START ---");
console.log(JSON.stringify(results, null, 2));
console.log("--- JSON_RESULTS_END ---");
