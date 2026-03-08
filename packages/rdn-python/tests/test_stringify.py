"""Tests for rdn._serializer -- primitives, strings, extended types, containers, and options."""

from __future__ import annotations

import math
import re
from datetime import datetime, time, timedelta, timezone
from io import StringIO

import pytest

import rdn
from rdn._serializer import _escape_string, stringify


# ---------------------------------------------------------------------------
# None
# ---------------------------------------------------------------------------

class TestNone:
    def test_none_serializes_to_null(self) -> None:
        assert stringify(None) == "null"


# ---------------------------------------------------------------------------
# Booleans
# ---------------------------------------------------------------------------

class TestBooleans:
    def test_true(self) -> None:
        assert stringify(True) == "true"

    def test_false(self) -> None:
        assert stringify(False) == "false"

    def test_bool_before_int_true(self) -> None:
        """True must serialize as 'true', not '1' (bool is subclass of int)."""
        assert stringify(True) == "true"

    def test_bool_before_int_false(self) -> None:
        """False must serialize as 'false', not '0'."""
        assert stringify(False) == "false"


# ---------------------------------------------------------------------------
# Integers
# ---------------------------------------------------------------------------

class TestIntegers:
    def test_zero(self) -> None:
        assert stringify(0) == "0"

    def test_positive(self) -> None:
        assert stringify(42) == "42"

    def test_negative(self) -> None:
        assert stringify(-1) == "-1"

    def test_large_positive(self) -> None:
        assert stringify(1000000) == "1000000"

    def test_large_negative(self) -> None:
        assert stringify(-999999) == "-999999"


# ---------------------------------------------------------------------------
# BigInt auto-promote
# ---------------------------------------------------------------------------

class TestBigIntAutoPromote:
    def test_safe_boundary_positive(self) -> None:
        """MAX_SAFE_INTEGER (2**53 - 1) should NOT get the 'n' suffix."""
        assert stringify(9007199254740991) == "9007199254740991"

    def test_safe_boundary_negative(self) -> None:
        """-MAX_SAFE_INTEGER should NOT get the 'n' suffix."""
        assert stringify(-9007199254740991) == "-9007199254740991"

    def test_above_safe_positive(self) -> None:
        """2**53 should get the 'n' suffix."""
        assert stringify(9007199254740992) == "9007199254740992n"

    def test_above_safe_negative(self) -> None:
        """-(2**53) should get the 'n' suffix."""
        assert stringify(-9007199254740992) == "-9007199254740992n"

    def test_very_large(self) -> None:
        assert stringify(10**30) == str(10**30) + "n"


# ---------------------------------------------------------------------------
# Floats
# ---------------------------------------------------------------------------

class TestFloats:
    def test_pi_ish(self) -> None:
        assert stringify(3.14) == "3.14"

    def test_zero(self) -> None:
        assert stringify(0.0) == "0.0"

    def test_negative(self) -> None:
        assert stringify(-0.5) == "-0.5"

    def test_scientific(self) -> None:
        result = stringify(1e10)
        # Should be a valid float repr
        assert result is not None
        assert float(result) == 1e10

    def test_nan(self) -> None:
        assert stringify(float("nan")) == "NaN"

    def test_positive_infinity(self) -> None:
        assert stringify(float("inf")) == "Infinity"

    def test_negative_infinity(self) -> None:
        assert stringify(float("-inf")) == "-Infinity"

    def test_negative_zero(self) -> None:
        """Negative zero should serialize distinctly via repr."""
        result = stringify(-0.0)
        assert result is not None
        # Python's repr(-0.0) produces '-0.0'
        assert result == "-0.0"


# ---------------------------------------------------------------------------
# Strings -- basic
# ---------------------------------------------------------------------------

class TestStrings:
    def test_hello(self) -> None:
        assert stringify("hello") == '"hello"'

    def test_empty(self) -> None:
        assert stringify("") == '""'

    def test_with_newline(self) -> None:
        assert stringify("a\nb") == '"a\\nb"'

    def test_with_tab(self) -> None:
        assert stringify("a\tb") == '"a\\tb"'

    def test_with_quote(self) -> None:
        assert stringify('a"b') == '"a\\"b"'

    def test_with_backslash(self) -> None:
        assert stringify("a\\b") == '"a\\\\b"'

    def test_with_carriage_return(self) -> None:
        assert stringify("a\rb") == '"a\\rb"'

    def test_with_backspace(self) -> None:
        assert stringify("a\bb") == '"a\\bb"'

    def test_with_formfeed(self) -> None:
        assert stringify("a\fb") == '"a\\fb"'

    def test_control_char_as_unicode_escape(self) -> None:
        """Control char 0x01 should be escaped as \\u0001."""
        assert stringify("\x01") == '"\\u0001"'


