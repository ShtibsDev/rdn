# prettier-plugin-rdn

A [Prettier](https://prettier.io/) plugin for formatting [RDN (Rich Data Notation)](../../spec/rdn-spec.md) files.

## Installation

```bash
npm install --save-dev prettier prettier-plugin-rdn
```

## Usage

### CLI

```bash
npx prettier --plugin prettier-plugin-rdn --write "**/*.rdn"
```

### Configuration

Add to your `.prettierrc`:

```json
{
  "plugins": ["prettier-plugin-rdn"]
}
```

Then run:

```bash
npx prettier --write "**/*.rdn"
```

### Editor Integration

The plugin works automatically with any editor that uses Prettier — VS Code (via the Prettier extension), JetBrains IDEs, Neovim, etc. Just install the plugin and configure Prettier as above.

## Formatting Rules

The plugin formats `.rdn` files with the same philosophy as Prettier's JSON formatter:

- **Indentation** — 2 spaces by default (configurable via `tabWidth`)
- **Bracket spacing** — `{ "a": 1 }` by default (configurable via `bracketSpacing`)
- **Line breaking** — short containers stay on one line; long ones break across lines (controlled by `printWidth`)
- **Trailing newline** — every file ends with a newline

### What gets formatted

| Type | Example |
|------|---------|
| Objects | `{ "key": "value" }` |
| Arrays | `[1, 2, 3]` |
| Tuples | `(1, 2, 3)` |
| Maps | `Map{ "a" => 1 }` or `{ "a" => 1 }` |
| Sets | `Set{ 1, 2, 3 }` or `{ 1, 2 }` |
| Strings | Re-escaped with canonical `\"`, `\\n`, etc. |
| Numbers, BigInts | Preserved as-is |
| Dates, Times, Durations | Preserved as-is |
| RegExp, Binary | Preserved as-is |
| `null`, `true`, `false`, `NaN`, `Infinity` | Preserved as-is |

### Empty containers

Empty containers stay compact: `{}`, `[]`, `()`, `Map{}`, `Set{}`

## Options

### Standard Prettier options

| Option | Default | Description |
|--------|---------|-------------|
| `printWidth` | `80` | Line width before breaking |
| `tabWidth` | `2` | Spaces per indent level |
| `useTabs` | `false` | Indent with tabs instead of spaces |
| `bracketSpacing` | `true` | Spaces inside object/map/set braces |

### RDN-specific options

| Option | Default | Description |
|--------|---------|-------------|
| `useExplicitMapKeyword` | `false` | Keep the explicit `Map` keyword on non-empty maps. When `false`, `Map{ k => v }` is formatted as `{ k => v }`. Empty `Map{}` always keeps the keyword. |
| `useExplicitSetKeyword` | `false` | Keep the explicit `Set` keyword on non-empty sets. When `false`, `Set{ 1, 2 }` is formatted as `{ 1, 2 }`. Empty `Set{}` always keeps the keyword. |

## Example

**Input:**
```rdn
{"meta":{"version":"1.0","timestamp":@2024-01-15T10:30:00.000Z},"data":[1,2,3],"tags":Set{"a","b"},"pattern":/hello/gi}
```

**Output:**
```rdn
{
  "meta": { "version": "1.0", "timestamp": @2024-01-15T10:30:00.000Z },
  "data": [1, 2, 3],
  "tags": { "a", "b" },
  "pattern": /hello/gi
}
```

## Development

```bash
npm install
npm run build   # Compile TypeScript
npm test        # Run tests
npm run lint    # Type-check
```
