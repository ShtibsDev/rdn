# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RDN (Rich Data Notation) is a **JSON superset** that adds native representations for dates, BigInts, regular expressions, binary data, Maps, Sets, tuples, TimeOnly, Duration, and special numeric values (NaN, Infinity). Any valid JSON is valid RDN — no comments, no trailing commas, no unquoted keys.

This is a monorepo containing the specification, multi-language implementations, a shared conformance test suite, V8 integration docs, and tooling.

## Build & Test Commands

### All JS/TS Packages (pnpm + Turborepo)
```bash
pnpm install              # install all dependencies (from repo root)
pnpm build                # turbo run build (all JS/TS packages)
pnpm test                 # turbo run test
pnpm lint                 # turbo run lint (tsc --noEmit)
```

### Individual JS/TS Packages
```bash
pnpm --filter @rdn/typescript test       # run tests for rdn-js only
pnpm --filter prettier-plugin-rdn build  # build prettier plugin only
pnpm --filter rdn build                  # build vscode extension only
```

### Changesets (versioning & changelogs)
```bash
pnpm changeset            # create a new changeset
pnpm version-packages     # bump versions from pending changesets
pnpm release              # build + publish to npm

# Pre-releases (alpha, beta, rc)
pnpm pre:enter alpha      # enter pre-release mode → versions become x.y.z-alpha.0
pnpm changeset            # create changesets as normal
pnpm version-packages     # bumps to e.g. 0.2.0-alpha.0
pnpm pre:exit             # exit pre-release mode → next version-packages produces stable release
```

### Rust
```bash
cd implementations/rust
cargo test
cargo bench                    # criterion benchmarks
cargo build --features wasm    # WASM build with wasm-bindgen
```

### C# (.NET 9)
```bash
cd packages/rdn-dotnet
dotnet build Rdn.sln
dotnet test
```
The C# version is managed by Changesets via `packages/rdn-dotnet/package.json`. Running `pnpm version-packages` syncs the version to `Directory.Build.props` automatically.

### Go / Python (placeholders)
```bash
cd implementations/go && go test
cd implementations/python && pip install -e . && pytest
```

## Architecture

### Source of Truth
- **Specification:** `spec/rdn-spec.md` — the authoritative reference for all parsing/serialization behavior
- **Grammar:** `spec/grammar.ebnf` — formal EBNF grammar
- **Examples:** `spec/examples/` — annotated .rdn files

### Conformance Test Suite (`test-suite/`)
All implementations must pass the shared language-agnostic test suite:
- `valid/*.rdn` + `valid/*.expected.json` — parse input and expected output pairs
- `invalid/*.rdn` — files that must cause a parse error
- `roundtrip/*.rdn` — parse → serialize → parse identity tests

Extended types in expected JSON use a tagged convention: `{"$type": "TypeName", "value": ...}` (e.g., `{"$type": "Date", "value": "2024-01-15T00:00:00.000Z"}`).

### TypeScript Implementation (`packages/rdn-js/`)
- ESM-only, strict TypeScript, zero runtime dependencies
- Entry: `src/index.ts` → exports `parse`, `stringify`, types, helpers
- Key types in `src/types.ts`: `RDNValue` (union of all value types), `RDNTimeOnly`, `RDNDuration`
- Types without native JS equivalents use tagged interfaces (`__type__: "TimeOnly"` / `__type__: "Duration"`)
- Tests via Vitest in `src/**/*.test.ts`

### Rust Implementation (`implementations/rust/`)
- `src/lib.rs` — `RdnValue` enum, `parse()` / `stringify()` API
- Optional `wasm` feature flag for wasm-bindgen
- Benchmarks via criterion (`cargo bench`)

### V8 Integration (`v8-integration/`)
The V8 fork lives at `~/v8/v8/` (external, not a submodule). Key files in the fork:
- `src/json/rdn-parser.h` / `rdn-parser.cc` — recursive-descent parser with 256-entry dispatch table
- `src/json/rdn-stringifier.cc` — serializer with SWAR string escaping
- Build: `cd ~/v8/v8 && tools/dev/gm.py x64.release`
- Run: `~/v8/v8/out/x64.release/d8 script.js`

## Key Design Decisions

### Brace Disambiguation
`{` can start an Object, Map, or Set. The parser must look ahead after the first value:
- `:` → Object
- `=>` → Map
- `,` or `}` → Set
- Empty `{}` → Object

### Parser Architecture (from spec)
- Recursive-descent, templated on char width (UTF-8 / UTF-16)
- 256-entry constexpr lookup table for O(1) first-character dispatch
- Deferred string materialization (scan first, allocate later)

### API Surface
Mirrors `JSON.parse()` / `JSON.stringify()` with `reviver` / `replacer` support:
```
RDN.parse(text [, reviver])  → RDNValue
RDN.stringify(value [, replacer])  → string | undefined
```
