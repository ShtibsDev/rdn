"""Pure Python recursive-descent RDN parser."""

from __future__ import annotations

import base64
import re
from datetime import datetime, time, timedelta, timezone
from typing import Any, Callable, NoReturn

from .exceptions import RDNDecodeError
from ._tables import B64_DECODE, HEX_DECODE

# ---------------------------------------------------------------------------
# Pre-compiled regex patterns for hot-path scanning
# ---------------------------------------------------------------------------
# Matches runs of unescaped, non-control string characters
_STRINGCHUNK = re.compile(r'[^"\\\x00-\x1f]*')
# Matches one or more whitespace characters
_WS_RE = re.compile(r'[ \t\n\r]+')
# Matches a number: integer part, optional fraction, optional exponent, optional BigInt 'n'
_NUMBER_RE = re.compile(r'([0-9]+)(\.[0-9]+)?([eE][+-]?[0-9]+)?(n)?')

# ---------------------------------------------------------------------------
# Module-level cursor state — set on entry, cleared in finally
# ---------------------------------------------------------------------------
_source: str = ""
_pos: int = 0
_len: int = 0
_depth: int = 0

MAX_DEPTH: int = 128
MAX_BINARY_SIZE: int = 100 * 1024 * 1024  # 100 MB

# Key memo for string deduplication (like CPython json)
_memo: dict[str, str] = {}

# ---------------------------------------------------------------------------
# Module-level hook state — set on entry, cleared in finally
# ---------------------------------------------------------------------------
_parse_int_hook: Callable[[str], Any] | None = None
_parse_float_hook: Callable[[str], Any] | None = None
_parse_bigint_hook: Callable[[str], Any] | None = None
_parse_datetime_hook: Callable[[datetime], Any] | None = None
_parse_timeonly_hook: Callable[[time], Any] | None = None
_parse_duration_hook: Callable[[timedelta | str], Any] | None = None
_parse_regexp_hook: Callable[[re.Pattern[str]], Any] | None = None
_parse_binary_hook: Callable[[bytes], Any] | None = None
_object_hook: Callable[[dict[str, Any]], Any] | None = None
_object_pairs_hook: Callable[[list[tuple[str, Any]]], Any] | None = None

# ---------------------------------------------------------------------------
# Utility helpers
# ---------------------------------------------------------------------------

def _skip_ws() -> None:
    """Skip whitespace characters using regex for bulk scanning."""
    global _pos
    if _pos < _len and _source[_pos] <= ' ':
        m = _WS_RE.match(_source, _pos)
        if m:
            _pos = m.end()


def _error(msg: str) -> NoReturn:
    """Raise an RDNDecodeError at the current position."""
    raise RDNDecodeError(msg, _source, _pos)


def _expect(char: str) -> None:
    """Consume a single expected character or raise an error."""
    global _pos
    if _pos >= _len or _source[_pos] != char:
        _error(f"Expected '{char}'")
    _pos += 1


def _parse_literal(expected: str) -> None:
    """Consume expected string using slice comparison."""
    global _pos
    end = _pos + len(expected)
    if end > _len or _source[_pos:end] != expected:
        _error(f"Expected '{expected}'")
    _pos = end


# ---------------------------------------------------------------------------
# String parsing with deferred materialization
# ---------------------------------------------------------------------------

def _parse_string() -> str:
    """Parse a JSON-style double-quoted string using regex chunked scanning."""
    global _pos
    source = _source
    slen = _len
    chunk_match = _STRINGCHUNK.match
    pos = _pos + 1  # skip opening "

    # Fast path: regex scans for end of unescaped content in one C-level call
    m = chunk_match(source, pos)
    assert m is not None
    end = m.end()
    if end < slen and source[end] == '"':
        # Common case: no escapes, no control chars
        _pos = end + 1
        return source[pos:end]

    # Slow path: escapes or control chars present — single-pass with parts list
    parts: list[str] = []
    append = parts.append

    while True:
        if end > pos:
            append(source[pos:end])

        if end >= slen:
            _pos = end
            _error("Unterminated string")

        ch = source[end]

        if ch == '"':
            _pos = end + 1
            return ''.join(parts)

        if ch != '\\':
            # Must be a control character (0x00-0x1F)
            _pos = end
            _error("Unescaped control character in string")

        # Handle escape sequence
        pos = end + 1
        if pos >= slen:
            _pos = pos
            _error("Unterminated string")

        esc = source[pos]
        if esc == '"':
            append('"'); pos += 1
        elif esc == '\\':
            append('\\'); pos += 1
        elif esc == '/':
            append('/'); pos += 1
        elif esc == 'n':
            append('\n'); pos += 1
        elif esc == 'r':
            append('\r'); pos += 1
        elif esc == 't':
            append('\t'); pos += 1
        elif esc == 'b':
            append('\b'); pos += 1
        elif esc == 'f':
            append('\f'); pos += 1
        elif esc == 'u':
            hex_str = source[pos + 1:pos + 5]
            if len(hex_str) < 4:
                _pos = pos
                _error("Invalid unicode escape")
            try:
                code = int(hex_str, 16)
            except ValueError:
                _pos = pos
                _error("Invalid unicode escape")

            # Handle surrogate pairs
            if 0xD800 <= code <= 0xDBFF:
                # High surrogate — expect \uXXXX low surrogate
                if (pos + 5 < slen and source[pos + 5] == '\\' and pos + 6 < slen and source[pos + 6] == 'u'):
                    low_hex = source[pos + 7:pos + 11]
                    if len(low_hex) < 4:
                        _pos = pos
                        _error("Invalid unicode escape")
                    try:
                        low = int(low_hex, 16)
                    except ValueError:
                        _pos = pos
                        _error("Invalid unicode escape")
                    if 0xDC00 <= low <= 0xDFFF:
                        codepoint = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00)
                        append(chr(codepoint))
                        pos += 11
                    else:
                        _pos = pos
                        _error("Invalid surrogate pair")
                else:
                    _pos = pos
                    _error("Invalid surrogate pair")
            elif 0xDC00 <= code <= 0xDFFF:
                _pos = pos
                _error("Unexpected low surrogate")
            else:
                append(chr(code))
                pos += 5
        else:
            _pos = pos
            _error(f"Invalid escape sequence '\\{esc}'")

        # Scan next chunk of normal characters
        m = chunk_match(source, pos)
        assert m is not None
        end = m.end()

    # unreachable, but keeps mypy happy
    return ""  # pragma: no cover


