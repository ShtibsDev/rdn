# Task 12: Implement serializer -- containers, cycle detection, indent

**Status:** pending
**Dependencies:** Task 11

## Description

Add serialization for container types (`list`, `tuple`, `dict`, `set`/`frozenset`) to the serializer. Implement cycle detection using `set[int]` of `id()` values. Implement `indent` and `sort_keys` support for pretty-printing.

### Cycle Detection

```python
_seen: set[int] | None = None  # set of id() values

def _check_cycle(obj):
    obj_id = id(obj)
    if obj_id in _seen:
        raise ValueError("Converting circular structure to RDN")
    _seen.add(obj_id)

def _remove_cycle(obj):
    _seen.discard(id(obj))
```

Cycle detection is enabled by default (`check_circular=True`). When disabled, no `_seen` set is created and `_check_cycle`/`_remove_cycle` are no-ops. Only mutable containers (`list`, `dict`, `set`) need cycle checking; `tuple` and `frozenset` are immutable and cannot form cycles.

### Container Serialization

**Array (`list`)**:
- Cycle check, serialize elements, join with `,`. Non-serializable elements replaced with `"null"`.
- Output: `[elem1, elem2, ...]`

**Tuple (`tuple`)**:
- Same as array but output with `(...)` parentheses: `(elem1, elem2, ...)`
- Non-serializable elements replaced with `"null"`.

**Object (`dict`)**:
- Cycle check. Non-string keys raise `TypeError("Object key must be a string, got <type>")`.
- If `sort_keys=True`, iterate keys in sorted order.
- Properties with non-serializable values are omitted.
- Output: `{key1: value1, key2: value2, ...}`

**Set (`set`/`frozenset`)**:
- Always uses explicit `Set{...}` prefix.
- Empty sets serialize as `Set{}`.
- Cycle check for mutable `set` only.
- Output: `Set{elem1, elem2, ...}`

**Map Note**: Regular `dict` always serializes as Object syntax (`{key: value}`). Map syntax is lost during parse-serialize round-trip (Decision #17).

### Indent / Pretty-Print

When `indent` is not `None`:
- `indent` can be `int` (number of spaces) or `str` (literal indent string, e.g., `"\t"`).
- Default separators change from `(",", ":")` to `(",\n", ": ")`.
- Each nesting level adds one `indent` prefix.
- Closing delimiters are on their own line with the parent's indent.
- The `_stringify_value` function accepts a `level: int` parameter for tracking nesting.

### Type Dispatch Additions (continuing from Task 11)

12. `isinstance(value, dict)` -> object serialization
13. `isinstance(value, list)` -> array serialization
14. `isinstance(value, tuple)` -> tuple serialization
15. `isinstance(value, (set, frozenset))` -> set serialization
16. Fall through: raise `TypeError("Object of type <type> is not RDN serializable")`

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_serializer.py` (modify)
- `packages/rdn-python/tests/test_stringify.py` (modify)

## Acceptance Criteria
- `stringify([1, 2, 3])` returns `"[1,2,3]"`
- `stringify([])` returns `"[]"`
- `stringify((1, 2, 3))` returns `"(1,2,3)"`
- `stringify(())` returns `"()"`
- `stringify({"a": 1, "b": 2})` returns `'{"a":1,"b":2}'`
- `stringify({})` returns `"{}"`
- `stringify({1, 2, 3})` returns a valid `Set{...}` string (order may vary)
- `stringify(set())` returns `"Set{}"`
- `stringify(frozenset({1}))` returns `"Set{1}"`
- Circular references raise `ValueError("Converting circular structure to RDN")`
- `stringify({"b": 2, "a": 1}, sort_keys=True)` returns `'{"a":1,"b":2}'`
- `stringify([1, 2], indent=2)` produces multi-line indented output
- `stringify({"a": 1}, indent="\t")` uses tab indentation
- Non-string dict keys raise `TypeError`
- Non-serializable types with no `default` handler raise `TypeError`

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 12
- Tech Design: Section 3.4.1 (Cycle Detection -- `set[int]` of `id()`)
- Tech Design: Section 3.4.7 (Container Serialization -- Array, Object, Set, Tuple)
- Tech Design: Section 3.4.8 (Indent / Pretty-Print Support)
- Tech Design: Section 3.4.2 (Type Dispatch Order -- items 12-16)
- Tech Design: Section 7.3 (Serialization Errors)
- Tech Design: Decision #11 (Cycle detection), #17 (Map serialization round-trip)
- TypeScript Reference: `packages/rdn-js/src/serializer.ts` lines 98-215 (container serialization)
- Discovery: `docs/features/python-integration/discovery.md`
