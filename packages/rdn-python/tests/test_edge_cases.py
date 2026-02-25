"""Edge case and boundary condition tests for the RDN parser and serializer."""

from __future__ import annotations

import math
import re
from datetime import datetime, time, timedelta, timezone

import pytest

import rdn
from rdn.exceptions import RDNDecodeError


# ---------------------------------------------------------------------------
# Empty / whitespace-only input
# ---------------------------------------------------------------------------


class TestEmptyInput:
    def test_empty_string_raises(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            rdn.loads("")

    def test_whitespace_only_space(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            rdn.loads("   ")

    def test_whitespace_only_tab(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            rdn.loads("\t\t\t")

    def test_whitespace_only_newline(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            rdn.loads("\n\n\n")

    def test_whitespace_only_cr(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            rdn.loads("\r\r\r")

    def test_whitespace_only_mixed(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            rdn.loads(" \t \n \r ")


# ---------------------------------------------------------------------------
# Trailing whitespace after valid value (should be OK)
# ---------------------------------------------------------------------------


class TestTrailingWhitespace:
    def test_trailing_spaces(self) -> None:
        assert rdn.loads("42   ") == 42

    def test_trailing_tabs(self) -> None:
        assert rdn.loads("true\t\t") is True

    def test_trailing_newlines(self) -> None:
        assert rdn.loads('"hello"\n\n') == "hello"

    def test_trailing_cr_lf(self) -> None:
        assert rdn.loads("null\r\n") is None

    def test_trailing_mixed_whitespace(self) -> None:
        assert rdn.loads("false \t \n \r ") is False


# ---------------------------------------------------------------------------
# All whitespace characters between tokens
# ---------------------------------------------------------------------------


class TestWhitespaceBetweenTokens:
    def test_spaces_between_array_elements(self) -> None:
        assert rdn.loads("[  1  ,  2  ,  3  ]") == [1, 2, 3]

    def test_tabs_between_object_entries(self) -> None:
        assert rdn.loads('{\t"a"\t:\t1\t,\t"b"\t:\t2\t}') == {"a": 1, "b": 2}

    def test_newlines_between_tokens(self) -> None:
        result = rdn.loads('{\n"a"\n:\n1\n}')
        assert result == {"a": 1}

    def test_cr_lf_between_tokens(self) -> None:
        result = rdn.loads('[\r\n1\r\n,\r\n2\r\n]')
        assert result == [1, 2]

    def test_mixed_whitespace_in_nested(self) -> None:
        result = rdn.loads('{ \t\n"x" \r\n: \t[1 , 2] \n}')
        assert result == {"x": [1, 2]}


# ---------------------------------------------------------------------------
# Unicode surrogate pairs in strings
# ---------------------------------------------------------------------------


class TestUnicodeSurrogatePairs:
    def test_emoji_grinning_face(self) -> None:
        """U+1F600 (Grinning Face) encoded as surrogate pair."""
        result = rdn.loads('"\\uD83D\\uDE00"')
        assert result == "\U0001f600"

    def test_musical_symbol_g_clef(self) -> None:
        """U+1D11E (Musical Symbol G Clef) encoded as surrogate pair."""
        result = rdn.loads('"\\uD834\\uDD1E"')
        assert result == "\U0001d11e"

    def test_surrogate_pair_in_middle_of_string(self) -> None:
        result = rdn.loads('"before\\uD83D\\uDE00after"')
        assert result == "before\U0001f600after"

    def test_multiple_surrogate_pairs(self) -> None:
        result = rdn.loads('"\\uD83D\\uDE00\\uD83D\\uDE01"')
        assert result == "\U0001f600\U0001f601"

    def test_lone_high_surrogate_raises(self) -> None:
        with pytest.raises(RDNDecodeError):
            rdn.loads('"\\uD83D"')

    def test_lone_low_surrogate_raises(self) -> None:
        with pytest.raises(RDNDecodeError):
            rdn.loads('"\\uDE00"')

    def test_high_surrogate_followed_by_non_surrogate(self) -> None:
        with pytest.raises(RDNDecodeError):
            rdn.loads('"\\uD83D\\u0041"')


# ---------------------------------------------------------------------------
# Maximum nesting depth
# ---------------------------------------------------------------------------


class TestNestingDepth:
    def test_depth_128_arrays_succeeds(self) -> None:
        """128 levels of nesting is the maximum allowed."""
        text = "[" * 128 + "1" + "]" * 128
        result = rdn.loads(text)
        # Drill down 128 levels to find the 1
        v = result
        for _ in range(128):
            assert isinstance(v, list)
            assert len(v) == 1
            v = v[0]
        assert v == 1

    def test_depth_129_arrays_raises(self) -> None:
        """129 levels of nesting must raise an error."""
        text = "[" * 129 + "1" + "]" * 129
        with pytest.raises(RDNDecodeError, match="Maximum nesting depth exceeded"):
            rdn.loads(text)

    def test_depth_128_objects_succeeds(self) -> None:
        """128 levels of nested objects should succeed."""
        text = '{"a":' * 128 + '1' + '}' * 128
        result = rdn.loads(text)
        v = result
        for _ in range(128):
            assert isinstance(v, dict)
            v = v["a"]
        assert v == 1

    def test_depth_129_objects_raises(self) -> None:
        """129 levels of nested objects must raise."""
        text = '{"a":' * 129 + '1' + '}' * 129
        with pytest.raises(RDNDecodeError, match="Maximum nesting depth exceeded"):
            rdn.loads(text)

    def test_depth_128_mixed_containers(self) -> None:
        """Mix of arrays and objects up to depth 128 should succeed."""
        # Alternate: [{"a": [{"a": ... 1 ...}]}]
        # Each pair is 2 levels, so 64 pairs = 128 levels
        text = ""
        for _ in range(64):
            text += '[{"a":'
        text += "1"
        for _ in range(64):
            text += "}]"
        result = rdn.loads(text)
        assert result is not None

    def test_depth_128_tuples_succeeds(self) -> None:
        """128 levels of nested tuples should succeed."""
        text = "(" * 128 + "1" + ")" * 128
        result = rdn.loads(text)
        v = result
        for _ in range(128):
            assert isinstance(v, tuple)
            assert len(v) == 1
            v = v[0]
        assert v == 1


# ---------------------------------------------------------------------------
# Very large BigInt numbers
# ---------------------------------------------------------------------------


class TestLargeBigInt:
    def test_very_large_bigint(self) -> None:
        """A BigInt with many digits should parse correctly."""
        big = "123456789" * 50 + "n"
        result = rdn.loads(big)
        assert result == int("123456789" * 50)
        assert isinstance(result, int)

    def test_large_negative_bigint(self) -> None:
        big = "-" + "9" * 100 + "n"
        result = rdn.loads(big)
        assert result == -int("9" * 100)

    def test_bigint_zero(self) -> None:
        assert rdn.loads("0n") == 0

    def test_bigint_roundtrip(self) -> None:
        """Large BigInt should roundtrip through dumps/loads."""
        big = 10**200
        serialized = rdn.dumps(big)
        assert serialized.endswith("n")
        parsed = rdn.loads(serialized)
        assert parsed == big


# ---------------------------------------------------------------------------
# Very long strings
# ---------------------------------------------------------------------------


class TestLongStrings:
    def test_long_ascii_string(self) -> None:
        """A very long string should parse correctly."""
        long_str = "a" * 100_000
        rdn_text = '"' + long_str + '"'
        result = rdn.loads(rdn_text)
        assert result == long_str
        assert len(result) == 100_000

    def test_long_string_with_escapes(self) -> None:
        """A long string with escape sequences."""
        # 10000 escaped newlines
        rdn_text = '"' + "\\n" * 10_000 + '"'
        result = rdn.loads(rdn_text)
        assert result == "\n" * 10_000
        assert len(result) == 10_000

    def test_long_string_roundtrip(self) -> None:
        """Long string should survive roundtrip."""
        long_str = "hello world " * 1000
        serialized = rdn.dumps(long_str)
        parsed = rdn.loads(serialized)
        assert parsed == long_str


# ---------------------------------------------------------------------------
# Nested containers
# ---------------------------------------------------------------------------


class TestNestedContainers:
    def test_array_in_object_in_array(self) -> None:
        result = rdn.loads('[{"items": [1, 2]}, {"items": [3, 4]}]')
        assert result == [{"items": [1, 2]}, {"items": [3, 4]}]

    def test_object_in_array_in_object(self) -> None:
        result = rdn.loads('{"data": [{"nested": true}]}')
        assert result == {"data": [{"nested": True}]}

    def test_tuple_in_array(self) -> None:
        result = rdn.loads("[(1, 2), (3, 4)]")
        assert result == [(1, 2), (3, 4)]

    def test_set_in_object(self) -> None:
        result = rdn.loads('{"tags": Set{"a", "b", "c"}}')
        assert result == {"tags": frozenset({"a", "b", "c"})}

    def test_deeply_nested_mixed(self) -> None:
        """Deeply nested structure with arrays, objects, tuples."""
        text = '{"a": [{"b": (1, [2, {"c": 3}])}]}'
        result = rdn.loads(text)
        assert result["a"][0]["b"] == (1, [2, {"c": 3}])


# ---------------------------------------------------------------------------
# RDN-specific literals: NaN, Infinity, -Infinity
# ---------------------------------------------------------------------------


class TestSpecialLiterals:
    def test_nan(self) -> None:
        result = rdn.loads("NaN")
        assert math.isnan(result)

    def test_infinity(self) -> None:
        assert rdn.loads("Infinity") == float("inf")

    def test_negative_infinity(self) -> None:
        assert rdn.loads("-Infinity") == float("-inf")

    def test_nan_in_array(self) -> None:
        result = rdn.loads("[NaN, 1, NaN]")
        assert math.isnan(result[0])
        assert result[1] == 1
        assert math.isnan(result[2])

    def test_infinity_in_object(self) -> None:
        result = rdn.loads('{"max": Infinity, "min": -Infinity}')
        assert result["max"] == float("inf")
        assert result["min"] == float("-inf")

    def test_nan_roundtrip(self) -> None:
        serialized = rdn.dumps(float("nan"))
        assert serialized == "NaN"
        result = rdn.loads(serialized)
        assert math.isnan(result)

    def test_infinity_roundtrip(self) -> None:
        serialized = rdn.dumps(float("inf"))
        assert serialized == "Infinity"
        assert rdn.loads(serialized) == float("inf")

    def test_negative_infinity_roundtrip(self) -> None:
        serialized = rdn.dumps(float("-inf"))
        assert serialized == "-Infinity"
        assert rdn.loads(serialized) == float("-inf")


# ---------------------------------------------------------------------------
# DateTime edge cases
# ---------------------------------------------------------------------------


class TestDateTimeEdgeCases:
    def test_date_only_roundtrip(self) -> None:
        """Date-only parses to midnight UTC; serializer emits full datetime."""
        parsed = rdn.loads("@2024-01-15")
        assert parsed == datetime(2024, 1, 15, tzinfo=timezone.utc)
        serialized = rdn.dumps(parsed)
        assert serialized == "@2024-01-15T00:00:00.000Z"

    def test_milliseconds_preserved(self) -> None:
        parsed = rdn.loads("@2024-12-31T23:59:59.999Z")
        assert parsed.microsecond == 999000
        serialized = rdn.dumps(parsed)
        assert ".999Z" in serialized


# ---------------------------------------------------------------------------
# TimeOnly edge cases
# ---------------------------------------------------------------------------


class TestTimeOnlyEdgeCases:
    def test_midnight(self) -> None:
        result = rdn.loads("@00:00:00")
        assert result == time(0, 0, 0)

    def test_end_of_day(self) -> None:
        result = rdn.loads("@23:59:59.999")
        assert result == time(23, 59, 59, 999000)

    def test_roundtrip_with_ms(self) -> None:
        parsed = rdn.loads("@14:30:00.500")
        serialized = rdn.dumps(parsed)
        reparsed = rdn.loads(serialized)
        assert parsed == reparsed

    def test_roundtrip_without_ms(self) -> None:
        parsed = rdn.loads("@14:30:00")
        serialized = rdn.dumps(parsed)
        reparsed = rdn.loads(serialized)
        assert parsed == reparsed


# ---------------------------------------------------------------------------
# Duration edge cases
# ---------------------------------------------------------------------------


class TestDurationEdgeCases:
    def test_zero_duration(self) -> None:
        result = rdn.loads("@PT0S")
        assert result == timedelta(0)

    def test_year_month_returns_string(self) -> None:
        """Durations with Y/M components return a plain string."""
        result = rdn.loads("@P1Y2M3DT4H5M6S")
        assert isinstance(result, str)
        assert result == "P1Y2M3DT4H5M6S"

    def test_days_hours_minutes_seconds(self) -> None:
        result = rdn.loads("@P3DT4H5M6S")
        assert result == timedelta(days=3, hours=4, minutes=5, seconds=6)


# ---------------------------------------------------------------------------
# RegExp edge cases
# ---------------------------------------------------------------------------


class TestRegExpEdgeCases:
    def test_empty_pattern(self) -> None:
        """An empty regex pattern ``//`` should be valid."""
        result = rdn.loads("//")
        assert isinstance(result, re.Pattern)
        assert result.pattern == ""

    def test_pattern_with_forward_slash(self) -> None:
        """Escaped forward slash in pattern."""
        result = rdn.loads("/a\\/b/")
        assert isinstance(result, re.Pattern)
        assert result.pattern == "a\\/b"

    def test_flags_only_mapped_subset(self) -> None:
        """Only i, m, s map to Python flags; g, d, v, y are dropped."""
        result = rdn.loads("/test/gimsvy")
        assert result.flags & re.IGNORECASE
        assert result.flags & re.MULTILINE
        assert result.flags & re.DOTALL


# ---------------------------------------------------------------------------
# Binary edge cases
# ---------------------------------------------------------------------------


class TestBinaryEdgeCases:
    def test_empty_base64(self) -> None:
        assert rdn.loads('b""') == b""

    def test_empty_hex(self) -> None:
        assert rdn.loads('x""') == b""

    def test_base64_roundtrip(self) -> None:
        data = b"Hello, World!"
        serialized = rdn.dumps(data)
        parsed = rdn.loads(serialized)
        assert parsed == data

    def test_binary_data_roundtrip(self) -> None:
        """Binary data with all byte values 0x00-0xFF."""
        data = bytes(range(256))
        serialized = rdn.dumps(data)
        parsed = rdn.loads(serialized)
        assert parsed == data


# ---------------------------------------------------------------------------
# Set and Map edge cases
# ---------------------------------------------------------------------------


class TestSetMapEdgeCases:
    def test_empty_set_explicit(self) -> None:
        result = rdn.loads("Set{}")
        assert result == frozenset()

    def test_empty_map_explicit(self) -> None:
        result = rdn.loads("Map{}")
        assert result == {}

    def test_single_element_set(self) -> None:
        result = rdn.loads("{42}")
        assert result == frozenset({42})

    def test_set_with_strings(self) -> None:
        result = rdn.loads('Set{"a", "b", "c"}')
        assert result == frozenset({"a", "b", "c"})

    def test_map_with_various_key_types(self) -> None:
        """Map keys can be any hashable type."""
        result = rdn.loads('Map{1 => "one", "two" => 2, true => "yes"}')
        assert result == {1: "one", "two": 2, True: "yes"}


# ---------------------------------------------------------------------------
# Tuple edge cases
# ---------------------------------------------------------------------------


class TestTupleEdgeCases:
    def test_empty_tuple(self) -> None:
        result = rdn.loads("()")
        assert result == ()

    def test_single_element_tuple(self) -> None:
        result = rdn.loads("(42)")
        assert result == (42,)

    def test_nested_tuples(self) -> None:
        result = rdn.loads("((1, 2), (3, 4))")
        assert result == ((1, 2), (3, 4))

    def test_tuple_roundtrip(self) -> None:
        """Tuples should roundtrip correctly."""
        original = (1, "two", True, None)
        serialized = rdn.dumps(original)
        parsed = rdn.loads(serialized)
        assert parsed == original


# ---------------------------------------------------------------------------
# Error message quality
# ---------------------------------------------------------------------------


class TestErrorMessages:
    def test_unexpected_character(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected character") as exc_info:
            rdn.loads("undefined")
        err = exc_info.value
        assert err.pos == 0
        assert err.lineno == 1
        assert err.colno == 1

    def test_unterminated_string_position(self) -> None:
        with pytest.raises(RDNDecodeError) as exc_info:
            rdn.loads('"hello')
        err = exc_info.value
        assert err.doc == '"hello'

    def test_error_in_nested_context(self) -> None:
        """Error position should reflect the nested location."""
        with pytest.raises(RDNDecodeError) as exc_info:
            rdn.loads('{"a": undefined}')
        err = exc_info.value
        assert err.pos > 0  # Points inside the object

    def test_error_attributes(self) -> None:
        """RDNDecodeError should have msg, doc, pos, lineno, colno."""
        with pytest.raises(RDNDecodeError) as exc_info:
            rdn.loads("")
        err = exc_info.value
        assert hasattr(err, "msg")
        assert hasattr(err, "doc")
        assert hasattr(err, "pos")
        assert hasattr(err, "lineno")
        assert hasattr(err, "colno")

    def test_multiline_error_position(self) -> None:
        """Error on line 2 should report correct lineno/colno."""
        text = '{\n  "a": undefined\n}'
        with pytest.raises(RDNDecodeError) as exc_info:
            rdn.loads(text)
        err = exc_info.value
        assert err.lineno == 2


# ---------------------------------------------------------------------------
# Input type handling
# ---------------------------------------------------------------------------


class TestInputTypes:
    def test_bytes_input(self) -> None:
        assert rdn.loads(b'"hello"') == "hello"

    def test_bytearray_input(self) -> None:
        assert rdn.loads(bytearray(b"42")) == 42

    def test_non_string_raises_type_error(self) -> None:
        with pytest.raises(TypeError):
            rdn.loads(123)  # type: ignore[arg-type]

    def test_non_string_raises_type_error_list(self) -> None:
        with pytest.raises(TypeError):
            rdn.loads([1, 2, 3])  # type: ignore[arg-type]


# ---------------------------------------------------------------------------
# Primitive roundtrips
# ---------------------------------------------------------------------------


class TestPrimitiveRoundtrips:
    def test_null_roundtrip(self) -> None:
        assert rdn.loads(rdn.dumps(None)) is None

    def test_true_roundtrip(self) -> None:
        assert rdn.loads(rdn.dumps(True)) is True

    def test_false_roundtrip(self) -> None:
        assert rdn.loads(rdn.dumps(False)) is False

    def test_integer_roundtrip(self) -> None:
        assert rdn.loads(rdn.dumps(42)) == 42

    def test_float_roundtrip(self) -> None:
        assert rdn.loads(rdn.dumps(3.14)) == 3.14

    def test_string_roundtrip(self) -> None:
        assert rdn.loads(rdn.dumps("hello")) == "hello"

    def test_empty_string_roundtrip(self) -> None:
        assert rdn.loads(rdn.dumps("")) == ""

    def test_string_with_special_chars_roundtrip(self) -> None:
        original = 'hello\n\t"world"\\\x00'
        assert rdn.loads(rdn.dumps(original)) == original
