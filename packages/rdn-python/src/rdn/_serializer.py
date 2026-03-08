"""Pure Python RDN serializer with cycle detection."""

from __future__ import annotations

import base64
import math
import re
from datetime import datetime, time, timedelta, timezone
from typing import Any, Callable

from rdn._tables import DIGIT_PAIRS, ESCAPE_TABLE
from rdn.exceptions import MAX_SAFE_INTEGER


# ---------------------------------------------------------------------------
# Pre-compiled regex patterns for string escaping
# ---------------------------------------------------------------------------
# For ensure_ascii=False: only control chars, quote, and backslash need escaping
_NEEDS_ESCAPE = re.compile(r'[\x00-\x1f"\\]')
# For ensure_ascii=True: anything outside safe printable ASCII needs escaping
# Safe: 0x20-0x21 (sp, !), 0x23-0x5B (# through [), 0x5D-0x7E (] through ~)
_NEEDS_ESCAPE_ASCII = re.compile(r'[^\x20-\x21\x23-\x5b\x5d-\x7e]')

# Lookup dict for common escape sequences (faster than ESCAPE_TABLE for sub())
_ESCAPE_DCT = {
    '"': '\\"', '\\': '\\\\', '\b': '\\b', '\f': '\\f',
    '\n': '\\n', '\r': '\\r', '\t': '\\t',
}

# Pre-computed constants
_INF = float('inf')
_NEG_INF = float('-inf')


# ---------------------------------------------------------------------------
# String escaping — regex-based
# ---------------------------------------------------------------------------

def _replace_char(m: re.Match) -> str:  # type: ignore[type-arg]
    """Replacement function for regex-based string escaping."""
    ch = m.group()
    r = _ESCAPE_DCT.get(ch)
    if r is not None:
        return r
    cp = ord(ch)
    if cp < 0x10000:
        return f'\\u{cp:04x}'
    # Surrogate pair for non-BMP characters
    high = 0xD800 + ((cp - 0x10000) >> 10)
    low = 0xDC00 + ((cp - 0x10000) & 0x3FF)
    return f'\\u{high:04x}\\u{low:04x}'


def _escape_string(s: str, ensure_ascii: bool = True) -> str:
    """Escape and quote a string for RDN output using regex-based scanning.

    Fast path: if no characters need escaping, return ``'"' + s + '"'``
    directly. Slow path: delegates detection AND replacement to the C regex
    engine via ``re.sub()``.
    """
    pattern = _NEEDS_ESCAPE_ASCII if ensure_ascii else _NEEDS_ESCAPE
    if not pattern.search(s):
        return '"' + s + '"'
    return '"' + pattern.sub(_replace_char, s) + '"'


# ---------------------------------------------------------------------------
# Cycle detection helpers
# ---------------------------------------------------------------------------

def _check_cycle(obj: object, _seen: set[int]) -> None:
    """Raise :class:`ValueError` if *obj* has already been visited."""
    obj_id = id(obj)
    if obj_id in _seen:
        raise ValueError("Converting circular structure to RDN")
    _seen.add(obj_id)


def _remove_cycle(obj: object, _seen: set[int]) -> None:
    """Remove *obj* from the visited set after serialization completes."""
    _seen.discard(id(obj))


# ---------------------------------------------------------------------------
# Container formatting helper
# ---------------------------------------------------------------------------

def _format_container(open_delim: str, close_delim: str, parts: list[str], item_sep: str, indent_str: str | None, level: int) -> str:
    """Format a container (list, tuple, dict, set) with optional indentation."""
    if not parts:
        return open_delim + close_delim

    if indent_str is not None:
        child_indent = indent_str * (level + 1)
        closing_indent = indent_str * level
        inner = ("," + "\n" + child_indent).join(parts)
        return open_delim + "\n" + child_indent + inner + "\n" + closing_indent + close_delim

    return open_delim + item_sep.join(parts) + close_delim


# ---------------------------------------------------------------------------
# Extended type formatting
# ---------------------------------------------------------------------------

def _format_date(d: datetime) -> str:
    """Format a datetime as ``@YYYY-MM-DDTHH:mm:ss.sssZ`` using f-string."""
    if d.tzinfo is not None and d.tzinfo != timezone.utc:
        d = d.astimezone(timezone.utc)
    return f"@{d.year:04d}-{DIGIT_PAIRS[d.month]}-{DIGIT_PAIRS[d.day]}T{DIGIT_PAIRS[d.hour]}:{DIGIT_PAIRS[d.minute]}:{DIGIT_PAIRS[d.second]}.{d.microsecond // 1000:03d}Z"


