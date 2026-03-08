# Changelog

## [Unreleased]

### Added

- `ParseZeroCopy(data []byte) (Value, error)` — zero-copy string parsing via `unsafe.String`; strings without escapes reference the input buffer directly
- Object key interning — repeated short keys (≤64 bytes) are deduplicated across collections
- Reusable scratch buffer for escaped string materialization
- Streaming I/O: `NewEncoder(w io.Writer)` / `NewDecoder(r io.Reader)` — mirrors `encoding/json`'s streaming API with `Encode`, `Decode`, and `SetIndent`
- `rdnhttp` sub-package — HTTP content-type negotiation and handler utilities:
  - Content negotiation: `NegotiateFormat`, `DetectContentType`, `AcceptsRDN`, `IsRDNContentType`
  - Request/response helpers: `ReadRequest`, `WriteResponse`
  - Middleware: `Negotiate`, `NegotiateFunc`, `FormatFromContext`
  - Full handler wrapper: `HandleRDN` — read → process → write with format negotiation
  - JSON fallback for the JSON-compatible subset of RDN values
  - Configurable body size limits (default 10 MB)

### Changed

- **Compact Value struct**: reduced from 224 bytes to 64 bytes (71% reduction) using `unsafe.Pointer` for collection and rare-type storage
  - `KeyValue`: 240B → 80B (67% reduction)
  - `MapEntry`: 448B → 128B (71% reduction)
- **Large payload parsing 4.3x faster**: LargeArray1K 88.6 µs → 20.4 µs, allocation bytes 784KB → 145KB (81% reduction)
- **Small/medium parsing 1.1-1.5x faster** across all payload sizes
- Encoder updated to use compact struct layout with direct `unsafe.Pointer` access

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
