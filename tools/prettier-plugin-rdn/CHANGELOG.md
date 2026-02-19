# prettier-plugin-rdn

## 0.1.0

### Added

- **Prettier plugin** for `.rdn` files with full RDN syntax support
- Smart line-breaking: short containers stay inline, long ones break at `printWidth`
- Canonical string escaping and trailing newline enforcement
- All RDN-specific literals preserved as-is (dates, times, durations, regex, binary, bigints)
- Empty containers always compact: `{}`, `[]`, `()`, `Map{}`, `Set{}`
- **RDN-specific options:**
  - `sortKeys` — alphabetically sort object keys recursively
  - `useExplicitMapKeyword` — keep `Map{...}` keyword on non-empty maps
  - `useExplicitSetKeyword` — keep `Set{...}` keyword on non-empty sets
- JSON schema for `.prettierrc` autocompletion of RDN options