# ---------------------------------------------------------------------------
# Number parsing
# ---------------------------------------------------------------------------

def _parse_number(negative: bool) -> Any:
    """Parse a JSON-style number or BigInt literal using regex scanning.

    When *negative* is True the leading ``-`` has already been consumed and
    ``_pos`` points to the first digit.
    """
    global _pos

    m = _NUMBER_RE.match(_source, _pos)
    if not m or m.start() != _pos:
        _error("Expected digit")

    int_part = m.group(1)
    frac_part = m.group(2)
    exp_part = m.group(3)
    bigint_n = m.group(4)
    _pos = m.end()

    # Leading-zero check: "007" is invalid; "0", "0.5", "0e1" are fine
    if len(int_part) > 1 and int_part[0] == '0':
        _error("Leading zeros not allowed")

    # Validate edge cases the regex doesn't catch with specific error messages
    end_pos = _pos
    if not frac_part and end_pos < _len and _source[end_pos] == '.':
        _pos = end_pos + 1
        _error("Expected digit after decimal point")
    if not exp_part and frac_part and end_pos < _len and _source[end_pos] in ('e', 'E'):
        _pos = end_pos + 1
        if _pos < _len and _source[_pos] in ('+', '-'):
            _pos += 1
        _error("Expected digit in exponent")
    if not exp_part and not frac_part and end_pos < _len and _source[end_pos] in ('e', 'E'):
        _pos = end_pos + 1
        if _pos < _len and _source[_pos] in ('+', '-'):
            _pos += 1
        _error("Expected digit in exponent")

    # -- BigInt suffix 'n' --------------------------------------------------
    if bigint_n:
        if frac_part or exp_part:
            _error("BigInt cannot have decimal point or exponent")
        raw = ('-' + int_part) if negative else int_part
        if _parse_bigint_hook is not None:
            return _parse_bigint_hook(raw)
        return int(raw)

    # -- float (has fraction or exponent) -----------------------------------
    if frac_part or exp_part:
        num_str = m.group()
        if bigint_n:
            num_str = num_str[:-1]
        raw = ('-' + num_str) if negative else num_str
        if _parse_float_hook is not None:
            return _parse_float_hook(raw)
        return float(raw)

    # -- integer ------------------------------------------------------------
    raw = ('-' + int_part) if negative else int_part
    if _parse_int_hook is not None:
        return _parse_int_hook(raw)
    if len(int_part) <= 15:
        value = int(int_part)
        return -value if negative else value
    return int(raw)


# ---------------------------------------------------------------------------
# Digit-reading helpers for date/time parsing
# ---------------------------------------------------------------------------

def _read_digits(n: int) -> int:
    """Read exactly *n* digit characters and return the integer value."""
    global _pos
    if _pos + n > _len:
        _error(f"Expected {n}-digit number")
    value = 0
    for _ in range(n):
        d = ord(_source[_pos]) - 0x30
        if d < 0 or d > 9:
            _error(f"Expected {n}-digit number")
        value = value * 10 + d
        _pos += 1
    return value


# ---------------------------------------------------------------------------
# @-prefixed type parsing: DateTime, TimeOnly, Duration, Unix timestamp
# ---------------------------------------------------------------------------

def _parse_at() -> Any:
    """Disambiguate @-prefixed literals: datetime, timeonly, duration, unix timestamp."""
    global _pos
    _pos += 1  # skip '@'

    if _pos >= _len:
        _error("Unexpected end after @")

    ch = _source[_pos]

    # Duration: @P...
    if ch == "P":
        return _parse_duration()

    # Digit-based: need to distinguish TimeOnly vs DateTime vs Unix timestamp
    if ch.isdigit():
        # TimeOnly: digit at +0, colon at +2 → HH:MM:SS
        if _pos + 2 < _len and _source[_pos + 2] == ":":
            return _parse_timeonly()
        # DateTime: digit at +0, dash at +4 → YYYY-MM-DD
        if _pos + 4 < _len and _source[_pos + 4] == "-":
            return _parse_datetime()
        # Unix timestamp: just digits
        return _parse_unix_timestamp()

    _error("Invalid @ literal")