def _format_timeonly(t: time) -> str:
    """Format a time as ``@HH:MM:SS[.mmm]``."""
    ms = t.microsecond // 1000
    if ms > 0:
        return f"@{DIGIT_PAIRS[t.hour]}:{DIGIT_PAIRS[t.minute]}:{DIGIT_PAIRS[t.second]}.{ms:03d}"
    return f"@{DIGIT_PAIRS[t.hour]}:{DIGIT_PAIRS[t.minute]}:{DIGIT_PAIRS[t.second]}"


def _format_duration(td: timedelta) -> str:
    """Format a timedelta as ``@PnDTnHnMnS`` (ISO 8601 duration)."""
    total_seconds = int(td.total_seconds())
    negative = total_seconds < 0
    if negative:
        total_seconds = -total_seconds
    days = total_seconds // 86400
    remaining = total_seconds % 86400
    hours = remaining // 3600
    remaining %= 3600
    minutes = remaining // 60
    seconds = remaining % 60

    result = "@"
    if negative:
        result += "-"
    result += "P"
    if days:
        result += f"{days}D"
    if hours or minutes or seconds:
        result += "T"
        if hours:
            result += f"{hours}H"
        if minutes:
            result += f"{minutes}M"
        if seconds:
            result += f"{seconds}S"
    if result == "@P" or result == "@-P":
        result += "T0S"  # Zero duration
    return result


def _format_regexp(pattern: re.Pattern[str]) -> str:
    """Format a compiled regex as ``/pattern/flags``."""
    flags = ""
    if pattern.flags & re.IGNORECASE:
        flags += "i"
    if pattern.flags & re.MULTILINE:
        flags += "m"
    if pattern.flags & re.DOTALL:
        flags += "s"
    return "/" + pattern.pattern + "/" + flags


def _format_binary(data: bytes | bytearray) -> str:
    """Format binary data as ``b"<base64>"``."""
    if isinstance(data, bytearray):
        data = bytes(data)
    return 'b"' + base64.b64encode(data).decode("ascii") + '"'


# ---------------------------------------------------------------------------
# Public entry point
# ---------------------------------------------------------------------------

