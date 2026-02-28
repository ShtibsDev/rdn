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
cd packages/rdn-rust
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
cd packages/rdn-go && go test
cd packages/rdn-python && pip install -e . && pytest
```

### JetBrains Plugin (Gradle + Kotlin)
```bash
cd tools/jetbrains-plugin
./gradlew build              # build the plugin
./gradlew test               # run all tests (lexer, parser, conformance, formatter, etc.)
./gradlew buildPlugin        # produce installable .zip in build/distributions/
./gradlew runIde             # launch a sandboxed IntelliJ IDEA with the plugin installed
./gradlew check              # full quality check (build + test + verify)
./gradlew generateLexer      # regenerate JFlex lexer from Rdn.flex
./gradlew generateParser     # regenerate GrammarKit parser from Rdn.bnf

# Run specific test suites:
./gradlew test --tests "*LexerTest*"      # lexer tests only
./gradlew test --tests "*ParserTest*"     # parser tests only
./gradlew test --tests "*Conformance*"    # conformance tests against test-suite/
./gradlew test --tests "*Formatter*"      # formatter tests only
./gradlew test --tests "*Scanner*"        # scanner tests only
./gradlew test --tests "*Completion*"     # completion tests only
```

The plugin is located at `tools/jetbrains-plugin/`. The conformance tests reference `test-suite/` via a relative path configured in `build.gradle.kts`.

### Release Scripts (`scripts/release/`, run with `bun`)
```bash
# Generate changelog for a package from conventional commits
pnpm changelog -- --package <name> [--version <ver>] [--from <ref>] [--to <ref>] [--stdout] [--dry-run]

# Bump version across all version files for a package
pnpm bump-version -- --package <name> --bump <patch|minor|major|prerelease> [--preid <alpha|beta|rc>] [--dry-run]

# Full release: bump + changelog + commit + tag + push
pnpm release-package -- --package <name> --bump <patch|minor|major|prerelease> [--preid <alpha|beta|rc>] [--dry-run] [--no-push]

# Regenerate all changelogs from scratch
pnpm regenerate-changelogs [-- --dry-run]
```

Package names: `@rdn/typescript`, `rdn-dotnet`, `rdn-vscode`, `prettier-plugin-rdn`, `rdn-jetbrains`, `rdn-rust`, `rdn-python`, `rdn-go`.

The package registry (`scripts/release/packages.ts`) is the single source of truth mapping each package to its scopes, directory, tag prefix, version files, and ecosystem.

Changesets continues to handle npm publishing for `@rdn/typescript` and `prettier-plugin-rdn`. The release scripts handle changelog generation and git tagging for all packages. Non-npm packages (VS Code, JetBrains, NuGet, Rust, Python, Go) use tag-triggered CI workflows.

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

### Rust Implementation (`packages/rdn-rust/`)
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