# ---------------------------------------------------------------------------
# Strings -- ensure_ascii
# ---------------------------------------------------------------------------

class TestEnsureAscii:
    def test_non_ascii_escaped_by_default(self) -> None:
        """Non-ASCII chars should be \\uXXXX-escaped when ensure_ascii=True."""
        assert stringify("\u00e9") == '"\\u00e9"'  # e

    def test_non_ascii_passthrough_when_false(self) -> None:
        """Non-ASCII chars should pass through when ensure_ascii=False."""
        assert stringify("\u00e9", ensure_ascii=False) == '"\u00e9"'

    def test_cjk_escaped(self) -> None:
        """CJK character should be \\uXXXX-escaped with ensure_ascii=True."""
        assert stringify("\u4e16") == '"\\u4e16"'  # world

    def test_cjk_passthrough(self) -> None:
        assert stringify("\u4e16", ensure_ascii=False) == '"\u4e16"'

    def test_fast_path_ascii_only(self) -> None:
        """Pure ASCII string hits fast path even with ensure_ascii=True."""
        result = _escape_string("hello world", ensure_ascii=True)
        assert result == '"hello world"'

    def test_fast_path_ensure_ascii_false_with_non_ascii(self) -> None:
        """Non-ASCII string hits fast path when ensure_ascii=False (no escaping needed)."""
        result = _escape_string("caf\u00e9", ensure_ascii=False)
        assert result == '"caf\u00e9"'


# ---------------------------------------------------------------------------
# Strings -- surrogate pairs
# ---------------------------------------------------------------------------

class TestSurrogatePairs:
    def test_emoji_surrogate_pair(self) -> None:
        """Codepoint > U+FFFF (emoji) should be encoded as surrogate pair."""
        # U+1F600 = face -> \ud83d\ude00
        result = stringify("\U0001f600")
        assert result == '"\\ud83d\\ude00"'

    def test_musical_symbol(self) -> None:
        """U+1D11E (musical symbol G clef) -> surrogate pair."""
        result = stringify("\U0001d11e")
        assert result == '"\\ud834\\udd1e"'

    def test_supplementary_passthrough_ensure_ascii_false(self) -> None:
        """Supplementary codepoints pass through with ensure_ascii=False."""
        result = stringify("\U0001f600", ensure_ascii=False)
        assert result == '"\U0001f600"'


# ---------------------------------------------------------------------------
# Datetime
# ---------------------------------------------------------------------------

class TestDatetime:
    def test_utc_date(self) -> None:
        assert stringify(datetime(2024, 1, 15, tzinfo=timezone.utc)) == "@2024-01-15T00:00:00.000Z"

    def test_date_with_milliseconds(self) -> None:
        assert stringify(datetime(2024, 1, 15, 10, 30, 45, 123000, tzinfo=timezone.utc)) == "@2024-01-15T10:30:45.123Z"

    def test_naive_datetime_treated_as_utc(self) -> None:
        """Naive datetimes (no tzinfo) should be treated as UTC."""
        assert stringify(datetime(2024, 6, 1, 12, 0, 0)) == "@2024-06-01T12:00:00.000Z"

    def test_non_utc_timezone_conversion(self) -> None:
        """Non-UTC timezone should be converted to UTC."""
        est = timezone(timedelta(hours=-5))
        d = datetime(2024, 1, 15, 10, 0, 0, tzinfo=est)
        assert stringify(d) == "@2024-01-15T15:00:00.000Z"

    def test_midnight(self) -> None:
        assert stringify(datetime(2024, 12, 31, 0, 0, 0, tzinfo=timezone.utc)) == "@2024-12-31T00:00:00.000Z"

    def test_epoch(self) -> None:
        assert stringify(datetime(1970, 1, 1, tzinfo=timezone.utc)) == "@1970-01-01T00:00:00.000Z"

    def test_zero_milliseconds(self) -> None:
        """Even when ms=0, the .000 should be present (always 24-char output)."""
        result = stringify(datetime(2024, 1, 1, 0, 0, 0, 0, tzinfo=timezone.utc))
        assert result == "@2024-01-01T00:00:00.000Z"


# ---------------------------------------------------------------------------
# TimeOnly
# ---------------------------------------------------------------------------

class TestTimeOnly:
    def test_basic_time(self) -> None:
        assert stringify(time(14, 30, 0)) == "@14:30:00"

    def test_with_milliseconds(self) -> None:
        assert stringify(time(14, 30, 0, 500000)) == "@14:30:00.500"

    def test_without_milliseconds(self) -> None:
        """When microseconds are zero, no .mmm part should appear."""
        assert stringify(time(9, 15, 30)) == "@09:15:30"

    def test_midnight(self) -> None:
        assert stringify(time(0, 0, 0)) == "@00:00:00"

    def test_end_of_day(self) -> None:
        assert stringify(time(23, 59, 59, 999000)) == "@23:59:59.999"


