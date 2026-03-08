# Task 5: Implement parser -- numbers and BigInt

**Status:** pending
**Dependencies:** Task 4

## Description

Add number parsing (`_parse_number`) and BigInt detection to the parser. Also wire up `NaN`, `Infinity`, and `-Infinity` in the `_parse_value` dispatch.

### Number Parsing Algorithm (`_parse_number(negative: bool)`)

Mirrors `parseNumber()` at `parser.ts:112-185`:

1. Record start position. Accumulate integer digits into a running `int_value` to avoid string-to-number conversion for the common case of small integers.
2. Count digits. If `digit_count == 0`, error "Expected digit".
3. Leading zero check: if `digit_count > 1` and first digit is `0`, error "Leading zeros not allowed" (matches JSON spec).
4. Check for BigInt suffix `n` at current position:
   - If found: advance past `n`, return `int(_source[start:_pos-1])`
   - If the number had a decimal point or exponent before `n`, error "BigInt cannot have decimal point or exponent"
5. Check for fraction (`.` followed by digits). Set `is_float = True`. If no digits after `.`, error "Expected digit after decimal point".
6. Check for exponent (`e`/`E` with optional `+`/`-` sign and digits). Set `is_float = True`. If no digits in exponent, error "Expected digit in exponent".
7. Fast path: if `not is_float and digit_count <= 15`, return the accumulated `int_value` directly (with negative sign applied). This avoids `int()` string parsing.
8. Slow path: return `float(_source[start:_pos])` or `int(_source[start:_pos])` depending on `is_float`.

### Special Numeric Values

Wire up in `_parse_value` dispatch:
- `TOKEN_INFINITY` -> `_parse_literal("Infinity")` then return `float("inf")`
- `TOKEN_NAN` -> `_parse_literal("NaN")` then return `float("nan")`
- `TOKEN_MINUS` -> advance, check if next char is `I` for `-Infinity`, otherwise call `_parse_number(negative=True)`

### BigInt

BigInt values (e.g., `42n`) parse to plain Python `int`. Python's `int` is already arbitrary precision, so no wrapper is needed. The `n` suffix is consumed but not included in the parsed value.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_parser.py` (modify)
- `packages/rdn-python/tests/test_parse.py` (modify)

## Acceptance Criteria
- `parse("42")` returns `42` (type `int`)
- `parse("3.14")` returns `3.14` (type `float`)
- `parse("-7")` returns `-7`
- `parse("1e10")` returns `10000000000.0` (type `float`)
- `parse("42n")` returns `42` (type `int`, BigInt)
- `parse("9007199254740992n")` returns `9007199254740992` (large BigInt)
- `parse("NaN")` returns `float('nan')` (verified with `math.isnan()`)
- `parse("Infinity")` returns `float('inf')`
- `parse("-Infinity")` returns `float('-inf')`
- `parse("007")` raises `RDNDecodeError` ("Leading zeros not allowed")
- `parse("3.14n")` raises `RDNDecodeError` ("BigInt cannot have decimal point or exponent")
- `parse("1e10n")` raises `RDNDecodeError`
- `parse("3.")` raises `RDNDecodeError` ("Expected digit after decimal point")
- `parse("3e")` raises `RDNDecodeError` ("Expected digit in exponent")

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 5
- Tech Design: Section 3.3.3 (`_parse_number` algorithm, `_parse_value` dispatch for numbers/NaN/Infinity)
- Tech Design: Section 3.2 (Type Mapping -- BigInt to `int`, MAX_SAFE_INTEGER)
- Tech Design: Section 7.2 (Parse error messages for numbers)
- TypeScript Reference: `packages/rdn-js/src/parser.ts` lines 112-185 (parseNumber)
- Discovery: `docs/features/python-integration/discovery.md`