def stringify(value: object, *, skipkeys: bool = False, ensure_ascii: bool = True, check_circular: bool = True, allow_nan: bool = True, sort_keys: bool = False, indent: int | str | None = None, separators: tuple[str, str] | None = None, default: Callable[[Any], Any] | None = None) -> str | None:
    """Serialize *value* to an RDN-formatted string.

    Parameters
    ----------
    value:
        The Python value to serialize.
    skipkeys:
        When ``True``, dict keys that are not strings are silently
        skipped instead of raising :class:`TypeError`. Default ``False``.
    ensure_ascii:
        When ``True`` (default), all non-ASCII characters in strings are
        escaped as ``\\uXXXX``. When ``False``, UTF-8 characters are
        passed through verbatim.
    check_circular:
        When ``True`` (default), detect circular references and raise
        :class:`ValueError`. Set to ``False`` to skip the check (at the
        risk of a :class:`RecursionError`).
    sort_keys:
        When ``True``, dictionary keys are sorted alphabetically in the
        output.
    indent:
        If a non-negative integer or string, pretty-print with that indent
        level. ``None`` (default) selects the most compact representation.
        An integer means that many spaces per level; a string (e.g.
        ``"\\t"``) is used verbatim.
    separators:
        A ``(item_separator, key_separator)`` tuple overriding defaults.
    default:
        A callable that is invoked for objects that are not natively
        serializable. It should return a serializable object or raise
        :class:`TypeError`.

    Returns
    -------
    str | None
        The RDN text representation, or ``None`` for non-serializable values.
    """
    # Compute indent string
    indent_str: str | None = None
    if indent is not None:
        indent_str = " " * indent if isinstance(indent, int) else indent

    # Compute separators
    if separators is not None:
        item_sep, key_sep = separators
    elif indent is not None:
        item_sep, key_sep = ",", ": "
    else:
        item_sep, key_sep = ",", ":"

    # Initialise cycle detection set
    _seen: set[int] = set()

    # Bind frequently-used names as closure variables (LOAD_DEREF vs LOAD_GLOBAL)
    _isinstance = isinstance
    _str = str
    _int = int
    _float = float
    _escape = _escape_string
    _fmt_date = _format_date
    _fmt_time = _format_timeonly
    _fmt_dur = _format_duration
    _fmt_re = _format_regexp
    _fmt_bin = _format_binary
    _fmt_cont = _format_container
    _check = _check_cycle
    _remove = _remove_cycle
    _isnan = math.isnan

    # Key escape cache — avoids re-escaping repeated dict keys (common in JSON arrays)
    _key_cache: dict[str, str] = {}

    def _encode(value: Any, _level: int = 0, _in_default: bool = False) -> str | None:
        """Serialize a single value. Config captured by closure — no kwargs overhead."""
        # 1. Singletons — identity checks (pointer comparison, fastest possible)
        if value is None:
            return "null"
        if value is True:
            return "true"
        if value is False:
            return "false"

        # 2. str
        if _isinstance(value, _str):
            return _escape(value, ensure_ascii)

        # 3. int (bool already handled above via identity checks)
        if _isinstance(value, _int):
            if abs(value) > MAX_SAFE_INTEGER:
                return _str(value) + "n"
            return _str(value)

        # 4. float -- special values, then repr for shortest round-trip
        if _isinstance(value, _float):
            if _isnan(value):
                if not allow_nan:
                    raise ValueError("Out of range float values are not RDN compliant")
                return "NaN"
            if value == _INF:
                if not allow_nan:
                    raise ValueError("Out of range float values are not RDN compliant")
                return "Infinity"
            if value == _NEG_INF:
                if not allow_nan:
                    raise ValueError("Out of range float values are not RDN compliant")
                return "-Infinity"
            return repr(value)

        # 5. datetime -> @YYYY-MM-DDTHH:mm:ss.sssZ
        if _isinstance(value, datetime):
            return _fmt_date(value)

        # 6. time -> @HH:MM:SS[.mmm]
        if _isinstance(value, time):
            return _fmt_time(value)

        # 7. timedelta -> @PnDTnHnMnS
        if _isinstance(value, timedelta):
            return _fmt_dur(value)

        # 8. re.Pattern -> /pattern/flags
        if _isinstance(value, re.Pattern):
            return _fmt_re(value)

        # 9. bytes / bytearray -> b"base64"
        if _isinstance(value, (bytes, bytearray)):
            return _fmt_bin(value)

        # 10. list
        if _isinstance(value, list):
            if check_circular:
                _check(value, _seen)
            child_level = _level + 1
            parts: list[str] = []
            for item in value:
                el = _encode(item, child_level)
                parts.append(el if el is not None else "null")
            if check_circular:
                _remove(value, _seen)
            return _fmt_cont("[", "]", parts, item_sep, indent_str, _level)

        # 11. tuple
        if _isinstance(value, tuple):
            child_level = _level + 1
            parts = []
            for item in value:
                el = _encode(item, child_level)
                parts.append(el if el is not None else "null")
            return _fmt_cont("(", ")", parts, item_sep, indent_str, _level)

        # 12. dict — with key escape caching for repeated keys
        if _isinstance(value, dict):
            if check_circular:
                _check(value, _seen)
            if skipkeys:
                raw_keys = [k for k in value.keys() if _isinstance(k, _str)]
            else:
                raw_keys = value.keys()
            keys = sorted(raw_keys) if sort_keys else raw_keys
            child_level = _level + 1
            parts = []
            kcache = _key_cache
            for k in keys:
                if not _isinstance(k, _str):
                    raise TypeError(f"Object key must be a string, got {type(k).__name__}")
                sv = _encode(value[k], child_level)
                if sv is not None:
                    ek = kcache.get(k)
                    if ek is None:
                        ek = _escape(k, ensure_ascii) + key_sep
                        kcache[k] = ek
                    parts.append(ek + sv)
            if check_circular:
                _remove(value, _seen)
            return _fmt_cont("{", "}", parts, item_sep, indent_str, _level)

        # 14. set / frozenset
        if _isinstance(value, (set, frozenset)):
            if _isinstance(value, set) and check_circular:
                _check(value, _seen)
            if len(value) == 0:
                if _isinstance(value, set) and check_circular:
                    _remove(value, _seen)
                return "Set{}"
            child_level = _level + 1
            parts = []
            for item in value:
                el = _encode(item, child_level)
                if el is not None:
                    parts.append(el)
            if _isinstance(value, set) and check_circular:
                _remove(value, _seen)
            return _fmt_cont("Set{", "}", parts, item_sep, indent_str, _level)

        # 15. default function fallback
        if default is not None and not _in_default:
            fallback = default(value)
            result = _encode(fallback, _level, True)
            if result is not None:
                return result
            raise TypeError(f"Object of type {type(fallback).__name__} is not RDN serializable")

        # Unsupported type
        raise TypeError(f"Object of type {type(value).__name__} is not RDN serializable")

    return _encode(value)
