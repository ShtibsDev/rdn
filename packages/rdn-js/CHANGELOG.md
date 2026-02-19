# @rdn/typescript

## 0.1.0

### Added

- **Parser** (`RDN.parse`) — full JSON-superset recursive-descent parser with 256-entry dispatch table
  - `Date` literals: ISO 8601 DateTime (`@2024-01-15T10:30:00.000Z`), date-only (`@2024-01-15`), Unix timestamps with auto seconds/milliseconds detection
  - `TimeOnly` (`@HH:MM:SS`) and `Duration` (`@P1Y2M3DT4H5M6S`) with tagged interfaces
  - `BigInt` via `n` suffix (`42n`) → native `bigint`
  - `RegExp` via `/pattern/flags` syntax → native `RegExp`
  - Binary data: base64 (`b"..."`) and hex (`x"..."`) → `Uint8Array` with strict validation
  - `Map` with brace disambiguation (`{ k => v }`) and explicit `Map{...}` syntax
  - `Set` with brace disambiguation (`{ v1, v2 }`) and explicit `Set{...}` syntax
  - Tuple syntax `(v1, v2, v3)`
  - Special numbers: `NaN`, `Infinity`, `-Infinity`
  - Reviver function support (mirrors `JSON.parse`)
  - Nesting depth limit (128) and binary size limit (100 MB)
- **Serializer** (`RDN.stringify`) — round-trip serialization for all RDN value types
  - Circular reference detection via `WeakSet`
  - Replacer function support (mirrors `JSON.stringify`)
- **Types** — `RDNValue` union type, `RDNTimeOnly` and `RDNDuration` tagged interfaces, `timeOnly()` and `duration()` factory helpers
- ESM-only, strict TypeScript, zero runtime dependencies
