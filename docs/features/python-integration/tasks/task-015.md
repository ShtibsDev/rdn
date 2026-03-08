# Task 15: Conformance test runner and edge case tests

**Status:** completed
**Dependencies:** Tasks 9, 13

## Description

Implement the conformance test runner that consumes the shared `test-suite/` directory (language-agnostic test suite used by all RDN implementations). Also add edge case tests for boundary conditions.

### Conformance Test Runner (`test_conformance.py`)

The runner uses `pytest.mark.parametrize` to generate one test per file from `test-suite/`:

**Valid tests** (`test-suite/valid/*.rdn` + `*.expected.json`):
1. Read the `.rdn` file.
2. Parse with `rdn.loads()`.
3. Normalize the parsed result using `normalize_for_comparison()` to convert Python types to the `$type`-tagged JSON convention used in expected files.
4. Read the corresponding `.expected.json` file and parse with `json.loads()`.
5. Deep-compare the normalized result with the expected JSON.

**Invalid tests** (`test-suite/invalid/*.rdn`):
1. Read the `.rdn` file.
2. Assert that `rdn.loads()` raises `rdn.RDNDecodeError`.

**Roundtrip tests** (`test-suite/roundtrip/*.rdn`):
1. Read the `.rdn` file.
2. Parse with `rdn.loads()`.
3. Serialize with `rdn.dumps()`.
4. Parse the serialized result again.
5. Normalize both results and deep-compare.

### Normalization Function

The `normalize_for_comparison()` function converts parsed Python values to the `$type`-tagged dict format used in expected JSON files:

- `datetime` -> `{"$type": "Date", "value": "2024-01-15T00:00:00.000Z"}`
- `time` -> `{"$type": "TimeOnly", "value": {"hours": H, "minutes": M, "seconds": S, "milliseconds": ms}}`
- `timedelta` -> `{"$type": "Duration", "value": "P3DT4H..."}`
- `re.Pattern` -> `{"$type": "RegExp", "value": {"source": "pattern", "flags": "ims"}}`
- `bytes` -> `{"$type": "Binary", "value": "<base64>"}`
- `set`/`frozenset` -> `{"$type": "Set", "value": [...]}`
- `float('nan')` -> `{"$type": "Number", "value": "NaN"}`
- `float('inf')` -> `{"$type": "Number", "value": "Infinity"}`
- `dict`, `list`, `tuple`, primitive types normalized recursively

**Map/dict note**: Since Maps parse to `dict` and we lose the Map vs Object distinction, special handling is needed when the `.expected.json` contains `$type: "Map"` tags. Compare dict entries as ordered pairs.

### Edge Case Tests (`test_edge_cases.py`)

- Unicode surrogate pairs
- Maximum nesting depth (128 levels)
- Empty input (should raise error)
- Whitespace-only input (should raise error)
- Very large numbers (BigInt with many digits)
- Very long strings
- Binary data at size limit boundary (`MAX_BINARY_SIZE`)
- All error messages from the parser (verify exact message text)
- Various whitespace characters (space, tab, LF, CR)
- Nested containers (arrays within objects within arrays)
- Trailing whitespace after valid value (should be OK)

## Files to Create/Modify
- `packages/rdn-python/tests/test_conformance.py` (create)
- `packages/rdn-python/tests/test_edge_cases.py` (create)

## Acceptance Criteria
- All valid tests in `test-suite/valid/` pass (currently ~11 files)
- All invalid tests in `test-suite/invalid/` pass (currently ~10 files)
- Both roundtrip tests in `test-suite/roundtrip/` pass
- Edge case tests cover: max depth, empty input, surrogate pairs, large numbers
- `normalize_for_comparison()` correctly handles all RDN types including the Map/dict special case
- Test discovery works via `pytest tests/test_conformance.py` and `pytest tests/test_edge_cases.py`

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 15
- Tech Design: Section 8.1 (Conformance Test Runner -- full `normalize_for_comparison` implementation)
- Tech Design: Section 8.2 (Unit Tests -- edge case coverage list)
- Conformance Test Suite: `test-suite/` (valid/, invalid/, roundtrip/ directories)
- Conformance Convention: `{"$type": "TypeName", "value": ...}` tagged format for extended types in expected JSON
- Discovery: `docs/features/python-integration/discovery.md`
