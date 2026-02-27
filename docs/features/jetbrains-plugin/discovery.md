# Discovery: JetBrains Plugin for RDN

## 1. Overview

Build a JetBrains IDE plugin that provides first-class support for RDN (Rich Data Notation) files across all IntelliJ-based IDEs (IntelliJ IDEA, WebStorm, PyCharm, CLion, GoLand, Rider, PhpStorm, RubyMine, DataGrip, RustRover). The plugin should achieve feature parity with the existing VSCode extension, which provides syntax highlighting for 20+ token types, real-time diagnostics with quick fixes, document formatting, completions, hover information, and configurable settings.

RDN is a JSON superset with native representations for dates, BigInts, regular expressions, binary data, Maps, Sets, tuples, TimeOnly, Duration, and special numeric values (NaN, Infinity). Any valid JSON is valid RDN.

## 2. Current Behavior & Code References

### VSCode Extension Location

The extension lives at `tools/vscode-extension/` in the monorepo and is registered as a pnpm workspace package.

### Source File Inventory

| File | Role | Lines | Dependencies |
|------|------|-------|-------------|
| `src/extension.ts` | Activation, diagnostics (3-pass), quick fixes, formatting, completions ($schema + 11 keywords + 12 snippets), hover registration, sort command | ~335 | `@rdn/parser`, `./scanner`, `./formatter`, `./hover`, `./config` |
| `src/scanner.ts` | Lightweight scanner for unquoted key detection and binary character validation. Tracks brace context (Object/Map/Set/Array/Tuple) for disambiguation. | ~410 | None (standalone) |
| `src/hover.ts` | Hover provider with 18 token kinds (dateTimeFull, dateTimeNoMillis, dateOnly, unixTimestamp, timeOnly, duration, bigint, binaryBase64, binaryHex, regexp, nan, infinity, negInfinity, mapKeyword, setKeyword, mapArrow, tuple, implicitMap/Set). Includes image detection, base64/hex decoding, regex flag expansion, collection element counting. | ~900 | `./config`, `./format` |
| `src/formatter.ts` | CST-based formatter using `@rdn/cst-parser`. Compact vs multi-line printing with 80-char width. Sort keys command. | ~188 | `@rdn/cst-parser`, `@rdn/cst-types` |
| `src/format.ts` | Date/time/number formatting utilities for hover tooltips. Token-based date formatter, duration expansion, digit grouping, byte size formatting. | ~175 | None (standalone) |
| `src/config.ts` | Cached hover configuration with 13 toggle/format settings. | ~66 | `vscode` API only |
| `spec/textmate/rdn.tmLanguage.json` | TextMate grammar with 30+ rules covering all RDN types, including full RegExp sub-pattern highlighting (character classes, lookarounds, named groups, quantifiers, anchors, alternation, backreferences). Shared across editors. | ~700 | N/A |
| `spec/textmate/rdn.markdown.tmLanguage.json` | Markdown code fence injection grammar for ` ```rdn ` blocks. Shared across editors. | ~35 | Injects `source.rdn` |
| `language-configuration.json` | Bracket matching, auto-closing pairs, indentation rules, word pattern. | ~25 | N/A |

### Parser Packages Used

| Import Alias | Actual Source | Purpose |
|-------------|--------------|---------|
| `@rdn/parser` | `packages/rdn-js/src/parser.ts` | Full RDN parser producing `RDNValue` (used for diagnostics) |
| `@rdn/cst-parser` | `tools/prettier-plugin-rdn/src/parser.ts` | CST parser producing `RdnCstNode` tree with source positions (used for formatting) |
| `@rdn/cst-types` | `tools/prettier-plugin-rdn/src/cst.ts` | CST node type definitions (DocumentNode, RdnCstNode, etc.) |

Both parsers are TypeScript/ESM-only, zero-dependency, recursive-descent parsers with module-scoped cursor state and a 256-entry token dispatch table.

### TextMate Grammar Token Coverage

The existing `rdn.tmLanguage.json` covers these scopes:
- `constant.language.null/boolean.true/boolean.false.rdn`
- `constant.numeric.integer/float/bigint/nan/infinity/date/time/milliseconds/duration.*.rdn`
- `string.quoted.double.rdn`, `string.other.binary.base64/hex.rdn`, `string.regexp.rdn`
- `keyword.operator.new.datetime.rdn`, `keyword.other.binary.prefix/map/set.rdn`
- `keyword.control.duration/unit.*.rdn`
- `punctuation.definition.string/object/array/tuple/map/set/regexp/group.*.rdn`
- `punctuation.separator.arrow/comma/colon.rdn`
- `support.type.property-name.rdn` (object keys)
- `invalid.illegal.bad-base64-char/bad-hex-char/unrecognized-escape.rdn`
- Full RegExp sub-patterns: `keyword.operator.quantifier/lookaround/or/negation/range.regexp.rdn`, `constant.other.character-class.*.regexp.rdn`, `constant.character.escape.regexp.rdn`, `keyword.other.back-reference/named-capture/non-capturing.regexp.rdn`, `keyword.control.anchor.regexp.rdn`

## 3. Target JetBrains Platform

| Property | Value |
|----------|-------|
| **Target IDEs** | All IntelliJ-based IDEs (IntelliJ IDEA, WebStorm, PyCharm, CLion, GoLand, Rider, PhpStorm, RubyMine, DataGrip, RustRover) |
| **Minimum Platform Version** | 2024.2 (build 242.*) -- broad compatibility; LSP features require 2024.2+ for full capability set |
| **Plugin Type** | IntelliJ Platform Plugin |
| **Language** | Kotlin |
| **Build System** | Gradle with IntelliJ Platform Gradle Plugin 2.x |
| **Plugin ID** | `com.rdn.intellij` |
| **Plugin Template** | [JetBrains/intellij-platform-plugin-template](https://github.com/JetBrains/intellij-platform-plugin-template) |

## 4. Feature Parity Matrix

| # | VSCode Feature | VSCode Implementation | JetBrains Equivalent API | Approach A (Full Custom) | Approach B (TextMate + Annotators) | Approach C (LSP) |
|---|---------------|----------------------|--------------------------|--------------------------|-----------------------------------|--------------------|
| 1 | File type registration (`.rdn`) | `contributes.languages` in `package.json` | `com.intellij.fileType` extension point + `LanguageFileType` | Native | Native | Native (still need file type EP) |
| 2 | Bracket/quote auto-closing | `language-configuration.json` | `com.intellij.lang.braceMatcher` + `BracePairProvider` | Native | Partial (TextMate supports basic pairs) | Not covered by LSP |
| 3 | Indentation rules | `indentationRules` in language config | `com.intellij.lang.formatter` + `FormattingModelBuilder` | Native | Not supported by TextMate | Not covered by LSP |
| 4 | Syntax highlighting (20+ token types) | `rdn.tmLanguage.json` (TextMate grammar) | JFlex Lexer + `SyntaxHighlighter` or TextMate bundle | Native (JFlex-based, full control) | Reuse existing `.tmLanguage.json` directly | TextMate bundle for highlighting + LSP for semantic tokens |
| 5 | RegExp sub-pattern highlighting | Nested TextMate rules (12 sub-patterns) | Nested lexer states or TextMate | Native (complex JFlex states) | Reuse TextMate grammar (full fidelity) | TextMate grammar handles this |
| 6 | Markdown code fence injection | `rdn.markdown.tmLanguage.json` injected into `text.html.markdown` | `com.intellij.lang.injection.MultiHostInjector` or TextMate injection | Native (MultiHostInjector) | TextMate injection grammar | Not supported by LSP |
| 7 | Real-time diagnostics (3 passes) | `@rdn/parser` parse errors, `scanUnquotedKeys()`, `scanBinaryErrors()` | `com.intellij.lang.annotation.ExternalAnnotator` or `Inspection` | Native (ExternalAnnotator calling Kotlin parser) | Native (ExternalAnnotator with Kotlin parser) | LSP `textDocument/publishDiagnostics` |
| 8 | Quick fixes (wrap unquoted keys) | `CodeActionProvider` with `WorkspaceEdit` | `com.intellij.codeInsight.intention.IntentionAction` or `LocalQuickFix` | Native | Native | LSP `textDocument/codeAction` |
| 9 | Document formatting | CST-based formatter via `@rdn/cst-parser` | `com.intellij.formatting.FormattingModelBuilder` | Native (rewrite formatter in Kotlin) | Native (rewrite formatter in Kotlin) | LSP `textDocument/formatting` |
| 10 | Sort Document Keys command | `rdn.sortDocument` command | `com.intellij.openapi.actionSystem.AnAction` | Native | Native | Custom command (outside LSP) |
| 11 | `$schema` completion | `CompletionItemProvider` with context-aware brace depth check | `com.intellij.codeInsight.completion.CompletionContributor` | Native | Native | LSP `textDocument/completion` |
| 12 | Keyword completions (11 keywords) | `CompletionItemProvider` with string-context guard | `CompletionContributor` | Native | Native | LSP `textDocument/completion` |
| 13 | Snippet completions (12 snippets) | `CompletionItemProvider` with `SnippetString` | `com.intellij.codeInsight.template.impl.LiveTemplateCompletionContributor` or `CompletionContributor` | Native (Live Templates) | Native (Live Templates) | LSP snippets (limited) |
| 14 | Hover information (16 token types) | `HoverProvider` with `MarkdownString`, inline image previews | `com.intellij.lang.documentation.DocumentationProvider` | Native | Native | LSP `textDocument/hover` |
| 15 | Configurable settings (13 toggles/formats) | `contributes.configuration` in `package.json` | `com.intellij.openapi.options.Configurable` + `PersistentStateComponent` | Native | Native | Server-side config (less integrated) |

### Feature Coverage Summary

| Approach | Features Fully Covered | Features Partially Covered | Features Not Covered |
|----------|----------------------|---------------------------|---------------------|
| **A: Full Custom** | 15/15 | 0 | 0 |
| **B: TextMate + Annotators** | 13/15 | 1 (indentation) | 1 (indentation rules not supported by TextMate) |
| **C: LSP-Based** | 10/15 | 3 (bracket matching, indentation, snippets) | 2 (markdown injection, deep settings integration) |

## 5. Architectural Approaches

### Approach A: Full Custom Plugin

Build a complete IntelliJ platform plugin with a JFlex-based lexer, GrammarKit BNF parser, full PSI tree, and native implementations of all features.

**Architecture:**
```
tools/jetbrains-plugin/
  src/main/kotlin/com/rdn/intellij/
    RdnLanguage.kt              # Language singleton
    RdnFileType.kt              # File type registration
    RdnIcons.kt                 # Icon constants
    lexer/
      RdnLexer.flex             # JFlex grammar (~300-400 rules for all RDN tokens + RegExp states)
      RdnTokenTypes.kt          # IElementType definitions
    parser/
      Rdn.bnf                   # GrammarKit BNF grammar
      RdnParserDefinition.kt    # ParserDefinition EP
    psi/                        # Generated + custom PSI elements
    highlighting/
      RdnSyntaxHighlighter.kt   # Maps tokens to TextAttributesKey
      RdnColorSettingsPage.kt   # Settings > Editor > Color Scheme
    annotator/
      RdnAnnotator.kt           # Parse error + unquoted key + binary validation
    quickfix/
      WrapKeyInQuotesQuickFix.kt
    completion/
      RdnCompletionContributor.kt   # Keywords, snippets, $schema
    formatter/
      RdnFormattingModelBuilder.kt   # CST-based formatter rewrite
      RdnBlock.kt
    documentation/
      RdnDocumentationProvider.kt    # Hover-equivalent
    actions/
      SortDocumentKeysAction.kt
    settings/
      RdnSettingsConfigurable.kt
      RdnSettingsState.kt
    injection/
      RdnMarkdownInjector.kt    # Markdown code fence injection
    braceMatcher/
      RdnBraceMatcher.kt
  src/main/resources/
    META-INF/plugin.xml
    fileTemplates/
    icons/
