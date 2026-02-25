# Tech Design: JetBrains RDN Plugin

## 1. Overview

Build a JetBrains IDE plugin that provides first-class RDN (Rich Data Notation) support across all IntelliJ-based IDEs (IntelliJ IDEA, WebStorm, PyCharm, CLion, GoLand, Rider, PhpStorm, RubyMine, DataGrip, RustRover). The plugin achieves full feature parity with the existing VSCode extension using **Approach A: Full Custom** -- a JFlex lexer, GrammarKit parser, full PSI tree, and native Kotlin implementations of all features.

The plugin lives at `tools/jetbrains-plugin/` in the monorepo and targets IntelliJ Platform 2024.3+ (build 243).

## 2. As-Is Behavior (from discovery)

The discovery document (`docs/features/jetbrains-plugin/discovery.md`) evaluated three architectural approaches. Key findings:

- The VSCode extension provides 15 features across 8 source files (~2,600 lines of TypeScript).
- Two parsers are used: `@rdn/parser` (value parser for diagnostics) and `@rdn/cst-parser` (CST parser for formatting).
- The TextMate grammar (`rdn.tmLanguage.json`) covers 30+ scope rules including 12 RegExp sub-patterns.
- A lightweight scanner (`scanner.ts`) handles unquoted key detection with brace disambiguation and binary character validation.
- Hover information covers 18 token kinds with rich content (image preview, date formatting, byte size, regex flag expansion).
- 13 configurable settings control hover behavior and formatting preferences.
- Approach A (Full Custom) was selected for 100% feature coverage and maximum IDE integration depth.

## 3. To-Be Behavior

### 3.1 File Type Recognition

When a user opens a `.rdn` file, the IDE:
- Assigns the RDN file icon (16x16 SVG) in the project tree, editor tabs, and file choosers.
- Activates RDN-specific syntax highlighting, completions, diagnostics, and formatting.
- Shows "RDN" in the status bar language indicator.

### 3.2 Syntax Highlighting

All 20+ RDN token types are highlighted with distinct colors configurable via **Settings > Editor > Color Scheme > RDN**:

| Token Category | Examples | Default Color Mapping |
|---|---|---|
| Keywords | `null`, `true`, `false` | `KEYWORD` |
| Numbers | `42`, `3.14`, `1e10` | `NUMBER` |
| BigInts | `42n`, `-999n` | `NUMBER` (italic) |
| Strings | `"hello"` | `STRING` |
| Object Keys | `"key":` | `INSTANCE_FIELD` |
| Date/Time | `@2024-01-15`, `@14:30:00` | `METADATA` (date), `NUMBER` (time components) |
| Duration | `@P1Y2M3D` | `METADATA` (P), `NUMBER` (digits), `KEYWORD` (units) |
| Binary | `b"SGVsbG8="`, `x"48656C6C6F"` | `KEYWORD` (prefix), `STRING` (content) |
| RegExp | `/pattern/flags` | `STRING` (body), sub-patterns highlighted per kind |
| Special Numbers | `NaN`, `Infinity`, `-Infinity` | `NUMBER` |
| Map/Set Keywords | `Map{`, `Set{` | `KEYWORD` |
| Punctuation | `{`, `}`, `[`, `]`, `(`, `)`, `,`, `:`, `=>` | `BRACES`, `COMMA`, `SEMICOLON` |
| Invalid chars | Bad base64/hex chars, bad escape sequences | `BAD_CHARACTER` |

RegExp sub-patterns are highlighted individually:
- Character classes (`\d`, `\w`, `.`), escapes (`\n`, `\t`), anchors (`^`, `$`), quantifiers (`+`, `*`, `?`, `{n,m}`), groups (`(...)`, `(?:...)`, `(?=...)`, `(?<name>...)`), alternation (`|`), backreferences (`\1`, `\k<name>`), character class sets (`[a-z]`), and negation (`[^...]`).

### 3.3 Real-Time Diagnostics

As the user types (300ms debounce), three diagnostic passes run:

1. **Unquoted key detection** -- Bare identifiers in object-key position are flagged as errors with the message: `Unquoted key "foo" -- RDN requires all object keys to be quoted strings`.
2. **Binary character validation** -- Invalid characters inside `b"..."` and `x"..."` literals are individually flagged.
3. **Full parse validation** -- The Kotlin RDN parser runs and reports syntax errors. Parse errors on lines already covered by unquoted key diagnostics are suppressed to avoid duplicate reporting.

All diagnostics appear as red squiggly underlines with error severity.

### 3.4 Quick Fixes

When the cursor is on an unquoted key diagnostic:
- **Wrap "key" in quotes** -- Single-key fix that replaces `key` with `"key"`.
- **Wrap all unquoted keys in quotes** -- Bulk fix (appears when there are 2+ unquoted keys) that fixes all at once.

Quick fixes appear in the Alt+Enter intention menu and the gutter light bulb.

### 3.5 Completions

Three completion sources:

1. **`$schema` completion** -- When typing inside a top-level object (brace depth 1) in key position, offer `$schema` with a snippet that inserts `"$schema": ""` with the cursor inside the URL quotes. Only offered when `$schema` does not already exist in the document.

2. **Keyword completions** (11 keywords) -- `true`, `false`, `null`, `NaN`, `Infinity`, `-Infinity`, `Map`, `Set`, `@`, `b`, `x`. Each has a detail label and documentation string. Not offered when the cursor is inside a string.

3. **Snippet completions** (12 snippets) -- `@date`, `@datetime`, `@time`, `@duration`, `@unix`, `Map{}`, `Set{}`, `tuple()`, `base64 b""`, `hex x""`, `regex //`, `bigint 0n`. Each inserts a template with tab stops via Live Templates. Not offered when the cursor is inside a string.

### 3.6 Hover / Documentation

Hovering over an RDN-specific token shows a documentation popup with rich content:

| Token Kind | Hover Content |
|---|---|
| DateTime (full) | **DateTime** _(full ISO 8601)_ + formatted date string |
| DateTime (no millis) | **DateTime** _(no milliseconds)_ + formatted date string |
| Date only | **DateTime** _(date only)_ + formatted date string |
| Unix timestamp | **Unix Timestamp** _(seconds/milliseconds)_ + formatted date; ambiguity hint for 10-digit values |
| TimeOnly | **TimeOnly** + formatted time string |
| Duration | **Duration** + expanded English (e.g., "1 year, 2 months") |
| BigInt | **BigInt** + grouped digits + bit length |
| Base64 binary | **Base64 Binary** + byte size + ASCII preview or image preview (PNG/JPEG/GIF/WebP/BMP/ICO detection) |
| Hex binary | **Hex Binary** + byte size + ASCII preview or image preview; odd-digits warning |
| RegExp | **RegExp** + expanded flag names |
| NaN / Infinity / -Infinity | IEEE 754 explanation |
| Map/Set keyword | **Map**/**Set** + element/entry count |
| Map arrow `=>` | **=>** + explanation |
| Tuple `(` | **Tuple** + element count |
| Implicit Map/Set `{` | **Map**/**Set** _(implicit)_ + element count |

All hover categories are individually toggleable via settings. Date formats are configurable.

### 3.7 Document Formatting

**Format Document** (Ctrl+Alt+L / Cmd+Opt+L) reformats the entire RDN document using CST-based formatting:
- Attempts compact single-line rendering for each node.
- Expands to multi-line when a line exceeds 80 characters.
- Uses the editor's configured tab size and spaces/tabs preference.
- Respects `useExplicitMapKeyword` and `useExplicitSetKeyword` settings.
- Returns the original text unchanged if parsing fails (no destructive formatting on invalid documents).

**Prettier fallback:** If a `.prettierrc` or `prettier.config.js` is detected in the project and the `prettier-plugin-rdn` package is installed, the plugin defers formatting to Prettier via the IDE's external formatter integration.

### 3.8 Sort Document Keys

**RDN: Sort Document Keys** action (available in the Command Palette when a `.rdn` file is active) recursively sorts all object keys alphabetically and reformats the document.

### 3.9 Bracket Matching

Bracket pairs `{}`, `[]`, `()` are matched and highlighted. The `Map{` and `Set{` prefixes are handled so that the opening `{` after `Map`/`Set` matches the closing `}`.

### 3.10 Code Folding

Objects, arrays, tuples, maps, and sets can be folded. The fold region spans from the opening bracket to the closing bracket. The placeholder text shows the type and element count (e.g., `{...3 properties}`, `[...5 elements]`).

### 3.11 Markdown Injection

