# Task 11: Implement serializer -- extended types

**Status:** pending
**Dependencies:** Task 10

## Description

Add serialization for extended RDN types to the serializer: `int` auto-promote for BigInt, `datetime`, `time` (TimeOnly), `timedelta` (Duration), `re.Pattern` (RegExp), and `bytes`/`bytearray` (Binary). Extend the type dispatch in `_stringify_value()`.

### Type Dispatch Additions (continuing from Task 10)

6. `isinstance(value, datetime)` -> `_format_date(value)`
7. `isinstance(value, time)` -> `_format_timeonly(value)`
8. `isinstance(value, timedelta)` -> `_format_duration(value)`
9. `isinstance(value, re.Pattern)` -> `"/" + pattern.pattern + "/" + _reconstruct_flags(pattern.flags)`
10. `isinstance(value, bytes)` -> `'b"' + base64.b64encode(value).decode() + '"'`
11. `isinstance(value, bytearray)` -> convert to `bytes`, same as above

### Date Formatting (`_format_date`)

Always outputs the 24-character ISO format `@YYYY-MM-DDTHH:mm:ss.sssZ` per spec:

```python
def _format_date(d: datetime) -> str:
    year = f"{d.year:04d}"
    return (
        "@" + year + "-" + DIGIT_PAIRS[d.month] + "-" + DIGIT_PAIRS[d.day]
        + "T" + DIGIT_PAIRS[d.hour] + ":" + DIGIT_PAIRS[d.minute]
        + ":" + DIGIT_PAIRS[d.second]
        + "." + f"{d.microsecond // 1000:03d}" + "Z"
    )
```

Uses `DIGIT_PAIRS` table for fast 2-digit formatting. Microseconds divided by 1000 to produce milliseconds. Non-UTC datetimes are first converted to UTC. Naive datetimes (no tzinfo) are treated as UTC.

### TimeOnly Formatting (`_format_timeonly`)

Format `time` as `@HH:MM:SS[.mmm]`. Include milliseconds only if non-zero (`microsecond // 1000`).

### Duration Formatting (`_format_duration`)

Format `timedelta` as `@PnDTnHnMnS`. Extract days, hours, minutes, seconds from the `timedelta` components. Only include non-zero components.

### RegExp Formatting

Reconstruct flags from `pattern.flags` bitmask:
- `re.IGNORECASE` -> `i`
- `re.MULTILINE` -> `m`
- `re.DOTALL` -> `s`

Output as `"/pattern/flags"`. The pattern source is taken from `pattern.pattern`.

### Binary Formatting

Use `base64.b64encode(value).decode()` wrapped in `b"..."`. Python's `base64` module is C-backed and fast.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_serializer.py` (modify)
- `packages/rdn-python/tests/test_stringify.py` (modify)

## Acceptance Criteria
- BigInt auto-promote: `stringify(9007199254740992)` returns `"9007199254740992n"` (> MAX_SAFE_INTEGER)
- BigInt auto-promote: `stringify(-9007199254740992)` returns `"-9007199254740992n"`
- Normal int: `stringify(42)` returns `"42"` (within safe range)
- Date: `stringify(datetime(2024, 1, 15, tzinfo=timezone.utc))` returns `"@2024-01-15T00:00:00.000Z"`
- Date with ms: `stringify(datetime(2024, 1, 15, 10, 30, 45, 123000, tzinfo=timezone.utc))` returns `"@2024-01-15T10:30:45.123Z"`
- Naive datetime treated as UTC
- TimeOnly: `stringify(time(14, 30, 0, 500000))` returns `"@14:30:00.500"`
- TimeOnly no ms: `stringify(time(14, 30, 0))` returns `"@14:30:00"`
- Duration: `stringify(timedelta(days=3, hours=4))` returns `"@P3DT4H"`
- RegExp: `stringify(re.compile("^test$", re.IGNORECASE))` returns `"/^test$/i"`
- RegExp: flags `i`, `m`, `s` correctly reconstructed from `pattern.flags`
- Binary: `stringify(b"Hello")` returns `'b"SGVsbG8="'`
- Binary: `stringify(bytearray(b"Hello"))` returns `'b"SGVsbG8="'`
- Empty binary: `stringify(b"")` returns `'b""'`

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 11
- Tech Design: Section 3.4.2 (Type Dispatch Order -- items 6-11)
- Tech Design: Section 3.4.5 (BigInt Detection and Serialization)
- Tech Design: Section 3.4.6 (Date Formatting with `DIGIT_PAIRS` and 24-char ISO format)
- Tech Design: Section 3.2 (Type Mapping table -- serialize behavior column)
- Tech Design: Decisions #4 (BigInt), #5 (TimeOnly), #6 (Duration), #7 (RegExp), #13 (Date formatting), #22 (RegExp flag round-trip)
- TypeScript Reference: `packages/rdn-js/src/serializer.ts` lines 48-96 (date/type formatting)
- Discovery: `docs/features/python-integration/discovery.md`
