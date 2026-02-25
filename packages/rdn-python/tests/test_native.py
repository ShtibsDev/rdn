"""Tests specific to the native Rust extension (rdn._native).

These tests verify:
- The native module is available and active
- Direct rdn._native.parse() / rdn._native.stringify() calls
- Parity with the pure Python implementation
- Fallback behavior when hooks are provided
"""

from __future__ import annotations

import math
import re
from datetime import datetime, time, timedelta, timezone

import pytest

import rdn
from rdn.exceptions import RDNDecodeError

# Skip all tests in this module if native extension is not available
pytestmark = pytest.mark.skipif(not rdn._USE_NATIVE, reason="Native extension not available")


class TestNativeAvailability:
    """Verify the native extension is loaded."""

    def test_use_native_flag(self) -> None:
        assert rdn._USE_NATIVE is True

    def test_native_module_importable(self) -> None:
        from rdn._native import parse, stringify
        assert callable(parse)
        assert callable(stringify)


class TestNativeParse:
    """Test rdn._native.parse() directly."""

    def test_null(self) -> None:
        from rdn._native import parse
        assert parse("null") is None

    def test_true(self) -> None:
        from rdn._native import parse
        assert parse("true") is True

    def test_false(self) -> None:
        from rdn._native import parse
        assert parse("false") is False

    def test_integer(self) -> None:
        from rdn._native import parse
        assert parse("42") == 42
        assert parse("-7") == -7
        assert parse("0") == 0

    def test_float(self) -> None:
        from rdn._native import parse
        assert parse("3.14") == 3.14
        assert parse("-0.5") == -0.5
        assert parse("1e10") == 1e10

    def test_special_floats(self) -> None:
        from rdn._native import parse
        assert math.isnan(parse("NaN"))
        assert parse("Infinity") == float("inf")
        assert parse("-Infinity") == float("-inf")

    def test_string(self) -> None:
        from rdn._native import parse
        assert parse('"hello"') == "hello"
        assert parse('"hello\\nworld"') == "hello\nworld"
        assert parse('"\\u0041"') == "A"

    def test_bigint(self) -> None:
        from rdn._native import parse
        assert parse("9007199254740992n") == 9007199254740992
        assert parse("-9007199254740992n") == -9007199254740992

    def test_array(self) -> None:
        from rdn._native import parse
        assert parse("[1, 2, 3]") == [1, 2, 3]
        assert parse("[]") == []

    def test_tuple(self) -> None:
        from rdn._native import parse
        assert parse("(1, 2, 3)") == (1, 2, 3)
        assert parse("()") == ()

    def test_object(self) -> None:
        from rdn._native import parse
        assert parse('{"a": 1, "b": 2}') == {"a": 1, "b": 2}
        assert parse("{}") == {}

    def test_set(self) -> None:
        from rdn._native import parse
        assert parse("{1, 2, 3}") == frozenset({1, 2, 3})
        assert parse("Set{}") == frozenset()

    def test_map(self) -> None:
        from rdn._native import parse
        assert parse('{1 => "one", 2 => "two"}') == {1: "one", 2: "two"}
        assert parse("Map{}") == {}

    def test_datetime(self) -> None:
        from rdn._native import parse
        result = parse("@2024-01-15T10:30:00.000Z")
        assert isinstance(result, datetime)
        assert result.year == 2024
        assert result.month == 1
        assert result.day == 15

    def test_timeonly(self) -> None:
        from rdn._native import parse
        result = parse("@10:30:00")
        assert isinstance(result, time)
        assert result.hour == 10
        assert result.minute == 30

    def test_duration(self) -> None:
        from rdn._native import parse
        result = parse("@PT1H30M")
        assert isinstance(result, timedelta)
        assert result == timedelta(hours=1, minutes=30)

    def test_regexp(self) -> None:
        from rdn._native import parse
        result = parse("/hello/i")
        assert isinstance(result, re.Pattern)
        assert result.pattern == "hello"
        assert result.flags & re.IGNORECASE

    def test_binary_base64(self) -> None:
        from rdn._native import parse
        assert parse('b"SGVsbG8="') == b"Hello"

    def test_binary_hex(self) -> None:
        from rdn._native import parse
        assert parse('x"48656c6c6f"') == b"Hello"

    def test_error_position(self) -> None:
        from rdn._native import parse
        with pytest.raises(RDNDecodeError) as exc_info:
            parse("[1, 2, ]")
        err = exc_info.value
        assert err.pos == 7
        assert err.lineno == 1

    def test_nested_containers(self) -> None:
        from rdn._native import parse
        result = parse('{"users": [{"name": "Alice", "scores": (1, 2, 3)}]}')
        assert result == {"users": [{"name": "Alice", "scores": (1, 2, 3)}]}


