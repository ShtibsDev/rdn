# Changelog

## [0.1.0] - 2026-02-28

### Added

- Core `Parse` / `Stringify` / `StringifyIndent` / `Valid` API
- `Value` union-style struct with constructors and accessors for all 15 RDN types
- Recursive-descent parser with 256-entry dispatch table
- Deferred string materialization with full Unicode escape and surrogate pair support
- Smi-first number parsing for integers up to 15 digits
- BigInt support with `n` suffix (stored as digit string)
- DateTime parsing: full ISO 8601 (`@YYYY-MM-DDTHH:MM:SS.mmmZ`), date-only, and Unix timestamps
- TimeOnly (`@HH:MM:SS.mmm`) and Duration (`@P...`) support
- RegExp (`/pattern/flags`) with flag validation
- Binary data: base64 (`b"..."`) and hex (`x"..."`) with inline decoding
- Brace disambiguation: `{` → Object, Map, or Set based on separator lookahead
- Explicit `Map{...}` and `Set{...}` syntax
- Tuple `(...)` support
- `SyntaxError` with byte offset
- Custom types: `TimeOnly`, `Duration`, `RegExp`, `Number`, `RawMessage`
- `Value.Equal()` deep equality (NaN == NaN)
- Pre-computed lookup tables: token dispatch, base64/hex decode, escape sequences, digit pairs
- sync.Pool buffer reuse for serializer
- Full conformance test suite (valid, invalid, roundtrip)
- Unit tests for parser, serializer, values, and types
- Benchmarks across 5 payload categories
