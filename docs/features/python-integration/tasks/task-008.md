# Task 8: Implement parser -- Arrays, Tuples, Brace disambiguation

**Status:** pending
**Dependencies:** Task 7

## Description

Add container parsing to the parser: `_parse_array`, `_parse_tuple`, `_parse_brace` (with disambiguation logic), `_finish_object`, `_finish_map`, `_finish_set`, `_parse_explicit_map`, and `_parse_explicit_set`. Implement depth tracking with `_enter_container`/`_exit_container`. Wire up `TOKEN_OPEN_BRACKET`, `TOKEN_OPEN_PAREN`, `TOKEN_OPEN_BRACE`, `TOKEN_MAP`, and `TOKEN_SET` in `_parse_value` dispatch.

### Depth Tracking

- `MAX_DEPTH = 128` (matches TS `parser.ts:11`)
- `_enter_container()` increments `_depth` and raises `RDNDecodeError("Maximum nesting depth exceeded (128)")` if exceeded
- `_exit_container()` decrements `_depth`

### `_parse_array()` -- Array (mirrors `parseArray()` at `parser.ts:467-488`)

1. Enter container, skip `[`, skip whitespace.
2. If `]`, return empty `list`.
3. Parse values separated by `,` until `]`.
4. Exit container, return `list`.

### `_parse_tuple()` -- Tuple (mirrors `parseTuple()` at `parser.ts:490-511`)

1. Enter container, skip `(`, skip whitespace.
2. If `)`, return empty `tuple`.
3. Parse values separated by `,` until `)`.
4. Exit container, return `tuple` (convert from intermediate list).

### `_parse_brace()` -- Brace Disambiguation (mirrors `parseBrace()` at `parser.ts:513-561`)

This is the most complex parsing function. `{` can start an Object, Map, or Set:

1. Enter container, skip `{`, skip whitespace.
2. If `}`, return empty `dict` (empty `{}` is always Object per spec).
3. Parse first value.
4. Skip whitespace. Inspect separator:
   - `:` -> first value must be `str`, call `_finish_object(first_key)`
   - `=` followed by `>` -> call `_finish_map(first_key)`
   - `,` -> call `_finish_set(first_value)`
   - `}` -> return `frozenset({first_value})` (single-element Set)
5. Error if none of the above: "Expected ':', '=>', ',' or '}' after value in brace expression"

### `_finish_object(first_key)` -- Object parsing

Uses `dict` (preserves insertion order). If `object_pairs_hook` is provided, collect `(key, value)` pairs into a list and pass to the hook. If `object_hook` is provided, pass the constructed dict. Non-string keys raise "Object key must be a string".

### `_finish_map(first_key)` -- Map parsing

Insert entries into a `dict` with `=>` separator. Raises `RDNDecodeError` if a key is unhashable.

### `_finish_set(first_value)` -- Set parsing

Insert elements into a `set`. Raises `RDNDecodeError` if an element is unhashable.

### `_parse_explicit_map()` -- `Map{...}` (mirrors `parseExplicitMap()` at `parser.ts:626-666`)

1. Verify `Map{` prefix, skip past it, enter container.
2. If `}`, return empty `dict`.
3. Parse `key => value` entries separated by `,`.
4. Exit container, return `dict`.

### `_parse_explicit_set()` -- `Set{...}` (mirrors `parseExplicitSet()` at `parser.ts:668-693`)

1. Verify `Set{` prefix, skip past it, enter container.
2. If `}`, return empty `set`.
3. Parse values separated by `,`.
4. Exit container, return `set`.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_parser.py` (modify)
- `packages/rdn-python/tests/test_parse.py` (modify)

## Acceptance Criteria
- `parse('[1, 2, 3]')` returns `[1, 2, 3]`
- `parse('[]')` returns `[]`
- `parse('(1, 2, 3)')` returns `(1, 2, 3)`
- `parse('()')` returns `()`
- `parse('{}')` returns `{}` (empty dict, not empty set)
- `parse('{"a": 1, "b": 2}')` returns `{"a": 1, "b": 2}` (Object)
- `parse('{"a" => 1, "b" => 2}')` returns `{"a": 1, "b": 2}` (Map as dict)
- `parse('{"a", "b", "c"}')` returns `{"a", "b", "c"}` (set)
- `parse('{"a"}')` returns `frozenset({"a"})` (single-element Set)
- `parse('Map{}')` returns `{}` (empty Map)
- `parse('Map{"a" => 1}')` returns `{"a": 1}`
- `parse('Set{}')` returns `set()`
- `parse('Set{1, 2, 3}')` returns `{1, 2, 3}`
- Depth limit enforced at 128 (deeply nested arrays/objects raise error)
- `pytest tests/test_parse.py` fully passes for all implemented types

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 8
- Tech Design: Section 3.3.3 (`_parse_brace`, `_parse_array`, `_parse_tuple`, `_parse_explicit_map`, `_parse_explicit_set`, `_finish_object`, `_finish_map`, `_finish_set` full algorithms)
- Tech Design: Section 3.3.5 (Max Depth -- `_enter_container` helper)
- Tech Design: Section 3.2 (Type Mapping -- Map to `dict`, Set to `set`/`frozenset`, Tuple to `tuple`)
- Tech Design: Section 7.2 (Parse error messages for containers and brace disambiguation)
- TypeScript Reference: `packages/rdn-js/src/parser.ts` lines 467-693 (all container parsing)
- RDN Spec: `spec/rdn-spec.md` -- Section 5 (Brace Disambiguation rules)
- Discovery: `docs/features/python-integration/discovery.md`
