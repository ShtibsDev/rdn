// d8 benchmark: RDN.stringify performance
// Run with: ~/v8/v8/out/x64.release/d8 stringify-bench.js

const ITERATIONS = 100_000;

const fixtures = {
  simple: { name: "test", value: 42, active: true },
  withDate: { created: new Date("2024-01-15T10:30:00.000Z"), name: "test" },
  withBigInt: { id: 900000001338n, name: "user" },
  withCollections: {
    tags: new Set(["admin", "editor"]),
    meta: new Map([["key", "val"]]),
  },
  withRegExp: { pattern: /^[A-Za-z]+$/i, name: "test" },
  withBinary: { data: new Uint8Array([72, 101, 108, 108, 111]) },
};

function bench(name, input) {
  const start = performance.now();
  for (let i = 0; i < ITERATIONS; i++) {
    RDN.stringify(input);
  }
  const elapsed = performance.now() - start;
  const opsPerSec = ((ITERATIONS / elapsed) * 1000).toFixed(0);
  print(`${name}: ${elapsed.toFixed(2)}ms (${opsPerSec} ops/sec)`);
}

print(`RDN.stringify benchmark — ${ITERATIONS} iterations each`);
print("=".repeat(60));

for (const [name, input] of Object.entries(fixtures)) {
  bench(name, input);
}
