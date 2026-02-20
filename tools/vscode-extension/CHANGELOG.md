# RDN (VS Code Extension)

## 0.2.1

### Fixed

- Add missing activationEvents to satisfy vsce packaging

## 0.2.0

### Added

- **Syntax highlighting** — full TextMate grammar for all RDN token types, including Markdown code fence injection
- **Diagnostics** — real-time parse validation powered by the reference RDN parser, unquoted key detection, and binary character validation
- **Quick fixes** — "Wrap in quotes" for single or all unquoted keys
- **Hover information** (individually toggleable):
  - DateTime with configurable format strings and Unix timestamp decoding
  - TimeOnly formatted display
  - Duration plain English expansion (e.g. `@P1Y2M3D` → "1 year, 2 months, 3 days")
  - BigInt comma-grouped digits with bit length
  - Binary byte count, ASCII preview, and inline image preview for known formats
  - RegExp expanded flag names
  - Special numbers IEEE 754 descriptions
  - Collection element/entry counts for Map, Set, Tuple
  - Diagnostic hints (odd hex digits, BigInt fits in Number, ambiguous Unix timestamps)
- **Completions** — `$schema` key suggestion, RDN keyword completions, and snippets with tab stops for all RDN types
- **Document formatting** — CST-based formatter with smart line-breaking, respects editor tab/space preferences
- **Commands** — `RDN: Sort Document Keys`
- **Markdown preview** — scripts and styles for RDN code blocks in VS Code Markdown preview
