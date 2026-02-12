// d8 benchmark: RDN.parse performance
// Run with: ~/v8/v8/out/x64.release/d8 parse-bench.js

const ITERATIONS = 100_000;

const fixtures = {
  simple: '{"name": "test", "value": 42, "active": true}',
  withDate: '{"created": @2024-01-15T10:30:00.000Z, "name": "test"}',
  withBigInt: '{"id": 900000001338n, "name": "user"}',
  withCollections: '{"tags": Set{"admin", "editor"}, "meta": Map{"key" => "val"}}',
  complex: `{
    "meta": {"apiVersion": "2.1.0", "timestamp": @2025-08-15T14:32:07.123Z},
    "data": {
      "id": 900000001338n,
      "created": @2024-11-05T10:34:33.000Z,
      "roles": {"admin", "editor"},
      "sessions": Map{@2025-09-07 => @PT2H30M, @2025-09-08 => @PT1H15M},
      "pattern": /^[A-Za-z]+$/i,
      "avatar": b"SGVsbG8="
    }
  }`,
};

function bench(name, input) {
  const start = performance.now();
  for (let i = 0; i < ITERATIONS; i++) {
    RDN.parse(input);
  }
  const elapsed = performance.now() - start;
  const opsPerSec = ((ITERATIONS / elapsed) * 1000).toFixed(0);
  print(`${name}: ${elapsed.toFixed(2)}ms (${opsPerSec} ops/sec)`);
}

print(`RDN.parse benchmark — ${ITERATIONS} iterations each`);
print("=".repeat(60));

for (const [name, input] of Object.entries(fixtures)) {
  bench(name, input);
}
