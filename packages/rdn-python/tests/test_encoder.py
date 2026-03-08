"""Tests for RDNEncoder -- class-based encoding API (Task 14)."""

from __future__ import annotations

import math
import re
from datetime import datetime, time, timedelta, timezone

import pytest

import rdn
from rdn.encoder import RDNEncoder


# ---------------------------------------------------------------------------
# Basic encode()
# ---------------------------------------------------------------------------

class TestEncodeBasic:
    def test_encode_object(self) -> None:
        """Acceptance criterion: RDNEncoder().encode({"a": 1}) returns '{"a":1}'."""
        assert RDNEncoder().encode({"a": 1}) == '{"a":1}'

    def test_encode_string(self) -> None:
        assert RDNEncoder().encode("hello") == '"hello"'

    def test_encode_integer(self) -> None:
        assert RDNEncoder().encode(42) == "42"

    def test_encode_float(self) -> None:
        assert RDNEncoder().encode(3.14) == "3.14"

    def test_encode_true(self) -> None:
        assert RDNEncoder().encode(True) == "true"

    def test_encode_false(self) -> None:
        assert RDNEncoder().encode(False) == "false"

    def test_encode_null(self) -> None:
        assert RDNEncoder().encode(None) == "null"

    def test_encode_array(self) -> None:
        assert RDNEncoder().encode([1, 2, 3]) == "[1,2,3]"

    def test_encode_nan(self) -> None:
        assert RDNEncoder().encode(float("nan")) == "NaN"

    def test_encode_infinity(self) -> None:
        assert RDNEncoder().encode(float("inf")) == "Infinity"

    def test_encode_negative_infinity(self) -> None:
        assert RDNEncoder().encode(float("-inf")) == "-Infinity"

    def test_encode_empty_object(self) -> None:
        assert RDNEncoder().encode({}) == "{}"

    def test_encode_empty_array(self) -> None:
        assert RDNEncoder().encode([]) == "[]"

    def test_encode_nested(self) -> None:
        result = RDNEncoder().encode({"a": [1, 2], "b": {"c": 3}})
        assert '"a":[1,2]' in result
        assert '"b":{"c":3}' in result


# ---------------------------------------------------------------------------
# Extended RDN types in encode()
# ---------------------------------------------------------------------------

class TestEncodeExtendedTypes:
    def test_bigint_auto_promote(self) -> None:
        assert RDNEncoder().encode(2**53) == str(2**53) + "n"

    def test_safe_int_no_suffix(self) -> None:
        assert RDNEncoder().encode(2**53 - 1) == str(2**53 - 1)

    def test_datetime(self) -> None:
        d = datetime(2024, 1, 15, 10, 30, 0, tzinfo=timezone.utc)
        assert RDNEncoder().encode(d) == "@2024-01-15T10:30:00.000Z"

    def test_timeonly(self) -> None:
        assert RDNEncoder().encode(time(14, 30, 0)) == "@14:30:00"

    def test_duration(self) -> None:
        assert RDNEncoder().encode(timedelta(seconds=30)) == "@PT30S"

    def test_regexp(self) -> None:
        assert RDNEncoder().encode(re.compile("test", re.IGNORECASE)) == "/test/i"

    def test_binary(self) -> None:
        assert RDNEncoder().encode(b"Hello") == 'b"SGVsbG8="'

    def test_tuple(self) -> None:
        assert RDNEncoder().encode((1, 2, 3)) == "(1,2,3)"

    def test_set(self) -> None:
        assert RDNEncoder().encode({42}) == "Set{42}"

    def test_frozenset(self) -> None:
        assert RDNEncoder().encode(frozenset({42})) == "Set{42}"


# ---------------------------------------------------------------------------
# Encoder settings
# ---------------------------------------------------------------------------

class TestEncoderSettings:
    def test_indent_int(self) -> None:
        """Acceptance criterion: RDNEncoder(indent=2).encode({"a": 1}) returns pretty-printed output."""
        result = RDNEncoder(indent=2).encode({"a": 1})
        expected = '{\n  "a": 1\n}'
        assert result == expected

    def test_indent_string(self) -> None:
        result = RDNEncoder(indent="\t").encode([1, 2])
        expected = "[\n\t1,\n\t2\n]"
        assert result == expected

    def test_sort_keys(self) -> None:
        result = RDNEncoder(sort_keys=True).encode({"c": 3, "a": 1, "b": 2})
        assert result == '{"a":1,"b":2,"c":3}'

    def test_ensure_ascii_true(self) -> None:
        result = RDNEncoder(ensure_ascii=True).encode("\u00e9")
        assert result == '"\\u00e9"'

    def test_ensure_ascii_false(self) -> None:
        result = RDNEncoder(ensure_ascii=False).encode("\u00e9")
        assert result == '"\u00e9"'

    def test_custom_separators(self) -> None:
        result = RDNEncoder(separators=(", ", ": ")).encode([1, 2, 3])
        assert result == "[1, 2, 3]"

    def test_check_circular_true(self) -> None:
        a: list = [1]
        a.append(a)
        with pytest.raises(ValueError, match="Converting circular structure to RDN"):
            RDNEncoder(check_circular=True).encode(a)

    def test_check_circular_false(self) -> None:
        shared = [1]
        result = RDNEncoder(check_circular=False).encode([shared, shared])
        assert result == "[[1],[1]]"


