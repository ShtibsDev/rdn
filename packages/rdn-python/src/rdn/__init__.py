"""RDN (Rich Data Notation) parser and serializer for Python.

A JSON superset with native support for dates, BigInts, regular expressions,
binary data, Maps, Sets, tuples, and more. API mirrors Python's json module.
"""

from __future__ import annotations

import re
from datetime import datetime, time, timedelta
from typing import IO, Any, Callable

from importlib.metadata import version as _meta_version, PackageNotFoundError

from rdn._parser import parse as _parse
from rdn._serializer import stringify as _stringify
from rdn.decoder import RDNDecoder
from rdn.encoder import RDNEncoder
from rdn.exceptions import MAX_SAFE_INTEGER, RDNDecodeError

try:
    __version__ = _meta_version("rdn")
except PackageNotFoundError:
    __version__ = "0.0.0-dev"

# Built-in native extension (Rust + maturin).
# Hot-path calls (no hooks) are routed to the native implementation for
# speed. Falls back to pure Python if the extension failed to compile.
try:
    from rdn._native import parse as _native_parse, stringify as _native_stringify
    _USE_NATIVE = True
except ImportError:
    _USE_NATIVE = False

__all__ = ["loads", "load", "dumps", "dump", "parse", "stringify", "RDNDecoder", "RDNEncoder", "RDNDecodeError", "MAX_SAFE_INTEGER", "__version__"]


def dumps(obj: Any, *, skipkeys: bool = False, cls: type | None = None, ensure_ascii: bool = True, check_circular: bool = True, allow_nan: bool = True, indent: int | str | None = None, separators: tuple[str, str] | None = None, default: Callable[[Any], Any] | None = None, sort_keys: bool = False) -> str:
    """Serialize *obj* to an RDN-formatted string.

    API mirrors :func:`json.dumps` with RDN-specific extensions.

    Parameters
    ----------
    obj:
        The Python value to serialize.
    skipkeys:
        When ``True``, dict keys that are not strings are silently
        skipped instead of raising :class:`TypeError`. Default ``False``.
    cls:
        An optional encoder class (e.g. :class:`RDNEncoder` or a
        subclass). If provided, the class is instantiated and its
        ``encode()`` method is called instead of the default serializer.
    ensure_ascii:
        When ``True`` (default), non-ASCII characters are escaped as
        ``\\uXXXX``. When ``False``, they pass through verbatim.
    check_circular:
        When ``True`` (default), circular references in containers raise
        :class:`ValueError`.
    indent:
        Pretty-print indent level. ``None`` for compact output, an int
        for that many spaces, or a string used verbatim per level.
    separators:
        A ``(item_separator, key_separator)`` tuple overriding defaults.
    default:
        Callable invoked for objects with no native RDN serialization.
        Should return a serializable value or raise :class:`TypeError`.
    sort_keys:
        When ``True``, dictionary keys are sorted alphabetically.

    Returns
    -------
    str
        The RDN text representation.

    Raises
    ------
    TypeError
        If *obj* (or a nested value) is not serializable and no *default*
        handles it.
    ValueError
        If a circular reference is detected.
    """
    if cls is not None:
        encoder = cls(skipkeys=skipkeys, ensure_ascii=ensure_ascii, check_circular=check_circular, allow_nan=allow_nan, indent=indent, separators=separators, default=default, sort_keys=sort_keys)
        return encoder.encode(obj)

    # Native hot path: when no cls or default callback is provided, route to
    # the Rust native extension for significantly better performance.
    if _USE_NATIVE and default is None:
        return _native_stringify(obj, skipkeys=skipkeys, ensure_ascii=ensure_ascii, check_circular=check_circular, allow_nan=allow_nan, sort_keys=sort_keys, indent=indent, separators=separators)

    result = _stringify(obj, skipkeys=skipkeys, ensure_ascii=ensure_ascii, check_circular=check_circular, allow_nan=allow_nan, indent=indent, separators=separators, default=default, sort_keys=sort_keys)
    # _stringify returns str | None; for top-level None value it returns "null"
    # but for truly non-serializable values it raises TypeError.
    # The only case result can be None is if value is non-serializable, but
    # _stringify raises TypeError in that case. So result is always str here.
    assert result is not None
    return result


