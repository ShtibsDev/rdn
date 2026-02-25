# Task 10: Implement serializer -- primitives and strings

**Status:** pending
**Dependencies:** Task 3

## Description

Implement the serializer module scaffold and the first layer of serialization: `_stringify_value` for `None`, `bool`, `int`, `float`, and `str`. Implement `_escape_string` with fast/slow path and `ensure_ascii` support.

### Type Dispatch Order (partial)

The `_stringify_value()` function checks types in this specific order (ordering matters!):

1. `value is None` -> `"null"`
2. `isinstance(value, str)` -> `_escape_string(value)`
3. `isinstance(value, bool)` -> `"true"` / `"false"` (**MUST** be before `int` since `bool` is a subclass of `int` -- `isinstance(True, int)` is `True`)
4. `isinstance(value, int)` -> number or bigint formatting
5. `isinstance(value, float)` -> number formatting with NaN/Infinity handling

### String Escaping (`_escape_string`)

Uses the pre-computed `ESCAPE_TABLE` from `_tables.py`:

**Fast path**: Scan the entire string looking for any char that needs escaping (`< 0x20`, `"`, `\`). If none found, return `'"' + s + '"'` (zero-copy wrap). This handles ~80% of strings.

**Slow path**: Build an output list, flushing plain segments and inserting escape sequences from `ESCAPE_TABLE`.

**`ensure_ascii=True`** (default): Also escape all chars with `ord(c) > 0x7F` using `\uXXXX` sequences. For codepoints above U+FFFF, use surrogate pair encoding (`\uD800-\uDBFF` + `\uDC00-\uDFFF`).

**`ensure_ascii=False`**: Pass UTF-8 characters through unescaped.

### Number Formatting

- **`int` (within safe range)**: `str(value)`. Auto-promote ints where `abs(value) > MAX_SAFE_INTEGER` to BigInt format: `str(value) + "n"`.
- **`float` (finite)**: Use `repr(value)` for shortest round-trip representation.
- **`float(NaN)`**: `"NaN"`
- **`float(+inf)`**: `"Infinity"`
- **`float(-inf)`**: `"-Infinity"`

### Duration Y/M Fallback

Note: `str` values starting with `"P"` serialize as plain quoted strings (e.g., `'"P1Y2M"'`), NOT as `@P1Y2M`. This is the intentional one-way parse behavior (Decision #21). Duration Y/M round-trip requires a custom encoder `default()`.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_serializer.py` (modify)
- `packages/rdn-python/tests/test_stringify.py` (create)

## Acceptance Criteria
- `stringify(None)` returns `"null"`
- `stringify(True)` returns `"true"`, `stringify(False)` returns `"false"`
- `stringify(42)` returns `"42"`
- `stringify(9007199254740992)` returns `"9007199254740992n"` (BigInt auto-promote)
- `stringify(3.14)` returns `"3.14"`
- `stringify(float('nan'))` returns `"NaN"`
- `stringify(float('inf'))` returns `"Infinity"`
- `stringify(float('-inf'))` returns `"-Infinity"`
- `stringify("hello")` returns `'"hello"'`
- `stringify("line\nnewline")` returns `'"line\\nnewline"'`
- `stringify("tab\there")` returns `'"tab\\there"'`
- String escaping handles all control chars (`< 0x20`), `"`, and `\`
- `ensure_ascii=True` escapes non-ASCII characters with `\uXXXX`
- `ensure_ascii=False` passes non-ASCII UTF-8 through unchanged
- Surrogate pair encoding works for codepoints above U+FFFF
- `bool` is correctly serialized as `true`/`false` (not as `1`/`0`)

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 10
- Tech Design: Section 3.4.2 (Type Dispatch Order -- full ordering with rationale)
- Tech Design: Section 3.4.3 (String Escaping -- fast/slow path, `ensure_ascii`)
- Tech Design: Section 3.4.4 (Number Formatting)
- Tech Design: Section 3.4.5 (BigInt Detection and Serialization)
- Tech Design: Decision #16 (`bool` before `int` in dispatch)
- Tech Design: Decision #19 (`ensure_ascii` default is `True`)
- Tech Design: Decision #21 (Duration Y/M one-way parse)
- TypeScript Reference: `packages/rdn-js/src/serializer.ts` lines 18-44 (string escaping)
- Discovery: `docs/features/python-integration/discovery.md`
