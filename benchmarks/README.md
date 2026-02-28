# RDN Benchmarks

Cross-implementation benchmark harness for RDN parsers and serializers.

## Fixtures

Each tier has two variants (**typical** and **rdn-heavy**), each as both `.rdn` and `.json` for apples-to-apples comparison.

| Fixture | Size | Description | Committed |
|---------|------|-------------|-----------|
| `small-typical` | ~3 KB | Product catalog API — 2 products, ~80% JSON-native | Yes |
| `small-rdn-heavy` | ~4 KB | IoT dashboard — 2 sensors, all RDN types | Yes |
| `medium-typical` | ~1 MB | Product catalog — 560 products | Yes |
| `medium-rdn-heavy` | ~1 MB | IoT dashboard — 185 sensors | Yes |
| `large-typical` | ~100 MB | Product catalog — 56,000 products | No (.gitignored) |
| `large-rdn-heavy` | ~100 MB | IoT dashboard — 18,500 sensors | No (.gitignored) |

### Typical Payload — Product Catalog API

Realistic e-commerce API response. ~80% JSON-native types, ~20% RDN (dates + BigInt IDs).

### RDN-Heavy Payload — IoT Monitoring Dashboard

Dense use of every RDN type: Date, BigInt, RegExp, binary (b64 + hex), Map, Set, tuple, TimeOnly, Duration, NaN, Infinity, -Infinity. ~30% JSON-native, ~70% RDN-specific.

### JSON Equivalents

| RDN | JSON |
|-----|------|
| `@2024-01-15T10:30:00.000Z` | `"2024-01-15T10:30:00.000Z"` |
| `42n` | `"42"` |
| `Set{"a", "b"}` / `{"a", "b"}` | `["a", "b"]` |
| `Map{"k" => "v"}` | `{"k": "v"}` |
| `(1, 2, 3)` | `[1, 2, 3]` |
| `/pattern/flags` | `"pattern"` |
| `b"SGVsbG8="` | `"SGVsbG8="` |
| `x"4A6F686E"` | `"Sm9obg=="` (converted to b64) |
| `@14:30:00` | `"14:30:00"` |
| `@PT2H30M` | `"PT2H30M"` |
| `NaN` / `Infinity` / `-Infinity` | `null` |

## Generating Fixtures

```bash
# Generate all medium + large fixtures
pnpm generate-fixtures

# Generate only medium
pnpm generate-fixtures -- --size medium

# Generate only large
pnpm generate-fixtures -- --size large

# Generate only typical variant
pnpm generate-fixtures -- --type typical

# Dry run (show what would be generated)
pnpm generate-fixtures -- --dry-run
```

## Running Benchmarks

```bash
./run.sh
```
