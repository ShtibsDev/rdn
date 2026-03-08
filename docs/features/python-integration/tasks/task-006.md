# Task 6: Implement parser -- DateTime, TimeOnly, Duration

**Status:** pending
**Dependencies:** Task 5

## Description

Add `@`-prefixed type parsing to the parser: `_parse_at` (disambiguation), `_parse_datetime`, `_parse_timeonly`, `_parse_duration`, and `_parse_unix_timestamp`. Wire up `TOKEN_AT` in `_parse_value` dispatch.

### `_parse_at()` -- Disambiguation (mirrors `parseAt()` at `parser.ts:219-249`)

1. Skip `@`.
2. If next char is `P` -> `_parse_duration()`.
3. If next char is a digit and char at `_pos+2` is `:` -> `_parse_timeonly()`.
4. If next char is a digit and char at `_pos+4` is `-` -> `_parse_datetime()`.
5. If next char is a digit (all digits) -> `_parse_unix_timestamp()`.
6. Otherwise error "Invalid @ literal".

### `_parse_datetime()` -- DateTime (mirrors `parseDateTime()` at `parser.ts:251-278`)

1. Read 4 digits (year), expect `-`, read 2 digits (month), expect `-`, read 2 digits (day).
2. If next char is not `T`, return `datetime(year, month, day, tzinfo=timezone.utc)` (date-only).
3. Skip `T`, read hours:minutes:seconds (2+2+2 digits with `:` separators).
4. If next char is `.`, read 3 digits (milliseconds). Convert: `microsecond = ms * 1000`.
5. Expect `Z`.
6. Return `datetime(year, month, day, hours, minutes, seconds, microsecond=ms*1000, tzinfo=timezone.utc)`.

All DateTimes are always UTC (`tzinfo=timezone.utc`).

### `_parse_timeonly()` -- TimeOnly (mirrors `parseTimeOnly()` at `parser.ts:280-294`)

1. Read 2+2+2 digit groups for hours:minutes:seconds with `:` separators.
2. Optional `.` followed by 3 digits for milliseconds.
3. Return `time(hours, minutes, seconds, milliseconds * 1000)` -- ms stored as microseconds.
4. No timezone attached (TimeOnly has no timezone in the spec).

### `_parse_duration()` -- Duration (mirrors `parseDuration()` at `parser.ts:296-312`)

1. Record start at `P`.
2. Scan forward while characters are digits, `Y`, `M`, `D`, `T`, `H`, `S`, or `.`.
3. Slice the ISO string (excluding `@` prefix, including `P`).
4. If length < 2, error "Invalid duration".
5. Parse components: if only D/H/M/S components present, return `timedelta(days=..., hours=..., minutes=..., seconds=...)`.
6. If Y or M components present, return raw ISO string (e.g., `"P1Y2M3D"`) since `timedelta` cannot represent variable-length months/years.

### `_parse_unix_timestamp()` -- Unix Timestamp

Parse the numeric value after `@`. Apply the seconds/milliseconds threshold: if value > a threshold (typically 10^10), treat as milliseconds and divide by 1000. Return as `datetime` in UTC.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_parser.py` (modify)
- `packages/rdn-python/tests/test_parse.py` (modify)

## Acceptance Criteria
- `parse('@2024-01-15T00:00:00.000Z')` returns `datetime(2024, 1, 15, tzinfo=timezone.utc)`
- `parse('@2024-01-15')` returns `datetime(2024, 1, 15, tzinfo=timezone.utc)` (date-only)
- `parse('@2024-01-15T10:30:45.123Z')` returns `datetime(2024, 1, 15, 10, 30, 45, 123000, tzinfo=timezone.utc)`
- `parse('@14:30:00.500')` returns `time(14, 30, 0, 500000)` (500ms as microseconds)
- `parse('@14:30:00')` returns `time(14, 30, 0)` (no milliseconds)
- `parse('@P3DT4H5M6S')` returns `timedelta(days=3, hours=4, minutes=5, seconds=6)`
- `parse('@PT30S')` returns `timedelta(seconds=30)`
- `parse('@P1Y2M3D')` returns `"P1Y2M3D"` (str fallback for Y/M components)
- Unix timestamps handle seconds vs milliseconds threshold correctly
- `parse('@P')` raises `RDNDecodeError` ("Invalid duration")

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 6
- Tech Design: Section 3.3.3 (`_parse_at`, `_parse_datetime`, `_parse_timeonly`, `_parse_duration` full algorithms)
- Tech Design: Section 3.2 (Type Mapping -- TimeOnly to `datetime.time`, Duration to `timedelta`/`str`)
- Tech Design: Section 7.2 (Parse error messages: "Unexpected end after @", "Invalid @ literal", "Invalid duration", "Expected 2-digit number", "Expected 3-digit number", "Expected 4-digit year")
- TypeScript Reference: `packages/rdn-js/src/parser.ts` lines 219-312 (parseAt, parseDateTime, parseTimeOnly, parseDuration)
- Discovery: `docs/features/python-integration/discovery.md`