# ---------------------------------------------------------------------------
# Duration
# ---------------------------------------------------------------------------

class TestDuration:
    def test_days_only(self) -> None:
        assert stringify(timedelta(days=3)) == "@P3D"

    def test_hours_only(self) -> None:
        assert stringify(timedelta(hours=4)) == "@PT4H"

    def test_mixed(self) -> None:
        assert stringify(timedelta(days=3, hours=4)) == "@P3DT4H"

    def test_zero_duration(self) -> None:
        assert stringify(timedelta(0)) == "@PT0S"

    def test_minutes_and_seconds(self) -> None:
        assert stringify(timedelta(minutes=30, seconds=15)) == "@PT30M15S"

    def test_negative_duration(self) -> None:
        assert stringify(timedelta(days=-1)) == "@-P1D"

    def test_all_components(self) -> None:
        assert stringify(timedelta(days=1, hours=2, minutes=3, seconds=4)) == "@P1DT2H3M4S"


# ---------------------------------------------------------------------------
# RegExp
# ---------------------------------------------------------------------------

class TestRegExp:
    def test_simple_pattern(self) -> None:
        assert stringify(re.compile("^test$")) == "/^test$/"

    def test_ignorecase_flag(self) -> None:
        assert stringify(re.compile("^test$", re.IGNORECASE)) == "/^test$/i"

    def test_multiline_flag(self) -> None:
        assert stringify(re.compile("^test$", re.MULTILINE)) == "/^test$/m"

    def test_dotall_flag(self) -> None:
        assert stringify(re.compile("^test$", re.DOTALL)) == "/^test$/s"

    def test_combined_flags(self) -> None:
        result = stringify(re.compile("^test$", re.IGNORECASE | re.MULTILINE | re.DOTALL))
        assert result == "/^test$/ims"

    def test_no_flags(self) -> None:
        """Default UNICODE flag should not appear in output."""
        result = stringify(re.compile("hello"))
        assert result == "/hello/"

    def test_pattern_with_special_chars(self) -> None:
        assert stringify(re.compile(r"\d+\.\d+")) == r"/\d+\.\d+/"


# ---------------------------------------------------------------------------
# Binary
# ---------------------------------------------------------------------------

class TestBinary:
    def test_bytes(self) -> None:
        assert stringify(b"Hello") == 'b"SGVsbG8="'

    def test_bytearray(self) -> None:
        assert stringify(bytearray(b"Hello")) == 'b"SGVsbG8="'

    def test_empty_bytes(self) -> None:
        assert stringify(b"") == 'b""'

    def test_binary_data(self) -> None:
        """Non-text binary data should encode correctly."""
        data = bytes([0x00, 0xFF, 0x80, 0x01])
        result = stringify(data)
        assert result == 'b"AP+AAQ=="'

    def test_single_byte(self) -> None:
        assert stringify(b"\x00") == 'b"AA=="'


# ---------------------------------------------------------------------------
# Lists (Task 12)
# ---------------------------------------------------------------------------

class TestList:
    def test_empty_list(self) -> None:
        assert stringify([]) == "[]"

    def test_single_element(self) -> None:
        assert stringify([1]) == "[1]"

    def test_multiple_elements(self) -> None:
        assert stringify([1, 2, 3]) == "[1,2,3]"

    def test_nested_list(self) -> None:
        assert stringify([[1, 2], [3, 4]]) == "[[1,2],[3,4]]"

    def test_with_none_elements(self) -> None:
        """None elements in a list should become 'null'."""
        assert stringify([1, None, 3]) == "[1,null,3]"

    def test_with_strings(self) -> None:
        assert stringify(["a", "b"]) == '["a","b"]'

    def test_mixed_types(self) -> None:
        assert stringify([1, "two", True, None]) == '[1,"two",true,null]'

    def test_deeply_nested(self) -> None:
        assert stringify([[[1]]]) == "[[[1]]]"


# ---------------------------------------------------------------------------
# Tuples (Task 12)
# ---------------------------------------------------------------------------

class TestTuple:
    def test_empty_tuple(self) -> None:
        assert stringify(()) == "()"

    def test_single_element(self) -> None:
        assert stringify((1,)) == "(1)"

    def test_multiple_elements(self) -> None:
        assert stringify((1, 2, 3)) == "(1,2,3)"

    def test_nested_tuple(self) -> None:
        assert stringify(((1, 2), (3, 4))) == "((1,2),(3,4))"

    def test_with_none(self) -> None:
        assert stringify((1, None, 3)) == "(1,null,3)"

    def test_mixed_types(self) -> None:
        assert stringify((1, "two", True)) == '(1,"two",true)'


