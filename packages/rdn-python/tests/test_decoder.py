"""Tests for RDNDecoder -- class-based decoding API (Task 14)."""

from __future__ import annotations

import math
import re
from collections import OrderedDict
from datetime import datetime, time, timedelta, timezone
from decimal import Decimal
from types import SimpleNamespace

import pytest

import rdn
from rdn.decoder import RDNDecoder
from rdn.exceptions import RDNDecodeError


# ---------------------------------------------------------------------------
# Basic decode()
# ---------------------------------------------------------------------------

class TestDecodeBasic:
    def test_decode_object(self) -> None:
        result = RDNDecoder().decode('{"a": 1}')
        assert result == {"a": 1}

    def test_decode_string(self) -> None:
        assert RDNDecoder().decode('"hello"') == "hello"

    def test_decode_integer(self) -> None:
        assert RDNDecoder().decode("42") == 42

    def test_decode_float(self) -> None:
        assert RDNDecoder().decode("3.14") == 3.14

    def test_decode_true(self) -> None:
        assert RDNDecoder().decode("true") is True

    def test_decode_false(self) -> None:
        assert RDNDecoder().decode("false") is False

    def test_decode_null(self) -> None:
        assert RDNDecoder().decode("null") is None

    def test_decode_array(self) -> None:
        assert RDNDecoder().decode("[1, 2, 3]") == [1, 2, 3]

    def test_decode_nested(self) -> None:
        result = RDNDecoder().decode('{"a": [1, 2], "b": {"c": 3}}')
        assert result == {"a": [1, 2], "b": {"c": 3}}

    def test_decode_nan(self) -> None:
        result = RDNDecoder().decode("NaN")
        assert math.isnan(result)

    def test_decode_infinity(self) -> None:
        assert RDNDecoder().decode("Infinity") == float("inf")

    def test_decode_negative_infinity(self) -> None:
        assert RDNDecoder().decode("-Infinity") == float("-inf")

    def test_decode_empty_object(self) -> None:
        assert RDNDecoder().decode("{}") == {}

    def test_decode_empty_array(self) -> None:
        assert RDNDecoder().decode("[]") == []


# ---------------------------------------------------------------------------
# Extended RDN types in decode()
# ---------------------------------------------------------------------------

class TestDecodeExtendedTypes:
    def test_bigint(self) -> None:
        assert RDNDecoder().decode("42n") == 42

    def test_datetime(self) -> None:
        result = RDNDecoder().decode("@2024-01-15T10:30:00Z")
        assert result == datetime(2024, 1, 15, 10, 30, 0, tzinfo=timezone.utc)

    def test_timeonly(self) -> None:
        result = RDNDecoder().decode("@14:30:00")
        assert result == time(14, 30, 0)

    def test_duration(self) -> None:
        result = RDNDecoder().decode("@PT30S")
        assert result == timedelta(seconds=30)

    def test_regexp(self) -> None:
        result = RDNDecoder().decode("/test/i")
        assert result == re.compile("test", re.IGNORECASE)

    def test_binary_b64(self) -> None:
        result = RDNDecoder().decode('b"SGVsbG8="')
        assert result == b"Hello"

    def test_binary_hex(self) -> None:
        result = RDNDecoder().decode('x"48656C6C6F"')
        assert result == b"Hello"

    def test_tuple(self) -> None:
        result = RDNDecoder().decode("(1, 2, 3)")
        assert result == (1, 2, 3)

    def test_set(self) -> None:
        result = RDNDecoder().decode("{1, 2, 3}")
        assert result == frozenset({1, 2, 3})


# ---------------------------------------------------------------------------
# Hooks passed to RDNDecoder
# ---------------------------------------------------------------------------

class TestDecodeHooks:
    def test_parse_bigint_hook(self) -> None:
        """Acceptance criterion: custom parse_bigint."""
        result = RDNDecoder(parse_bigint=lambda s: Decimal(s)).decode("42n")
        assert result == Decimal("42")
        assert isinstance(result, Decimal)

    def test_parse_int_hook(self) -> None:
        result = RDNDecoder(parse_int=Decimal).decode("42")
        assert result == Decimal("42")
        assert isinstance(result, Decimal)

    def test_parse_float_hook(self) -> None:
        result = RDNDecoder(parse_float=Decimal).decode("3.14")
        assert result == Decimal("3.14")
        assert isinstance(result, Decimal)

    def test_parse_datetime_hook(self) -> None:
        result = RDNDecoder(parse_datetime=lambda dt: dt.isoformat()).decode("@2024-01-15T10:30:00Z")
        assert result == "2024-01-15T10:30:00+00:00"

    def test_parse_timeonly_hook(self) -> None:
        result = RDNDecoder(parse_timeonly=lambda t: t.isoformat()).decode("@14:30:00")
        assert result == "14:30:00"

    def test_parse_duration_hook(self) -> None:
        result = RDNDecoder(parse_duration=lambda d: str(d)).decode("@PT30S")
        assert result == "0:00:30"

    def test_parse_regexp_hook(self) -> None:
        result = RDNDecoder(parse_regexp=lambda p: p.pattern).decode("/test/i")
        assert result == "test"

    def test_parse_binary_hook(self) -> None:
        result = RDNDecoder(parse_binary=lambda b: b.decode("utf-8")).decode('b"SGVsbG8="')
        assert result == "Hello"

    def test_object_hook(self) -> None:
        result = RDNDecoder(object_hook=lambda d: SimpleNamespace(**d)).decode('{"a": 1}')
        assert isinstance(result, SimpleNamespace)
        assert result.a == 1

    def test_object_pairs_hook(self) -> None:
        result = RDNDecoder(object_pairs_hook=OrderedDict).decode('{"b": 2, "a": 1}')
        assert isinstance(result, OrderedDict)
        assert list(result.keys()) == ["b", "a"]

    def test_object_pairs_hook_priority(self) -> None:
        """object_pairs_hook takes priority over object_hook."""
        result = RDNDecoder(
            object_hook=lambda d: "OBJECT",
            object_pairs_hook=lambda pairs: "PAIRS",
        ).decode('{"a": 1}')
        assert result == "PAIRS"


