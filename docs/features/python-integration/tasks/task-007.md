# Task 7: Implement parser -- RegExp and Binary

**Status:** pending
**Dependencies:** Task 5

## Description

Add regular expression and binary data parsing to the parser: `_parse_regexp`, `_parse_binary_b64`, and `_parse_binary_hex`. Wire up `TOKEN_SLASH`, `TOKEN_B64`, and `TOKEN_HEX` in `_parse_value` dispatch.

### `_parse_regexp()` -- RegExp (mirrors `parseRegExp()` at `parser.ts:332-372`)

1. Skip opening `/`.
2. Scan for closing `/`. Handle `\\` (escaped backslash) and `\/` (escaped slash) -- set an `escaped` flag on `\`, next char is always consumed.
3. If end of input before closing `/`, error "Unterminated regular expression".
4. Slice the pattern string.
5. Read flags: scan while char is in `{d, g, i, m, s, u, v, y}`.
6. Map compatible flags to Python `re` flags:
   - `i` -> `re.IGNORECASE`
   - `m` -> `re.MULTILINE`
   - `s` -> `re.DOTALL`
   - JS-only flags (`d`, `g`, `u`, `v`, `y`) are silently dropped
7. Return `re.compile(pattern, mapped_flags)`.

### `_parse_binary_b64()` -- Base64 Binary (mirrors `parseBinaryB64()` at `parser.ts:376-434`)

1. Skip `b`, expect `"`, skip it.
2. Scan to closing `"`. If EOF, error "Unterminated binary literal".
3. If content is empty, return `b""`.
4. Validate length is multiple of 4. If not, error "Invalid base64: length must be a multiple of 4".
5. Validate all characters are valid base64 (`A-Z`, `a-z`, `0-9`, `+`, `/`, `=`). If not, error "Invalid base64 character".
6. Decode using `base64.b64decode(content, validate=True)`.
7. Check decoded length against `MAX_BINARY_SIZE` (100 MB). If exceeded, error "Binary data too large".
8. Validate non-zero padding bits by manually checking the last encoded characters (TS does this at `parser.ts:417-423`). Python's `b64decode(validate=True)` does NOT reject non-zero padding bits. Error: "Invalid base64: non-zero padding bits".
9. Return `bytes` result.

### `_parse_binary_hex()` -- Hex Binary (mirrors `parseBinaryHex()` at `parser.ts:436-459`)

1. Skip `x`, expect `"`, skip it.
2. Scan to closing `"`. If EOF, error "Unterminated hex literal".
3. If empty, return `b""`.
4. Validate even length. If odd, error "Invalid hex: odd length".
5. Validate all characters are hex digits. If not, error "Invalid hex character".
6. Check decoded length against `MAX_BINARY_SIZE`.
7. Return `bytes.fromhex(content)`.

## Files to Create/Modify
- `packages/rdn-python/src/rdn/_parser.py` (modify)
- `packages/rdn-python/tests/test_parse.py` (modify)

## Acceptance Criteria
- `parse('/^[a-z]+$/i')` returns `re.compile("^[a-z]+$", re.IGNORECASE)`
- `parse('/hello/')` returns `re.compile("hello")`
- `parse('/test/gim')` returns `re.compile("test", re.IGNORECASE | re.MULTILINE)` (JS `g` flag silently dropped)
- `parse('/\\/path/')` correctly handles escaped slash
- `parse('b"SGVsbG8="')` returns `b"Hello"`
- `parse('b""')` returns `b""`
- `parse('x"48656C6C6F"')` returns `b"Hello"`
- `parse('x""')` returns `b""`
- Invalid base64 characters raise `RDNDecodeError`
- Invalid base64 length (not multiple of 4) raises `RDNDecodeError`
- Non-zero padding bits raise `RDNDecodeError`
- Odd-length hex raises `RDNDecodeError`
- Invalid hex characters raise `RDNDecodeError`
- Binary data exceeding `MAX_BINARY_SIZE` raises `RDNDecodeError`
- Unterminated regex/binary literals raise `RDNDecodeError`

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 7
- Tech Design: Section 3.3.3 (`_parse_regexp`, `_parse_binary_b64`, `_parse_binary_hex` full algorithms)
- Tech Design: Section 3.2 (Type Mapping -- RegExp to `re.Pattern`, Binary to `bytes`)
- Tech Design: Section 7.2 (Parse error messages for regex and binary)
- Tech Design: Decision #18 (Base64 non-zero padding bits manual check)
- TypeScript Reference: `packages/rdn-js/src/parser.ts` lines 332-459 (parseRegExp, parseBinaryB64, parseBinaryHex)
- Discovery: `docs/features/python-integration/discovery.md`
