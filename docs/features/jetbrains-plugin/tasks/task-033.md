# Task 033: Update Project Documentation

## References
- [Tech Design](../tech-design.md) — Section 12 (task 33)
- [Discovery](../discovery.md)

## Description
Update project documentation to reflect the new JetBrains plugin. Add JetBrains build/test commands to `CLAUDE.md`. Update the root `README.md` to mention the JetBrains plugin alongside the VSCode extension. Add `tools/jetbrains-plugin/` Gradle build directories to `.gitignore`.

## Files to Create/Modify
- `CLAUDE.md` — Add JetBrains build/test commands to the Build & Test Commands section
- `README.md` — Add JetBrains plugin mention alongside VSCode extension section
- `.gitignore` — Add Gradle build directories for the new module

## Implementation Details

### `CLAUDE.md` additions

Add a new subsection under the existing "Build & Test Commands" section:

```markdown
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
```

### `README.md` additions

Find the section that describes the VSCode extension and add a JetBrains plugin entry. The new content should read approximately:

```markdown
## IDE Extensions

### VSCode Extension
Install from the [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=rdn.rdn).
- Syntax highlighting for all 20+ RDN token types
- Real-time diagnostics with quick fixes
- IntelliSense completions
- Hover documentation
- Document formatting (built-in or via Prettier)
- Sort Document Keys command

Source: [`tools/vscode-extension/`](tools/vscode-extension/)

### JetBrains Plugin
Install from the [JetBrains Marketplace](https://plugins.jetbrains.com/plugin/XXXXX-rdn).
Works with IntelliJ IDEA, WebStorm, PyCharm, CLion, GoLand, Rider, PhpStorm, RubyMine, DataGrip, and RustRover (version 2024.3+).
- Full feature parity with the VSCode extension
- Native JFlex lexer + GrammarKit parser for deep IDE integration
- Bracket matching and code folding
- Markdown injection for ```rdn code blocks

Source: [`tools/jetbrains-plugin/`](tools/jetbrains-plugin/)
```

### `.gitignore` additions

```gitignore
# JetBrains plugin Gradle build artifacts
tools/jetbrains-plugin/.gradle/
tools/jetbrains-plugin/build/
tools/jetbrains-plugin/.idea/
tools/jetbrains-plugin/src/main/gen/
```

Note: `src/main/gen/` contains generated code from JFlex and GrammarKit. Whether this directory should be gitignored or committed depends on the project's policy for generated code. The recommended approach is to gitignore it (force developers to run `./gradlew generateLexer generateParser` before building) and regenerate in CI. Alternatively, commit the generated files to avoid a required generation step.

### Additional documentation considerations

If a `docs/` directory contains user-facing documentation beyond `CLAUDE.md`, update it accordingly:

1. Add `tools/jetbrains-plugin/README.md` with installation and development instructions — **only create this file if explicitly requested** (per global CLAUDE.md instructions: "NEVER proactively create documentation files").
2. Update any contribution guides to mention the JetBrains plugin build requirements (JDK 17, Gradle wrapper).
3. Update the monorepo's `pnpm-workspace.yaml` if the JetBrains plugin uses any JS/TS tooling (it does not, so no update needed).

## Acceptance Criteria
- [ ] `CLAUDE.md` Build & Test section includes JetBrains `./gradlew` commands
- [ ] `CLAUDE.md` mentions the `tools/jetbrains-plugin/` directory
- [ ] `README.md` (or equivalent root documentation) references the JetBrains plugin alongside VSCode
- [ ] `.gitignore` excludes `tools/jetbrains-plugin/.gradle/` and `tools/jetbrains-plugin/build/`
- [ ] `.gitignore` excludes `tools/jetbrains-plugin/src/main/gen/` (generated code)
- [ ] No dead links in the updated `README.md` (Marketplace URL placeholder is clearly marked)
- [ ] `CLAUDE.md` test commands are accurate and runnable

## Dependencies
- Depends on: task-001
- Blocks: None
