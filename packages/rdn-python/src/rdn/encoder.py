"""RDNEncoder class (mirrors json.JSONEncoder).

Provides a class-based encoding API so users can subclass and customise
behaviour, matching the pattern established by :class:`json.JSONEncoder`.
"""

from __future__ import annotations

from typing import Any, Callable, Iterator

from rdn._serializer import stringify as _stringify


class RDNEncoder:
    """Extensible RDN encoder.

    Instantiate with optional settings, then call :meth:`encode` or
    :meth:`iterencode` to produce RDN text.

    Users can subclass and override :meth:`default` to handle custom
    types::

        class CustomEncoder(RDNEncoder):
            def default(self, o):
                if isinstance(o, MyType):
                    return str(o)
                return super().default(o)

    Parameters
    ----------
    ensure_ascii:
        When ``True`` (default), non-ASCII characters are escaped as
        ``\\uXXXX``. When ``False``, they pass through verbatim.
    check_circular:
        When ``True`` (default), circular references in containers
        raise :class:`ValueError`.
    indent:
        Pretty-print indent level. ``None`` for compact output, an int
        for that many spaces, or a string used verbatim per level.
    separators:
        A ``(item_separator, key_separator)`` tuple overriding defaults.
    default:
        Callable invoked for objects with no native RDN serialization.
        If provided, this **replaces** :meth:`default` for this instance.
    sort_keys:
        When ``True``, dictionary keys are sorted alphabetically.
    """

    def __init__(
        self,
        *,
        ensure_ascii: bool = True,
        check_circular: bool = True,
        indent: int | str | None = None,
        separators: tuple[str, str] | None = None,
        default: Callable[[Any], Any] | None = None,
        sort_keys: bool = False,
    ) -> None:
        self.ensure_ascii = ensure_ascii
        self.check_circular = check_circular
        self.indent = indent
        self.separators = separators
        self.sort_keys = sort_keys
        if default is not None:
            self.default = default  # type: ignore[assignment]

    def encode(self, o: Any) -> str:
        """Return the RDN string representation of a Python value.

        Delegates to the module-level serializer with stored settings,
        using :meth:`default` as the fallback handler for unsupported
        types.

        Parameters
        ----------
        o:
            The Python value to serialize.

        Returns
        -------
        str
            The RDN text representation.

        Raises
        ------
        TypeError
            If *o* (or a nested value) is not serializable and
            :meth:`default` does not handle it.
        ValueError
            If a circular reference is detected.
        """
        result = _stringify(
            o,
            ensure_ascii=self.ensure_ascii,
            check_circular=self.check_circular,
            indent=self.indent,
            separators=self.separators,
            default=self.default,
            sort_keys=self.sort_keys,
        )
        assert result is not None
        return result

    def iterencode(self, o: Any) -> Iterator[str]:
        """Encode the given object and yield each string chunk.

        For simplicity this yields the full encoded result as a single
        chunk.  A more sophisticated implementation could yield
        per-element, but the single-chunk approach satisfies the
        ``json.JSONEncoder.iterencode`` contract and is sufficient for
        streaming use cases where the caller writes chunks to a file.

        Parameters
        ----------
        o:
            The Python value to serialize.

        Yields
        ------
        str
            One or more string chunks whose concatenation equals
            ``self.encode(o)``.
        """
        yield self.encode(o)

    def default(self, o: Any) -> Any:
        """Handle objects that the encoder cannot serialize by default.

        The base implementation always raises :class:`TypeError`.
        Override this in a subclass to add support for additional types::

            class CustomEncoder(RDNEncoder):
                def default(self, o):
                    if isinstance(o, MyType):
                        return str(o)
                    return super().default(o)

        Parameters
        ----------
        o:
            The object that could not be serialized.

        Raises
        ------
        TypeError
            Always, unless overridden in a subclass.
        """
        raise TypeError(f"Object of type {type(o).__name__} is not RDN serializable")
