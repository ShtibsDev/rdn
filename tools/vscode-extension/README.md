# RDN VS Code Extension

Syntax highlighting, diagnostics, and smart completions for [RDN (Rich Data Notation)](https://github.com/ShtibsDev/rdn/blob/main/spec/rdn-spec.md) files in Visual Studio Code.

## Features

### Syntax Highlighting

Full TextMate grammar for all RDN types:

- **JSON-compatible:** strings, numbers, booleans, null, arrays, objects
- **Dates & times:** `@2024-01-15T10:30:00.000Z`, `@14:30:00`, `@P1Y2M3DT4H5M6S`
- **BigInt:** `42n`, `-999n`
- **Special numbers:** `NaN`, `Infinity`, `-Infinity`
- **RegExp:** `/^test$/gi`
- **Binary data:** `b"SGVsbG8="`, `x"48656C6C6F"`
- **Map:** `Map{"a" => 1}`, `{"a" => 1}`
- **Set:** `Set{1, 2, 3}`, `{"a", "b"}`
- **Tuple:** `(1, 2, 3)`

### Full File Validation

The extension bundles the reference RDN parser and validates your `.rdn` files on every edit (with 300ms debounce), just like the built-in JSON validation. Any parse error — missing commas, unterminated strings, invalid literals, bad escape sequences, malformed dates, etc. — is shown as a red squiggle with a clear error message.

### Unquoted Key Detection + Quick Fixes

RDN requires all object keys to be quoted strings (just like JSON). A dedicated scanner finds **all** unquoted keys in the file and flags them with quick fixes:

```rdn
{foo: 1, bar: "test"}   ← "foo" and "bar" flagged as errors
{"foo": 1, "bar": "test"}  ← valid
```

The scanner correctly handles brace disambiguation — Maps (`{"a" => 1}`), Sets (`{"a", "b"}`), explicit `Map{...}` / `Set{...}`, and RDN literals (`@dates`, `/regex/`, `b"..."`) are not false-positived.

When an unquoted key is detected:

- **Wrap in quotes** — fixes a single key (`foo` → `"foo"`)
- **Wrap all unquoted keys in quotes** — fixes all unquoted keys in the file at once

### Binary Character Validation

A dedicated scanner validates the content of `b"..."` (base64) and `x"..."` (hex) binary literals:

- **Base64:** flags characters outside the base64 alphabet (`A-Z`, `a-z`, `0-9`, `+`, `/`, `=`) and data after padding
- **Hex:** flags characters outside the hex alphabet (`0-9`, `A-F`, `a-f`)

Each invalid character is highlighted individually with a red squiggle and a clear error message.

### `$schema` Completion

When typing `"` inside a top-level object, the extension suggests `"$schema"` as a completion item for adding a JSON Schema reference.

### RDN Keywords & Snippets

The extension provides two layers of completions in value positions:

**Keywords** — type the keyword and insert it directly:

| Keyword | Description |
|---------|-------------|
| `true` | Boolean true |
| `false` | Boolean false |
| `null` | Null value |
| `NaN` | IEEE 754 Not-a-Number |
| `Infinity` | Positive infinity |
| `-Infinity` | Negative infinity |
| `Map` | Map container keyword |
| `Set` | Set container keyword |
| `@` | Date/time/duration prefix |
| `b` | Base64 binary prefix |
| `x` | Hex binary prefix |

**Snippets** — type the name and expand into full RDN syntax with tab stops:

| Snippet | Expands to | Description |
|---------|------------|-------------|
| `@date` | `@2024-01-15` | ISO 8601 date |
| `@datetime` | `@2024-01-15T10:30:00.000Z` | DateTime with timezone |
| `@time` | `@14:30:00` | Time of day |
| `@duration` | `@P1Y2M3DT4H5M6S` | ISO 8601 duration |
| `@unix` | `@1704067200` | Unix epoch timestamp |
| `Map{}` | `Map{key => value}` | Map with entries |
| `Set{}` | `Set{value}` | Set with values |
| `tuple()` | `(value)` | Tuple |
| `base64 b""` | `b"SGVsbG8="` | Base64 binary data |
| `hex x""` | `x"48656C6C6F"` | Hex binary data |
| `regex //` | `/pattern/flags` | Regular expression |
| `bigint 0n` | `0n` | BigInt |

For example, typing `Map` shows both the `Map` keyword and the `Map{}` snippet — pick the keyword to just insert `Map`, or the snippet to get `Map{key => value}` with tab stops.

### Document Formatting

Built-in formatting support — use **Format Document** (`Shift+Alt+F`) or enable **Format on Save** to auto-format `.rdn` files. The formatter uses a CST-based approach that preserves RDN-specific syntax:

- Preserves tuples `(1, 2, 3)` (not collapsed to arrays)
- Preserves literal representations (dates, times, durations, regex, binary, bigints)
- Re-escapes strings with canonical escape sequences
- Smart line-breaking: keeps short collections on one line, expands long ones to multi-line with proper indentation
- Respects your editor's tab size and spaces/tabs preference
- Leaves invalid RDN files unchanged (no destructive formatting)

#### Formatting Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `rdn.useExplicitMapKeyword` | `false` | Keep the explicit `Map` keyword on non-empty maps. When `false`, `Map{ k => v }` is formatted as `{ k => v }`. Empty `Map{}` always keeps the keyword. |
| `rdn.useExplicitSetKeyword` | `false` | Keep the explicit `Set` keyword on non-empty sets. When `false`, `Set{ 1, 2 }` is formatted as `{ 1, 2 }`. Empty `Set{}` always keeps the keyword. |

### Hover Information

Hover over any RDN-specific literal to see contextual information:

- **DateTime** — formatted date with variant label (full ISO, date-only, no milliseconds)
- **Unix Timestamp** — converted date with seconds/milliseconds detection
- **TimeOnly** — formatted time display
- **Duration** — plain English expansion (e.g. `@P1Y2M3D` → "1 year, 2 months, 3 days")
- **BigInt** — comma-grouped digits with bit length
- **Binary** — byte count with optional ASCII preview; **image preview** when the data starts with recognized image magic bytes (PNG, JPEG, GIF, WebP, BMP, ICO)
- **RegExp** — expanded flag names (e.g. `gi` → "global, ignoreCase")
- **Special Numbers** — IEEE 754 description for `NaN`, `Infinity`, `-Infinity`
- **Collections** — element/entry count for Map, Set, and Tuple (both explicit and implicit)
- **`=>`** — Map entry separator hint

**Hover diagnostics:**
- Hex binary with odd digit count
- BigInt values that fit in a regular Number
- Ambiguous 10-digit Unix timestamps (seconds vs milliseconds)

**Editor diagnostics** (red squiggles):
- Invalid characters in base64 binary literals
- Invalid characters in hex binary literals

#### Hover Settings

All hover features are individually toggleable:

| Setting | Default | Description |
|---------|---------|-------------|
| `rdn.hover.enabled` | `true` | Master toggle for all hover information |
| `rdn.hover.dateTime.enabled` | `true` | Show hover for DateTime/date-only/Unix literals |
| `rdn.hover.dateTime.fullFormat` | `YYYY-MM-DD HH:mm:ss.SSS [UTC]` | Format for full ISO DateTime values |
| `rdn.hover.dateTime.dateOnlyFormat` | `MMMM D, YYYY` | Format for date-only values |
| `rdn.hover.dateTime.noMillisFormat` | `YYYY-MM-DD HH:mm:ss [UTC]` | Format for DateTime without milliseconds |
| `rdn.hover.dateTime.unixFormat` | `YYYY-MM-DD HH:mm:ss [UTC]` | Format for Unix timestamp display |
| `rdn.hover.timeOnly.enabled` | `true` | Show hover for TimeOnly literals |
| `rdn.hover.timeOnly.format` | `HH:mm:ss` | Format for TimeOnly display |
| `rdn.hover.duration.enabled` | `true` | Show hover for Duration literals |
| `rdn.hover.bigint.enabled` | `true` | Show hover for BigInt literals |
| `rdn.hover.bigint.showBitLength` | `true` | Show bit length in BigInt hover |
| `rdn.hover.binary.enabled` | `true` | Show hover for binary literals |
| `rdn.hover.binary.showPreview` | `true` | Show ASCII preview of binary data |
| `rdn.hover.regexp.enabled` | `true` | Show hover for RegExp literals |
| `rdn.hover.specialNumbers.enabled` | `true` | Show hover for NaN/Infinity/-Infinity |
| `rdn.hover.collections.enabled` | `true` | Show hover for Map/Set/Tuple |
| `rdn.hover.diagnostics.enabled` | `true` | Show diagnostic hints in hover |

Date format strings support the following tokens: `YYYY`, `YY`, `MMMM`, `MMM`, `MM`, `M`, `DD`, `D`, `HH`, `H`, `hh`, `h`, `mm`, `m`, `ss`, `s`, `SSS`, `SS`, `S`, `A`, `a`. Use `[brackets]` for literal text.

### Other

- Bracket matching and auto-closing for `{}`, `[]`, `()`
- Auto-closing for double quotes
- Indentation support for bracket blocks
- Invalid escape sequence highlighting
- Object key vs. value distinction (keys get `support.type.property-name` scope)

## Installation

### From source (development)

1. Open `tools/vscode-extension/` in VS Code
2. Run `npm install` and `npm run compile`
3. Press `F5` to launch the Extension Development Host
4. Open any `.rdn` file to see all features in action

### Manual install

```bash
cd tools/vscode-extension
npm install
npm run package
code --install-extension rdn-0.2.0.vsix
```

## Scope Reference

Use `Developer: Inspect Editor Tokens and Scopes` in VS Code to verify assignments.

| Token | Scope |
|-------|-------|
| `null` | `constant.language.null.rdn` |
| `true`, `false` | `constant.language.boolean.true/false.rdn` |
| `42` | `constant.numeric.integer.rdn` |
| `3.14`, `1e10` | `constant.numeric.float.rdn` |
| `42n` | `constant.numeric.bigint.rdn` |
| `NaN` | `constant.numeric.nan.rdn` |
| `Infinity`, `-Infinity` | `constant.numeric.infinity.rdn` |
| `"hello"` | `string.quoted.double.rdn` |
| `\n`, `\uFFFF` | `constant.character.escape.rdn` |
| `\q` | `invalid.illegal.unrecognized-escape.rdn` |
| `"key":` | `support.type.property-name.rdn` |
| `@2024-01-15` | `constant.other.date.rdn` |
| `@14:30:00` | `constant.other.time.rdn` |
| `@P1Y2M3D` | `constant.other.duration.rdn` |
| `/pattern/gi` | `string.regexp.rdn` |
| `b"..."` | `string.other.binary.base64.rdn` |
| `x"..."` | `string.other.binary.hex.rdn` |
| `Map` | `keyword.other.map.rdn` |
| `Set` | `keyword.other.set.rdn` |
| `=>` | `punctuation.separator.arrow.rdn` |
| `,` | `punctuation.separator.comma.rdn` |
| `:` | `punctuation.separator.colon.rdn` |

## Limitations

- **Implicit Map/Set vs Object disambiguation** is not possible at the TextMate grammar level. Implicit maps (`{"a" => 1}`) and sets (`{"a", "b"}`) are parsed as brace blocks — the `=>`, `:`, and `,` punctuation conveys the type visually.
- **RegExp body** is highlighted as a single token. Sub-pattern highlighting (character classes, quantifiers, etc.) is not provided.
- **Unquoted key detection** uses a lightweight scanner separate from the parser. Edge cases in deeply malformed documents may not be caught by the scanner, but the parser will still flag them.
- **Parser errors are one-at-a-time** — like JSON validation, the parser stops at the first error. Fix it and the next error (if any) will appear.