class TestNativeStringify:
    """Test rdn._native.stringify() directly."""

    def test_null(self) -> None:
        from rdn._native import stringify
        assert stringify(None) == "null"

    def test_bool(self) -> None:
        from rdn._native import stringify
        assert stringify(True) == "true"
        assert stringify(False) == "false"

    def test_integer(self) -> None:
        from rdn._native import stringify
        assert stringify(42) == "42"
        assert stringify(-7) == "-7"

    def test_bigint(self) -> None:
        from rdn._native import stringify
        assert stringify(9007199254740992) == "9007199254740992n"

    def test_float(self) -> None:
        from rdn._native import stringify
        assert stringify(3.14) == "3.14"

    def test_special_floats(self) -> None:
        from rdn._native import stringify
        assert stringify(float("nan")) == "NaN"
        assert stringify(float("inf")) == "Infinity"
        assert stringify(float("-inf")) == "-Infinity"

    def test_string(self) -> None:
        from rdn._native import stringify
        assert stringify("hello") == '"hello"'

    def test_string_escape(self) -> None:
        from rdn._native import stringify
        assert stringify("hello\nworld") == '"hello\\nworld"'

    def test_list(self) -> None:
        from rdn._native import stringify
        assert stringify([1, 2, 3]) == "[1,2,3]"

    def test_tuple(self) -> None:
        from rdn._native import stringify
        assert stringify((1, 2, 3)) == "(1,2,3)"

    def test_dict(self) -> None:
        from rdn._native import stringify
        result = stringify({"a": 1})
        assert result == '{"a":1}'

    def test_set(self) -> None:
        from rdn._native import stringify
        assert stringify(frozenset()) == "Set{}"

    def test_datetime(self) -> None:
        from rdn._native import stringify
        dt = datetime(2024, 1, 15, 10, 30, 0, tzinfo=timezone.utc)
        assert stringify(dt) == "@2024-01-15T10:30:00.000Z"

    def test_timeonly(self) -> None:
        from rdn._native import stringify
        t = time(10, 30, 0)
        assert stringify(t) == "@10:30:00"

    def test_duration(self) -> None:
        from rdn._native import stringify
        td = timedelta(hours=1, minutes=30)
        assert stringify(td) == "@PT1H30M"

    def test_regexp(self) -> None:
        from rdn._native import stringify
        p = re.compile("hello", re.IGNORECASE)
        assert stringify(p) == "/hello/i"

    def test_binary(self) -> None:
        from rdn._native import stringify
        assert stringify(b"Hello") == 'b"SGVsbG8="'

    def test_sort_keys(self) -> None:
        from rdn._native import stringify
        result = stringify({"b": 2, "a": 1}, sort_keys=True)
        assert result == '{"a":1,"b":2}'

    def test_indent(self) -> None:
        from rdn._native import stringify
        result = stringify([1, 2], indent=2)
        assert "[\n  1,\n  2\n]" == result

    def test_ensure_ascii_false(self) -> None:
        from rdn._native import stringify
        result = stringify("café", ensure_ascii=False)
        assert result == '"café"'

    def test_circular_detection(self) -> None:
        from rdn._native import stringify
        a: list = [1]
        a.append(a)
        with pytest.raises(ValueError, match="circular"):
            stringify(a)


class TestNativeParity:
    """Verify that native and pure Python produce identical results."""

    PARSE_CASES = [
        "null", "true", "false", "42", "-7", "3.14", "1e10",
        '"hello"', '"hello\\nworld"', '"\\u0041"',
        "NaN", "Infinity", "-Infinity",
        "[1, 2, 3]", "[]", "(1, 2)", "()",
        '{"a": 1}', "{}",
        "@2024-01-15T10:30:00.000Z", "@10:30:00", "@PT1H30M",
        'b"SGVsbG8="', 'x"48656c6c6f"',
        "9007199254740992n",
        'Set{1, 2, 3}', "Set{}",
        'Map{1 => "one"}', "Map{}",
    ]

    def test_parse_parity(self) -> None:
        """Native parse produces the same results as pure Python parse."""
        from rdn._native import parse as native_parse
        from rdn._parser import parse as python_parse

        for case in self.PARSE_CASES:
            native_result = native_parse(case)
            python_result = python_parse(case)

            if isinstance(native_result, float) and math.isnan(native_result):
                assert math.isnan(python_result), f"NaN parity failed for {case!r}"
            else:
                assert native_result == python_result, f"Parity failed for {case!r}: native={native_result!r} python={python_result!r}"
                assert type(native_result) == type(python_result), f"Type parity failed for {case!r}: native={type(native_result)} python={type(python_result)}"

    STRINGIFY_VALUES = [
        None, True, False, 42, -7, 3.14, float("nan"), float("inf"), float("-inf"),
        "hello", "hello\nworld", "",
        [1, 2, 3], [], (1, 2), (),
        {"a": 1}, {},
        frozenset({1, 2, 3}), frozenset(),
        datetime(2024, 1, 15, 10, 30, 0, tzinfo=timezone.utc),
        time(10, 30, 0), time(10, 30, 0, 500000),
        timedelta(hours=1, minutes=30), timedelta(0),
        re.compile("hello", re.IGNORECASE),
        b"Hello", b"",
        9007199254740992,
    ]

    def test_stringify_parity(self) -> None:
        """Native stringify produces the same results as pure Python stringify."""
        from rdn._native import stringify as native_stringify
        from rdn._serializer import stringify as python_stringify

        for value in self.STRINGIFY_VALUES:
            native_result = native_stringify(value)
            python_result = python_stringify(value)

            assert native_result == python_result, f"Parity failed for {value!r}: native={native_result!r} python={python_result!r}"


class TestNativeFallback:
    """Verify that hooks cause fallback to pure Python."""

    def test_parse_with_hook_uses_python(self) -> None:
        """When a hook is provided, loads() falls through to pure Python."""
        calls: list[str] = []

        def track_int(s: str) -> int:
            calls.append(s)
            return int(s)

        result = rdn.loads("42", parse_int=track_int)
        assert result == 42
        assert calls == ["42"]

    def test_dumps_with_default_uses_python(self) -> None:
        """When default is provided, dumps() falls through to pure Python."""
        class Custom:
            pass

        def handler(obj: object) -> str:
            return "custom"

        result = rdn.dumps(Custom(), default=handler)
        assert result == '"custom"'