# ---------------------------------------------------------------------------
# Dicts (Task 12)
# ---------------------------------------------------------------------------

class TestDict:
    def test_empty_dict(self) -> None:
        assert stringify({}) == "{}"

    def test_single_entry(self) -> None:
        assert stringify({"a": 1}) == '{"a":1}'

    def test_multiple_entries(self) -> None:
        result = stringify({"a": 1, "b": 2})
        # Keys should be in insertion order by default
        assert result == '{"a":1,"b":2}'

    def test_sorted_keys(self) -> None:
        result = stringify({"c": 3, "a": 1, "b": 2}, sort_keys=True)
        assert result == '{"a":1,"b":2,"c":3}'

    def test_non_string_key_raises(self) -> None:
        with pytest.raises(TypeError, match="Object key must be a string, got int"):
            stringify({1: "value"})

    def test_nested_dict(self) -> None:
        assert stringify({"a": {"b": 1}}) == '{"a":{"b":1}}'

    def test_dict_with_list_value(self) -> None:
        assert stringify({"items": [1, 2, 3]}) == '{"items":[1,2,3]}'

    def test_dict_with_none_value_omitted(self) -> None:
        """Values that serialize to None (non-serializable) are omitted, but
        None itself becomes 'null'."""
        assert stringify({"a": None}) == '{"a":null}'


# ---------------------------------------------------------------------------
# Sets (Task 12)
# ---------------------------------------------------------------------------

class TestSet:
    def test_empty_set(self) -> None:
        assert stringify(set()) == "Set{}"

    def test_single_element(self) -> None:
        result = stringify({42})
        assert result == "Set{42}"

    def test_frozenset_empty(self) -> None:
        assert stringify(frozenset()) == "Set{}"

    def test_frozenset_elements(self) -> None:
        result = stringify(frozenset({42}))
        assert result == "Set{42}"

    def test_set_with_strings(self) -> None:
        result = stringify({"hello"})
        assert result == 'Set{"hello"}'

    def test_set_multiple_elements(self) -> None:
        """Set with multiple elements (order may vary)."""
        result = stringify({1, 2})
        assert result is not None
        assert result.startswith("Set{")
        assert result.endswith("}")
        # Both elements should be present
        assert "1" in result
        assert "2" in result


# ---------------------------------------------------------------------------
# Cycle detection (Task 12)
# ---------------------------------------------------------------------------

class TestCycleDetection:
    def test_self_referencing_list(self) -> None:
        a: list = [1, 2]
        a.append(a)
        with pytest.raises(ValueError, match="Converting circular structure to RDN"):
            stringify(a)

    def test_self_referencing_dict(self) -> None:
        d: dict = {"a": 1}
        d["self"] = d
        with pytest.raises(ValueError, match="Converting circular structure to RDN"):
            stringify(d)

    def test_mutual_reference(self) -> None:
        a: list = [1]
        b: list = [2]
        a.append(b)
        b.append(a)
        with pytest.raises(ValueError, match="Converting circular structure to RDN"):
            stringify(a)

    def test_check_circular_false_skips_check(self) -> None:
        """With check_circular=False, no ValueError is raised (may recurse)."""
        a: list = [1]
        # We can't actually serialize an infinite structure, but we can test
        # that the flag is respected by checking a non-circular deep structure.
        deep: list = [1]
        wrapper: list = [deep]
        # No error even though same list is referenced twice (not circular though)
        result = stringify([deep, deep], check_circular=False)
        assert result == "[[1],[1]]"

    def test_non_circular_reuse_allowed(self) -> None:
        """The same list referenced multiple times (but not circularly) should work."""
        shared = [1, 2]
        result = stringify([shared, shared])
        assert result == "[[1,2],[1,2]]"

    def test_set_cycle_detection(self) -> None:
        """Sets cannot directly self-reference in Python, but we test the
        cycle tracking still works for mutable sets via dict nesting."""
        d: dict = {}
        d["self"] = d
        with pytest.raises(ValueError, match="Converting circular structure to RDN"):
            stringify(d)


# ---------------------------------------------------------------------------
# Indent / Pretty-print (Task 12)
# ---------------------------------------------------------------------------

