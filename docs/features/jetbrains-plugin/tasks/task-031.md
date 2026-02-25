# Task 031: Move TextMate Grammars to spec/textmate/

## References
- [Tech Design](../tech-design.md) — Sections 4 (decision #10), 6.14
- [Discovery](../discovery.md)

## Description
Move the two TextMate grammar files from `tools/vscode-extension/syntaxes/` to `spec/textmate/`. Update the VSCode extension's `package.json` to reference the new paths. Verify the VSCode extension still builds and packages correctly. The JetBrains plugin does not use these grammar files (it has a native JFlex lexer), but the shared location benefits any future language support consumers.

## Files to Create/Modify
- `spec/textmate/rdn.tmLanguage.json` — Moved from `tools/vscode-extension/syntaxes/rdn.tmLanguage.json`
- `spec/textmate/rdn.markdown.tmLanguage.json` — Moved from `tools/vscode-extension/syntaxes/rdn.markdown.tmLanguage.json`
- `tools/vscode-extension/package.json` — Update grammar paths to point to `spec/textmate/`
- `tools/vscode-extension/syntaxes/rdn.tmLanguage.json` — Delete (moved)
- `tools/vscode-extension/syntaxes/rdn.markdown.tmLanguage.json` — Delete (moved)

## Implementation Details

### File moves

```bash
mkdir -p spec/textmate
git mv tools/vscode-extension/syntaxes/rdn.tmLanguage.json spec/textmate/rdn.tmLanguage.json
git mv tools/vscode-extension/syntaxes/rdn.markdown.tmLanguage.json spec/textmate/rdn.markdown.tmLanguage.json
```

### VSCode `package.json` grammar path updates

Update the `grammars` array in `tools/vscode-extension/package.json` from:

```json
"grammars": [
  {
    "language": "rdn",
    "scopeName": "source.rdn",
    "path": "./syntaxes/rdn.tmLanguage.json"
  },
  {
    "scopeName": "markdown.rdn.codeblock",
    "path": "./syntaxes/rdn.markdown.tmLanguage.json",
    "injectTo": ["text.html.markdown"],
    "embeddedLanguages": { "meta.embedded.block.rdn": "rdn" }
  }
]
```

To:

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

### VSCode build verification

The VSCode extension uses `esbuild` which resolves file paths at build time. The grammar files referenced in `package.json` are also bundled by `vsce` (VS Code Extension Manager). After updating paths, verify:

1. `pnpm --filter rdn build` succeeds (esbuild compilation, which doesn't include grammar files directly)
2. `pnpm --filter rdn package` (or `vsce package`) resolves the grammar paths and includes them in the `.vsix` bundle

If the `vsce` build fails because the grammar paths are outside the extension root, check whether `vsce` requires a `"files"` field in `package.json` to include files from parent directories. If necessary, add the grammar files to the extension's `files` array:

```json
"files": [
  "dist/**",
  "../../spec/textmate/**"
]
```

Alternatively, check the `.vscodeignore` file and ensure `../../spec/textmate/` is not excluded.

**Important:** The VSCode Marketplace `vsce package` command does NOT follow symbolic links by default. If relative paths outside the workspace fail, the fallback is to keep copies of the grammars in `syntaxes/` (as symlinks) while the canonical copies live in `spec/textmate/`. However, the preferred solution is to make the relative path work with `vsce`.

### Verify no references to old paths

After the move, search for any remaining references to the old paths:

```bash
grep -r "syntaxes/rdn.tmLanguage" .
grep -r "syntaxes/rdn.markdown" .
```

Only `.gitignore` or build artifact references should remain (none expected).

## Acceptance Criteria
- [ ] `spec/textmate/rdn.tmLanguage.json` exists and contains the full grammar
- [ ] `spec/textmate/rdn.markdown.tmLanguage.json` exists and contains the Markdown injection grammar
- [ ] `tools/vscode-extension/syntaxes/rdn.tmLanguage.json` no longer exists
- [ ] `tools/vscode-extension/syntaxes/rdn.markdown.tmLanguage.json` no longer exists
- [ ] `pnpm --filter rdn build` succeeds
- [ ] `pnpm --filter rdn package` produces a `.vsix` file that, when installed, provides RDN syntax highlighting
- [ ] The `spec/textmate/` directory is committed to git (not gitignored)
- [ ] No remaining references to `syntaxes/rdn.tmLanguage.json` in the codebase

## Dependencies
- Depends on: None (this is a file restructuring task independent of the plugin)
- Blocks: None