def _parse_datetime() -> Any:
    """Parse @YYYY-MM-DD or @YYYY-MM-DDTHH:MM:SS[.mmm]Z into a UTC datetime."""
    global _pos
    year = _read_digits(4)
    _expect("-")
    month = _read_digits(2)
    _expect("-")
    day = _read_digits(2)

    # Date-only: @YYYY-MM-DD
    if _pos >= _len or _source[_pos] != "T":
        result = datetime(year, month, day, tzinfo=timezone.utc)
        if _parse_datetime_hook is not None:
            return _parse_datetime_hook(result)
        return result

    _pos += 1  # skip 'T'
    hours = _read_digits(2)
    _expect(":")
    minutes = _read_digits(2)
    _expect(":")
    seconds = _read_digits(2)

    microsecond = 0
    if _pos < _len and _source[_pos] == ".":
        _pos += 1  # skip '.'
        ms = _read_digits(3)
        microsecond = ms * 1000

    _expect("Z")
    result = datetime(year, month, day, hours, minutes, seconds, microsecond, tzinfo=timezone.utc)
    if _parse_datetime_hook is not None:
        return _parse_datetime_hook(result)
    return result


def _parse_timeonly() -> Any:
    """Parse @HH:MM:SS[.mmm] into a time object."""
    global _pos
    hours = _read_digits(2)
    _expect(":")
    minutes = _read_digits(2)
    _expect(":")
    seconds = _read_digits(2)

    microsecond = 0
    if _pos < _len and _source[_pos] == ".":
        _pos += 1  # skip '.'
        ms = _read_digits(3)
        microsecond = ms * 1000

    result = time(hours, minutes, seconds, microsecond)
    if _parse_timeonly_hook is not None:
        return _parse_timeonly_hook(result)
    return result


def _parse_duration() -> Any:
    """Parse @P... ISO 8601 duration.

    Returns a ``timedelta`` when possible (no year/month components),
    otherwise returns the raw ISO string as ``str``.
    """
    global _pos
    start = _pos
    _pos += 1  # skip 'P'

    # Scan forward while chars are valid duration characters
    while _pos < _len:
        c = _source[_pos]
        if c.isdigit() or c in ("Y", "M", "D", "T", "H", "S", "."):
            _pos += 1
        else:
            break

    iso = _source[start:_pos]
    if len(iso) < 2:
        _error("Invalid duration")

    # Split on 'T' to separate date-part and time-part
    if "T" in iso[1:]:
        t_idx = iso.index("T", 1)
        date_part = iso[1:t_idx]
        time_part = iso[t_idx + 1:]
    else:
        date_part = iso[1:]
        time_part = ""

    # If date_part contains Y or M (months), we cannot represent as timedelta
    if "Y" in date_part or "M" in date_part:
        result: timedelta | str = iso
        if _parse_duration_hook is not None:
            return _parse_duration_hook(result)
        return result

    # Parse date_part for D
    days = 0
    if date_part:
        if "D" in date_part:
            days = int(date_part.replace("D", ""))

    # Parse time_part for H, M (minutes), S
    total_hours = 0
    total_minutes = 0
    total_seconds = 0.0
    if time_part:
        # Extract hours
        if "H" in time_part:
            h_idx = time_part.index("H")
            total_hours = int(time_part[:h_idx])
            time_part = time_part[h_idx + 1:]
        # Extract minutes
        if "M" in time_part:
            m_idx = time_part.index("M")
            total_minutes = int(time_part[:m_idx])
            time_part = time_part[m_idx + 1:]
        # Extract seconds
        if "S" in time_part:
            s_idx = time_part.index("S")
            total_seconds = float(time_part[:s_idx])

    result = timedelta(days=days, hours=total_hours, minutes=total_minutes, seconds=total_seconds)
    if _parse_duration_hook is not None:
        return _parse_duration_hook(result)
    return result


def _parse_unix_timestamp() -> Any:
    """Parse @<digits> as a Unix timestamp (seconds or milliseconds)."""
    global _pos
    start = _pos
    while _pos < _len:
        d = ord(_source[_pos]) - 0x30
        if d < 0 or d > 9:
            break
        _pos += 1

    digits = _source[start:_pos]
    value = int(digits)

    # <= 10 digits → seconds; > 10 digits → milliseconds
    if len(digits) <= 10:
        result = datetime.fromtimestamp(value, tz=timezone.utc)
    else:
        result = datetime.fromtimestamp(value / 1000, tz=timezone.utc)
    if _parse_datetime_hook is not None:
        return _parse_datetime_hook(result)
    return result


# ---------------------------------------------------------------------------
# RegExp parsing
# ---------------------------------------------------------------------------

# Valid regexp flags in JS/RDN
_REGEXP_FLAGS = frozenset("dgimsvy")

# Mapping from RDN flag chars to Python re module flags
_REGEXP_FLAG_MAP: dict[str, re.RegexFlag] = {
    "i": re.IGNORECASE,
    "m": re.MULTILINE,
    "s": re.DOTALL,
}