class TestIndent:
    def test_indent_list_with_int(self) -> None:
        result = stringify([1, 2, 3], indent=2)
        expected = "[\n  1,\n  2,\n  3\n]"
        assert result == expected

    def test_indent_dict_with_int(self) -> None:
        result = stringify({"a": 1, "b": 2}, indent=2)
        expected = '{\n  "a": 1,\n  "b": 2\n}'
        assert result == expected

    def test_indent_with_tab(self) -> None:
        result = stringify([1, 2], indent="\t")
        expected = "[\n\t1,\n\t2\n]"
        assert result == expected

    def test_nested_indent(self) -> None:
        result = stringify({"a": [1, 2]}, indent=2)
        expected = '{\n  "a": [\n    1,\n    2\n  ]\n}'
        assert result == expected

    def test_indent_with_set(self) -> None:
        result = stringify({42}, indent=2)
        assert result == "Set{\n  42\n}"

    def test_indent_with_tuple(self) -> None:
        result = stringify((1, 2), indent=2)
        expected = "(\n  1,\n  2\n)"
        assert result == expected

    def test_indent_empty_containers(self) -> None:
        """Empty containers should not have newlines even with indent."""
        assert stringify([], indent=2) == "[]"
        assert stringify({}, indent=2) == "{}"
        assert stringify((), indent=2) == "()"
        assert stringify(set(), indent=2) == "Set{}"

    def test_indent_primitives_unchanged(self) -> None:
        """Indent should not affect primitive values."""
        assert stringify(42, indent=2) == "42"
        assert stringify("hello", indent=2) == '"hello"'
        assert stringify(True, indent=2) == "true"


# ---------------------------------------------------------------------------
# sort_keys (Task 12)
# ---------------------------------------------------------------------------

class TestSortKeys:
    def test_sort_keys_true(self) -> None:
        result = stringify({"c": 3, "a": 1, "b": 2}, sort_keys=True)
        assert result == '{"a":1,"b":2,"c":3}'

    def test_sort_keys_false_preserves_order(self) -> None:
        """With sort_keys=False (default), insertion order is preserved."""
        result = stringify({"c": 3, "a": 1, "b": 2}, sort_keys=False)
        assert result == '{"c":3,"a":1,"b":2}'

    def test_sort_keys_nested(self) -> None:
        result = stringify({"b": {"d": 4, "c": 3}, "a": 1}, sort_keys=True)
        assert result == '{"a":1,"b":{"c":3,"d":4}}'


# ---------------------------------------------------------------------------
# default function (Task 12)
# ---------------------------------------------------------------------------

class TestDefault:
    def test_custom_default_function(self) -> None:
        """A custom default function can serialize unsupported types."""
        class Point:
            def __init__(self, x: int, y: int) -> None:
                self.x = x
                self.y = y

        def point_default(obj: object) -> object:
            if isinstance(obj, Point):
                return {"x": obj.x, "y": obj.y}
            raise TypeError(f"Cannot serialize {type(obj).__name__}")

        result = stringify(Point(1, 2), default=point_default, sort_keys=True)
        assert result == '{"x":1,"y":2}'

    def test_default_returning_serializable(self) -> None:
        """Default returning a primitive should work."""
        result = stringify(object(), default=lambda o: "fallback")
        assert result == '"fallback"'

    def test_default_returning_list(self) -> None:
        result = stringify(object(), default=lambda o: [1, 2, 3])
        assert result == "[1,2,3]"

    def test_no_default_raises_typeerror(self) -> None:
        """Without a default function, unsupported types raise TypeError."""
        with pytest.raises(TypeError, match="object"):
            stringify(object())

    def test_default_not_called_recursively(self) -> None:
        """Default should only be called once -- if it returns something
        non-serializable, TypeError is raised rather than calling default again."""
        call_count = 0

        def bad_default(obj: object) -> object:
            nonlocal call_count
            call_count += 1
            return obj  # returns the same non-serializable object

        with pytest.raises(TypeError):
            stringify(object(), default=bad_default)
        assert call_count == 1

    def test_default_in_container(self) -> None:
        """Default function works for items inside containers."""
        class Wrapper:
            def __init__(self, val: int) -> None:
                self.val = val

        result = stringify([Wrapper(1), Wrapper(2)], default=lambda o: o.val if isinstance(o, Wrapper) else None)
        assert result == "[1,2]"


# ---------------------------------------------------------------------------
# Separators (Task 12)
# ---------------------------------------------------------------------------

class TestSeparators:
    def test_custom_separators(self) -> None:
        result = stringify([1, 2, 3], separators=(", ", ": "))
        assert result == "[1, 2, 3]"

    def test_custom_key_separator(self) -> None:
        result = stringify({"a": 1}, separators=(",", " : "))
        assert result == '{"a" : 1}'

    def test_separators_override_indent_defaults(self) -> None:
        """Explicit separators should override indent's default separators."""
        result = stringify({"a": 1}, indent=2, separators=(",", ":"))
        expected = '{\n  "a":1\n}'
        assert result == expected


# ---------------------------------------------------------------------------
# Mixed / complex structures (Task 12)
# ---------------------------------------------------------------------------

