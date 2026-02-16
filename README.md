# RDN — Rich Data Notation

A **JSON superset** that adds native representations for types every application needs but JSON lacks: dates, BigInts, regular expressions, binary data, Maps, Sets, and more.

Any valid JSON is valid RDN. RDN adds no comments, no trailing commas, and no unquoted keys — it stays close to JSON's simplicity while closing its type gaps.

## Quick Example

```
{
  "meta": {
    "apiVersion": "2.1.0",
    "timestamp": @2025-08-15T14:32:07.123Z,
    "rateLimit": {
      "remaining": 4982,
      "resetAt": @2025-08-15T15:00:00.000Z
    }
  },
  "data": {
    "users": [
      {
        "id": "usr_00001",
        "externalId": 900000001338n,
        "email": "jack.thompson12@gmail.com",
        "createdAt": @2024-11-05T10:34:33.000Z,
        "lastLogin": @2025-09-08T01:25:47.000Z,
        "preferences": {
          "theme": "light",
          "notifications": {"email": true, "push": false}
        },
        "avatar": b"t41AP44NzIXj1CFhg2UR7RE7SjxEBwlkoOVKGVLdHeTgEJAKaQ==",
        "roles": {"admin", "editor"},
        "sessionLog": Map{
          @2025-09-07 => @PT2H30M,
          @2025-09-08 => @PT1H15M
        },
        "namePattern": /^[A-Za-z\s'-]+$/
      }
    ]
  }
}
```

## Extended Types

| RDN Syntax | JavaScript Type | Example |
|---|---|---|
| `42n` | `BigInt` | `999999999999999999n` |
| `@2024-01-15T10:30:00.000Z` | `Date` | `@2024-01-15` |
| `@14:30:00` | `TimeOnly` | `@23:59:59.999` |
| `@P1Y2M3D` | `Duration` | `@PT1H30M` |
| `/pattern/flags` | `RegExp` | `/^[a-z]+$/i` |
| `b"base64..."` | `Uint8Array` | `x"48656C6C6F"` |
| `Map{k => v}` | `Map` | `{"a" => 1}` |
| `Set{1, 2, 3}` | `Set` | `{"a", "b"}` |
| `(1, 2, 3)` | `Array` (tuple) | `("x", "y")` |
| `NaN`, `Infinity` | `Number` | `-Infinity` |

## Repository Structure

```
rdn/
├── spec/                     # Formal specification
│   ├── rdn-spec.md           # Full RDN specification (source of truth)
│   ├── grammar.ebnf          # Formal grammar in EBNF notation
│   └── examples/             # Example .rdn files
├── test-suite/               # Language-agnostic conformance tests
│   ├── valid/                # .rdn + .expected.json pairs
│   ├── invalid/              # Files that must fail parsing
│   └── roundtrip/            # Parse → serialize → parse identity tests
├── packages/
│   ├── rdn-js/               # TypeScript reference implementation (vitest, ESM, strict TS)
│   ├── rdn-rust/             # Rust with WASM feature flag, criterion benchmarks
│   ├── rdn-dotnet/           # C# / .NET 8 with ASP.NET Core formatters & NuGet packaging
│   ├── rdn-go/               # Go (placeholder)
│   └── rdn-python/           # Python (placeholder)
├── v8-integration/           # Docs, patches, and d8 benchmarks for the V8 fork
│   ├── README.md
│   ├── patches/
│   └── benchmarks/
├── benchmarks/               # Cross-implementation benchmark harness
├── tools/
│   ├── rdn-cli/              # CLI: validate, fmt, convert json↔rdn
│   ├── vscode-extension/     # VS Code language support
│   └── playground/           # Web playground (Rust WASM)
├── assets/                   # Branding assets (SVG, WEBP)
└── docs/                     # Documentation
```

## Getting Started

### TypeScript (Reference Implementation)

```bash
cd packages/rdn-js
npm install
npm test
```

### Rust

```bash
cd packages/rdn-rust
cargo test
cargo bench
```

### C# (.NET 8)

```bash
cd packages/rdn-dotnet
dotnet build Rdn.sln
dotnet test
```

### Running the Conformance Test Suite

Each implementation should run against the shared test suite in `test-suite/`. Valid tests provide `.rdn` input files and `.expected.json` with the expected parse output. Extended types use a `{"$type": "TypeName", "value": ...}` convention in expected JSON.

## API

### `RDN.parse(text [, reviver])`

Parse an RDN string into native values. The optional `reviver` function is called bottom-up on each value.

### `RDN.stringify(value [, replacer])`

Serialize a value to an RDN string. The optional `replacer` function controls which values are included.

## Differences from JSON

- **NaN / Infinity**: Native literals instead of `null`
- **BigInt**: `42n` instead of throwing
- **Date**: `@2024-01-15T10:30:00.000Z` instead of strings
- **RegExp**: `/pattern/flags` instead of not serializable
- **Binary**: `b"..."` / `x"..."` instead of base64 strings or number arrays
- **Map / Set**: `Map{k => v}` / `Set{v}` instead of unsupported
- **Tuple**: `(v, v)` for ordered sequences
- **TimeOnly / Duration**: `@14:30:00` / `@P1Y2M3D`

## V8 Integration

The V8 fork with native `RDN.parse()` / `RDN.stringify()` lives at `~/v8`. See [v8-integration/README.md](v8-integration/README.md) for build instructions and benchmarks.

## License

MIT