# ---------------------------------------------------------------------------
# default() method
# ---------------------------------------------------------------------------

class TestDefault:
    def test_default_raises_typeerror(self) -> None:
        """Base default() always raises TypeError."""
        with pytest.raises(TypeError, match="object.*not RDN serializable"):
            RDNEncoder().encode(object())

    def test_default_callable_in_constructor(self) -> None:
        """Passing default= to constructor overrides the method."""
        encoder = RDNEncoder(default=lambda o: str(o))
        result = encoder.encode(object())
        assert result.startswith('"')

    def test_custom_subclass_default(self) -> None:
        """Acceptance criterion: custom subclass with overridden default() works."""
        class Point:
            def __init__(self, x: int, y: int) -> None:
                self.x = x
                self.y = y

        class CustomEncoder(RDNEncoder):
            def default(self, o):
                if isinstance(o, Point):
                    return {"x": o.x, "y": o.y}
                return super().default(o)

        result = CustomEncoder(sort_keys=True).encode(Point(1, 2))
        assert result == '{"x":1,"y":2}'

    def test_custom_subclass_default_still_raises_for_unknown(self) -> None:
        """Subclass default() that calls super() still raises for truly unknown types."""
        class MyEncoder(RDNEncoder):
            def default(self, o):
                return super().default(o)

        with pytest.raises(TypeError, match="not RDN serializable"):
            MyEncoder().encode(object())

    def test_default_in_container(self) -> None:
        """default function works for items inside containers."""
        class Wrapper:
            def __init__(self, val: int) -> None:
                self.val = val

        encoder = RDNEncoder(default=lambda o: o.val if isinstance(o, Wrapper) else None)
        result = encoder.encode([Wrapper(1), Wrapper(2)])
        assert result == "[1,2]"


# ---------------------------------------------------------------------------
# iterencode()
# ---------------------------------------------------------------------------

class TestIterencode:
    def test_iterencode_yields_chunks(self) -> None:
        """Acceptance criterion: list(RDNEncoder().iterencode([1, 2])) yields correct chunks."""
        chunks = list(RDNEncoder().iterencode([1, 2]))
        combined = "".join(chunks)
        assert combined == "[1,2]"

    def test_iterencode_single_value(self) -> None:
        chunks = list(RDNEncoder().iterencode(42))
        assert "".join(chunks) == "42"

    def test_iterencode_string(self) -> None:
        chunks = list(RDNEncoder().iterencode("hello"))
        assert "".join(chunks) == '"hello"'

    def test_iterencode_object(self) -> None:
        chunks = list(RDNEncoder().iterencode({"a": 1}))
        assert "".join(chunks) == '{"a":1}'

    def test_iterencode_with_indent(self) -> None:
        chunks = list(RDNEncoder(indent=2).iterencode([1, 2]))
        combined = "".join(chunks)
        expected = "[\n  1,\n  2\n]"
        assert combined == expected

    def test_iterencode_returns_iterator(self) -> None:
        result = RDNEncoder().iterencode([1, 2])
        assert hasattr(result, "__iter__")
        assert hasattr(result, "__next__")


# ---------------------------------------------------------------------------
# cls parameter in rdn.dumps()
# ---------------------------------------------------------------------------

class TestClsParameter:
    def test_dumps_with_cls(self) -> None:
        """Acceptance criterion: rdn.dumps(obj, cls=RDNEncoder) uses the provided class."""
        result = rdn.dumps({"a": 1}, cls=RDNEncoder)
        assert result == '{"a":1}'

    def test_dumps_with_cls_and_settings(self) -> None:
        result = rdn.dumps({"a": 1}, cls=RDNEncoder, indent=2)
        expected = '{\n  "a": 1\n}'
        assert result == expected

    def test_dumps_with_custom_encoder_subclass(self) -> None:
        class Coord:
            def __init__(self, x: int, y: int):
                self.x = x
                self.y = y

        class CoordEncoder(RDNEncoder):
            def default(self, o):
                if isinstance(o, Coord):
                    return (o.x, o.y)
                return super().default(o)

        result = rdn.dumps(Coord(10, 20), cls=CoordEncoder)
        assert result == "(10,20)"

    def test_dumps_with_cls_passes_sort_keys(self) -> None:
        result = rdn.dumps({"b": 2, "a": 1}, cls=RDNEncoder, sort_keys=True)
        assert result == '{"a":1,"b":2}'

    def test_dumps_without_cls(self) -> None:
        """Without cls, dumps still works normally."""
        result = rdn.dumps({"a": 1})
        assert result == '{"a":1}'


# ---------------------------------------------------------------------------
# RDNEncoder is exported from rdn module
# ---------------------------------------------------------------------------

class TestExport:
    def test_rdn_encoder_exported(self) -> None:
        assert hasattr(rdn, "RDNEncoder")
        assert rdn.RDNEncoder is RDNEncoder
