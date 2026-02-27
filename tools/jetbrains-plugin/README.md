# RDN Plugin for JetBrains IDEs

First-class [RDN (Rich Data Notation)](https://github.com/ShtibsDev/rdn) language support for all IntelliJ-based IDEs — IntelliJ IDEA, WebStorm, PyCharm, Rider, GoLand, and more.

## Features

### Syntax Highlighting

Full token-level coloring for all RDN constructs including strings, numbers, booleans, dates, durations, BigInts, binary literals, regular expressions, and collection keywords. Color schemes are customizable via **Settings → Editor → Color Scheme → RDN**.

### Real-time Diagnostics & Quick Fixes

A 3-pass external annotator validates your `.rdn` files as you type:

- **Unquoted key detection** — flags bare object keys that violate the RDN spec, with quick fixes to wrap individual keys or all keys in quotes
- **Binary literal validation** — checks base64/hex character sequences
- **Full parse validation** — reports syntax errors with precise locations

### Code Completion

- **Schema completion** — suggests `$schema` at the top level
- **Keyword completion** — `true`, `false`, `null`, `NaN`, `Infinity`, `-Infinity`, `Map`, `Set`
- **Snippet templates** — 12 live templates for common patterns:
  - Date/time: `@date`, `@datetime`, `@time`, `@duration`, `@unix`
  - Collections: `Map{}`, `Set{}`, `tuple()`
  - Binary: `b""` (base64), `x""` (hex)
  - RegExp: `//`
  - BigInt: `0n`

### Hover Documentation

Rich hover popups for all RDN-specific types:

- **DateTime** — formatted display with 4 configurable format options (full, date-only, no-millis, unix)
- **TimeOnly** — custom format display
- **Duration** — parsed human-readable breakdown
- **BigInt** — optional bit-length display
- **Binary** — ASCII preview with hex representation
- **RegExp** — syntax reference
- **Special numbers** — NaN, Infinity descriptions
- **Collections** — Map, Set, Tuple type information

Each hover category can be individually toggled in settings.

### Document Formatting

- **CST-based formatter** — full-document formatting via `Ctrl+Alt+L` / `Cmd+Alt+L`
- **Prettier integration** — automatically delegates to Prettier when detected in the project
- **Sort Document Keys** — recursively sort all object keys alphabetically (`Ctrl+Alt+Shift+S`)
- Configurable indent style (tabs/spaces), explicit `Map`/`Set` keyword preservation

### Bracket Matching & Code Folding

- Matches `{}`, `[]`, `()` pairs including `Map{}` and `Set{}`
- Foldable regions for objects, arrays, tuples, maps, and sets

### Auto-Indent

Smart indentation on Enter inside brackets — increases indent after `{`, `[`, `(` and splits matching bracket pairs into properly indented lines.

### Markdown Integration

Syntax highlighting for RDN code blocks inside Markdown files (requires the Markdown plugin).

### Per-Project Settings

All preferences are stored per-project under **Settings → Languages & Frameworks → RDN**:

- Formatting options (explicit Map/Set keywords)
- Hover documentation master toggle and per-category toggles
- DateTime/TimeOnly format strings
- BigInt bit-length display
- Binary ASCII preview
- Diagnostic hints

## Requirements

- **JDK 17** — required to build
- **IntelliJ Platform 2024.3+** — compatible with builds 243 through 253

## Setup

Gradle needs a JDK 17 to build the plugin. If `java -version` doesn't resolve or points to the wrong version, set `JAVA_HOME` before running any Gradle commands:

```bash
# macOS (Homebrew)
export JAVA_HOME=$(/usr/libexec/java_home -v 17 2>/dev/null || echo /opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home)
```

You can add this to your shell profile (`~/.zshrc`) to make it permanent.

## Building

```bash
cd tools/jetbrains-plugin
./gradlew build
```

This will generate the lexer and parser from `Rdn.flex` and `Rdn.bnf`, compile all sources, run tests, and produce the plugin artifact.

## Testing

```bash
./gradlew test                            # all tests
./gradlew test --tests "*LexerTest*"      # lexer only
./gradlew test --tests "*ParserTest*"     # parser only
./gradlew test --tests "*Conformance*"    # conformance suite (shared test-suite/)
./gradlew test --tests "*Formatter*"      # formatter only
./gradlew test --tests "*Completion*"     # completion only
./gradlew test --tests "*Scanner*"        # scanner/diagnostics only
```

The conformance tests run against the shared `test-suite/` at the repository root, validating that the parser handles all valid, invalid, and roundtrip cases correctly.

## Packaging

To produce an installable `.zip` archive:

```bash
./gradlew buildPlugin
```

The output is written to `build/distributions/`. Install it in any JetBrains IDE via **Settings → Plugins → ⚙️ → Install Plugin from Disk…**

## Running a Development Sandbox

```bash
./gradlew runIde
```

Launches a sandboxed IntelliJ IDEA instance with the plugin pre-installed for manual testing.

## Publishing

Publishing to the JetBrains Marketplace requires the following environment variables:

| Variable | Purpose |
|----------|---------|
| `CERTIFICATE_CHAIN` | Plugin signing certificate chain |
| `PRIVATE_KEY` | Plugin signing private key |
| `PRIVATE_KEY_PASSWORD` | Private key password |
| `PUBLISH_TOKEN` | JetBrains Marketplace upload token |

## Regenerating Generated Sources

The lexer and parser are generated from grammar files and committed under `src/main/gen/`:

```bash
./gradlew generateLexer    # regenerate from src/main/kotlin/.../lexer/Rdn.flex
./gradlew generateParser   # regenerate from src/main/kotlin/.../parser/Rdn.bnf
```

Both tasks run automatically before compilation.