```

**Pros:**
- Full control over PSI tree, enabling all advanced IDE features (refactoring, structure view, find usages, etc.)
- Best performance (native JVM lexer/parser, no external process)
- Deep integration with IntelliJ's code model (type inference, cross-references)
- Works in all editions (Community, Ultimate, all IDEs) with no restrictions
- Can evolve to support `.rdn` schema validation, go-to-definition for `$schema`, etc.

**Cons:**
- Highest development effort (~4-6 weeks for a single developer)
- Must rewrite the full RDN parser in Kotlin (the spec is non-trivial with brace disambiguation, 17 value types)
- JFlex lexer for RegExp sub-patterns is complex (~12 nested states)
- Risk of parser divergence from the TypeScript reference implementation
- Must be maintained separately from the TS parser; spec changes require dual updates

**Effort: High** (4-6 weeks)
**Feature Coverage: 100%**

### Approach B: TextMate + Custom Annotators

Use the existing `rdn.tmLanguage.json` grammar for syntax highlighting (bundled inside the plugin) and build custom Kotlin annotators, completions, formatter, and hover providers for everything beyond highlighting.

**Architecture:**
```
tools/jetbrains-plugin/
  src/main/kotlin/com/rdn/intellij/
    RdnLanguage.kt
    RdnFileType.kt
    textmate/
      RdnTextMateRegistration.kt   # Registers bundled .tmLanguage.json
    parser/
      RdnLightParser.kt            # Lightweight parser for diagnostics (Kotlin rewrite of scanner.ts)
      RdnCstParser.kt              # CST parser for formatting (Kotlin rewrite of cst-parser)
    annotator/
      RdnExternalAnnotator.kt      # 3-pass diagnostics using RdnLightParser
    quickfix/
      WrapKeyInQuotesQuickFix.kt
    completion/
      RdnCompletionContributor.kt
    formatter/
      RdnFormatter.kt              # Rewrite of formatter.ts in Kotlin
    documentation/
      RdnDocumentationProvider.kt   # Rewrite of hover.ts in Kotlin
    actions/
      SortDocumentKeysAction.kt
    settings/
      RdnSettingsConfigurable.kt
      RdnSettingsState.kt
  src/main/resources/
    META-INF/plugin.xml
    textmate/
      rdn.tmLanguage.json           # Bundled from vscode-extension
      rdn.markdown.tmLanguage.json