# ---------------------------------------------------------------------------
# raw_decode()
# ---------------------------------------------------------------------------

class TestRawDecode:
    def test_basic_raw_decode(self) -> None:
        """Acceptance criterion: raw_decode returns (value, end_position)."""
        value, end = RDNDecoder().raw_decode('[1, 2] extra', 0)
        assert value == [1, 2]
        assert end == 7  # position after '] ' (trailing whitespace consumed)

    def test_raw_decode_from_idx(self) -> None:
        value, end = RDNDecoder().raw_decode('   42 rest', 3)
        assert value == 42
        assert end == 6  # position after '42 '

    def test_raw_decode_string(self) -> None:
        value, end = RDNDecoder().raw_decode('"hello" world', 0)
        assert value == "hello"
        assert end == 8  # position after '"hello" '

    def test_raw_decode_object(self) -> None:
        value, end = RDNDecoder().raw_decode('{"a": 1}{"b": 2}', 0)
        assert value == {"a": 1}
        assert end == 8

    def test_raw_decode_second_value(self) -> None:
        """Parse the second value using the end position from the first."""
        text = '{"a": 1} {"b": 2}'
        v1, pos = RDNDecoder().raw_decode(text, 0)
        v2, end = RDNDecoder().raw_decode(text, pos)
        assert v1 == {"a": 1}
        assert v2 == {"b": 2}

    def test_raw_decode_with_hooks(self) -> None:
        decoder = RDNDecoder(parse_int=Decimal)
        value, end = decoder.raw_decode("42 rest", 0)
        assert value == Decimal("42")
        assert isinstance(value, Decimal)

    def test_raw_decode_at_end_of_string(self) -> None:
        value, end = RDNDecoder().raw_decode("true", 0)
        assert value is True
        assert end == 4

    def test_raw_decode_whitespace_skip(self) -> None:
        """Trailing whitespace is consumed."""
        value, end = RDNDecoder().raw_decode("42   ", 0)
        assert value == 42
        assert end == 5  # all trailing whitespace consumed

    def test_raw_decode_invalid_raises(self) -> None:
        with pytest.raises(RDNDecodeError):
            RDNDecoder().raw_decode("!!!invalid", 0)

    def test_raw_decode_empty_at_idx_raises(self) -> None:
        with pytest.raises(RDNDecodeError):
            RDNDecoder().raw_decode("hello", 5)


# ---------------------------------------------------------------------------
# decode() error cases
# ---------------------------------------------------------------------------

class TestDecodeErrors:
    def test_invalid_input(self) -> None:
        with pytest.raises(RDNDecodeError):
            RDNDecoder().decode("not valid rdn")

    def test_trailing_content(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected data after value"):
            RDNDecoder().decode("42 43")

    def test_empty_input(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            RDNDecoder().decode("")


# ---------------------------------------------------------------------------
# cls parameter in rdn.loads()
# ---------------------------------------------------------------------------

class TestClsParameter:
    def test_loads_with_cls(self) -> None:
        """Acceptance criterion: rdn.loads(text, cls=RDNDecoder) uses the provided class."""
        result = rdn.loads('{"a": 1}', cls=RDNDecoder)
        assert result == {"a": 1}

    def test_loads_with_cls_and_hooks(self) -> None:
        """All hooks are passed through cls."""
        result = rdn.loads("42n", cls=RDNDecoder, parse_bigint=lambda s: Decimal(s))
        assert result == Decimal("42")
        assert isinstance(result, Decimal)

    def test_loads_with_cls_and_all_rdn_hooks(self) -> None:
        """All RDN-specific hooks are forwarded to the decoder class."""
        calls: dict[str, list] = {"bigint": [], "datetime": [], "timeonly": [], "duration": [], "regexp": [], "binary": []}

        def track_bigint(s: str) -> int:
            calls["bigint"].append(s)
            return int(s)

        def track_datetime(dt: datetime) -> datetime:
            calls["datetime"].append(dt)
            return dt

        result = rdn.loads("42n", cls=RDNDecoder, parse_bigint=track_bigint)
        assert calls["bigint"] == ["42"]

    def test_loads_with_custom_decoder_subclass(self) -> None:
        class MyDecoder(RDNDecoder):
            def decode(self, s: str):
                result = super().decode(s)
                if isinstance(result, dict):
                    result["_decoded_by"] = "MyDecoder"
                return result

        result = rdn.loads('{"a": 1}', cls=MyDecoder)
        assert result["_decoded_by"] == "MyDecoder"
        assert result["a"] == 1

    def test_loads_with_bytes_and_cls(self) -> None:
        """bytes input should be decoded to str before passing to cls."""
        result = rdn.loads(b'"hello"', cls=RDNDecoder)
        assert result == "hello"

    def test_loads_without_cls(self) -> None:
        """Without cls, loads still works normally."""
        result = rdn.loads('{"a": 1}')
        assert result == {"a": 1}


# ---------------------------------------------------------------------------
# RDNDecoder is exported from rdn module
# ---------------------------------------------------------------------------

class TestExport:
    def test_rdn_decoder_exported(self) -> None:
        assert hasattr(rdn, "RDNDecoder")
        assert rdn.RDNDecoder is RDNDecoder