def _parse_regexp() -> Any:
    """Parse a /pattern/flags regexp literal."""
    global _pos
    _pos += 1  # skip opening /

    start = _pos
    escaped = False

    while _pos < _len:
        c = _source[_pos]
        if escaped:
            escaped = False
            _pos += 1
            continue
        if c == "\\":
            escaped = True
            _pos += 1
            continue
        if c == "/":
            break
        _pos += 1

    if _pos >= _len:
        _error("Unterminated regular expression")

    pattern = _source[start:_pos]
    _pos += 1  # skip closing /

    # Read flags
    flags = re.RegexFlag(0)
    while _pos < _len and _source[_pos] in _REGEXP_FLAGS:
        mapped = _REGEXP_FLAG_MAP.get(_source[_pos])
        if mapped is not None:
            flags |= mapped
        _pos += 1

    result = re.compile(pattern, flags)
    if _parse_regexp_hook is not None:
        return _parse_regexp_hook(result)
    return result


# ---------------------------------------------------------------------------
# Binary parsing — base64 and hex
# ---------------------------------------------------------------------------

def _parse_binary_b64() -> Any:
    """Parse b\"...\" base64 binary literal."""
    global _pos
    _pos += 1  # skip 'b'
    if _pos >= _len or _source[_pos] != '"':
        _error("Expected '\"' after 'b'")
    _pos += 1  # skip opening "

    start = _pos
    try:
        end = _source.index('"', _pos)
    except ValueError:
        _pos = _len
        _error("Unterminated binary literal")
    content = _source[start:end]
    _pos = end + 1  # skip closing "

    if len(content) == 0:
        result = b""
        if _parse_binary_hook is not None:
            return _parse_binary_hook(result)
        return result

    # Validate length is multiple of 4
    if len(content) % 4 != 0:
        _error("Invalid base64: length must be a multiple of 4")

    # Validate all chars are valid base64 (A-Z, a-z, 0-9, +, /, =)
    # and that = only appears at the end
    padding = 0
    for i, ch in enumerate(content):
        if ch == "=":
            padding += 1
            if i < len(content) - 2:
                _error("Invalid base64 character")
        else:
            if padding > 0:
                # non-pad char after pad char
                _error("Invalid base64 character")
            if B64_DECODE[ord(ch)] == -1:
                _error("Invalid base64 character")

    # Compute decoded size and check against MAX_BINARY_SIZE
    decoded_size = (len(content) // 4) * 3 - padding
    if decoded_size > MAX_BINARY_SIZE:
        _error("Binary data too large")

    # Non-zero padding bit check (Python's b64decode doesn't enforce this)
    if padding == 1:
        # Last data char is at index -2 (before the single '=')
        last_data_val = B64_DECODE[ord(content[-2])]
        if last_data_val & 0x03:
            _error("Invalid base64: non-zero padding bits")
    elif padding == 2:
        # Last data char is at index -3 (before the two '=')
        last_data_val = B64_DECODE[ord(content[-3])]
        if last_data_val & 0x0F:
            _error("Invalid base64: non-zero padding bits")

    result = base64.b64decode(content, validate=True)
    if _parse_binary_hook is not None:
        return _parse_binary_hook(result)
    return result


def _parse_binary_hex() -> Any:
    """Parse x\"...\" hex binary literal."""
    global _pos
    _pos += 1  # skip 'x'
    if _pos >= _len or _source[_pos] != '"':
        _error("Expected '\"' after 'x'")
    _pos += 1  # skip opening "

    start = _pos
    try:
        end = _source.index('"', _pos)
    except ValueError:
        _pos = _len
        _error("Unterminated hex literal")
    content = _source[start:end]
    _pos = end + 1  # skip closing "

    if len(content) == 0:
        result = b""
        if _parse_binary_hook is not None:
            return _parse_binary_hook(result)
        return result

    # Validate even length
    if len(content) % 2 != 0:
        _error("Invalid hex: odd length")

    # Validate all chars are hex digits
    for ch in content:
        if HEX_DECODE[ord(ch)] == -1:
            _error("Invalid hex character")

    # Check decoded size against MAX_BINARY_SIZE
    if len(content) // 2 > MAX_BINARY_SIZE:
        _error("Binary data too large")

    result = bytes.fromhex(content)
    if _parse_binary_hook is not None:
        return _parse_binary_hook(result)
    return result


# ---------------------------------------------------------------------------
# Depth tracking
# ---------------------------------------------------------------------------

def _enter_container() -> None:
    """Increment nesting depth, enforcing the maximum."""
    global _depth
    _depth += 1
    if _depth > MAX_DEPTH:
        _error("Maximum nesting depth exceeded (128)")


def _exit_container() -> None:
    """Decrement nesting depth."""
    global _depth
    _depth -= 1


# ---------------------------------------------------------------------------
# Container parsing — arrays, tuples, brace disambiguation
# ---------------------------------------------------------------------------

def _parse_array() -> list[Any]:
    """Parse ``[value, ...]``."""
    global _pos, _depth
    _depth += 1
    if _depth > MAX_DEPTH:
        _error("Maximum nesting depth exceeded (128)")
    source = _source
    slen = _len
    ws_match = _WS_RE.match
    pos = _pos + 1  # skip [

    # Inline ws skip
    if pos < slen and source[pos] <= ' ':
        m = ws_match(source, pos)
        if m:
            pos = m.end()

    if pos < slen and source[pos] == ']':
        _pos = pos + 1
        _depth -= 1
        return []

    _pos = pos
    items: list[Any] = [_parse_value()]
    pos = _pos

    # Inline ws skip
    if pos < slen and source[pos] <= ' ':
        m = ws_match(source, pos)
        if m:
            pos = m.end()

    while pos < slen and source[pos] == ',':
        pos += 1
        # Inline ws skip
        if pos < slen and source[pos] <= ' ':
            m = ws_match(source, pos)
            if m:
                pos = m.end()
        _pos = pos
        items.append(_parse_value())
        pos = _pos
        # Inline ws skip
        if pos < slen and source[pos] <= ' ':
            m = ws_match(source, pos)
            if m:
                pos = m.end()

    _pos = pos
    _expect(']')
    _depth -= 1
    return items


def _parse_tuple() -> tuple[Any, ...]:
    """Parse ``(value, ...)``."""
    global _pos
    _enter_container()
    _pos += 1  # skip (
    _skip_ws()

    if _pos < _len and _source[_pos] == ")":
        _pos += 1
        _exit_container()
        return ()

    items: list[Any] = []
    items.append(_parse_value())
    _skip_ws()

    while _pos < _len and _source[_pos] == ",":
        _pos += 1  # skip ,
        _skip_ws()
        items.append(_parse_value())
        _skip_ws()

    _expect(")")
    _exit_container()
    return tuple(items)


def _parse_brace() -> Any:
    """Disambiguate ``{`` into Object, Map, or Set."""
    global _pos, _depth
    _depth += 1
    if _depth > MAX_DEPTH:
        _error("Maximum nesting depth exceeded (128)")
    source = _source
    slen = _len
    ws_match = _WS_RE.match
    pos = _pos + 1  # skip {

    # Inline ws skip
    if pos < slen and source[pos] <= ' ':
        m = ws_match(source, pos)
        if m:
            pos = m.end()

    # Empty braces → Object (empty dict)
    if pos < slen and source[pos] == '}':
        _pos = pos + 1
        _depth -= 1
        if _object_pairs_hook is not None:
            return _object_pairs_hook([])
        if _object_hook is not None:
            return _object_hook({})
        return {}

    # Parse first value
    _pos = pos
    first_value = _parse_value()
    pos = _pos
    # Inline ws skip
    if pos < slen and source[pos] <= ' ':
        m = ws_match(source, pos)
        if m:
            pos = m.end()
    _pos = pos

    if _pos >= _len:
        _error("Unterminated brace expression")

    sep = _source[_pos]

    # : → Object
    if sep == ":":
        if not isinstance(first_value, str):
            _error("Object key must be a string")
        return _finish_object(first_value)

    # => → Map
    if sep == "=" and _pos + 1 < _len and _source[_pos + 1] == ">":
        return _finish_map(first_value)

    # , → Set
    if sep == ",":
        return _finish_set(first_value)

    # } → single-element Set
    if sep == "}":
        _pos += 1
        _exit_container()
        return frozenset({first_value})

    _error("Expected ':', '=>', ',' or '}' after value in brace expression")


def _finish_object(first_key: str) -> Any:
    """Finish parsing an Object after the first key and ``:``.

    ``_pos`` points at the ``:`` after *first_key*.
    """
    global _pos, _depth
    source = _source
    slen = _len
    ws_match = _WS_RE.match
    chunk_match = _STRINGCHUNK.match
    memo = _memo
    first_key = memo.setdefault(first_key, first_key)
    pos = _pos + 1  # skip :

    # Inline ws skip
    if pos < slen and source[pos] <= ' ':
        m = ws_match(source, pos)
        if m:
            pos = m.end()

    if _object_pairs_hook is not None:
        _pos = pos
        pairs: list[tuple[str, Any]] = []
        pairs.append((first_key, _parse_value()))
        pos = _pos
        # Inline ws skip
        if pos < slen and source[pos] <= ' ':
            m = ws_match(source, pos)
            if m:
                pos = m.end()

        while pos < slen and source[pos] == ',':
            pos += 1
            # Inline ws skip
            if pos < slen and source[pos] <= ' ':
                m = ws_match(source, pos)
                if m:
                    pos = m.end()
            # Inline string key fast path (avoids _parse_string function call)
            if pos < slen and source[pos] == '"':
                kstart = pos + 1
                km = chunk_match(source, kstart)
                assert km is not None
                kend = km.end()
                if kend < slen and source[kend] == '"':
                    key = source[kstart:kend]
                    pos = kend + 1
                else:
                    _pos = pos
                    key = _parse_string()
                    pos = _pos
            else:
                _pos = pos
                _error("Expected '\"'")
            key = memo.setdefault(key, key)
            # Inline ws skip
            if pos < slen and source[pos] <= ' ':
                m = ws_match(source, pos)
                if m:
                    pos = m.end()
            if pos >= slen or source[pos] != ':':
                _pos = pos
                _error("Expected ':'")
            pos += 1
            # Inline ws skip
            if pos < slen and source[pos] <= ' ':
                m = ws_match(source, pos)
                if m:
                    pos = m.end()
            _pos = pos
            pairs.append((key, _parse_value()))
            pos = _pos
            # Inline ws skip
            if pos < slen and source[pos] <= ' ':
                m = ws_match(source, pos)
                if m:
                    pos = m.end()

        _pos = pos
        _expect('}')
        _depth -= 1
        return _object_pairs_hook(pairs)

    _pos = pos
    result: dict[str, Any] = {}
    result[first_key] = _parse_value()
    pos = _pos
    # Inline ws skip
    if pos < slen and source[pos] <= ' ':
        m = ws_match(source, pos)
        if m:
            pos = m.end()

    while pos < slen and source[pos] == ',':
        pos += 1
        # Inline ws skip
        if pos < slen and source[pos] <= ' ':
            m = ws_match(source, pos)
            if m:
                pos = m.end()
        # Inline string key fast path (avoids _parse_string function call)
        if pos < slen and source[pos] == '"':
            kstart = pos + 1
            km = chunk_match(source, kstart)
            assert km is not None
            kend = km.end()
            if kend < slen and source[kend] == '"':
                key = source[kstart:kend]
                pos = kend + 1
            else:
                _pos = pos
                key = _parse_string()
                pos = _pos
        else:
            _pos = pos
            _error("Expected '\"'")
        key = memo.setdefault(key, key)
        # Inline ws skip
        if pos < slen and source[pos] <= ' ':
            m = ws_match(source, pos)
            if m:
                pos = m.end()
        if pos >= slen or source[pos] != ':':
            _pos = pos
            _error("Expected ':'")
        pos += 1
        # Inline ws skip
        if pos < slen and source[pos] <= ' ':
            m = ws_match(source, pos)
            if m:
                pos = m.end()
        _pos = pos
        result[key] = _parse_value()
        pos = _pos
        # Inline ws skip
        if pos < slen and source[pos] <= ' ':
            m = ws_match(source, pos)
            if m:
                pos = m.end()

    _pos = pos
    _expect('}')
    _depth -= 1
    if _object_hook is not None:
        return _object_hook(result)
    return result


def _finish_map(first_key: Any) -> dict[Any, Any]:
    """Finish parsing a Map after the first key and ``=>``.

    ``_pos`` points at the ``=`` of the first ``=>``.
    """
    global _pos
    _pos += 2  # skip =>
    _skip_ws()

    result: dict[Any, Any] = {}
    first_val = _parse_value()
    try:
        result[first_key] = first_val
    except TypeError:
        _error("Map key is not hashable")
    _skip_ws()

    while _pos < _len and _source[_pos] == ",":
        _pos += 1  # skip ,
        _skip_ws()
        key = _parse_value()
        _skip_ws()
        if _pos + 1 >= _len or _source[_pos] != "=" or _source[_pos + 1] != ">":
            _error("Expected '=>' in map entry")
        _pos += 2  # skip =>
        _skip_ws()
        val = _parse_value()
        try:
            result[key] = val
        except TypeError:
            _error("Map key is not hashable")
        _skip_ws()

    _expect("}")
    _exit_container()
    return result


def _finish_set(first_value: Any) -> frozenset[Any]:
    """Finish parsing a Set after the first value and ``,``.

    ``_pos`` points at the ``,`` after *first_value*.
    """
    global _pos
    items: list[Any] = []
    try:
        hash(first_value)
    except TypeError:
        _error("Set element is not hashable")
    items.append(first_value)

    _pos += 1  # skip ,
    _skip_ws()
    val = _parse_value()
    try:
        hash(val)
    except TypeError:
        _error("Set element is not hashable")
    items.append(val)
    _skip_ws()

    while _pos < _len and _source[_pos] == ",":
        _pos += 1  # skip ,
        _skip_ws()
        val = _parse_value()
        try:
            hash(val)
        except TypeError:
            _error("Set element is not hashable")
        items.append(val)
        _skip_ws()

    _expect("}")
    _exit_container()
    return frozenset(items)


# ---------------------------------------------------------------------------
# Explicit Map{} and Set{} parsing
# ---------------------------------------------------------------------------

def _parse_explicit_map() -> dict[Any, Any]:
    """Parse ``Map{key => value, ...}``."""
    global _pos
    # Verify and skip "Map{"
    if _pos + 3 >= _len or _source[_pos + 1] != "a" or _source[_pos + 2] != "p" or _source[_pos + 3] != "{":
        _error("Expected 'Map{'")
    _enter_container()
    _pos += 4  # skip Map{
    _skip_ws()

    result: dict[Any, Any] = {}

    if _pos < _len and _source[_pos] == "}":
        _pos += 1
        _exit_container()
        return result

    # Parse first entry
    key = _parse_value()
    _skip_ws()
    if _pos + 1 >= _len or _source[_pos] != "=" or _source[_pos + 1] != ">":
        _error("Expected '=>' in map entry")
    _pos += 2  # skip =>
    _skip_ws()
    val = _parse_value()
    try:
        result[key] = val
    except TypeError:
        _error("Map key is not hashable")
    _skip_ws()

    while _pos < _len and _source[_pos] == ",":
        _pos += 1  # skip ,
        _skip_ws()
        key = _parse_value()
        _skip_ws()
        if _pos + 1 >= _len or _source[_pos] != "=" or _source[_pos + 1] != ">":
            _error("Expected '=>' in map entry")
        _pos += 2  # skip =>
        _skip_ws()
        val = _parse_value()
        try:
            result[key] = val
        except TypeError:
            _error("Map key is not hashable")
        _skip_ws()

    _expect("}")
    _exit_container()
    return result


def _parse_explicit_set() -> frozenset[Any]:
    """Parse ``Set{value, ...}``."""
    global _pos
    # Verify and skip "Set{"
    if _pos + 3 >= _len or _source[_pos + 1] != "e" or _source[_pos + 2] != "t" or _source[_pos + 3] != "{":
        _error("Expected 'Set{'")
    _enter_container()
    _pos += 4  # skip Set{
    _skip_ws()

    if _pos < _len and _source[_pos] == "}":
        _pos += 1
        _exit_container()
        return frozenset()

    items: list[Any] = []
    val = _parse_value()
    try:
        hash(val)
    except TypeError:
        _error("Set element is not hashable")
    items.append(val)
    _skip_ws()

    while _pos < _len and _source[_pos] == ",":
        _pos += 1  # skip ,
        _skip_ws()
        val = _parse_value()
        try:
            hash(val)
        except TypeError:
            _error("Set element is not hashable")
        items.append(val)
        _skip_ws()

    _expect("}")
    _exit_container()
    return frozenset(items)


# ---------------------------------------------------------------------------
# Value dispatch
# ---------------------------------------------------------------------------

def _parse_value() -> Any:
    """Parse a single RDN value using direct character dispatch.

    Callers must skip leading whitespace before calling this function.
    Uses direct char comparison (no ord() + table lookup overhead).
    Common types (string, number, object, array) are checked first.
    Literals (true/false/null) are inlined to avoid _parse_literal call.
    """
    global _pos
    source = _source
    pos = _pos

    if pos >= _len:
        _error("Unexpected end of input")

    ch = source[pos]

    # Most common types first for JSON payloads
    if ch == '"':
        return _parse_string()
    if ch == '{':
        return _parse_brace()
    if ch == '[':
        return _parse_array()
    if '0' <= ch <= '9':
        return _parse_number(negative=False)
    if ch == 't':
        end = pos + 4
        if end <= _len and source[pos:end] == 'true':
            _pos = end
            return True
        _error("Expected 'true'")
    if ch == 'f':
        end = pos + 5
        if end <= _len and source[pos:end] == 'false':
            _pos = end
            return False
        _error("Expected 'false'")
    if ch == 'n':
        end = pos + 4
        if end <= _len and source[pos:end] == 'null':
            _pos = end
            return None
        _error("Expected 'null'")
    if ch == '-':
        _pos = pos + 1
        if _pos < _len and source[_pos] == 'I':
            _parse_literal("Infinity")
            return float("-inf")
        return _parse_number(negative=True)
    if ch == '@':
        return _parse_at()
    if ch == '/':
        return _parse_regexp()
    if ch == 'b':
        return _parse_binary_b64()
    if ch == 'x':
        return _parse_binary_hex()
    if ch == '(':
        return _parse_tuple()
    if ch == 'I':
        _parse_literal("Infinity")
        return float("inf")
    if ch == 'N':
        _parse_literal("NaN")
        return float("nan")
    if ch == 'M':
        return _parse_explicit_map()
    if ch == 'S':
        return _parse_explicit_set()
    _error(f"Unexpected character '{ch}'")


# ---------------------------------------------------------------------------
# Public entry point
# ---------------------------------------------------------------------------

def parse(
    text: str | bytes | bytearray,
    *,
    parse_int: Callable[[str], Any] | None = None,
    parse_float: Callable[[str], Any] | None = None,
    parse_bigint: Callable[[str], Any] | None = None,
    parse_datetime: Callable[[datetime], Any] | None = None,
    parse_timeonly: Callable[[time], Any] | None = None,
    parse_duration: Callable[[timedelta | str], Any] | None = None,
    parse_regexp: Callable[[re.Pattern[str]], Any] | None = None,
    parse_binary: Callable[[bytes], Any] | None = None,
    object_hook: Callable[[dict[str, Any]], Any] | None = None,
    object_pairs_hook: Callable[[list[tuple[str, Any]]], Any] | None = None,
) -> Any:
    """Parse an RDN string and return the corresponding Python value.

    Parameters
    ----------
    text:
        The RDN document to parse.  May be ``str``, ``bytes``, or
        ``bytearray``.  ``bytes``/``bytearray`` are decoded as UTF-8.
    parse_int:
        Called with the string representation of each integer.
    parse_float:
        Called with the string representation of each float.
    parse_bigint:
        Called with the string representation of each bigint (without
        the ``n`` suffix).
    parse_datetime:
        Called with each parsed ``datetime`` object.
    parse_timeonly:
        Called with each parsed ``time`` object.
    parse_duration:
        Called with each parsed ``timedelta`` or ``str``.
    parse_regexp:
        Called with each parsed ``re.Pattern`` object.
    parse_binary:
        Called with each parsed ``bytes`` object.
    object_hook:
        Called with each parsed ``dict``.
    object_pairs_hook:
        Called with a list of ``(key, value)`` pairs for each object.
        Takes priority over *object_hook*.

    Returns
    -------
    Any
        The decoded Python value.

    Raises
    ------
    RDNDecodeError
        If *text* is not valid RDN.
    TypeError
        If *text* is not ``str``, ``bytes``, or ``bytearray``.
    """
    global _source, _pos, _len, _depth
    global _parse_int_hook, _parse_float_hook, _parse_bigint_hook
    global _parse_datetime_hook, _parse_timeonly_hook, _parse_duration_hook
    global _parse_regexp_hook, _parse_binary_hook
    global _object_hook, _object_pairs_hook

    if isinstance(text, (bytes, bytearray)):
        text = text.decode("utf-8")
    elif not isinstance(text, str):
        raise TypeError("First argument must be a string, bytes, or bytearray")

    _source = text
    _pos = 0
    _len = len(text)
    _depth = 0

    # Set hook state
    _parse_int_hook = parse_int
    _parse_float_hook = parse_float
    _parse_bigint_hook = parse_bigint
    _parse_datetime_hook = parse_datetime
    _parse_timeonly_hook = parse_timeonly
    _parse_duration_hook = parse_duration
    _parse_regexp_hook = parse_regexp
    _parse_binary_hook = parse_binary
    _object_hook = object_hook
    _object_pairs_hook = object_pairs_hook

    try:
        _skip_ws()
        result = _parse_value()
        _skip_ws()
        if _pos < _len:
            _error("Unexpected data after value")
        return result
    finally:
        _source = ""
        _pos = 0
        _len = 0
        _depth = 0
        _parse_int_hook = None
        _parse_float_hook = None
        _parse_bigint_hook = None
        _parse_datetime_hook = None
        _parse_timeonly_hook = None
        _parse_duration_hook = None
        _parse_regexp_hook = None
        _parse_binary_hook = None
        _object_hook = None
        _object_pairs_hook = None
        _memo.clear()


def raw_parse(
    text: str,
    *,
    idx: int = 0,
    parse_int: Callable[[str], Any] | None = None,
    parse_float: Callable[[str], Any] | None = None,
    parse_bigint: Callable[[str], Any] | None = None,
    parse_datetime: Callable[[datetime], Any] | None = None,
    parse_timeonly: Callable[[time], Any] | None = None,
    parse_duration: Callable[[timedelta | str], Any] | None = None,
    parse_regexp: Callable[[re.Pattern[str]], Any] | None = None,
    parse_binary: Callable[[bytes], Any] | None = None,
    object_hook: Callable[[dict[str, Any]], Any] | None = None,
    object_pairs_hook: Callable[[list[tuple[str, Any]]], Any] | None = None,
) -> tuple[Any, int]:
    """Parse a single RDN value starting at position *idx* in *text*.

    Unlike :func:`parse`, this does **not** require the value to consume
    the entire string.  After parsing the value and skipping trailing
    whitespace, the current position is returned alongside the result.

    Parameters
    ----------
    text:
        The RDN source string.
    idx:
        Character offset at which to start parsing (default ``0``).
    parse_int / parse_float / ... / object_pairs_hook:
        Same hook parameters as :func:`parse`.

    Returns
    -------
    tuple[Any, int]
        ``(parsed_value, end_position)`` where *end_position* is the
        index of the first unconsumed character after the parsed value
        (whitespace is consumed).

    Raises
    ------
    RDNDecodeError
        If no valid RDN value can be parsed starting at *idx*.
    """
    global _source, _pos, _len, _depth
    global _parse_int_hook, _parse_float_hook, _parse_bigint_hook
    global _parse_datetime_hook, _parse_timeonly_hook, _parse_duration_hook
    global _parse_regexp_hook, _parse_binary_hook
    global _object_hook, _object_pairs_hook

    if not isinstance(text, str):
        raise TypeError("First argument must be a string")

    _source = text
    _pos = idx
    _len = len(text)
    _depth = 0

    # Set hook state
    _parse_int_hook = parse_int
    _parse_float_hook = parse_float
    _parse_bigint_hook = parse_bigint
    _parse_datetime_hook = parse_datetime
    _parse_timeonly_hook = parse_timeonly
    _parse_duration_hook = parse_duration
    _parse_regexp_hook = parse_regexp
    _parse_binary_hook = parse_binary
    _object_hook = object_hook
    _object_pairs_hook = object_pairs_hook

    try:
        _skip_ws()
        result = _parse_value()
        _skip_ws()
        end = _pos
        return (result, end)
    finally:
        _source = ""
        _pos = 0
        _len = 0
        _depth = 0
        _parse_int_hook = None
        _parse_float_hook = None
        _parse_bigint_hook = None
        _parse_datetime_hook = None
        _parse_timeonly_hook = None
        _parse_duration_hook = None
        _parse_regexp_hook = None
        _parse_binary_hook = None
        _object_hook = None
        _object_pairs_hook = None
        _memo.clear()