```

**Pros:**
- Reuses the battle-tested TextMate grammar for highlighting (identical colors to VSCode)
- Moderate development effort since syntax highlighting is "free"
- Still provides native JetBrains feel for completions, formatting, hover
- TextMate grammar changes automatically propagate to both extensions
- No need to write a JFlex lexer (~300-400 rules) or GrammarKit BNF

**Cons:**
- TextMate highlighting in JetBrains has limitations: no semantic highlighting, slightly different color mapping, no PSI tree
- Still must rewrite the RDN parser (at least a lightweight one) in Kotlin for diagnostics and formatting
- Indentation rules from `language-configuration.json` are not supported by TextMate bundles in JetBrains
- TextMate bundles must be on disk (not in JAR); requires extraction at plugin load or plugin-provided directory
- No structural PSI tree means no structure view, no smart rename, no find usages on keys
- Two-tier architecture: TextMate for highlighting + Kotlin for everything else adds conceptual complexity

**Effort: Medium** (2-4 weeks)
**Feature Coverage: ~90%** (missing indentation rules, limited PSI-dependent features)

### Approach C: LSP-Based

Create a standalone RDN Language Server in TypeScript (reusing the existing `@rdn/parser` and `@rdn/cst-parser`) and connect to it via IntelliJ's built-in LSP client API.

**Architecture:**
```
tools/rdn-language-server/        # New package: standalone TS language server
  src/
    server.ts                     # LSP server entry (uses vscode-languageserver)
    capabilities/
      diagnostics.ts              # Reuses @rdn/parser + scanner logic
      completion.ts               # Keywords, snippets, $schema
      formatting.ts               # Reuses @rdn/cst-parser formatter
      hover.ts                    # Reuses hover.ts logic
      codeAction.ts               # Quick fixes
  package.json
  tsconfig.json

