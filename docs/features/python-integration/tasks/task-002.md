# Task 2: Implement exceptions and constants

**Status:** pending
**Dependencies:** Task 1

## Description

Implement `RDNDecodeError(ValueError)` with `msg`, `doc`, `pos`, `lineno`, and `colno` attributes, plus the `MAX_SAFE_INTEGER` constant. No custom type classes are needed since all RDN types map to Python stdlib types.

### RDNDecodeError

The exception inherits from `ValueError` (matching `json.JSONDecodeError(ValueError)`). It must:

1. Accept `msg: str`, `doc: str`, `pos: int` in the constructor.
2. Compute `lineno` (1-indexed) from `doc.count("\n", 0, pos) + 1`.
3. Compute `colno` (1-indexed) from `pos - doc.rfind("\n", 0, pos)`.
4. Format the `str()` representation as: `"<msg> in RDN at position <pos> (line <lineno> column <colno>)"`.
5. Store `msg`, `doc`, `pos`, `lineno`, `colno` as instance attributes.

### MAX_SAFE_INTEGER

```python
MAX_SAFE_INTEGER = 2**53 - 1  # 9007199254740991 -- JS Number.MAX_SAFE_INTEGER
```

This constant is used by the serializer for BigInt auto-promote detection: integers with `abs(value) > MAX_SAFE_INTEGER` get the `n` suffix.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/exceptions.py` (modify)

## Acceptance Criteria
- `RDNDecodeError("msg", "doc", 5)` formats as `"msg in RDN at position 5 (line 1 column 6)"`
- `str()` and `repr()` work correctly
- Has `msg`, `doc`, `pos`, `lineno`, `colno` attributes accessible on the instance
- `isinstance(RDNDecodeError(...), ValueError)` is `True`
- `MAX_SAFE_INTEGER` equals `9007199254740991`

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 2
- Tech Design: Section 3.2 (Type Mapping -- MAX_SAFE_INTEGER constant)
- Tech Design: Section 7.1 (RDNDecodeError full specification)
- Tech Design: Section 7.2 (Complete list of all parse error messages)
- Discovery: `docs/features/python-integration/discovery.md`
