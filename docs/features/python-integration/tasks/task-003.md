# Task 3: Implement lookup tables

**Status:** pending
**Dependencies:** Task 1

## Description

Implement the pre-computed lookup tables used by both the parser and serializer. These tables provide O(1) character classification and fast encoding/decoding. All table entries must match the TypeScript reference tables in `packages/rdn-js/src/tables.ts`.

### Tables to implement

1. **`TOKEN_TABLE`** -- 256-entry `list[int]` dispatch table indexed by `ord(char)`. Maps each possible first character of a value to a token constant for O(1) dispatch in `_parse_value()`. Token constants:
   - `TOKEN_INVALID = 0`, `TOKEN_STRING = 1` (`"`), `TOKEN_NUMBER = 2` (`0-9`), `TOKEN_MINUS = 3` (`-`), `TOKEN_OPEN_BRACE = 4` (`{`), `TOKEN_OPEN_BRACKET = 6` (`[`), `TOKEN_OPEN_PAREN = 8` (`(`), `TOKEN_TRUE = 12` (`t`), `TOKEN_FALSE = 13` (`f`), `TOKEN_NULL = 14` (`n`), `TOKEN_AT = 15` (`@`), `TOKEN_SLASH = 16` (`/`), `TOKEN_B64 = 17` (`b`), `TOKEN_HEX = 18` (`x`), `TOKEN_INFINITY = 19` (`I`), `TOKEN_NAN = 20` (`N`), `TOKEN_MAP = 21` (`M`), `TOKEN_SET = 22` (`S`)

2. **`B64_DECODE`** -- 256-entry `list[int]` for base64 character validation and decoding. Maps each byte value to its 6-bit decoded value, or `-1` for invalid characters. Covers `A-Z` (0-25), `a-z` (26-51), `0-9` (52-61), `+` (62), `/` (63).

3. **`HEX_DECODE`** -- 256-entry `list[int]` for hex character validation and decoding. Maps `0-9` to 0-9, `a-f`/`A-F` to 10-15, all others to `-1`.

4. **`ESCAPE_TABLE`** -- 256-entry `list[str]` for string serialization escaping. Pre-computed escape sequences for characters that must be escaped in RDN strings:
   - `0x22` (`"`) -> `\\"`
   - `0x5C` (`\`) -> `\\\\`
   - `0x08` (backspace) -> `\\b`
   - `0x09` (tab) -> `\\t`
   - `0x0A` (LF) -> `\\n`
   - `0x0C` (FF) -> `\\f`
   - `0x0D` (CR) -> `\\r`
   - All other control chars (`0x00-0x1F`) -> `\\uXXXX`
   - Empty string `""` means no escaping needed

5. **`DIGIT_PAIRS`** -- 100-entry `list[str]` for fast 2-digit number formatting: `["00", "01", ..., "99"]`. Used for date/time serialization.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_tables.py` (modify)

## Acceptance Criteria
- `TOKEN_TABLE[ord('"')] == TOKEN_STRING`
- `TOKEN_TABLE[ord('0')] == TOKEN_NUMBER` through `TOKEN_TABLE[ord('9')] == TOKEN_NUMBER`
- `TOKEN_TABLE[ord('M')] == TOKEN_MAP`
- `TOKEN_TABLE[ord('S')] == TOKEN_SET`
- `B64_DECODE[ord('A')] == 0`, `B64_DECODE[ord('z')] == 51`, `B64_DECODE[ord('+')] == 62`
- `HEX_DECODE[ord('f')] == 15`, `HEX_DECODE[ord('F')] == 15`, `HEX_DECODE[ord('g')] == -1`
- `ESCAPE_TABLE[0x22] == '\\"'`, `ESCAPE_TABLE[0x0A] == '\\n'`
- `DIGIT_PAIRS[7] == "07"`, `DIGIT_PAIRS[42] == "42"`
- All table entries match the TypeScript tables (`tables.ts`)

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 3
- Tech Design: Section 3.3.2 (Dispatch Table -- full TOKEN_TABLE specification)
- Tech Design: Section 3.4.3 (String Escaping -- ESCAPE_TABLE)
- Tech Design: Section 3.4.6 (Date Formatting -- DIGIT_PAIRS)
- TypeScript Reference: `packages/rdn-js/src/tables.ts` (source of truth for table values)
- Discovery: `docs/features/python-integration/discovery.md`