RDN code blocks in Markdown files (`` ```rdn ``) are syntax highlighted using language injection. The `MultiHostInjector` detects fenced code blocks with the `rdn` language identifier and injects the RDN language.

### 3.12 Settings

All 13+ settings are accessible via **Settings > Languages & Frameworks > RDN** and are organized into groups. Changes take effect immediately without IDE restart. See Section 9 for the full schema.

### 3.13 Color Settings Page

**Settings > Editor > Color Scheme > RDN** provides a preview pane with all token types visible and individually configurable foreground/background colors, bold, italic, and underline attributes.

## 4. Design Decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | **JFlex lexer with 5 states** (YYINITIAL, STRING, REGEXP, REGEXP_CHAR_CLASS, BINARY) | A single lexer with multiple states is simpler to maintain than multiple lexers. Binary base64 and hex share the same BINARY state with a flag distinguishing the encoding. RegExp highlighting requires two dedicated states for the body and character class interiors. |
| 2 | **GrammarKit BNF with brace disambiguation** | The `parseBrace` rule parses the first value, then dispatches on the separator (`:` for Object, `=>` for Map, `,`/`}` for Set). GrammarKit's PEG-style ordered choice handles this naturally. |
| 3 | **PSI tree mirrors CST node types** | PSI elements map 1:1 to the 17 CST node types (StringLiteral, NumberLiteral, BigIntLiteral, BooleanLiteral, NullLiteral, NaNLiteral, InfinityLiteral, DateTimeLiteral, TimeOnlyLiteral, DurationLiteral, BinaryLiteral, RegExpLiteral, Array, Tuple, Object, Map, Set). This makes formatter and hover logic straightforward. |
| 4 | **JFlex brace disambiguation via lexer state tracking** | The lexer does NOT attempt brace disambiguation -- it emits a generic `LBRACE` token. The parser handles disambiguation by looking ahead after the first value. This keeps the lexer context-free and the parser responsible for semantic decisions. |
| 5 | **RegExp sub-pattern highlighting via nested JFlex states** | On encountering `/` in YYINITIAL, the lexer transitions to REGEXP state. Inside REGEXP, encountering `[` transitions to REGEXP_CHAR_CLASS. Each state emits granular tokens (quantifier, anchor, escape, character class, etc.) matching the TextMate scopes. |
| 6 | **Formatter: Kotlin CST formatter with Prettier detection** | The formatter is a direct Kotlin port of `formatter.ts`. Before formatting, it checks for Prettier configuration in the project. If found and prettier-plugin-rdn is available, it delegates to Prettier via `ProcessBuilder`. Otherwise, it uses the built-in CST formatter. |
| 7 | **Settings via `PersistentStateComponent` with XML serialization** | IntelliJ's standard persistence mechanism. Settings are project-level (stored in `.idea/rdn.xml`). A `Configurable` UI panel provides the settings editor with grouped sections matching the VSCode configuration layout. |
| 8 | **Markdown injection via `MultiHostInjector`** | Detects `` ```rdn `` fenced code blocks in Markdown files and injects the RDN language for syntax highlighting and diagnostics within the block. |
| 9 | **Conformance tests reference `test-suite/` via relative Gradle path** | The Gradle build uses `rootProject.projectDir.resolve("../../test-suite")` to locate test files. No file copying. Tests run as part of `./gradlew test`. |
| 10 | **TextMate grammars moved to `spec/textmate/`** | Both the VSCode extension and JetBrains plugin reference grammars from `spec/textmate/rdn.tmLanguage.json` and `spec/textmate/rdn.markdown.tmLanguage.json`. The VSCode `package.json` updates its grammar paths. The JetBrains plugin does not use TextMate grammars (it has a JFlex lexer) but the shared location benefits future consumers. |
| 11 | **Kotlin parser as a standalone module** | The parser lives in `src/main/kotlin/com/rdn/intellij/parser/` but is designed with clean interfaces so it can later be extracted to a standalone Maven Central artifact. The parser produces both PSI trees (for IDE integration) and can produce CST nodes (for formatting). |
| 12 | **`ExternalAnnotator` for diagnostics, not `Annotator`** | `ExternalAnnotator` runs on a background thread, avoiding UI freezes on large files. It receives a snapshot of the document text and produces annotations asynchronously. |
| 13 | **Icon generation from SVG** | The existing `assets/rdn-icon.svg` is processed to create 16x16 and 13x13 SVG variants for the file type icon. Plugin icon (40x40) is generated for Marketplace listing. |

## 5. Interfaces & Models (Kotlin)

### 5.1 RDN Value Types (Full-Fidelity Parser)

```kotlin
package com.rdn.intellij.parser.model

import java.math.BigInteger
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.Duration as JavaDuration

/**
 * Tagged interface for all RDN values produced by the parser.
 * Used for diagnostics validation (parse succeeds or throws).
 */
sealed interface RdnValue

data object RdnNull : RdnValue
data class RdnBoolean(val value: Boolean) : RdnValue
data class RdnNumber(val value: Double) : RdnValue
data class RdnBigInt(val value: BigInteger) : RdnValue
data class RdnString(val value: String) : RdnValue
data class RdnDateTime(val instant: Instant) : RdnValue
data class RdnDateOnly(val date: LocalDate) : RdnValue
data class RdnTimeOnly(val hours: Int, val minutes: Int, val seconds: Int, val milliseconds: Int) : RdnValue
data class RdnDuration(val iso: String) : RdnValue
data class RdnRegExp(val pattern: String, val flags: String) : RdnValue
data class RdnBinaryBase64(val data: ByteArray) : RdnValue {
    override fun equals(other: Any?) = other is RdnBinaryBase64 && data.contentEquals(other.data)
    override fun hashCode() = data.contentHashCode()
}
data class RdnBinaryHex(val data: ByteArray) : RdnValue {
    override fun equals(other: Any?) = other is RdnBinaryHex && data.contentEquals(other.data)
    override fun hashCode() = data.contentHashCode()
}
data class RdnNaN(val dummy: Unit = Unit) : RdnValue
data class RdnInfinity(val negative: Boolean) : RdnValue
data class RdnArray(val elements: List<RdnValue>) : RdnValue
data class RdnTuple(val elements: List<RdnValue>) : RdnValue
data class RdnObject(val properties: List<Pair<String, RdnValue>>) : RdnValue
data class RdnMap(val entries: List<Pair<RdnValue, RdnValue>>, val explicit: Boolean) : RdnValue
data class RdnSet(val elements: List<RdnValue>, val explicit: Boolean) : RdnValue
```

### 5.2 Token Types for the JFlex Lexer

```kotlin
package com.rdn.intellij.lexer

import com.intellij.psi.tree.IElementType
import com.rdn.intellij.RdnLanguage

class RdnTokenType(debugName: String) : IElementType(debugName, RdnLanguage)

object RdnTokenTypes {
    // Structural
    @JvmField val LBRACE = RdnTokenType("LBRACE")            // {
    @JvmField val RBRACE = RdnTokenType("RBRACE")            // }
    @JvmField val LBRACKET = RdnTokenType("LBRACKET")        // [
    @JvmField val RBRACKET = RdnTokenType("RBRACKET")        // ]
    @JvmField val LPAREN = RdnTokenType("LPAREN")            // (
    @JvmField val RPAREN = RdnTokenType("RPAREN")            // )
    @JvmField val COLON = RdnTokenType("COLON")              // :
    @JvmField val COMMA = RdnTokenType("COMMA")              // ,
    @JvmField val ARROW = RdnTokenType("ARROW")              // =>

    // Literals
    @JvmField val NULL = RdnTokenType("NULL")
    @JvmField val TRUE = RdnTokenType("TRUE")
    @JvmField val FALSE = RdnTokenType("FALSE")
    @JvmField val INTEGER = RdnTokenType("INTEGER")
    @JvmField val FLOAT = RdnTokenType("FLOAT")
    @JvmField val BIGINT = RdnTokenType("BIGINT")
    @JvmField val NAN = RdnTokenType("NAN")
    @JvmField val INFINITY = RdnTokenType("INFINITY")
    @JvmField val NEG_INFINITY = RdnTokenType("NEG_INFINITY")

    // Strings
    @JvmField val STRING_OPEN = RdnTokenType("STRING_OPEN")                   // opening "
    @JvmField val STRING_CONTENT = RdnTokenType("STRING_CONTENT")             // text between quotes
    @JvmField val STRING_ESCAPE = RdnTokenType("STRING_ESCAPE")               // \n, \t, \uXXXX, etc.
    @JvmField val STRING_INVALID_ESCAPE = RdnTokenType("STRING_INVALID_ESCAPE") // bad escape like \q
    @JvmField val STRING_CLOSE = RdnTokenType("STRING_CLOSE")                 // closing "

    // Object keys (same structure as strings but with distinct types for highlighting)
    @JvmField val KEY_OPEN = RdnTokenType("KEY_OPEN")
    @JvmField val KEY_CONTENT = RdnTokenType("KEY_CONTENT")
    @JvmField val KEY_ESCAPE = RdnTokenType("KEY_ESCAPE")
    @JvmField val KEY_CLOSE = RdnTokenType("KEY_CLOSE")

    // Date/Time
    @JvmField val AT_SIGN = RdnTokenType("AT_SIGN")                           // @
    @JvmField val DATE_PART = RdnTokenType("DATE_PART")                       // 2024-01-15
    @JvmField val TIME_SEPARATOR = RdnTokenType("TIME_SEPARATOR")             // T
    @JvmField val TIME_PART = RdnTokenType("TIME_PART")                       // 10:30:00
    @JvmField val MILLIS_PART = RdnTokenType("MILLIS_PART")                   // .000
    @JvmField val TIMEZONE = RdnTokenType("TIMEZONE")                         // Z
    @JvmField val UNIX_TIMESTAMP = RdnTokenType("UNIX_TIMESTAMP")             // digits after @

    // Duration
    @JvmField val DURATION_P = RdnTokenType("DURATION_P")                     // P
    @JvmField val DURATION_NUMBER = RdnTokenType("DURATION_NUMBER")           // digit groups
    @JvmField val DURATION_UNIT = RdnTokenType("DURATION_UNIT")               // Y, M, D, H, S
    @JvmField val DURATION_T = RdnTokenType("DURATION_T")                     // T (time separator in duration)

    // Binary
    @JvmField val BINARY_PREFIX = RdnTokenType("BINARY_PREFIX")               // b or x
    @JvmField val BINARY_OPEN = RdnTokenType("BINARY_OPEN")                   // opening "
    @JvmField val BINARY_CONTENT = RdnTokenType("BINARY_CONTENT")             // valid base64/hex chars
    @JvmField val BINARY_INVALID_CHAR = RdnTokenType("BINARY_INVALID_CHAR")   // invalid char in binary
    @JvmField val BINARY_CLOSE = RdnTokenType("BINARY_CLOSE")                 // closing "

    // Map/Set keywords
    @JvmField val MAP_KEYWORD = RdnTokenType("MAP_KEYWORD")                   // Map (before {)
    @JvmField val SET_KEYWORD = RdnTokenType("SET_KEYWORD")                   // Set (before {)

    // RegExp tokens (emitted in REGEXP lexer state)
    @JvmField val REGEXP_OPEN = RdnTokenType("REGEXP_OPEN")                   // opening /
    @JvmField val REGEXP_CLOSE = RdnTokenType("REGEXP_CLOSE")                 // closing /
    @JvmField val REGEXP_FLAGS = RdnTokenType("REGEXP_FLAGS")                 // dgimsuvy
    @JvmField val REGEXP_CONTENT = RdnTokenType("REGEXP_CONTENT")             // plain regex body text
    @JvmField val REGEXP_ESCAPE = RdnTokenType("REGEXP_ESCAPE")               // \n, \t, etc.
    @JvmField val REGEXP_CHAR_CLASS_ESCAPE = RdnTokenType("REGEXP_CHAR_CLASS_ESCAPE") // \d, \w, \s, etc.
    @JvmField val REGEXP_QUANTIFIER = RdnTokenType("REGEXP_QUANTIFIER")       // +, *, ?, {n,m}
    @JvmField val REGEXP_ANCHOR = RdnTokenType("REGEXP_ANCHOR")               // ^, $
    @JvmField val REGEXP_ALTERNATION = RdnTokenType("REGEXP_ALTERNATION")     // |
    @JvmField val REGEXP_DOT = RdnTokenType("REGEXP_DOT")                     // .
    @JvmField val REGEXP_GROUP_OPEN = RdnTokenType("REGEXP_GROUP_OPEN")       // (
    @JvmField val REGEXP_GROUP_CLOSE = RdnTokenType("REGEXP_GROUP_CLOSE")     // )
    @JvmField val REGEXP_LOOKAROUND = RdnTokenType("REGEXP_LOOKAROUND")       // ?=, ?!, ?<=, ?<!
    @JvmField val REGEXP_NAMED_GROUP = RdnTokenType("REGEXP_NAMED_GROUP")     // ?<name>
    @JvmField val REGEXP_NON_CAPTURING = RdnTokenType("REGEXP_NON_CAPTURING") // ?:
    @JvmField val REGEXP_BACKREFERENCE = RdnTokenType("REGEXP_BACKREFERENCE") // \1, \k<name>
    @JvmField val REGEXP_CHAR_CLASS_OPEN = RdnTokenType("REGEXP_CHAR_CLASS_OPEN")   // [
    @JvmField val REGEXP_CHAR_CLASS_CLOSE = RdnTokenType("REGEXP_CHAR_CLASS_CLOSE") // ]
    @JvmField val REGEXP_NEGATION = RdnTokenType("REGEXP_NEGATION")           // ^ inside [...]
    @JvmField val REGEXP_RANGE = RdnTokenType("REGEXP_RANGE")                 // - inside [...]

    // Special
    @JvmField val WHITE_SPACE = com.intellij.psi.TokenType.WHITE_SPACE
    @JvmField val BAD_CHARACTER = com.intellij.psi.TokenType.BAD_CHARACTER
}
```

### 5.3 PSI Element Types

```kotlin
package com.rdn.intellij.psi

import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.IFileElementType
import com.rdn.intellij.RdnLanguage

class RdnElementType(debugName: String) : IElementType(debugName, RdnLanguage)

object RdnElementTypes {
    @JvmField val FILE = IFileElementType(RdnLanguage)

    // Value nodes
    @JvmField val STRING_LITERAL = RdnElementType("STRING_LITERAL")
    @JvmField val NUMBER_LITERAL = RdnElementType("NUMBER_LITERAL")
    @JvmField val BIGINT_LITERAL = RdnElementType("BIGINT_LITERAL")
    @JvmField val BOOLEAN_LITERAL = RdnElementType("BOOLEAN_LITERAL")
    @JvmField val NULL_LITERAL = RdnElementType("NULL_LITERAL")
    @JvmField val NAN_LITERAL = RdnElementType("NAN_LITERAL")
    @JvmField val INFINITY_LITERAL = RdnElementType("INFINITY_LITERAL")
    @JvmField val DATETIME_LITERAL = RdnElementType("DATETIME_LITERAL")
    @JvmField val TIME_ONLY_LITERAL = RdnElementType("TIME_ONLY_LITERAL")
    @JvmField val DURATION_LITERAL = RdnElementType("DURATION_LITERAL")
    @JvmField val BINARY_LITERAL = RdnElementType("BINARY_LITERAL")
    @JvmField val REGEXP_LITERAL = RdnElementType("REGEXP_LITERAL")

    // Collection nodes
    @JvmField val ARRAY = RdnElementType("ARRAY")
    @JvmField val TUPLE = RdnElementType("TUPLE")
    @JvmField val OBJECT = RdnElementType("OBJECT")
    @JvmField val OBJECT_PROPERTY = RdnElementType("OBJECT_PROPERTY")
    @JvmField val MAP = RdnElementType("MAP")
    @JvmField val MAP_ENTRY = RdnElementType("MAP_ENTRY")
    @JvmField val SET = RdnElementType("SET")

    // Key nodes
    @JvmField val OBJECT_KEY = RdnElementType("OBJECT_KEY")
}
```

### 5.4 Settings State

```kotlin
package com.rdn.intellij.settings

import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.project.Project
import com.intellij.util.xmlb.XmlSerializerUtil

@Service(Service.Level.PROJECT)
@State(name = "RdnSettings", storages = [Storage("rdn.xml")])
class RdnSettingsState : PersistentStateComponent<RdnSettingsState> {
    // Formatting
    var useExplicitMapKeyword: Boolean = false
    var useExplicitSetKeyword: Boolean = false

    // Hover: master toggle
    var hoverEnabled: Boolean = true

    // Hover: category toggles
    var hoverDateTimeEnabled: Boolean = true
    var hoverTimeOnlyEnabled: Boolean = true
    var hoverDurationEnabled: Boolean = true
    var hoverBigintEnabled: Boolean = true
    var hoverBinaryEnabled: Boolean = true
    var hoverRegexpEnabled: Boolean = true
    var hoverSpecialNumbersEnabled: Boolean = true
    var hoverCollectionsEnabled: Boolean = true
    var hoverDiagnosticsEnabled: Boolean = true

    // Hover: format strings
    var hoverDateTimeFullFormat: String = "YYYY-MM-DD HH:mm:ss.SSS [UTC]"
    var hoverDateTimeDateOnlyFormat: String = "MMMM D, YYYY"
    var hoverDateTimeNoMillisFormat: String = "YYYY-MM-DD HH:mm:ss [UTC]"
    var hoverDateTimeUnixFormat: String = "YYYY-MM-DD HH:mm:ss [UTC]"
    var hoverTimeOnlyFormat: String = "HH:mm:ss"

    // Hover: detail toggles
    var hoverBigintShowBitLength: Boolean = true
    var hoverBinaryShowPreview: Boolean = true

    override fun getState(): RdnSettingsState = this

    override fun loadState(state: RdnSettingsState) {
        XmlSerializerUtil.copyBean(state, this)
    }

    companion object {
        fun getInstance(project: Project): RdnSettingsState =
            project.getService(RdnSettingsState::class.java)
    }
}
```

### 5.5 CST Nodes for the Formatter

```kotlin
package com.rdn.intellij.formatter.cst

/**
 * Base for all CST nodes. Every node carries source positions.
 */
sealed interface RdnCstNode {
    val start: Int
    val end: Int
}

data class DocumentNode(val body: RdnCstNode, override val start: Int, override val end: Int) : RdnCstNode

data class StringLiteralNode(val value: String, val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class NumberLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class BigIntLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class BooleanLiteralNode(val value: Boolean, override val start: Int, override val end: Int) : RdnCstNode
data class NullLiteralNode(override val start: Int, override val end: Int) : RdnCstNode
data class NaNLiteralNode(override val start: Int, override val end: Int) : RdnCstNode
data class InfinityLiteralNode(val negative: Boolean, override val start: Int, override val end: Int) : RdnCstNode
data class DateTimeLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class TimeOnlyLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class DurationLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class BinaryLiteralNode(val encoding: BinaryEncoding, val raw: String, override val start: Int, override val end: Int) : RdnCstNode
data class RegExpLiteralNode(val raw: String, override val start: Int, override val end: Int) : RdnCstNode

data class ArrayNode(val elements: List<RdnCstNode>, override val start: Int, override val end: Int) : RdnCstNode
data class TupleNode(val elements: List<RdnCstNode>, override val start: Int, override val end: Int) : RdnCstNode

data class ObjectPropertyNode(val key: StringLiteralNode, val value: RdnCstNode, override val start: Int, override val end: Int) : RdnCstNode
data class ObjectNode(val properties: List<ObjectPropertyNode>, override val start: Int, override val end: Int) : RdnCstNode

data class MapEntryNode(val key: RdnCstNode, val value: RdnCstNode, override val start: Int, override val end: Int) : RdnCstNode
data class MapNode(val entries: List<MapEntryNode>, val explicit: Boolean, override val start: Int, override val end: Int) : RdnCstNode

data class SetNode(val elements: List<RdnCstNode>, val explicit: Boolean, override val start: Int, override val end: Int) : RdnCstNode

enum class BinaryEncoding { BASE64, HEX }
```

### 5.6 Hover Token Detection Types

```kotlin
package com.rdn.intellij.documentation

import com.intellij.openapi.util.TextRange

enum class HoverTokenKind {
    DATE_TIME_FULL,
    DATE_TIME_NO_MILLIS,
    DATE_ONLY,
    UNIX_TIMESTAMP,
    TIME_ONLY,
    DURATION,
    BIGINT,
    BINARY_BASE64,
    BINARY_HEX,
    REGEXP,
    NAN,
    INFINITY,
    NEG_INFINITY,
    MAP_KEYWORD,
    SET_KEYWORD,
    MAP_ARROW,
    TUPLE,
    IMPLICIT_MAP,
    IMPLICIT_SET,
}

data class HoverTokenInfo(val kind: HoverTokenKind, val text: String, val range: TextRange)
```

## 6. Implementation Details

### 6.1 Core

#### `src/main/kotlin/com/rdn/intellij/RdnLanguage.kt`
- **What:** Singleton `Language` subclass. Registers "RDN" as a language in the IntelliJ platform.
- **Key class:** `RdnLanguage : Language("RDN")`
- **VSCode equivalent:** `contributes.languages[0].id` in `package.json`.
- **Dependencies:** None.

#### `src/main/kotlin/com/rdn/intellij/RdnFileType.kt`
- **What:** Registers `.rdn` file extension and provides the file icon.
- **Key class:** `RdnFileType : LanguageFileType(RdnLanguage)` -- returns `"rdn"` as default extension, `"RDN File"` as description.
- **VSCode equivalent:** `contributes.languages[0].extensions` in `package.json`.
- **Dependencies:** `RdnLanguage`, `RdnIcons`.

#### `src/main/kotlin/com/rdn/intellij/RdnIcons.kt`
- **What:** Loads icon constants from resources.
- **Key constants:** `FILE` (16x16 SVG), `PLUGIN` (40x40 SVG for Marketplace).
- **VSCode equivalent:** `contributes.languages[0].icon` in `package.json`.
- **Dependencies:** None. Icons loaded from `src/main/resources/icons/`.

### 6.2 Lexer

#### `src/main/kotlin/com/rdn/intellij/lexer/Rdn.flex`
- **What:** JFlex grammar file defining the lexer. Produces `RdnLexer.java` via JFlex generation.
- **Key states:** `YYINITIAL`, `STRING`, `REGEXP`, `REGEXP_CHAR_CLASS`, `BINARY`. See Section 7 for full design.
- **VSCode equivalent:** `spec/textmate/rdn.tmLanguage.json`.
- **Dependencies:** `RdnTokenTypes`.

#### `src/main/kotlin/com/rdn/intellij/lexer/RdnTokenTypes.kt`
- **What:** All `IElementType` token definitions used by the lexer. See Section 5.2.
- **VSCode equivalent:** TextMate scope names.
- **Dependencies:** `RdnLanguage`.

#### `src/main/kotlin/com/rdn/intellij/lexer/RdnLexerAdapter.kt`
- **What:** Wraps the generated JFlex lexer in a `FlexAdapter` for IntelliJ consumption.
- **Key class:** `RdnLexerAdapter : FlexAdapter(RdnFlexLexer())`
- **VSCode equivalent:** N/A (TextMate grammars are self-contained).
- **Dependencies:** Generated `RdnFlexLexer`.

#### `src/main/kotlin/com/rdn/intellij/highlighting/RdnSyntaxHighlighter.kt`
- **What:** Maps `RdnTokenTypes` to `TextAttributesKey` color keys.
- **Key method:** `getTokenHighlights(tokenType: IElementType): Array<TextAttributesKey>`
- **VSCode equivalent:** Theme-to-scope mapping in TextMate.
- **Dependencies:** `RdnTokenTypes`.

#### `src/main/kotlin/com/rdn/intellij/highlighting/RdnSyntaxHighlighterFactory.kt`
- **What:** Factory that creates `RdnSyntaxHighlighter` instances.
- **VSCode equivalent:** N/A (implicit in TextMate registration).
- **Dependencies:** `RdnSyntaxHighlighter`.

#### `src/main/kotlin/com/rdn/intellij/highlighting/RdnColorSettingsPage.kt`
- **What:** Provides the **Settings > Editor > Color Scheme > RDN** page with a sample code preview.
- **Key method:** `getDemoText(): String` -- returns an RDN sample showcasing all token types.
- **VSCode equivalent:** N/A (VSCode uses theme-level scope coloring).
- **Dependencies:** `RdnSyntaxHighlighter`.

### 6.3 Parser

#### `src/main/kotlin/com/rdn/intellij/parser/Rdn.bnf`
- **What:** GrammarKit BNF grammar producing the PSI tree. See Section 8 for full grammar.
- **VSCode equivalent:** `@rdn/parser` (the TypeScript recursive-descent parser).
- **Dependencies:** `RdnTokenTypes`, `RdnElementTypes`.

#### `src/main/kotlin/com/rdn/intellij/parser/RdnParserDefinition.kt`
- **What:** `ParserDefinition` extension point that ties together the lexer, parser, and PSI element factory.
- **Key methods:** `createLexer()`, `createParser()`, `createElement()`, `createFile()`.
- **VSCode equivalent:** N/A (VSCode uses TextMate + separate parser).
- **Dependencies:** `RdnLexerAdapter`, generated parser, `RdnElementTypes`, `RdnFile`.

#### `src/main/kotlin/com/rdn/intellij/psi/RdnFile.kt`
- **What:** `PsiFile` subclass for `.rdn` files.
- **VSCode equivalent:** `vscode.TextDocument` with `languageId === "rdn"`.
- **Dependencies:** `RdnFileType`, `RdnLanguage`.

#### `src/main/kotlin/com/rdn/intellij/psi/RdnElementTypes.kt`
- **What:** PSI element type definitions. See Section 5.3.
- **Dependencies:** `RdnLanguage`.

#### `src/main/kotlin/com/rdn/intellij/psi/impl/` (generated + custom)
- **What:** PSI element implementation classes generated by GrammarKit, plus custom mixin classes for typed access (e.g., `RdnObjectProperty.getKey()`, `RdnArray.getElements()`).
- **Dependencies:** Generated from `Rdn.bnf`.

### 6.4 Diagnostics

#### `src/main/kotlin/com/rdn/intellij/annotator/RdnExternalAnnotator.kt`
- **What:** `ExternalAnnotator<RdnAnnotatorInput, RdnAnnotatorResult>` that runs 3-pass validation on a background thread.
- **Key methods:**
  - `collectInformation(file, editor): RdnAnnotatorInput` -- snapshots the document text.
  - `doAnnotate(input): RdnAnnotatorResult` -- runs (1) `scanUnquotedKeys`, (2) `scanBinaryErrors`, (3) full parse.
  - `apply(file, result, holder)` -- creates error annotations with quick-fix references.
- **VSCode equivalent:** `updateDiagnostics()` in `extension.ts`.
- **Dependencies:** `RdnKotlinParser`, `RdnScanner`.

#### `src/main/kotlin/com/rdn/intellij/annotator/RdnScanner.kt`
- **What:** Kotlin port of `scanner.ts`. Contains `scanUnquotedKeys()` and `scanBinaryErrors()`.
- **Key classes:** `UnquotedKey(name: String, offset: Int, length: Int)`, `BinaryCharError(offset: Int, length: Int, message: String, kind: BinaryEncoding)`.
- **VSCode equivalent:** `src/scanner.ts`.
- **Dependencies:** None (standalone utility).

#### `src/main/kotlin/com/rdn/intellij/annotator/RdnKotlinParser.kt`
- **What:** Full-fidelity RDN parser in Kotlin. Recursive-descent with a 256-entry dispatch table mirroring `packages/rdn-js/src/parser.ts`. Throws `RdnSyntaxError` with position information on parse failure.
- **Key method:** `fun parse(text: String): RdnValue`
- **VSCode equivalent:** `@rdn/parser` (`packages/rdn-js/src/parser.ts`).
- **Dependencies:** `RdnValue` model types.

### 6.5 Quick Fixes

#### `src/main/kotlin/com/rdn/intellij/quickfix/WrapKeyInQuotesQuickFix.kt`
- **What:** `LocalQuickFix` that wraps an unquoted key in double quotes.
- **Key method:** `applyFix(project, descriptor)` -- replaces `key` with `"key"` in the document.
- **VSCode equivalent:** `CodeActionProvider` in `extension.ts`.
- **Dependencies:** None.

#### `src/main/kotlin/com/rdn/intellij/quickfix/WrapAllKeysInQuotesQuickFix.kt`
- **What:** Bulk quick fix that wraps all unquoted keys in the document. Applied in reverse offset order to preserve positions.
- **VSCode equivalent:** "Fix all" code action in `extension.ts`.
- **Dependencies:** None.

### 6.6 Completions

#### `src/main/kotlin/com/rdn/intellij/completion/RdnCompletionContributor.kt`
- **What:** `CompletionContributor` with three completion providers registered via `extend()`.
- **Key inner classes/providers:**
  - `RdnSchemaCompletionProvider` -- `$schema` property completion at top-level object depth 1.
  - `RdnKeywordCompletionProvider` -- 11 RDN keywords with detail/documentation.
  - `RdnSnippetCompletionProvider` -- 12 snippet templates using `InsertHandler` with tab stops or Live Templates.
- **String-context guard:** Before providing completions, scans backwards on the current line to check quote parity. Suppresses completions when inside a string.
- **VSCode equivalent:** `CompletionItemProvider` registrations in `extension.ts`.
- **Dependencies:** None.

### 6.7 Formatter

#### `src/main/kotlin/com/rdn/intellij/formatter/RdnFormattingModelBuilder.kt`
- **What:** `FormattingModelBuilder` that creates an `RdnFormattingModel`.
- **Key decision:** If Prettier is available (detected via project files), delegates formatting to Prettier. Otherwise, uses the built-in CST formatter.
- **VSCode equivalent:** `DocumentFormattingEditProvider` in `extension.ts`.
- **Dependencies:** `RdnCstFormatter`, `RdnPrettierDetector`.

#### `src/main/kotlin/com/rdn/intellij/formatter/RdnCstFormatter.kt`
- **What:** Kotlin port of `formatter.ts`. Parses text into CST, then prints with compact/multi-line logic.
- **Key methods:**
  - `fun format(text: String, tabSize: Int, insertSpaces: Boolean, opts: RdnFormatOptions): String`
  - `fun formatSorted(text: String, tabSize: Int, insertSpaces: Boolean, opts: RdnFormatOptions): String?`
  - `private fun printCompact(node: RdnCstNode, opts: RdnFormatOptions): String`
  - `private fun printNode(node: RdnCstNode, indent: String, indentUnit: String, opts: RdnFormatOptions): String`
- **VSCode equivalent:** `src/formatter.ts`.
- **Dependencies:** `RdnCstParser`, `RdnCstNode` types.

#### `src/main/kotlin/com/rdn/intellij/formatter/RdnCstParser.kt`
- **What:** Kotlin port of `tools/prettier-plugin-rdn/src/parser.ts`. Produces `DocumentNode` CST with source positions.
- **Key method:** `fun parse(text: String): DocumentNode`
- **VSCode equivalent:** `@rdn/cst-parser`.
- **Dependencies:** `RdnCstNode` types.

#### `src/main/kotlin/com/rdn/intellij/formatter/RdnPrettierDetector.kt`
- **What:** Utility that checks if Prettier is configured in the project and if `prettier-plugin-rdn` is installed.
- **Key method:** `fun isPrettierAvailable(project: Project): Boolean` -- checks for `.prettierrc`, `prettier.config.js`, or `"prettier"` key in `package.json`, then verifies `prettier-plugin-rdn` is in `node_modules`.
- **VSCode equivalent:** N/A (VSCode defers to Prettier automatically via the Prettier extension).
- **Dependencies:** None.

### 6.8 Hover / Documentation

#### `src/main/kotlin/com/rdn/intellij/documentation/RdnDocumentationProvider.kt`
- **What:** `DocumentationProvider` that generates HTML hover content for 18 token kinds.
- **Key methods:**
  - `generateDoc(element, originalElement): String?` -- detects the token under cursor and generates HTML.
  - `private fun detectToken(document, offset): HoverTokenInfo?` -- mirrors `detectToken()` in `hover.ts`.
  - Private render methods for each token kind: `renderDateTime()`, `renderBigInt()`, `renderBinaryBase64()`, `renderRegExp()`, etc.
- **VSCode equivalent:** `RdnHoverProvider` in `src/hover.ts`.
- **Dependencies:** `RdnSettingsState`, `RdnFormatUtils`, `HoverTokenInfo`.

#### `src/main/kotlin/com/rdn/intellij/documentation/RdnFormatUtils.kt`
- **What:** Kotlin port of `format.ts`. Date/time/number formatting utilities.
- **Key methods:**
  - `fun formatDate(date: Instant, formatStr: String, defaultFormat: String): String`
  - `fun expandDuration(iso: String): String`
  - `fun groupDigits(digits: String): String`
  - `fun formatByteSize(bytes: Int): String`
- **VSCode equivalent:** `src/format.ts`.
- **Dependencies:** None.

#### `src/main/kotlin/com/rdn/intellij/documentation/RdnBinaryUtils.kt`
- **What:** Base64/hex decoding, ASCII preview, image detection utilities.
- **Key methods:**
  - `fun decodeBase64ToBytes(b64: String, maxBytes: Int): ByteArray`
  - `fun decodeHexToBytes(hex: String, maxBytes: Int): ByteArray`
  - `fun detectImageFromBytes(bytes: ByteArray): ImageInfo?`
  - `fun bytesToAsciiPreview(bytes: ByteArray): String?`
- **VSCode equivalent:** Helper functions in `src/hover.ts`.
- **Dependencies:** None.

### 6.9 Actions

#### `src/main/kotlin/com/rdn/intellij/actions/SortDocumentKeysAction.kt`
- **What:** `AnAction` registered in the **Tools** menu and Command Palette. Sorts all object keys recursively.
- **Key method:** `actionPerformed(e)` -- gets active editor, calls `RdnCstFormatter.formatSorted()`, replaces document text.
- **Visibility:** Only visible/enabled when the active file is `.rdn`.
- **VSCode equivalent:** `rdn.sortDocument` command in `extension.ts`.
- **Dependencies:** `RdnCstFormatter`.

### 6.10 Settings

#### `src/main/kotlin/com/rdn/intellij/settings/RdnSettingsState.kt`
- **What:** `PersistentStateComponent` storing all settings. See Section 5.4.
- **VSCode equivalent:** `contributes.configuration` in `package.json`.
- **Dependencies:** None.

#### `src/main/kotlin/com/rdn/intellij/settings/RdnSettingsConfigurable.kt`
- **What:** `Configurable` that provides the UI panel for **Settings > Languages & Frameworks > RDN**.
- **Key methods:** `createComponent(): JComponent`, `isModified(): Boolean`, `apply()`, `reset()`.
- **UI layout:** Kotlin DSL panel with grouped sections (Formatting, Hover, Hover > DateTime, Hover > Binary, etc.).
- **VSCode equivalent:** `contributes.configuration.properties` in `package.json`.
- **Dependencies:** `RdnSettingsState`.

### 6.11 Bracket Matching

#### `src/main/kotlin/com/rdn/intellij/braceMatcher/RdnBraceMatcher.kt`
- **What:** `PairedBraceMatcher` that defines bracket pairs and structural/code braces.
- **Key method:** `getPairs()` -- returns `BracePair(LBRACE, RBRACE)`, `BracePair(LBRACKET, RBRACKET)`, `BracePair(LPAREN, RPAREN)`.
- **VSCode equivalent:** `brackets` in `language-configuration.json`.
- **Dependencies:** `RdnTokenTypes`.

### 6.12 Code Folding

#### `src/main/kotlin/com/rdn/intellij/folding/RdnFoldingBuilder.kt`
- **What:** `FoldingBuilderEx` that creates fold regions for objects, arrays, tuples, maps, and sets.
- **Key method:** `buildFoldRegions(root, document, quick)` -- walks the PSI tree, creates `FoldingDescriptor` for each collection node.
- **Placeholder text:** `{...}`, `[...]`, `(...)` with optional element count.
- **VSCode equivalent:** N/A (VSCode uses indentation-based folding by default).
- **Dependencies:** PSI element types.

### 6.13 Markdown Injection

#### `src/main/kotlin/com/rdn/intellij/injection/RdnMarkdownInjector.kt`
- **What:** `MultiHostInjector` that injects the RDN language into Markdown fenced code blocks tagged with `rdn`.
- **Key method:** `getLanguagesToInject(registrar, context)` -- detects `` ```rdn `` blocks and registers injection places.
- **VSCode equivalent:** `spec/textmate/rdn.markdown.tmLanguage.json`.
- **Dependencies:** `RdnLanguage`.

### 6.14 Shared Grammar (spec/textmate/)

#### Restructuring

Move TextMate grammars from `tools/vscode-extension/syntaxes/` to `spec/textmate/`:

- `spec/textmate/rdn.tmLanguage.json` -- main grammar.
- `spec/textmate/rdn.markdown.tmLanguage.json` -- Markdown injection grammar.

Update the VSCode extension's `package.json` to reference the new paths:
```json
"grammars": [
  {
    "language": "rdn",
    "scopeName": "source.rdn",
    "path": "../../spec/textmate/rdn.tmLanguage.json"
  },
  {
    "scopeName": "markdown.rdn.codeblock",
    "path": "../../spec/textmate/rdn.markdown.tmLanguage.json",
    "injectTo": ["text.html.markdown"],
    "embeddedLanguages": { "meta.embedded.block.rdn": "rdn" }
  }
]
```

The JetBrains plugin does NOT use these TextMate grammars (it has a native JFlex lexer). However, the shared location ensures any future language support tool can reference the canonical TextMate grammars.

### 6.15 Build & CI

#### `tools/jetbrains-plugin/build.gradle.kts`
- **What:** Gradle build script using IntelliJ Platform Gradle Plugin 2.x.
- **Key configuration:**
  - `intellijPlatform { pluginConfiguration { id = "com.rdn.intellij"; name = "RDN"; version = ... } }`
  - `intellijPlatform { create("IC", "2024.3") }` -- target IntelliJ IDEA Community.
  - JFlex generation task: `generateLexer` that runs JFlex on `Rdn.flex`.
  - GrammarKit generation task: `generateParser` that runs GrammarKit on `Rdn.bnf`.
  - Conformance test task: `test` with system property pointing to `test-suite/` directory.
- **Dependencies:** Kotlin stdlib, IntelliJ Platform SDK, JUnit 5.

#### `tools/jetbrains-plugin/settings.gradle.kts`
- **What:** Gradle settings with plugin management for IntelliJ Platform Gradle Plugin.

#### `tools/jetbrains-plugin/gradle.properties`
- **What:** Version properties (`pluginVersion`, `platformVersion`, `sinceBuild`, `untilBuild`).

#### `src/main/resources/META-INF/plugin.xml`
- **What:** Plugin descriptor registering all extension points.
- **Key extensions:** `fileType`, `lang.parserDefinition`, `lang.syntaxHighlighterFactory`, `colorSettingsPage`, `externalAnnotator`, `completion.contributor`, `lang.formatter`, `lang.documentationProvider`, `lang.braceMatcher`, `lang.foldingBuilder`, `multiHostInjector`, `projectConfigurable`.

#### `.github/workflows/jetbrains-ci.yml`
- **What:** CI workflow that runs `./gradlew check` on PRs touching `tools/jetbrains-plugin/**`.

#### `.github/workflows/jetbrains-release.yml`
- **What:** Release workflow triggered by tags matching `jetbrains-v*`. Builds the plugin, signs it, and publishes to JetBrains Marketplace.

## 7. JFlex Lexer Grammar Design

The JFlex lexer has 5 states and produces ~50 distinct token types.

### 7.1 State Diagram

```
YYINITIAL ──── " ────────────→ STRING ──── " ────→ YYINITIAL
    │                              │
    │                              └── \X ────→ (emit escape, stay in STRING)
    │
    ├──── / ────────────────→ REGEXP ──── / ────→ YYINITIAL (read flags)
    │                            │
    │                            ├── [ ────→ REGEXP_CHAR_CLASS ── ] ──→ REGEXP
    │                            │
    │                            └── \X ──→ (emit escape, stay in REGEXP)
    │
    ├──── b" or x" ─────────→ BINARY ──── " ────→ YYINITIAL
    │                            │
    │                            └── (validate chars per encoding)
    │
    └──── (all other tokens handled in YYINITIAL)
```

### 7.2 YYINITIAL State

This is the default state. It handles all structural tokens and value-level tokens.

```
Whitespace:     [ \t\n\r]+                          → WHITE_SPACE

Structural:     "{"                                 → LBRACE
                "}"                                 → RBRACE
                "["                                 → LBRACKET
                "]"                                 → RBRACKET
                "("                                 → LPAREN
                ")"                                 → RPAREN
                ":"                                 → COLON
                ","                                 → COMMA
                "=>"                                → ARROW

Keywords:       "null"                              → NULL
                "true"                              → TRUE
                "false"                             → FALSE
                "NaN"                               → NAN
                "Infinity"                          → INFINITY
                "-Infinity"                         → NEG_INFINITY

Numbers:        -?[0-9]+n                           → BIGINT
                -?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?
                  (with fraction or exponent)       → FLOAT
                -?(0|[1-9][0-9]*)                   → INTEGER

Map/Set:        "Map" / "{"                         → MAP_KEYWORD
                "Set" / "{"                         → SET_KEYWORD

DateTime:       "@" followed by date patterns       → AT_SIGN, then scan date/time/duration/unix
                  (see 7.3 below)

Binary:         "b" / "\""                          → BINARY_PREFIX, push BINARY state
                "x" / "\""                          → BINARY_PREFIX, push BINARY state

RegExp:         "/"                                 → REGEXP_OPEN, push REGEXP state

String:         "\""                                → STRING_OPEN, push STRING state

Fallback:       .                                   → BAD_CHARACTER
```

### 7.3 Date/Time/Duration Scanning in YYINITIAL

After emitting `AT_SIGN` for `@`, the lexer remains in YYINITIAL and uses match priority to tokenize the date/time/duration body:

```
Duration:       "P" followed by duration body       → DURATION_P, then:
                  [0-9]+                            → DURATION_NUMBER
                  [YMDHS]                           → DURATION_UNIT
                  "T"                               → DURATION_T

DateTime:       [0-9]{4}-[0-9]{2}-[0-9]{2}         → DATE_PART
                "T"                                 → TIME_SEPARATOR
                [0-9]{2}:[0-9]{2}:[0-9]{2}          → TIME_PART
                \.[0-9]{3}                          → MILLIS_PART
                "Z"                                 → TIMEZONE

TimeOnly:       (handled by same TIME_PART token after AT_SIGN)

Unix:           [0-9]+                              → UNIX_TIMESTAMP (when after @ and no date pattern)
```

The lexer uses a flag (`afterAt`) to track that the previous token was `@`. This flag conditions which patterns are active for the immediately following token. After the first non-`@`-continuation token, the flag resets.

### 7.4 STRING State

Entered when `"` is encountered in YYINITIAL. Emits string content tokens until the closing quote.

```
Valid escapes:  \\["\\/bfnrt]                      → STRING_ESCAPE
                \\u[0-9A-Fa-f]{4}                   → STRING_ESCAPE
Invalid escape: \\.                                 → STRING_INVALID_ESCAPE
Content:        [^"\\]+                             → STRING_CONTENT
Close:          "\""                                → STRING_CLOSE, pop to YYINITIAL
```

**Object key detection:** The lexer does NOT distinguish keys from values at the lexer level. The parser (or a post-processing step in the syntax highlighter) handles re-mapping string tokens to key tokens when the string is followed by `:`. This is done via a `RdnHighlightingLexer` wrapper that looks ahead for `:` after a string and re-maps the token types.

### 7.5 REGEXP State

Entered when `/` is encountered in YYINITIAL.

```
Escape:         \\[tvnrf.\\*+?()\[\]{}|^$/]        → REGEXP_ESCAPE
                \\c[A-Za-z]                         → REGEXP_ESCAPE
                \\x[0-9A-Fa-f]{2}                   → REGEXP_ESCAPE
                \\u[0-9A-Fa-f]{4}                   → REGEXP_ESCAPE
                \\u\{[0-9A-Fa-f]+\}                 → REGEXP_ESCAPE
                \\0(?![0-9])                        → REGEXP_ESCAPE

Class escapes:  \\[dDsSwWbB]                       → REGEXP_CHAR_CLASS_ESCAPE

Backreference:  \\[1-9][0-9]*                      → REGEXP_BACKREFERENCE
                \\k<[a-zA-Z_$][a-zA-Z0-9_$]*>      → REGEXP_BACKREFERENCE

Lookaround:     "(?=" | "(?!" | "(?<=" | "(?<!"    → REGEXP_LOOKAROUND (followed by REGEXP_GROUP_OPEN)
Named group:    "(?<" [a-zA-Z_$][a-zA-Z0-9_$]* ">" → REGEXP_NAMED_GROUP (followed by REGEXP_GROUP_OPEN)
Non-capturing:  "(?:"                               → REGEXP_NON_CAPTURING (followed by REGEXP_GROUP_OPEN)

Quantifier:     [+*?]                               → REGEXP_QUANTIFIER
                \{[0-9]+(,[0-9]*)?\}[?+]?           → REGEXP_QUANTIFIER

Anchor:         "^" | "$"                           → REGEXP_ANCHOR
Alternation:    "|"                                 → REGEXP_ALTERNATION
Dot:            "."                                 → REGEXP_DOT

Group open:     "("                                 → REGEXP_GROUP_OPEN
Group close:    ")"                                 → REGEXP_GROUP_CLOSE

Char class:     "["                                 → REGEXP_CHAR_CLASS_OPEN, push REGEXP_CHAR_CLASS

Content:        [^/\\\[\]().|^$+*?{}]+             → REGEXP_CONTENT

Close:          "/"                                 → REGEXP_CLOSE, pop to YYINITIAL
Flags:          [dgimsuvy]+                         → REGEXP_FLAGS (read immediately after REGEXP_CLOSE)
```

### 7.6 REGEXP_CHAR_CLASS State

Entered from REGEXP when `[` is encountered.

```
Negation:       "^" (only at start of class)       → REGEXP_NEGATION
Range:          "-"                                → REGEXP_RANGE
Class escape:   \\[dDsSwWbB]                       → REGEXP_CHAR_CLASS_ESCAPE
Escape:         \\[tvnrf.\\*+?()\[\]{}|^$/]        → REGEXP_ESCAPE
                (same escape patterns as REGEXP)
Content:        [^\]\\-]+                           → REGEXP_CONTENT
Close:          "]"                                → REGEXP_CHAR_CLASS_CLOSE, pop to REGEXP
```

### 7.7 BINARY State

Entered when `b"` or `x"` is encountered. A lexer field (`binaryKind: "base64" | "hex"`) tracks which encoding is active.

```
For base64 (binaryKind == "base64"):
  Valid:        [A-Za-z0-9+/=]+                    → BINARY_CONTENT
  Invalid:      [^"A-Za-z0-9+/=]                   → BINARY_INVALID_CHAR

For hex (binaryKind == "hex"):
  Valid:        [0-9A-Fa-f]+                        → BINARY_CONTENT
  Invalid:      [^"0-9A-Fa-f]                       → BINARY_INVALID_CHAR

Open:           "\""                                → BINARY_OPEN (emitted on entry)
Close:          "\""                                → BINARY_CLOSE, pop to YYINITIAL
```

### 7.8 Token Count Summary

| State | Distinct Token Types | Description |
|---|---|---|
| YYINITIAL | ~25 | Structural, keywords, numbers, date/time components, Map/Set keywords |
| STRING | 4 | Content, escape, invalid escape, close |
| REGEXP | ~15 | Content, escape, class escape, quantifier, anchor, alternation, dot, groups, backreference, lookaround, named group, non-capturing, close, flags |
| REGEXP_CHAR_CLASS | 5 | Content, negation, range, escape, close |
| BINARY | 3 | Content, invalid char, close |
| **Total** | **~52** | |

## 8. GrammarKit BNF Grammar

```bnf
{
    parserClass="com.rdn.intellij.parser.RdnParser"
    extends="com.intellij.extapi.psi.ASTWrapperPsiElement"

    psiClassPrefix="Rdn"
    psiImplClassSuffix="Impl"
    psiPackage="com.rdn.intellij.psi"
    psiImplPackage="com.rdn.intellij.psi.impl"

    elementTypeHolderClass="com.rdn.intellij.psi.RdnElementTypes"
    elementTypeClass="com.rdn.intellij.psi.RdnElementType"
    tokenTypeClass="com.rdn.intellij.lexer.RdnTokenType"

    tokens = [
        LBRACE="{"
        RBRACE="}"
        LBRACKET="["
        RBRACKET="]"
        LPAREN="("
        RPAREN=")"
        COLON=":"
        COMMA=","
        ARROW="=>"
    ]
}

rdnFile ::= value

value ::= object
        | array
        | tuple
        | map
        | set
        | string_literal
        | number_literal
        | bigint_literal
        | boolean_literal
        | null_literal
        | nan_literal
        | infinity_literal
        | datetime_literal
        | time_only_literal
        | duration_literal
        | binary_literal
        | regexp_literal

// Collections
object ::= LBRACE RBRACE
          | LBRACE object_property (COMMA object_property)* RBRACE
          {pin=1}

object_property ::= object_key COLON value
          {pin=2}

object_key ::= KEY_OPEN KEY_CONTENT? KEY_ESCAPE* KEY_CLOSE
             | STRING_OPEN STRING_CONTENT? STRING_ESCAPE* STRING_CLOSE

array ::= LBRACKET RBRACKET
         | LBRACKET value (COMMA value)* RBRACKET
         {pin=1}

tuple ::= LPAREN RPAREN
         | LPAREN value (COMMA value)* RPAREN
         {pin=1}

map ::= MAP_KEYWORD LBRACE RBRACE
      | MAP_KEYWORD LBRACE map_entry (COMMA map_entry)* RBRACE
      | LBRACE map_entry (COMMA map_entry)* RBRACE

map_entry ::= value ARROW value
         {pin=2}

set ::= SET_KEYWORD LBRACE RBRACE
      | SET_KEYWORD LBRACE value (COMMA value)* RBRACE
      | LBRACE value RBRACE
      | LBRACE value (COMMA value)+ RBRACE

// Atomic literals
string_literal ::= STRING_OPEN STRING_CONTENT? (STRING_ESCAPE | STRING_INVALID_ESCAPE)* STRING_CLOSE

number_literal ::= INTEGER | FLOAT

bigint_literal ::= BIGINT

boolean_literal ::= TRUE | FALSE

null_literal ::= NULL

nan_literal ::= NAN

infinity_literal ::= INFINITY | NEG_INFINITY

datetime_literal ::= AT_SIGN (DATE_PART TIME_SEPARATOR? TIME_PART? MILLIS_PART? TIMEZONE? | UNIX_TIMESTAMP)

time_only_literal ::= AT_SIGN TIME_PART MILLIS_PART?

duration_literal ::= AT_SIGN DURATION_P (DURATION_NUMBER DURATION_UNIT)* DURATION_T? (DURATION_NUMBER DURATION_UNIT)*

binary_literal ::= BINARY_PREFIX BINARY_OPEN (BINARY_CONTENT | BINARY_INVALID_CHAR)* BINARY_CLOSE

regexp_literal ::= REGEXP_OPEN regexp_body REGEXP_CLOSE REGEXP_FLAGS?

private regexp_body ::= (REGEXP_CONTENT | REGEXP_ESCAPE | REGEXP_CHAR_CLASS_ESCAPE
                        | REGEXP_QUANTIFIER | REGEXP_ANCHOR | REGEXP_ALTERNATION | REGEXP_DOT
                        | REGEXP_GROUP_OPEN | REGEXP_GROUP_CLOSE
                        | REGEXP_LOOKAROUND | REGEXP_NAMED_GROUP | REGEXP_NON_CAPTURING
                        | REGEXP_BACKREFERENCE
                        | REGEXP_CHAR_CLASS_OPEN regexp_char_class_body REGEXP_CHAR_CLASS_CLOSE)*

private regexp_char_class_body ::= REGEXP_NEGATION? (REGEXP_CONTENT | REGEXP_ESCAPE
                                   | REGEXP_CHAR_CLASS_ESCAPE | REGEXP_RANGE)*
```

**Note on brace disambiguation:** The GrammarKit grammar lists `object`, `map`, and `set` as alternatives in the `value` rule. Since GrammarKit uses PEG-style ordered choice, the parser tries `object` first (which expects `{ key : value ...}`), then `map` (which expects `{ value => value ...}`), then `set` (which expects `{ value , ... }` or `{ value }`). In practice, error recovery and the parser's lookahead mechanism handle disambiguation. The actual implementation may use a custom `parseBrace` method that peeks at the separator after the first value, identical to the TypeScript parser's approach.

## 9. Settings Schema

All settings are project-level and stored in `.idea/rdn.xml`.

| # | Key | Type | Default | UI Group | Description |
|---|---|---|---|---|---|
| 1 | `useExplicitMapKeyword` | `Boolean` | `false` | Formatting | Keep `Map` keyword on non-empty maps |
| 2 | `useExplicitSetKeyword` | `Boolean` | `false` | Formatting | Keep `Set` keyword on non-empty sets |
| 3 | `hoverEnabled` | `Boolean` | `true` | Hover | Master toggle for all hover information |
| 4 | `hoverDateTimeEnabled` | `Boolean` | `true` | Hover > DateTime | Show hover for DateTime/date/unix literals |
| 5 | `hoverDateTimeFullFormat` | `String` | `"YYYY-MM-DD HH:mm:ss.SSS [UTC]"` | Hover > DateTime | Format for full ISO 8601 DateTime |
| 6 | `hoverDateTimeDateOnlyFormat` | `String` | `"MMMM D, YYYY"` | Hover > DateTime | Format for date-only values |
| 7 | `hoverDateTimeNoMillisFormat` | `String` | `"YYYY-MM-DD HH:mm:ss [UTC]"` | Hover > DateTime | Format for DateTime without milliseconds |
| 8 | `hoverDateTimeUnixFormat` | `String` | `"YYYY-MM-DD HH:mm:ss [UTC]"` | Hover > DateTime | Format for Unix timestamp display |
| 9 | `hoverTimeOnlyEnabled` | `Boolean` | `true` | Hover > TimeOnly | Show hover for TimeOnly literals |
| 10 | `hoverTimeOnlyFormat` | `String` | `"HH:mm:ss"` | Hover > TimeOnly | Format for TimeOnly display |
| 11 | `hoverDurationEnabled` | `Boolean` | `true` | Hover > Duration | Show hover for Duration literals |
| 12 | `hoverBigintEnabled` | `Boolean` | `true` | Hover > BigInt | Show hover for BigInt literals |
| 13 | `hoverBigintShowBitLength` | `Boolean` | `true` | Hover > BigInt | Show bit length in BigInt hover |
| 14 | `hoverBinaryEnabled` | `Boolean` | `true` | Hover > Binary | Show hover for binary literals |
| 15 | `hoverBinaryShowPreview` | `Boolean` | `true` | Hover > Binary | Show ASCII preview of binary data |
| 16 | `hoverRegexpEnabled` | `Boolean` | `true` | Hover > RegExp | Show hover for RegExp literals |
| 17 | `hoverSpecialNumbersEnabled` | `Boolean` | `true` | Hover > Special Numbers | Show hover for NaN/Infinity |
| 18 | `hoverCollectionsEnabled` | `Boolean` | `true` | Hover > Collections | Show hover for Map/Set/Tuple |
| 19 | `hoverDiagnosticsEnabled` | `Boolean` | `true` | Hover > Diagnostics | Show diagnostic hints in hover |

The UI panel is organized into collapsible sections:

```
RDN Settings
├── Formatting
│   ├── [x] Keep explicit Map keyword
│   └── [x] Keep explicit Set keyword
└── Hover Information
    ├── [x] Enable hover information
    ├── DateTime
    │   ├── [x] Enable
    │   ├── Full format:    [YYYY-MM-DD HH:mm:ss.SSS [UTC]]
    │   ├── Date-only format: [MMMM D, YYYY]
    │   ├── No-millis format: [YYYY-MM-DD HH:mm:ss [UTC]]
    │   └── Unix format:    [YYYY-MM-DD HH:mm:ss [UTC]]
    ├── TimeOnly
    │   ├── [x] Enable
    │   └── Format: [HH:mm:ss]
    ├── Duration
    │   └── [x] Enable
    ├── BigInt
    │   ├── [x] Enable
    │   └── [x] Show bit length
    ├── Binary
    │   ├── [x] Enable
    │   └── [x] Show ASCII preview
    ├── RegExp
    │   └── [x] Enable
    ├── Special Numbers
    │   └── [x] Enable
    ├── Collections
    │   └── [x] Enable
    └── Diagnostics
        └── [x] Show diagnostic hints
```

## 10. Testing Strategy

### 10.1 Conformance Tests

The Kotlin parser is validated against the shared `test-suite/`:

**Valid tests** (`test-suite/valid/*.rdn` + `*.expected.json`):
- `primitives`, `bigint`, `binary`, `datetime`, `map`, `nested`, `regexp`, `set`, `special-numbers`, `time-and-duration`, `tuple` (11 test files).
- Each test reads the `.rdn` file, parses it with `RdnKotlinParser.parse()`, serializes the result to JSON using the `$type` convention, and asserts equality with the `.expected.json` file.

**Invalid tests** (`test-suite/invalid/*.rdn`):
- `bigint-decimal`, `bigint-exponent`, `invalid-binary`, `invalid-date`, `invalid-hex`, `invalid-regexp`, `single-quotes`, `trailing-comma`, `unclosed-map`, `unquoted-key` (10 test files).
- Each test reads the `.rdn` file, attempts to parse, and asserts that a `RdnSyntaxError` is thrown.

**Roundtrip tests** (`test-suite/roundtrip/*.rdn`):
- `all-types`, `empty-containers` (2 test files).
- Parse, stringify, parse again, and assert the two parse results are structurally equal.

### 10.2 Lexer Tests

```kotlin
class RdnLexerTest {
    // Assert that a given input produces the expected token stream.
    // Example:
    // input: `{"key": 42}`
    // expected: [LBRACE, KEY_OPEN, KEY_CONTENT("key"), KEY_CLOSE, COLON, WHITE_SPACE, INTEGER("42"), RBRACE]

    fun testBasicObject() { ... }
    fun testBigInt() { ... }
    fun testDateTime() { ... }
    fun testDuration() { ... }
    fun testBinary() { ... }
    fun testRegExp() { ... }
    fun testRegExpCharClass() { ... }
    fun testMapKeyword() { ... }
    fun testSetKeyword() { ... }
    fun testSpecialNumbers() { ... }
    fun testStringEscapes() { ... }
    fun testInvalidEscapes() { ... }
    fun testBinaryInvalidChars() { ... }
}
```

### 10.3 Parser Tests

```kotlin
class RdnParserTest : ParsingTestCase("", "rdn", RdnParserDefinition()) {
    // Uses IntelliJ's ParsingTestCase framework.
    // Each test compares the PSI tree structure against a golden .txt file.

    fun testObject() { doTest(true) }
    fun testArray() { doTest(true) }
    fun testBraceDisambiguation() { doTest(true) }
    fun testAllTypes() { doTest(true) }
}
```

### 10.4 Formatter Tests

```kotlin
class RdnFormatterTest {
    fun testCompactFormatting() { ... }
    fun testMultiLineExpansion() { ... }
    fun testSortKeys() { ... }
    fun testPreservesInvalidInput() { ... }
    fun testExplicitMapKeyword() { ... }
    fun testExplicitSetKeyword() { ... }
    fun testTabIndentation() { ... }
}
```

### 10.5 Completion Tests

```kotlin
class RdnCompletionTest : BasePlatformTestCase() {
    fun testSchemaCompletion() { ... }
    fun testSchemaNotOfferedAtDepth2() { ... }
    fun testSchemaNotOfferedWhenExists() { ... }
    fun testKeywordCompletions() { ... }
    fun testSnippetCompletions() { ... }
    fun testNoCompletionsInsideString() { ... }
}
```

### 10.6 Annotator Tests

```kotlin
class RdnAnnotatorTest : BasePlatformTestCase() {
    fun testUnquotedKeyDetection() { ... }
    fun testBinaryCharValidation() { ... }
    fun testParseErrorReporting() { ... }
    fun testQuickFixWrapKey() { ... }
    fun testQuickFixWrapAllKeys() { ... }
}
```

### 10.7 Scanner Tests

```kotlin
class RdnScannerTest {
    fun testUnquotedKeysSimple() { ... }
    fun testUnquotedKeysNested() { ... }
    fun testBraceDisambiguationObject() { ... }
    fun testBraceDisambiguationMap() { ... }
    fun testBraceDisambiguationSet() { ... }
    fun testBinaryErrorsBase64() { ... }
    fun testBinaryErrorsHex() { ... }
    fun testSkipsRegexAndStrings() { ... }
}
```

### 10.8 Running Tests

```bash
cd tools/jetbrains-plugin
./gradlew test                    # all tests
./gradlew test --tests "*Lexer*"  # lexer tests only
./gradlew test --tests "*Parser*" # parser tests only
```

## 11. Risks & Mitigations

| # | Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|---|
| 1 | **Kotlin parser diverges from TypeScript parser** | Subtle parsing differences cause different behavior between VSCode and JetBrains. | Medium | Run both parsers against the shared `test-suite/` in CI. Add a new conformance test for every spec change. Document parser behavior edge cases in the spec. |
| 2 | **JFlex RegExp state machine has bugs** | Incorrect highlighting of regex sub-patterns (e.g., quantifiers inside character classes). | Medium | Comprehensive lexer tests with edge cases from the TextMate grammar's regex patterns. Test against the same RegExp samples used in the TextMate grammar tests. |
| 3 | **GrammarKit brace disambiguation fails** | Parser produces incorrect PSI tree for implicit maps/sets, causing cascading errors. | Medium | Implement brace disambiguation as a custom `parseBrace()` method in the parser (overriding the generated code) that peeks at the separator after the first value. This mirrors the TypeScript parser's proven approach. |
| 4 | **Performance degradation on large files** | The `ExternalAnnotator` running 3 passes on a 10K+ line file causes UI lag. | Low | The `ExternalAnnotator` runs on a background thread. The Kotlin parser uses a 256-entry dispatch table for O(1) character dispatch and deferred string materialization for minimal allocation. Profile with 10K-line test files during development. |
| 5 | **IntelliJ Platform API changes break compatibility** | New IntelliJ versions deprecate or remove APIs used by the plugin. | Low | Target 2024.3 (build 243) as minimum with `untilBuild` set to `253.*`. Use only stable, non-internal APIs. Run CI against the latest EAP builds. |
| 6 | **Prettier integration detection is fragile** | The plugin incorrectly detects or fails to detect Prettier configuration. | Low | Use a conservative detection strategy: only defer to Prettier when both a config file AND `prettier-plugin-rdn` in `node_modules` are found. Default to the built-in formatter otherwise. |
| 7 | **Markdown injection produces incorrect highlighting** | RDN code inside Markdown fenced blocks is not properly highlighted or produces false diagnostics. | Low | Test injection with various Markdown parsers (CommonMark, GFM). The `MultiHostInjector` only activates on `` ```rdn `` blocks, not on generic code blocks. |
| 8 | **TextMate grammar relocation breaks VSCode extension** | Moving grammars to `spec/textmate/` breaks the VSCode extension's build or packaging. | Low | Update the VSCode `package.json` grammar paths and test the `vsce package` command in CI. The VSCode extension uses `esbuild` which resolves relative paths at build time. |
| 9 | **JetBrains Marketplace initial review delay** | First plugin submission takes 1-2 weeks for manual review. | High (but schedule risk only) | Submit a minimal functional build early to start the review process. Iterate with updates after approval. |
| 10 | **Object key highlighting requires parser context** | The lexer cannot distinguish `"key": value` from `"value"` without parser context. | Medium | Implement a `RdnHighlightingLexer` wrapper that maintains a minimal state machine: after emitting `STRING_CLOSE`, peek at the next non-whitespace token -- if it is `COLON`, retroactively re-map the string tokens to `KEY_*` variants. This is a common pattern in JetBrains language plugins. |

## 12. Ordered Task List

1. **Scaffold Gradle project** -- Create `tools/jetbrains-plugin/` with `build.gradle.kts`, `settings.gradle.kts`, `gradle.properties`, `src/main/resources/META-INF/plugin.xml` skeleton, and `.gitignore` entries. Configure IntelliJ Platform Gradle Plugin 2.x, Kotlin 2.0, JDK 17 target, platform version 2024.3. (5 files)

2. **Create core plugin classes** -- Implement `RdnLanguage.kt` (Language singleton), `RdnFileType.kt` (LanguageFileType for `.rdn`), `RdnIcons.kt` (icon loader). Generate 16x16 and 13x13 SVG file icons from `assets/rdn-icon.svg`. Register in `plugin.xml`. (4 files + icon assets)

3. **Define token types** -- Create `RdnTokenTypes.kt` with all ~52 `IElementType` constants for every token the lexer will produce (structural, literals, strings, date/time, binary, regexp, etc.). (1 file)

4. **Implement JFlex lexer** -- Write `Rdn.flex` JFlex grammar with 5 states (YYINITIAL, STRING, REGEXP, REGEXP_CHAR_CLASS, BINARY). Implement all token rules per Section 7. Create `RdnLexerAdapter.kt` wrapping the generated lexer. Add Gradle JFlex generation task. (3 files)

5. **Write lexer tests** -- Create `RdnLexerTest.kt` with token stream assertions for all RDN token types: basic JSON, BigInts, dates, durations, binary literals, regexps (including sub-patterns and character classes), Map/Set keywords, special numbers, string escapes, and invalid characters. (1 file + test data)

6. **Implement syntax highlighter** -- Create `RdnSyntaxHighlighter.kt` mapping all token types to `TextAttributesKey` color keys. Create `RdnSyntaxHighlighterFactory.kt`. Implement `RdnHighlightingLexer.kt` wrapper that re-maps string tokens to key tokens when followed by `:`. Register in `plugin.xml`. (3 files)

7. **Create color settings page** -- Implement `RdnColorSettingsPage.kt` with a demo RDN text showing all token types. Define `AttributesDescriptor` entries for each highlightable element. Register in `plugin.xml`. (1 file)

8. **Define PSI element types** -- Create `RdnElementTypes.kt` with all PSI element type constants. Create `RdnFile.kt` (PsiFile subclass). (2 files)

9. **Implement GrammarKit BNF grammar** -- Write `Rdn.bnf` per Section 8. Run GrammarKit to generate parser and PSI classes. Implement custom `parseBrace()` method for brace disambiguation. Create `RdnParserDefinition.kt`. Add Gradle GrammarKit generation task. Register in `plugin.xml`. (3 files + generated code)

10. **Write parser tests** -- Create PSI tree assertion tests using `ParsingTestCase` with golden `.txt` files for all value types, brace disambiguation (object vs map vs set), nested structures, and error recovery. (1 file + test data)

11. **Implement Kotlin value parser for diagnostics** -- Write `RdnKotlinParser.kt`, a full-fidelity RDN parser producing `RdnValue` types. Implement with a 256-entry dispatch table, deferred string materialization, and all value types (strings, numbers, BigInts, dates, durations, TimeOnly, regexps, binary, objects, arrays, tuples, maps, sets). (2 files: parser + value model)

12. **Run conformance tests against test-suite** -- Create `RdnConformanceTest.kt` that reads all `test-suite/valid/*.rdn` and `*.expected.json` files, parses with `RdnKotlinParser`, serializes to JSON with `$type` convention, and asserts equality. Test all `test-suite/invalid/*.rdn` files throw `RdnSyntaxError`. Test roundtrip files. (1 file)

13. **Implement scanner (unquoted keys + binary validation)** -- Port `scanner.ts` to `RdnScanner.kt`. Implement `scanUnquotedKeys()` with brace context tracking (UnknownBrace, Object, Map, Set, ExplicitMap, ExplicitSet, Array, Tuple) and `scanBinaryErrors()` for base64/hex character validation. (1 file)

14. **Write scanner tests** -- Create `RdnScannerTest.kt` testing unquoted key detection in various contexts (objects, nested objects, not in maps/sets/arrays), brace disambiguation, and binary character validation for both base64 and hex encodings. (1 file)

15. **Implement ExternalAnnotator for diagnostics** -- Create `RdnExternalAnnotator.kt` implementing the 3-pass diagnostic pipeline: (1) unquoted keys, (2) binary errors, (3) full parse. Suppress parse errors on lines already covered by unquoted key diagnostics. Register in `plugin.xml`. (1 file)

16. **Implement quick fixes** -- Create `WrapKeyInQuotesQuickFix.kt` (single key) and `WrapAllKeysInQuotesQuickFix.kt` (bulk fix). Wire into the `ExternalAnnotator` via `LocalQuickFix` attachments on annotations. (2 files)

17. **Implement completion contributor** -- Create `RdnCompletionContributor.kt` with three providers: (1) `$schema` at top-level depth 1, (2) 11 keyword completions, (3) 12 snippet completions with tab stops. Implement string-context guard. Register in `plugin.xml`. (1 file)

18. **Write completion tests** -- Create `RdnCompletionTest.kt` testing `$schema` at correct depth, keywords outside strings, snippets, and suppression inside strings. (1 file)

19. **Implement CST parser for formatting** -- Port `tools/prettier-plugin-rdn/src/parser.ts` to `RdnCstParser.kt`. Produce `DocumentNode` trees with source positions. (1 file + CST node types)

20. **Implement CST formatter** -- Port `formatter.ts` to `RdnCstFormatter.kt`. Implement `format()` and `formatSorted()` with compact/multi-line printing, 80-char width threshold, configurable tab size and spaces/tabs. Implement `escapeString()` and `sortKeys()`. (1 file)

21. **Implement formatting model builder** -- Create `RdnFormattingModelBuilder.kt` that integrates `RdnCstFormatter` with IntelliJ's formatting API. Create `RdnPrettierDetector.kt` for Prettier fallback detection. Register in `plugin.xml`. (2 files)

22. **Write formatter tests** -- Create `RdnFormatterTest.kt` testing compact formatting, multi-line expansion, key sorting, tab vs spaces, explicit Map/Set keywords, and graceful handling of invalid input. (1 file)

23. **Implement format utilities** -- Port `format.ts` to `RdnFormatUtils.kt`. Implement token-based date formatter with `[literal]` escapes, duration expansion, digit grouping, and byte size formatting. (1 file)

24. **Implement binary utilities** -- Create `RdnBinaryUtils.kt` with base64/hex decoding, ASCII preview generation, and image format detection (PNG, JPEG, GIF, WebP, BMP, ICO magic bytes). (1 file)

25. **Implement DocumentationProvider for hover** -- Create `RdnDocumentationProvider.kt` with token detection logic (mirrors `detectToken()` in `hover.ts`) and HTML content generation for all 18 token kinds. Implement collection element counting, implicit map/set detection, regex flag expansion, and image preview embedding. Register in `plugin.xml`. (1 file)

26. **Implement settings state and configurable** -- Create `RdnSettingsState.kt` (PersistentStateComponent with all 19 settings) and `RdnSettingsConfigurable.kt` (UI panel with grouped sections using Kotlin DSL). Register in `plugin.xml`. (2 files)

27. **Implement bracket matching** -- Create `RdnBraceMatcher.kt` defining 3 bracket pairs (`{}`, `[]`, `()`). Register in `plugin.xml`. (1 file)

28. **Implement code folding** -- Create `RdnFoldingBuilder.kt` creating fold regions for objects, arrays, tuples, maps, and sets with placeholder text. Register in `plugin.xml`. (1 file)

29. **Implement Sort Document Keys action** -- Create `SortDocumentKeysAction.kt` that calls `RdnCstFormatter.formatSorted()` on the active editor's content. Register in `plugin.xml` with menu placement and `editorLangId == rdn` visibility. (1 file)

30. **Implement Markdown injection** -- Create `RdnMarkdownInjector.kt` detecting `` ```rdn `` fenced code blocks in Markdown files and injecting the RDN language. Register in `plugin.xml`. (1 file)

31. **Move TextMate grammars to spec/textmate/** -- Create `spec/textmate/` directory. Move `rdn.tmLanguage.json` and `rdn.markdown.tmLanguage.json` from `tools/vscode-extension/syntaxes/`. Update VSCode `package.json` grammar paths. Verify VSCode extension builds and packages correctly with the new paths. (4 files changed)

32. **Add CI workflows** -- Create `.github/workflows/jetbrains-ci.yml` for PR checks (`./gradlew check` on `tools/jetbrains-plugin/**` changes). Create `.github/workflows/jetbrains-release.yml` for tag-triggered releases with plugin signing and Marketplace publishing. (2 files)

33. **Update project documentation** -- Add JetBrains build/test commands to `CLAUDE.md`. Update root `README.md` to mention the JetBrains plugin alongside the VSCode extension. Add `tools/jetbrains-plugin/` to `.gitignore` for Gradle build directories. (3 files)