def dump(obj: Any, fp: IO[str], **kwargs: Any) -> None:
    """Serialize *obj* as an RDN-formatted stream to *fp*.

    Calls :func:`dumps` with all keyword arguments and writes the result
    to the file-like object *fp*.

    Parameters
    ----------
    obj:
        The Python value to serialize.
    fp:
        A file-like object with a ``.write()`` method (e.g.
        :class:`io.StringIO` or an open text file).
    **kwargs:
        All keyword arguments supported by :func:`dumps`.
    """
    fp.write(dumps(obj, **kwargs))


# ---------------------------------------------------------------------------
# Deserialization
# ---------------------------------------------------------------------------

def loads(
    s: str | bytes | bytearray,
    *,
    cls: type | None = None,
    object_hook: Callable[[dict[str, Any]], Any] | None = None,
    parse_float: Callable[[str], Any] | None = None,
    parse_int: Callable[[str], Any] | None = None,
    parse_bigint: Callable[[str], Any] | None = None,
    parse_datetime: Callable[[datetime], Any] | None = None,
    parse_timeonly: Callable[[time], Any] | None = None,
    parse_duration: Callable[[timedelta | str], Any] | None = None,
    parse_regexp: Callable[[re.Pattern[str]], Any] | None = None,
    parse_binary: Callable[[bytes], Any] | None = None,
    object_pairs_hook: Callable[[list[tuple[str, Any]]], Any] | None = None,
) -> Any:
    """Deserialize *s* (an RDN document) to a Python object.

    API mirrors :func:`json.loads` with RDN-specific hook extensions.

    Parameters
    ----------
    s:
        The RDN document. May be ``str``, ``bytes``, or ``bytearray``.
        ``bytes``/``bytearray`` are decoded as UTF-8.
    cls:
        An optional decoder class (e.g. :class:`RDNDecoder` or a
        subclass). If provided, the class is instantiated with the
        hook parameters and its ``decode()`` method is called.
    object_hook:
        Called with each parsed ``dict`` (Object).
    parse_float:
        Called with the string representation of each float.
    parse_int:
        Called with the string representation of each integer.
    parse_bigint:
        Called with the string representation of each bigint (without the
        ``n`` suffix).
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
        If *s* is not valid RDN.
    TypeError
        If *s* is not ``str``, ``bytes``, or ``bytearray``.
    """
    if cls is not None:
        # Instantiate decoder with applicable hooks and delegate
        if isinstance(s, (bytes, bytearray)):
            s = s.decode("utf-8")
        decoder = cls(
            object_hook=object_hook,
            parse_float=parse_float,
            parse_int=parse_int,
            parse_bigint=parse_bigint,
            parse_datetime=parse_datetime,
            parse_timeonly=parse_timeonly,
            parse_duration=parse_duration,
            parse_regexp=parse_regexp,
            parse_binary=parse_binary,
            object_pairs_hook=object_pairs_hook,
        )
        return decoder.decode(s)

    # Native hot path: when no hooks/callbacks are provided, route to the
    # Rust native extension for significantly better performance.
    if _USE_NATIVE and all(x is None for x in [object_hook, parse_float, parse_int, parse_bigint, parse_datetime, parse_timeonly, parse_duration, parse_regexp, parse_binary, object_pairs_hook]):
        text = s if isinstance(s, str) else s.decode("utf-8") if isinstance(s, (bytes, bytearray)) else None
        if text is None:
            raise TypeError("First argument must be a string, bytes, or bytearray")
        return _native_parse(text)

    return _parse(
        s,
        parse_int=parse_int,
        parse_float=parse_float,
        parse_bigint=parse_bigint,
        parse_datetime=parse_datetime,
        parse_timeonly=parse_timeonly,
        parse_duration=parse_duration,
        parse_regexp=parse_regexp,
        parse_binary=parse_binary,
        object_hook=object_hook,
        object_pairs_hook=object_pairs_hook,
    )


def load(fp: IO[str] | IO[bytes], **kwargs: Any) -> Any:
    """Deserialize *fp* (a file-like object containing an RDN document) to a Python object.

    Reads the entire contents via ``fp.read()`` and delegates to
    :func:`loads`.

    Parameters
    ----------
    fp:
        A file-like object with a ``.read()`` method.
    **kwargs:
        All keyword arguments supported by :func:`loads`.

    Returns
    -------
    Any
        The decoded Python value.
    """
    return loads(fp.read(), **kwargs)


# ---------------------------------------------------------------------------
# Cross-implementation aliases (match TypeScript/Rust API)
# ---------------------------------------------------------------------------

parse = loads
stringify = dumps