tools/jetbrains-plugin/           # Thin JetBrains wrapper
  src/main/kotlin/com/rdn/intellij/
    RdnLanguage.kt
    RdnFileType.kt
    lsp/
      RdnLspServerSupportProvider.kt   # Starts and connects to the TS language server
    textmate/
      RdnTextMateRegistration.kt       # For syntax highlighting (TextMate bundle)
    settings/
      RdnSettingsConfigurable.kt       # Passes config to LSP server
  src/main/resources/
    META-INF/plugin.xml
    textmate/
      rdn.tmLanguage.json
    server/                             # Bundled language server (compiled JS)
      server.js
      node_modules/
```

**Pros:**
- Maximum code reuse: the language server reuses the exact TypeScript parsers, scanner, formatter, and hover logic
- Single source of truth: parser changes only need to happen once in TypeScript
- VSCode extension could also migrate to use the language server (shared logic)
- Smaller Kotlin footprint (mostly bootstrapping)
- LSP is a well-understood protocol with broad tooling support

**Cons:**
- Requires Node.js runtime on the user's machine (or bundled)
- LSP API availability in JetBrains was historically limited to Ultimate editions; as of 2025.2 it is available to all IntelliJ IDEA users, but Community Edition is being sunset
- Inter-process communication overhead for every keystroke (typing latency)
- LSP does not cover: bracket matching, indentation rules, markdown injection, Live Templates/snippets with tab stops, deep settings integration
- Syntax highlighting still needs a TextMate bundle or JFlex lexer (LSP semantic tokens alone are insufficient)
- Requires maintaining a new `rdn-language-server` package
- Node.js process lifecycle management adds complexity (startup, crash recovery, version management)
- LSP hover returns Markdown strings, but JetBrains `DocumentationProvider` expects HTML -- mapping layer needed
- Two runtime environments (JVM + Node.js) increase system requirements and deployment complexity

**Effort: Medium-High** (3-5 weeks -- new language server package + thin JetBrains wrapper)
**Feature Coverage: ~70%** (missing bracket matching, indentation, markdown injection, settings depth, snippet tab stops)

## 6. Recommended Approach

**Recommendation: Approach B (TextMate + Custom Annotators)** with a planned migration path to Approach A for specific components.

### Rationale

1. **Fastest path to a usable plugin.** Reusing the TextMate grammar eliminates the most tedious part of language support (writing a JFlex lexer with 30+ regex rules and 12 nested states for RegExp sub-patterns). The existing grammar is well-tested and already covers 100% of RDN's syntax.

2. **Still feels native.** Completions, formatting, hover, diagnostics, and quick fixes are all implemented in Kotlin using the native IntelliJ APIs. Users will not perceive a difference from a "full custom" plugin for these features.

3. **Shared grammar asset.** Changes to `rdn.tmLanguage.json` automatically benefit both the VSCode and JetBrains extensions, reducing maintenance.

4. **Incremental path to Approach A.** If PSI-dependent features (structure view, find usages, smart rename) are needed later, a JFlex lexer + GrammarKit parser can be added alongside the TextMate grammar. JetBrains allows both to coexist, with the native lexer taking precedence when available.

5. **Avoids LSP complexity.** Approach C requires maintaining a separate language server package, bundling Node.js, and dealing with inter-process communication. The overhead is not justified for a language with a relatively simple grammar (no type system, no imports, no cross-file references).

### What Still Needs Rewriting in Kotlin

Even with Approach B, the following TypeScript logic must be ported to Kotlin:

| Component | Source | Estimated Kotlin Lines | Complexity |
|-----------|--------|----------------------|------------|
| Unquoted key scanner | `scanner.ts` `scanUnquotedKeys()` | ~200 | Medium (brace disambiguation) |
| Binary char validator | `scanner.ts` `scanBinaryErrors()` | ~80 | Low |
| RDN value parser (for diagnostics) | `packages/rdn-js/src/parser.ts` `parse()` | ~500 | High (full spec compliance) |
| CST parser (for formatting) | `tools/prettier-plugin-rdn/src/parser.ts` | ~500 | High |
| Formatter | `src/formatter.ts` | ~150 | Medium |
| Hover token detection | `src/hover.ts` `detectToken()` | ~300 | Medium |
| Hover content generation | `src/hover.ts` `RdnHoverProvider` | ~250 | Medium |
| Date/format utilities | `src/format.ts` | ~120 | Low |

**Total estimated Kotlin lines: ~2,100**

### Migration Path to Approach A

If the plugin matures and PSI-dependent features are requested:

1. **Phase 1 (now):** Ship with TextMate grammar + Kotlin annotators/completions/formatter/hover.
2. **Phase 2 (future):** Add a JFlex lexer that produces the same token types as the TextMate grammar. Register it as the primary lexer; TextMate becomes the fallback for markdown injection.
3. **Phase 3 (future):** Add a GrammarKit BNF parser to produce a PSI tree. Enable structure view, breadcrumbs, find usages on object keys.

## 7. Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| **Language** | Kotlin | 2.0+ |
| **Build System** | Gradle with IntelliJ Platform Gradle Plugin | 2.x (2.11+) |
| **Gradle Version** | Gradle | 8.13+ |
| **Java Target** | JDK | 17+ |
| **Platform SDK** | IntelliJ Platform | 2024.2+ (build 242.*) |
| **Since Build** | `242` | |
| **Until Build** | `253.*` (or unset for forward compat) | |
| **Testing** | JUnit 5 + IntelliJ test framework (`BasePlatformTestCase`) | |
| **TextMate Grammar** | Bundled `rdn.tmLanguage.json` (from `spec/textmate/`) | |
| **CI/CD** | GitHub Actions (new workflow: `jetbrains-release.yml`) | |
| **Distribution** | JetBrains Marketplace | |
| **Plugin Template** | [intellij-platform-plugin-template](https://github.com/JetBrains/intellij-platform-plugin-template) | |

### Plugin Directory Structure

```
tools/jetbrains-plugin/
  build.gradle.kts
  gradle.properties
  settings.gradle.kts
  src/
    main/
      kotlin/com/rdn/intellij/
        ...
      resources/
        META-INF/
          plugin.xml
          pluginIcon.svg
        textmate/
          rdn.tmLanguage.json
          rdn.markdown.tmLanguage.json
    test/
      kotlin/com/rdn/intellij/
        parser/
          RdnParserTest.kt       # Uses test-suite/valid/*.rdn and test-suite/invalid/*.rdn
        formatter/
          RdnFormatterTest.kt
        completion/
          RdnCompletionTest.kt
      testData/
        ...
