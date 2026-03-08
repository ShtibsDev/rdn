"""RDN exception types and constants."""

from __future__ import annotations

#: JavaScript Number.MAX_SAFE_INTEGER — used by the serializer for BigInt auto-promote detection.
MAX_SAFE_INTEGER: int = 2**53 - 1  # 9007199254740991


class RDNDecodeError(ValueError):
    """Exception raised when an RDN document cannot be decoded.

    Modelled after :class:`json.JSONDecodeError` — inherits from
    :class:`ValueError` so callers can catch either.

    Parameters
    ----------
    msg:
        Human-readable description of the error.
    doc:
        The RDN document being parsed.
    pos:
        Character offset where the error was detected (0-indexed).

    Attributes
    ----------
    msg : str
    doc : str
    pos : int
    lineno : int
        1-indexed line number derived from *doc* and *pos*.
    colno : int
        1-indexed column number derived from *doc* and *pos*.
    """

    msg: str
    doc: str
    pos: int
    lineno: int
    colno: int

    def __init__(self, msg: str, doc: str, pos: int) -> None:
        self.msg = msg
        self.doc = doc
        self.pos = pos
        self.lineno = doc.count("\n", 0, pos) + 1
        self.colno = pos - doc.rfind("\n", 0, pos)
        super().__init__(str(self))

    def __str__(self) -> str:
        return f"{self.msg} in RDN at position {self.pos} (line {self.lineno} column {self.colno})"

    def __repr__(self) -> str:
        return f"{self.__class__.__name__}({self.msg!r}, {self.doc!r}, {self.pos!r})"
