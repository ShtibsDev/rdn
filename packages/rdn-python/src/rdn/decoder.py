"""RDNDecoder class (mirrors json.JSONDecoder).

Provides a class-based decoding API so users can subclass and customise
behaviour, matching the pattern established by :class:`json.JSONDecoder`.
"""

from __future__ import annotations

import re
from datetime import datetime, time, timedelta
from typing import Any, Callable

from rdn._parser import parse as _parse, raw_parse as _raw_parse


class RDNDecoder:
    """Extensible RDN decoder.

    Instantiate with optional hook callables, then call :meth:`decode` or
    :meth:`raw_decode` to parse RDN text.

    Parameters
    ----------
    object_hook:
        Called with each parsed ``dict`` (Object).
    parse_float:
        Called with the string representation of each float.
    parse_int:
        Called with the string representation of each integer.
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
    object_pairs_hook:
        Called with a list of ``(key, value)`` pairs for each object.
        Takes priority over *object_hook*.
    """

    def __init__(
        self,
        *,
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
    ) -> None:
        self.object_hook = object_hook
        self.parse_float = parse_float
        self.parse_int = parse_int
        self.parse_bigint = parse_bigint
        self.parse_datetime = parse_datetime
        self.parse_timeonly = parse_timeonly
        self.parse_duration = parse_duration
        self.parse_regexp = parse_regexp
        self.parse_binary = parse_binary
        self.object_pairs_hook = object_pairs_hook

    def decode(self, s: str) -> Any:
        """Decode an RDN string and return the Python representation.

        Delegates to the module-level parser with stored hooks.

        Parameters
        ----------
        s:
            The RDN document string.

        Returns
        -------
        Any
            The decoded Python value.

        Raises
        ------
        RDNDecodeError
            If *s* is not valid RDN.
        """
        return _parse(
            s,
            parse_int=self.parse_int,
            parse_float=self.parse_float,
            parse_bigint=self.parse_bigint,
            parse_datetime=self.parse_datetime,
            parse_timeonly=self.parse_timeonly,
            parse_duration=self.parse_duration,
            parse_regexp=self.parse_regexp,
            parse_binary=self.parse_binary,
            object_hook=self.object_hook,
            object_pairs_hook=self.object_pairs_hook,
        )

    def raw_decode(self, s: str, idx: int = 0) -> tuple[Any, int]:
        """Decode an RDN value starting at position *idx*.

        Unlike :meth:`decode`, this does **not** require the value to
        consume the entire string.  Returns a ``(value, end_position)``
        tuple where *end_position* is the index of the first unconsumed
        character after the parsed value (trailing whitespace is consumed).

        Parameters
        ----------
        s:
            The source string.
        idx:
            Character offset at which to start parsing (default ``0``).

        Returns
        -------
        tuple[Any, int]
            ``(parsed_value, end_position)``

        Raises
        ------
        RDNDecodeError
            If no valid RDN value can be parsed starting at *idx*.
        """
        return _raw_parse(
            s,
            idx=idx,
            parse_int=self.parse_int,
            parse_float=self.parse_float,
            parse_bigint=self.parse_bigint,
            parse_datetime=self.parse_datetime,
            parse_timeonly=self.parse_timeonly,
            parse_duration=self.parse_duration,
            parse_regexp=self.parse_regexp,
            parse_binary=self.parse_binary,
            object_hook=self.object_hook,
            object_pairs_hook=self.object_pairs_hook,
        )