```

### Monorepo Integration

- Add `tools/jetbrains-plugin` to the repo (not to `pnpm-workspace.yaml` since it is a Gradle project)
- The Gradle project is standalone but lives in the same monorepo
- CI: new GitHub Actions workflow that runs `./gradlew check` on PRs touching `tools/jetbrains-plugin/**`
- TextMate grammar: copy or symlink from `spec/textmate/` during build (Gradle task)
- Conformance tests: reference `test-suite/valid/*.rdn` and `test-suite/invalid/*.rdn` for parser validation
- Changesets: not applicable (Gradle project uses its own versioning via `gradle.properties`)

## 8. Blast Radius

### Existing Code Affected

| File/Package | Impact |
|-------------|--------|
| `pnpm-workspace.yaml` | No change (Gradle project is separate) |
| `turbo.json` | No change |
| `.github/workflows/ci.yml` | Add new `jetbrains:` job for `./gradlew check` |
| `.github/workflows/` | Add new `jetbrains-release.yml` workflow |
| `spec/textmate/rdn.tmLanguage.json` | Shared asset -- changes affect both extensions |
| `spec/textmate/rdn.markdown.tmLanguage.json` | Shared asset |
| `test-suite/` | Conformance tests used by both TS and Kotlin parsers |
| `spec/grammar.ebnf` | Reference for Kotlin parser implementation |
| `.gitignore` | Add Gradle build directories (`tools/jetbrains-plugin/.gradle/`, `tools/jetbrains-plugin/build/`) |
| `README.md` | Update to mention JetBrains plugin alongside VSCode extension |
| `CLAUDE.md` | Add JetBrains build/test commands |

### Integration Points

1. **TextMate Grammar (shared):** The `rdn.tmLanguage.json` file in `spec/textmate/` is the single source of truth for both extensions' syntax highlighting. A Gradle build task should copy this file from the shared location into the JetBrains plugin's resources.

2. **Conformance Test Suite (shared):** The Kotlin parser should be validated against the same `test-suite/valid/*.rdn` + `*.expected.json` and `test-suite/invalid/*.rdn` files used by the TypeScript and C# implementations.

3. **Specification (shared):** The `spec/grammar.ebnf` and `spec/rdn-spec.md` are the authoritative references for parser behavior. The Kotlin parser must implement identical disambiguation logic (brace lookahead: `:` -> Object, `=>` -> Map, `,`/`}` -> Set, empty `{}` -> Object).

## 9. Edge Cases & Risks

### TextMate Grammar in JetBrains

- **Bundle registration:** TextMate bundles in IntelliJ must be actual files on disk, not resources inside a JAR. The plugin must extract the `.tmLanguage.json` to a temporary directory or use the `com.intellij.openapi.extensions.ExtensionPointName` for TextMate registration.
- **Color mapping:** TextMate scopes map to IntelliJ's `TextAttributesKey` through a theme-dependent mapping. Some scopes (particularly the highly granular RDN-specific ones like `constant.numeric.duration.year.rdn`) may not have default color mappings and will need custom `additionalTextAttributes` entries.
- **No folding from TextMate:** Code folding must be implemented separately via `FoldingBuilder`.

### Parser Divergence Risk

- The Kotlin parser is a separate implementation from the TypeScript parser. Spec changes require updates in both places.
- **Mitigation:** Run both parsers against the shared `test-suite/` conformance suite in CI. Any new test case must pass in all implementations.

### Brace Disambiguation Complexity

RDN's `{` can start an Object, Map, or Set. The parser must look ahead past the first value to find `:` (Object), `=>` (Map), or `,`/`}` (Set). This lookahead logic is non-trivial and must exactly match the spec. The existing TypeScript scanner (`scanner.ts`) implements this as a stack-based state machine.

### RegExp Literal Ambiguity

`/` can be a division operator in JSON numeric contexts or the start of a regex. In RDN (where division doesn't exist), `/` always starts a regex in value position, but the parser must correctly handle edge cases like `/` inside strings.

### Cross-IDE Compatibility

- The plugin targets all IntelliJ-based IDEs via `com.intellij.modules.platform` dependency.
- Must not depend on any IDE-specific module (e.g., `com.intellij.modules.java`).
- Test on at least IntelliJ IDEA and WebStorm to verify broad compatibility.

### JetBrains Marketplace Review

- First-time plugin submissions undergo manual review (1-2 weeks).
- Plugin must not bundle excessive dependencies or violate marketplace guidelines.
- Plugin signing is required for marketplace distribution.

### Performance

- The diagnostics annotator runs on every keystroke (debounced). The Kotlin parser must be fast enough for large RDN files (>10K lines).
- The formatter re-parses the entire document. CST parsing should complete in <100ms for typical files.

## 10. Resolved Decisions

| # | Question | Decision |
|---|----------|----------|
| 1 | **Approach** | **Approach A: Full Custom** — JFlex lexer + GrammarKit parser + full PSI tree. 100% feature coverage. |
| 2 | **Plugin name** | **RDN** (matching VSCode extension name) |
| 3 | **Grammar sharing** | Move TextMate grammars to **shared `spec/textmate/`** directory. Both VSCode and JetBrains extensions reference from there. |
| 4 | **Kotlin parser scope** | **Full-fidelity parser** — complete RDN parser in Kotlin, publishable as standalone library to Maven Central. |
| 5 | **Settings** | **All 13 settings from day one** — full parity with VSCode extension. |
| 6 | **Minimum platform version** | **2024.3 (build 243)** |
| 7 | **Versioning** | **Synced with VSCode extension** — same version numbers, coordinated releases. |
| 8 | **Prettier integration** | **Defer to Prettier** when the Prettier RDN plugin is available; use built-in formatter otherwise. |
| 9 | **Conformance tests** | Reference `test-suite/` directly from Gradle (no copy). Gradle task reads test files from repo root. |
| 10 | **Icon assets** | Generate JetBrains-sized icons (40x40, 16x16 SVG) from existing `assets/rdn-icon.svg`. |