class TestMixed:
    def test_complex_nested_structure(self) -> None:
        """Complex nested structure with multiple types."""
        value = {
            "name": "test",
            "scores": [1, 2, 3],
            "metadata": {
                "created": datetime(2024, 1, 15, tzinfo=timezone.utc),
                "tags": ("a", "b"),
            },
            "active": True,
            "count": None,
        }
        result = stringify(value)
        assert result is not None
        assert '"name":"test"' in result
        assert '"scores":[1,2,3]' in result
        assert '"active":true' in result
        assert '"count":null' in result
        assert '("a","b")' in result

    def test_list_of_dicts(self) -> None:
        result = stringify([{"a": 1}, {"b": 2}])
        assert result == '[{"a":1},{"b":2}]'

    def test_dict_with_tuple_value(self) -> None:
        result = stringify({"coords": (10, 20)})
        assert result == '{"coords":(10,20)}'

    def test_dict_with_set_value(self) -> None:
        result = stringify({"items": {42}})
        assert result == '{"items":Set{42}}'

    def test_pretty_printed_complex(self) -> None:
        """Pretty-printed complex structure."""
        result = stringify({"a": [1, 2]}, indent=2, sort_keys=True)
        expected = '{\n  "a": [\n    1,\n    2\n  ]\n}'
        assert result == expected


# ---------------------------------------------------------------------------
# Type errors for unsupported types (updated from Task 11)
# ---------------------------------------------------------------------------

class TestUnsupportedTypes:
    def test_object_raises(self) -> None:
        with pytest.raises(TypeError, match="object"):
            stringify(object())

    def test_custom_class_raises(self) -> None:
        class Foo:
            pass
        with pytest.raises(TypeError, match="Foo"):
            stringify(Foo())


# ---------------------------------------------------------------------------
# Public API surface
# ---------------------------------------------------------------------------

class TestPublicAPI:
    def test_stringify_is_callable(self) -> None:
        assert callable(stringify)

    def test_stringify_returns_string_for_primitives(self) -> None:
        assert isinstance(stringify(42), str)

    def test_stringify_keyword_only_ensure_ascii(self) -> None:
        """ensure_ascii must be keyword-only."""
        # This should work:
        stringify("hello", ensure_ascii=False)
        # This should fail (positional):
        with pytest.raises(TypeError):
            stringify("hello", False)  # type: ignore[misc]

    def test_stringify_keyword_only_all_params(self) -> None:
        """All new parameters must be keyword-only."""
        # These should all work:
        stringify([], check_circular=True)
        stringify({}, sort_keys=True)
        stringify([], indent=2)
        stringify([], separators=(",", ":"))
        stringify(object(), default=lambda o: None)


# ---------------------------------------------------------------------------
# rdn.dumps() -- public API (Task 13)
# ---------------------------------------------------------------------------

