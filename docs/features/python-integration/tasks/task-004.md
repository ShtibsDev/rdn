# Task 4: Implement parser -- primitives and strings

**Status:** pending
**Dependencies:** Tasks 2, 3

## Description

Implement the parser module scaffold and the first layer of parsing: module-level state management, utility functions, string parsing with deferred materialization, keyword parsing (`null`, `true`, `false`), and the initial `_parse_value` dispatch for these types.

### Module-Level State

The parser uses module-level cursor state (set on entry, cleared in `finally`):

```python
_source: str = ""
_pos: int = 0
_len: int = 0
_depth: int = 0

MAX_DEPTH: int = 128
MAX_BINARY_SIZE: int = 100 * 1024 * 1024  # 100 MB
```

Each function that modifies `_pos` or `_depth` must use a `global` declaration.

### Utility Functions

- **`_skip_ws()`** -- Skip whitespace (space, tab, LF, CR).
- **`_error(msg: str)`** -- Raise `RDNDecodeError(msg, _source, _pos)`.
- **`_expect(char: str)`** -- If current char matches, advance `_pos`; otherwise call `_error`.
- **`_parse_literal(expected: str)`** -- Consume `len(expected)` chars, verifying each matches.

### String Parsing

- **`_parse_string()`** -- Parse a double-quoted string with deferred materialization:
  1. Skip opening `"`.
  2. Fast-scan character-by-character for `"` (end), `\` (escape), or control chars (error).
  3. If no escape found, return `_source[start:_pos]` (zero-copy slice -- fast path for ~80% of strings).
  4. If escapes found, call `_materialize_string()` for the slow path.

- **`_materialize_string(start, end)`** -- Process escape sequences:
  - `\"`, `\\`, `\/`, `\b`, `\f`, `\n`, `\r`, `\t` -- standard escapes
  - `\uXXXX` -- unicode escape, parse 4 hex digits, convert to `chr(code_point)`
  - Surrogate pairs handled transparently by Python's UCS-4 representation
  - Unknown escapes raise `RDNDecodeError`
  - Uses a list of string parts joined at the end (same as TS `parser.ts:71-108`)

### Initial _parse_value Dispatch

Wire up dispatch for `TOKEN_STRING`, `TOKEN_TRUE`, `TOKEN_FALSE`, `TOKEN_NULL` only. Other token types should raise `_error("Unexpected character")` as placeholders. Use `TOKEN_TABLE[ord(char)]` for O(1) dispatch.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_parser.py` (modify)
- `packages/rdn-python/tests/test_parse.py` (create)

## Acceptance Criteria
- `parse('"hello"')` returns `"hello"`
- `parse('"esc\\nape"')` returns `"esc\nape"`
- `parse('"unicode \\u0041"')` returns `"unicode A"`
- `parse("null")` returns `None`
- `parse("true")` returns `True`
- `parse("false")` returns `False`
- Unicode escapes work including surrogate pairs
- Unescaped control characters in strings raise `RDNDecodeError`
- Unterminated strings raise `RDNDecodeError`
- Unknown escape sequences raise `RDNDecodeError`
- `pytest tests/test_parse.py` passes for string and keyword tests

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 4
- Tech Design: Section 3.3.1 (Module-Level State)
- Tech Design: Section 3.3.3 (`_parse_value` dispatch, `_parse_string`, `_materialize_string`, `_parse_literal`)
- Tech Design: Section 3.3.4 (Error Handling -- `_error` helper)
- Tech Design: Section 7.2 (Parse error messages for strings: "Unterminated string", "Unescaped control character in string", "Invalid escape sequence", "Invalid unicode escape")
- TypeScript Reference: `packages/rdn-js/src/parser.ts` lines 37-108 (string parsing), lines 697-743 (value dispatch)
- Discovery: `docs/features/python-integration/discovery.md`