class TestDumps:
    """Test the public ``rdn.dumps()`` function."""

    def test_basic_dict(self) -> None:
        """rdn.dumps({"key": "value"}) returns compact output."""
        assert rdn.dumps({"key": "value"}) == '{"key":"value"}'

    def test_basic_list(self) -> None:
        assert rdn.dumps([1, 2, 3]) == "[1,2,3]"

    def test_none(self) -> None:
        assert rdn.dumps(None) == "null"

    def test_bool_true(self) -> None:
        assert rdn.dumps(True) == "true"

    def test_bool_false(self) -> None:
        assert rdn.dumps(False) == "false"

    def test_integer(self) -> None:
        assert rdn.dumps(42) == "42"

    def test_float(self) -> None:
        assert rdn.dumps(3.14) == "3.14"

    def test_string(self) -> None:
        assert rdn.dumps("hello") == '"hello"'

    def test_nan(self) -> None:
        """RDN natively supports NaN (no allow_nan needed)."""
        assert rdn.dumps(float("nan")) == "NaN"

    def test_infinity(self) -> None:
        """RDN natively supports Infinity."""
        assert rdn.dumps(float("inf")) == "Infinity"

    def test_negative_infinity(self) -> None:
        assert rdn.dumps(float("-inf")) == "-Infinity"

    def test_indent_int(self) -> None:
        result = rdn.dumps({"key": "value"}, indent=2)
        expected = '{\n  "key": "value"\n}'
        assert result == expected

    def test_indent_string(self) -> None:
        result = rdn.dumps([1, 2], indent="\t")
        expected = "[\n\t1,\n\t2\n]"
        assert result == expected

    def test_sort_keys(self) -> None:
        result = rdn.dumps({"b": 2, "a": 1}, sort_keys=True)
        assert result == '{"a":1,"b":2}'

    def test_ensure_ascii_true(self) -> None:
        result = rdn.dumps("\u00e9")
        assert result == '"\\u00e9"'

    def test_ensure_ascii_false(self) -> None:
        result = rdn.dumps("\u00e9", ensure_ascii=False)
        assert result == '"\u00e9"'

    def test_custom_separators(self) -> None:
        result = rdn.dumps([1, 2], separators=(", ", ": "))
        assert result == "[1, 2]"

    def test_default_function(self) -> None:
        """default function handles unsupported types."""
        result = rdn.dumps(object(), default=lambda o: str(o))
        assert result is not None
        assert result.startswith('"')

    def test_default_returning_non_serializable_raises(self) -> None:
        """default returning the same non-serializable object raises TypeError."""
        with pytest.raises(TypeError):
            rdn.dumps(object(), default=lambda o: o)

    def test_check_circular_true(self) -> None:
        a: list = [1]
        a.append(a)
        with pytest.raises(ValueError, match="Converting circular structure to RDN"):
            rdn.dumps(a)

    def test_check_circular_false(self) -> None:
        """Non-circular shared references work with check_circular=False."""
        shared = [1]
        result = rdn.dumps([shared, shared], check_circular=False)
        assert result == "[[1],[1]]"

    def test_bigint_auto_promote(self) -> None:
        """Integers beyond MAX_SAFE_INTEGER get 'n' suffix."""
        assert rdn.dumps(2**53) == str(2**53) + "n"

    def test_datetime(self) -> None:
        d = datetime(2024, 1, 15, tzinfo=timezone.utc)
        assert rdn.dumps(d) == "@2024-01-15T00:00:00.000Z"

    def test_tuple(self) -> None:
        assert rdn.dumps((1, 2, 3)) == "(1,2,3)"

    def test_set(self) -> None:
        assert rdn.dumps({42}) == "Set{42}"

    def test_bytes(self) -> None:
        assert rdn.dumps(b"Hello") == 'b"SGVsbG8="'

    def test_keyword_only_params(self) -> None:
        """All parameters after obj must be keyword-only."""
        with pytest.raises(TypeError):
            rdn.dumps(42, None)  # type: ignore[misc]

    def test_returns_str(self) -> None:
        """dumps always returns a str (never None)."""
        result = rdn.dumps(None)
        assert isinstance(result, str)


# ---------------------------------------------------------------------------
# rdn.dump() -- file I/O (Task 13)
# ---------------------------------------------------------------------------

class TestDump:
    """Test the public ``rdn.dump()`` function."""

    def test_dump_to_stringio(self) -> None:
        """dump() writes RDN to a file-like object."""
        fp = StringIO()
        rdn.dump({"key": 42}, fp)
        assert fp.getvalue() == '{"key":42}'

    def test_dump_with_indent(self) -> None:
        fp = StringIO()
        rdn.dump([1, 2], fp, indent=2)
        assert fp.getvalue() == "[\n  1,\n  2\n]"

    def test_dump_with_sort_keys(self) -> None:
        fp = StringIO()
        rdn.dump({"b": 2, "a": 1}, fp, sort_keys=True)
        assert fp.getvalue() == '{"a":1,"b":2}'

    def test_dump_with_default(self) -> None:
        fp = StringIO()
        rdn.dump(object(), fp, default=lambda o: "fallback")
        assert fp.getvalue() == '"fallback"'

    def test_dump_returns_none(self) -> None:
        """dump() returns None (writes to fp, not stdout)."""
        fp = StringIO()
        result = rdn.dump(42, fp)
        assert result is None

    def test_dump_with_ensure_ascii_false(self) -> None:
        fp = StringIO()
        rdn.dump("\u00e9", fp, ensure_ascii=False)
        assert fp.getvalue() == '"\u00e9"'

    def test_dump_complex_structure(self) -> None:
        """dump() handles complex nested structures."""
        fp = StringIO()
        value = {"items": [1, 2, 3], "active": True, "name": None}
        rdn.dump(value, fp)
        result = fp.getvalue()
        assert '"items":[1,2,3]' in result
        assert '"active":true' in result
        assert '"name":null' in result


# ---------------------------------------------------------------------------
# rdn module exports (Task 13)
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# skipkeys (Task 003)
# ---------------------------------------------------------------------------

class TestSkipKeys:
    """Test the ``skipkeys`` parameter for dict serialization."""

    def test_skipkeys_true_skips_non_string_keys(self) -> None:
        """Non-string keys are silently skipped when skipkeys=True."""
        assert stringify({1: "a", "b": 2}, skipkeys=True) == '{"b":2}'

    def test_skipkeys_false_raises_typeerror(self) -> None:
        """Default (skipkeys=False) raises TypeError for non-string keys."""
        with pytest.raises(TypeError, match="Object key must be a string, got int"):
            stringify({1: "a", "b": 2})

    def test_skipkeys_all_keys_skipped(self) -> None:
        """When all keys are non-string, result is empty object."""
        assert stringify({1: "a", 2: "b"}, skipkeys=True) == "{}"

    def test_skipkeys_nested_dicts(self) -> None:
        """skipkeys applies at all nesting levels."""
        result = stringify({"a": {1: "skip", "b": 2}}, skipkeys=True)
        assert result == '{"a":{"b":2}}'

    def test_skipkeys_with_sort_keys(self) -> None:
        """skipkeys=True combined with sort_keys=True."""
        result = stringify({1: "skip", "c": 3, "a": 1, "b": 2}, skipkeys=True, sort_keys=True)
        assert result == '{"a":1,"b":2,"c":3}'

    def test_skipkeys_via_encoder(self) -> None:
        """skipkeys works through RDNEncoder."""
        from rdn.encoder import RDNEncoder
        encoder = RDNEncoder(skipkeys=True)
        assert encoder.encode({1: "a", "b": 2}) == '{"b":2}'

    def test_skipkeys_via_dumps(self) -> None:
        """skipkeys works through rdn.dumps()."""
        assert rdn.dumps({1: "a", "b": 2}, skipkeys=True) == '{"b":2}'

    def test_skipkeys_various_non_string_key_types(self) -> None:
        """Various non-string key types are all skipped."""
        result = stringify({1: "int", 2.5: "float", True: "bool", None: "none", "ok": "str"}, skipkeys=True)
        assert result == '{"ok":"str"}'


# ---------------------------------------------------------------------------
# allow_nan (Task 004)
# ---------------------------------------------------------------------------

class TestAllowNan:
    """Test the ``allow_nan`` parameter for float serialization."""

    def test_default_allows_nan(self) -> None:
        """By default, NaN/Infinity are serialized normally."""
        assert stringify(float("nan")) == "NaN"
        assert stringify(float("inf")) == "Infinity"
        assert stringify(float("-inf")) == "-Infinity"

    def test_allow_nan_false_nan_raises(self) -> None:
        with pytest.raises(ValueError, match="Out of range float values are not RDN compliant"):
            stringify(float("nan"), allow_nan=False)

    def test_allow_nan_false_infinity_raises(self) -> None:
        with pytest.raises(ValueError, match="Out of range float values are not RDN compliant"):
            stringify(float("inf"), allow_nan=False)

    def test_allow_nan_false_neg_infinity_raises(self) -> None:
        with pytest.raises(ValueError, match="Out of range float values are not RDN compliant"):
            stringify(float("-inf"), allow_nan=False)

    def test_allow_nan_false_nested_in_list(self) -> None:
        with pytest.raises(ValueError, match="Out of range float values are not RDN compliant"):
            stringify([1, float("nan")], allow_nan=False)

    def test_allow_nan_false_nested_in_dict(self) -> None:
        with pytest.raises(ValueError, match="Out of range float values are not RDN compliant"):
            stringify({"a": float("inf")}, allow_nan=False)

    def test_normal_floats_unaffected(self) -> None:
        """Normal floats work fine with allow_nan=False."""
        assert stringify(3.14, allow_nan=False) == "3.14"
        assert stringify(0.0, allow_nan=False) == "0.0"
        assert stringify(-1.5, allow_nan=False) == "-1.5"

    def test_via_encoder(self) -> None:
        """allow_nan works through RDNEncoder."""
        from rdn.encoder import RDNEncoder
        with pytest.raises(ValueError, match="Out of range float values are not RDN compliant"):
            RDNEncoder(allow_nan=False).encode(float("nan"))

    def test_via_dumps(self) -> None:
        """allow_nan works through rdn.dumps()."""
        with pytest.raises(ValueError, match="Out of range float values are not RDN compliant"):
            rdn.dumps(float("nan"), allow_nan=False)

    def test_via_dumps_normal_float(self) -> None:
        """Normal floats via rdn.dumps() with allow_nan=False."""
        assert rdn.dumps(3.14, allow_nan=False) == "3.14"


class TestModuleExports:
    """Verify that the rdn module exports the expected public API."""

    def test_dumps_is_exported(self) -> None:
        assert hasattr(rdn, "dumps")
        assert callable(rdn.dumps)

    def test_dump_is_exported(self) -> None:
        assert hasattr(rdn, "dump")
        assert callable(rdn.dump)

    def test_rdn_decode_error_is_exported(self) -> None:
        assert hasattr(rdn, "RDNDecodeError")
        assert issubclass(rdn.RDNDecodeError, ValueError)

    def test_max_safe_integer_is_exported(self) -> None:
        assert hasattr(rdn, "MAX_SAFE_INTEGER")
        assert rdn.MAX_SAFE_INTEGER == 2**53 - 1
