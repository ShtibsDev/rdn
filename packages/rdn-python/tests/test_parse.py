"""Tests for the RDN parser — primitives, strings, numbers, date/time, regexp, and binary types."""

from __future__ import annotations

import math
import re
from collections import OrderedDict
from datetime import datetime, time, timedelta, timezone
from decimal import Decimal
from types import SimpleNamespace

import pytest

from rdn._parser import parse
from rdn.exceptions import RDNDecodeError


# ---------------------------------------------------------------------------
# Basic strings
# ---------------------------------------------------------------------------

class TestBasicStrings:
    def test_simple_string(self) -> None:
        assert parse('"hello"') == "hello"

    def test_string_with_spaces(self) -> None:
        assert parse('"with spaces"') == "with spaces"

    def test_empty_string(self) -> None:
        assert parse('""') == ""

    def test_string_with_digits(self) -> None:
        assert parse('"abc123"') == "abc123"

    def test_string_with_special_chars(self) -> None:
        assert parse('"hello, world!"') == "hello, world!"


# ---------------------------------------------------------------------------
# Escaped strings
# ---------------------------------------------------------------------------

class TestEscapedStrings:
    def test_newline_escape(self) -> None:
        assert parse('"line\\nnewline"') == "line\nnewline"

    def test_tab_escape(self) -> None:
        assert parse('"tab\\there"') == "tab\there"

    def test_quote_escape(self) -> None:
        assert parse('"quote\\"d"') == 'quote"d'

    def test_backslash_escape(self) -> None:
        assert parse('"back\\\\slash"') == "back\\slash"

    def test_slash_escape(self) -> None:
        assert parse('"forward\\/slash"') == "forward/slash"

    def test_carriage_return_escape(self) -> None:
        assert parse('"cr\\rhere"') == "cr\rhere"

    def test_formfeed_escape(self) -> None:
        assert parse('"ff\\fhere"') == "ff\fhere"

    def test_backspace_escape(self) -> None:
        assert parse('"bs\\bhere"') == "bs\bhere"

    def test_multiple_escapes(self) -> None:
        assert parse('"a\\nb\\tc"') == "a\nb\tc"


# ---------------------------------------------------------------------------
# Unicode escapes
# ---------------------------------------------------------------------------

class TestUnicodeEscapes:
    def test_basic_unicode_escape(self) -> None:
        assert parse('"\\u0041"') == "A"

    def test_unicode_chinese_chars(self) -> None:
        assert parse('"\\u4e16\\u754c"') == "\u4e16\u754c"

    def test_unicode_in_context(self) -> None:
        assert parse('"unicode \\u0041"') == "unicode A"

    def test_unicode_null_char(self) -> None:
        assert parse('"\\u0000"') == "\x00"

    def test_unicode_uppercase_hex(self) -> None:
        assert parse('"\\u00E9"') == "\u00e9"

    def test_unicode_lowercase_hex(self) -> None:
        assert parse('"\\u00e9"') == "\u00e9"


# ---------------------------------------------------------------------------
# Surrogate pairs
# ---------------------------------------------------------------------------

class TestSurrogatePairs:
    def test_grinning_face_emoji(self) -> None:
        # U+1F600 = D83D DE00
        assert parse('"\\uD83D\\uDE00"') == "\U0001f600"

    def test_surrogate_pair_in_context(self) -> None:
        assert parse('"hello \\uD83D\\uDE00 world"') == "hello \U0001f600 world"

    def test_musical_symbol(self) -> None:
        # U+1D11E = D834 DD1E (MUSICAL SYMBOL G CLEF)
        assert parse('"\\uD834\\uDD1E"') == "\U0001d11e"


# ---------------------------------------------------------------------------
# Keywords: null, true, false
# ---------------------------------------------------------------------------

class TestKeywords:
    def test_null(self) -> None:
        assert parse("null") is None

    def test_true(self) -> None:
        assert parse("true") is True

    def test_false(self) -> None:
        assert parse("false") is False


# ---------------------------------------------------------------------------
# Whitespace handling
# ---------------------------------------------------------------------------

class TestWhitespace:
    def test_leading_trailing_spaces(self) -> None:
        assert parse("  true  ") is True

    def test_tabs_and_newlines(self) -> None:
        assert parse("\n\tnull\n") is None

    def test_carriage_return(self) -> None:
        assert parse("\r\ntrue\r\n") is True

    def test_string_with_surrounding_ws(self) -> None:
        assert parse('  "hello"  ') == "hello"

    def test_mixed_whitespace(self) -> None:
        assert parse(" \t \n \r false \t \n ") is False


# ---------------------------------------------------------------------------
# Error cases
# ---------------------------------------------------------------------------

class TestErrors:
    def test_unterminated_string(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unterminated string"):
            parse('"hello')

    def test_control_char_in_string(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unescaped control character"):
            parse('"hello\x01world"')

    def test_newline_in_string(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unescaped control character"):
            parse('"hello\nworld"')

    def test_tab_in_string_literal(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unescaped control character"):
            parse('"hello\tworld"')

    def test_invalid_escape(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid escape sequence"):
            parse('"bad\\qescape"')

    def test_invalid_unicode_escape_short(self) -> None:
        # Truncated \uXX — fast scan overshoots past the closing quote,
        # so we get "Unterminated string" rather than "Invalid unicode escape".
        # This matches the TypeScript reference implementation behavior.
        with pytest.raises(RDNDecodeError):
            parse('"\\u00"')

    def test_invalid_unicode_escape_bad_hex(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid unicode escape"):
            parse('"\\uZZZZ"')

    def test_empty_input(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            parse("")

    def test_whitespace_only(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end of input"):
            parse("   ")

    def test_trailing_content(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected data after value"):
            parse("true false")

    def test_unknown_keyword(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected character"):
            parse("undefined")

    def test_lone_backslash_in_string(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unterminated string"):
            parse('"test\\')

    def test_non_string_input(self) -> None:
        with pytest.raises(TypeError, match="First argument must be a string, bytes, or bytearray"):
            parse(123)  # type: ignore[arg-type]

    def test_lone_high_surrogate(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid surrogate pair"):
            parse('"\\uD83D"')

    def test_lone_low_surrogate(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected low surrogate"):
            parse('"\\uDE00"')

    def test_high_surrogate_without_low(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid surrogate pair"):
            parse('"\\uD83Dhello"')

    def test_high_surrogate_bad_low(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid surrogate pair"):
            parse('"\\uD83D\\u0041"')


# ---------------------------------------------------------------------------
# Numbers (Task 5)
# ---------------------------------------------------------------------------

class TestNumbers:
    def test_integer(self) -> None:
        assert parse("42") == 42
        assert isinstance(parse("42"), int)

    def test_zero(self) -> None:
        assert parse("0") == 0
        assert isinstance(parse("0"), int)

    def test_negative_integer(self) -> None:
        assert parse("-7") == -7
        assert isinstance(parse("-7"), int)

    def test_float(self) -> None:
        assert parse("3.14") == 3.14
        assert isinstance(parse("3.14"), float)

    def test_negative_float(self) -> None:
        assert parse("-0.5") == -0.5
        assert isinstance(parse("-0.5"), float)

    def test_exponent(self) -> None:
        result = parse("1e10")
        assert result == 1e10
        assert isinstance(result, float)

    def test_float_with_exponent(self) -> None:
        result = parse("1.5e-3")
        assert result == 1.5e-3
        assert isinstance(result, float)

    def test_uppercase_exponent(self) -> None:
        result = parse("1E+10")
        assert result == 1e10
        assert isinstance(result, float)

    def test_large_integer(self) -> None:
        result = parse("123456789012345")
        assert result == 123456789012345
        assert isinstance(result, int)

    def test_leading_whitespace(self) -> None:
        assert parse("  42  ") == 42

    def test_negative_zero(self) -> None:
        result = parse("-0")
        assert result == 0


# ---------------------------------------------------------------------------
# BigInt (Task 5)
# ---------------------------------------------------------------------------

class TestBigInt:
    def test_bigint(self) -> None:
        assert parse("42n") == 42
        assert isinstance(parse("42n"), int)

    def test_bigint_zero(self) -> None:
        assert parse("0n") == 0
        assert isinstance(parse("0n"), int)

    def test_negative_bigint(self) -> None:
        assert parse("-123n") == -123
        assert isinstance(parse("-123n"), int)

    def test_large_bigint(self) -> None:
        result = parse("9007199254740992n")
        assert result == 9007199254740992
        assert isinstance(result, int)


# ---------------------------------------------------------------------------
# Special numbers (Task 5)
# ---------------------------------------------------------------------------

class TestSpecialNumbers:
    def test_nan(self) -> None:
        result = parse("NaN")
        assert math.isnan(result)

    def test_infinity(self) -> None:
        assert parse("Infinity") == float("inf")

    def test_negative_infinity(self) -> None:
        assert parse("-Infinity") == float("-inf")

    def test_nan_with_whitespace(self) -> None:
        assert math.isnan(parse("  NaN  "))

    def test_infinity_with_whitespace(self) -> None:
        assert parse("  Infinity  ") == float("inf")

    def test_negative_infinity_with_whitespace(self) -> None:
        assert parse("  -Infinity  ") == float("-inf")


# ---------------------------------------------------------------------------
# Number error cases (Task 5)
# ---------------------------------------------------------------------------

class TestNumberErrors:
    def test_leading_zeros(self) -> None:
        with pytest.raises(RDNDecodeError, match="Leading zeros not allowed"):
            parse("007")

    def test_bigint_with_fraction(self) -> None:
        with pytest.raises(RDNDecodeError, match="BigInt cannot have decimal point or exponent"):
            parse("3.14n")

    def test_bigint_with_exponent(self) -> None:
        with pytest.raises(RDNDecodeError, match="BigInt cannot have decimal point or exponent"):
            parse("1e10n")

    def test_no_digit_after_decimal(self) -> None:
        with pytest.raises(RDNDecodeError, match="Expected digit after decimal point"):
            parse("3.")

    def test_no_digit_in_exponent(self) -> None:
        with pytest.raises(RDNDecodeError, match="Expected digit in exponent"):
            parse("3e")

    def test_minus_alone(self) -> None:
        with pytest.raises(RDNDecodeError, match="Expected digit"):
            parse("-")

    def test_hex_not_valid(self) -> None:
        # "0x1" should parse "0" then fail with trailing content
        with pytest.raises(RDNDecodeError, match="Unexpected data after value"):
            parse("0x1")


# ---------------------------------------------------------------------------
# DateTime (Task 6)
# ---------------------------------------------------------------------------

class TestDateTime:
    def test_full_iso_with_ms(self) -> None:
        result = parse("@2024-01-15T10:30:45.123Z")
        assert result == datetime(2024, 1, 15, 10, 30, 45, 123000, tzinfo=timezone.utc)

    def test_full_iso_without_ms(self) -> None:
        result = parse("@2024-01-15T10:30:00Z")
        assert result == datetime(2024, 1, 15, 10, 30, 0, tzinfo=timezone.utc)

    def test_date_only(self) -> None:
        result = parse("@2024-01-15")
        assert result == datetime(2024, 1, 15, tzinfo=timezone.utc)

    def test_midnight(self) -> None:
        result = parse("@2024-01-15T00:00:00.000Z")
        assert result == datetime(2024, 1, 15, tzinfo=timezone.utc)

    def test_end_of_day(self) -> None:
        result = parse("@2024-12-31T23:59:59.999Z")
        assert result == datetime(2024, 12, 31, 23, 59, 59, 999000, tzinfo=timezone.utc)

    def test_result_is_utc(self) -> None:
        result = parse("@2024-01-15T10:30:00Z")
        assert result.tzinfo == timezone.utc

    def test_with_whitespace(self) -> None:
        result = parse("  @2024-01-15  ")
        assert result == datetime(2024, 1, 15, tzinfo=timezone.utc)


# ---------------------------------------------------------------------------
# TimeOnly (Task 6)
# ---------------------------------------------------------------------------

class TestTimeOnly:
    def test_with_ms(self) -> None:
        result = parse("@14:30:00.500")
        assert result == time(14, 30, 0, 500000)

    def test_without_ms(self) -> None:
        result = parse("@14:30:00")
        assert result == time(14, 30, 0)

    def test_midnight(self) -> None:
        result = parse("@00:00:00")
        assert result == time(0, 0, 0)

    def test_end_of_day(self) -> None:
        result = parse("@23:59:59.999")
        assert result == time(23, 59, 59, 999000)

    def test_with_whitespace(self) -> None:
        result = parse("  @14:30:00  ")
        assert result == time(14, 30, 0)


# ---------------------------------------------------------------------------
# Duration (Task 6)
# ---------------------------------------------------------------------------

class TestDuration:
    def test_full_duration(self) -> None:
        result = parse("@P3DT4H5M6S")
        assert result == timedelta(days=3, hours=4, minutes=5, seconds=6)

    def test_just_seconds(self) -> None:
        result = parse("@PT30S")
        assert result == timedelta(seconds=30)

    def test_just_hours(self) -> None:
        result = parse("@PT2H")
        assert result == timedelta(hours=2)

    def test_just_minutes(self) -> None:
        result = parse("@PT45M")
        assert result == timedelta(minutes=45)

    def test_just_days(self) -> None:
        result = parse("@P7D")
        assert result == timedelta(days=7)

    def test_hours_and_minutes(self) -> None:
        result = parse("@PT1H30M")
        assert result == timedelta(hours=1, minutes=30)

    def test_zero_duration(self) -> None:
        result = parse("@PT0S")
        assert result == timedelta(0)

    def test_year_month_fallback_str(self) -> None:
        result = parse("@P1Y2M3D")
        assert result == "P1Y2M3D"
        assert isinstance(result, str)

    def test_year_only_fallback_str(self) -> None:
        result = parse("@P1Y")
        assert result == "P1Y"
        assert isinstance(result, str)

    def test_month_only_fallback_str(self) -> None:
        result = parse("@P6M")
        assert result == "P6M"
        assert isinstance(result, str)

    def test_fractional_seconds(self) -> None:
        result = parse("@PT1.5S")
        assert result == timedelta(seconds=1.5)


# ---------------------------------------------------------------------------
# Unix Timestamp (Task 6)
# ---------------------------------------------------------------------------

class TestUnixTimestamp:
    def test_seconds_10_digits(self) -> None:
        result = parse("@1705312200")
        assert result == datetime.fromtimestamp(1705312200, tz=timezone.utc)

    def test_milliseconds_13_digits(self) -> None:
        result = parse("@1705312200000")
        assert result == datetime.fromtimestamp(1705312200, tz=timezone.utc)

    def test_small_timestamp(self) -> None:
        result = parse("@0")
        assert result == datetime(1970, 1, 1, tzinfo=timezone.utc)

    def test_with_whitespace(self) -> None:
        result = parse("  @1705312200  ")
        assert result == datetime.fromtimestamp(1705312200, tz=timezone.utc)


# ---------------------------------------------------------------------------
# @ error cases (Task 6)
# ---------------------------------------------------------------------------

class TestAtErrors:
    def test_empty_after_at(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected end after @"):
            parse("@")

    def test_invalid_at_literal(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid @ literal"):
            parse("@not-a-date")

    def test_p_alone(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid duration"):
            parse("@P")


# ---------------------------------------------------------------------------
# RegExp (Task 7)
# ---------------------------------------------------------------------------

class TestRegExp:
    def test_case_insensitive(self) -> None:
        result = parse("/^[a-z]+$/i")
        assert result == re.compile("^[a-z]+$", re.IGNORECASE)

    def test_simple_pattern_no_flags(self) -> None:
        result = parse("/hello/")
        assert result == re.compile("hello")

    def test_gim_flags_g_dropped(self) -> None:
        result = parse("/test/gim")
        assert result == re.compile("test", re.IGNORECASE | re.MULTILINE)

    def test_escaped_slash_in_pattern(self) -> None:
        result = parse("/\\//")
        assert result == re.compile("\\/")

    def test_escaped_slash_in_path(self) -> None:
        # RDN source: /\/path/ — the \/ keeps the / in the pattern
        result = parse("/\\/path/")
        assert result == re.compile("\\/path")

    def test_all_flags_only_ims_mapped(self) -> None:
        result = parse("/pattern/dgimsvy")
        assert result == re.compile("pattern", re.IGNORECASE | re.MULTILINE | re.DOTALL)

    def test_unterminated_regex(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unterminated regular expression"):
            parse("/hello")

    def test_with_whitespace(self) -> None:
        result = parse("  /test/i  ")
        assert result == re.compile("test", re.IGNORECASE)


# ---------------------------------------------------------------------------
# Binary base64 (Task 7)
# ---------------------------------------------------------------------------

class TestBinaryBase64:
    def test_hello(self) -> None:
        result = parse('b"SGVsbG8="')
        assert result == b"Hello"

    def test_empty(self) -> None:
        result = parse('b""')
        assert result == b""

    def test_three_null_bytes(self) -> None:
        result = parse('b"AAAA"')
        assert result == b"\x00\x00\x00"

    def test_two_pad_chars(self) -> None:
        # "QQ==" decodes to b"A"
        result = parse('b"QQ=="')
        assert result == b"A"

    def test_invalid_base64_char(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid base64 character"):
            parse('b"SGVs!G8="')

    def test_non_multiple_of_4_length(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid base64: length must be a multiple of 4"):
            parse('b"SGVsbG8"')

    def test_non_zero_padding_bits_1pad(self) -> None:
        # "SGVsbG9=" — the char before '=' is '9' which is index 61 in b64.
        # 61 = 0b111101, lower 2 bits = 0b01 → non-zero
        with pytest.raises(RDNDecodeError, match="Invalid base64: non-zero padding bits"):
            parse('b"SGVsbG9="')

    def test_non_zero_padding_bits_2pad(self) -> None:
        # "QR==" — 'R' is index 17 = 0b010001, lower 4 bits = 0b0001 → non-zero
        with pytest.raises(RDNDecodeError, match="Invalid base64: non-zero padding bits"):
            parse('b"QR=="')

    def test_unterminated(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unterminated binary literal"):
            parse('b"SGVsbG8=')

    def test_with_whitespace(self) -> None:
        result = parse('  b"SGVsbG8="  ')
        assert result == b"Hello"


# ---------------------------------------------------------------------------
# Binary hex (Task 7)
# ---------------------------------------------------------------------------

class TestBinaryHex:
    def test_hello(self) -> None:
        result = parse('x"48656C6C6F"')
        assert result == b"Hello"

    def test_empty(self) -> None:
        result = parse('x""')
        assert result == b""

    def test_ff00(self) -> None:
        result = parse('x"FF00"')
        assert result == b"\xff\x00"

    def test_case_insensitive(self) -> None:
        result = parse('x"aaBB"')
        assert result == b"\xaa\xbb"

    def test_odd_length(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid hex: odd length"):
            parse('x"ABC"')

    def test_invalid_hex_char(self) -> None:
        with pytest.raises(RDNDecodeError, match="Invalid hex character"):
            parse('x"GHIJ"')

    def test_unterminated(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unterminated hex literal"):
            parse('x"48656C6C6F')

    def test_with_whitespace(self) -> None:
        result = parse('  x"FF00"  ')
        assert result == b"\xff\x00"


# ---------------------------------------------------------------------------
# Bytes and bytearray input (Task 9)
# ---------------------------------------------------------------------------

class TestBytesInput:
    def test_bytes_input(self) -> None:
        assert parse(b'"hello"') == "hello"

    def test_bytearray_input(self) -> None:
        assert parse(bytearray(b'"hello"')) == "hello"

    def test_bytes_integer(self) -> None:
        assert parse(b"42") == 42

    def test_bytes_utf8(self) -> None:
        # UTF-8 encoded string with non-ASCII content
        assert parse(b'"\xc3\xa9"') == "\u00e9"

    def test_bytes_invalid_utf8(self) -> None:
        with pytest.raises(UnicodeDecodeError):
            parse(b'"\xff\xfe"')

    def test_non_string_non_bytes_raises(self) -> None:
        with pytest.raises(TypeError, match="First argument must be a string, bytes, or bytearray"):
            parse(123)  # type: ignore[arg-type]


# ---------------------------------------------------------------------------
# parse_int hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseIntHook:
    def test_parse_int_with_decimal(self) -> None:
        result = parse("42", parse_int=Decimal)
        assert result == Decimal("42")
        assert isinstance(result, Decimal)

    def test_parse_int_with_str(self) -> None:
        result = parse("42", parse_int=str)
        assert result == "42"
        assert isinstance(result, str)

    def test_parse_int_negative(self) -> None:
        result = parse("-7", parse_int=Decimal)
        assert result == Decimal("-7")

    def test_parse_int_zero(self) -> None:
        result = parse("0", parse_int=str)
        assert result == "0"

    def test_parse_int_large(self) -> None:
        result = parse("9007199254740992", parse_int=str)
        assert result == "9007199254740992"

    def test_parse_int_in_array(self) -> None:
        result = parse("[1, 2, 3]", parse_int=Decimal)
        assert result == [Decimal("1"), Decimal("2"), Decimal("3")]

    def test_parse_int_does_not_affect_float(self) -> None:
        result = parse("3.14", parse_int=lambda s: "INT:" + s)
        assert result == 3.14
        assert isinstance(result, float)


# ---------------------------------------------------------------------------
# parse_float hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseFloatHook:
    def test_parse_float_with_decimal(self) -> None:
        result = parse("3.14", parse_float=Decimal)
        assert result == Decimal("3.14")
        assert isinstance(result, Decimal)

    def test_parse_float_with_str(self) -> None:
        result = parse("3.14", parse_float=str)
        assert result == "3.14"

    def test_parse_float_exponent(self) -> None:
        result = parse("1e10", parse_float=Decimal)
        assert result == Decimal("1e10")

    def test_parse_float_negative(self) -> None:
        result = parse("-0.5", parse_float=Decimal)
        assert result == Decimal("-0.5")

    def test_parse_float_does_not_affect_int(self) -> None:
        result = parse("42", parse_float=lambda s: "FLOAT:" + s)
        assert result == 42
        assert isinstance(result, int)

    def test_parse_float_does_not_affect_nan(self) -> None:
        """NaN, Infinity, -Infinity should NOT go through parse_float."""
        called = []
        result = parse("NaN", parse_float=lambda s: called.append(s) or float(s))
        assert math.isnan(result)
        assert called == []

    def test_parse_float_does_not_affect_infinity(self) -> None:
        called = []
        result = parse("Infinity", parse_float=lambda s: called.append(s) or float(s))
        assert result == float("inf")
        assert called == []

    def test_parse_float_does_not_affect_neg_infinity(self) -> None:
        called = []
        result = parse("-Infinity", parse_float=lambda s: called.append(s) or float(s))
        assert result == float("-inf")
        assert called == []


# ---------------------------------------------------------------------------
# parse_bigint hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseBigintHook:
    def test_parse_bigint_with_decimal(self) -> None:
        result = parse("42n", parse_bigint=Decimal)
        assert result == Decimal("42")
        assert isinstance(result, Decimal)

    def test_parse_bigint_with_str(self) -> None:
        result = parse("42n", parse_bigint=str)
        assert result == "42"

    def test_parse_bigint_negative(self) -> None:
        result = parse("-123n", parse_bigint=Decimal)
        assert result == Decimal("-123")

    def test_parse_bigint_receives_string_without_n(self) -> None:
        received = []
        parse("42n", parse_bigint=lambda s: received.append(s) or int(s))
        assert received == ["42"]

    def test_parse_bigint_negative_receives_minus(self) -> None:
        received = []
        parse("-42n", parse_bigint=lambda s: received.append(s) or int(s))
        assert received == ["-42"]

    def test_parse_bigint_does_not_affect_int(self) -> None:
        result = parse("42", parse_bigint=lambda s: "BIG:" + s)
        assert result == 42
        assert isinstance(result, int)


# ---------------------------------------------------------------------------
# parse_datetime hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseDatetimeHook:
    def test_parse_datetime_hook(self) -> None:
        result = parse("@2024-01-15T10:30:00Z", parse_datetime=lambda dt: dt.isoformat())
        assert result == "2024-01-15T10:30:00+00:00"

    def test_parse_datetime_date_only(self) -> None:
        result = parse("@2024-01-15", parse_datetime=lambda dt: dt.date())
        from datetime import date
        assert result == date(2024, 1, 15)

    def test_parse_datetime_unix_timestamp(self) -> None:
        """Unix timestamps also go through parse_datetime hook."""
        called = []
        result = parse("@0", parse_datetime=lambda dt: (called.append(dt), dt)[1])
        assert len(called) == 1
        assert called[0] == datetime(1970, 1, 1, tzinfo=timezone.utc)

    def test_parse_datetime_does_not_affect_timeonly(self) -> None:
        result = parse("@14:30:00", parse_datetime=lambda dt: "DATETIME")
        assert result == time(14, 30, 0)


# ---------------------------------------------------------------------------
# parse_timeonly hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseTimeonlyHook:
    def test_parse_timeonly_hook(self) -> None:
        result = parse("@14:30:00", parse_timeonly=lambda t: t.isoformat())
        assert result == "14:30:00"

    def test_parse_timeonly_with_ms(self) -> None:
        result = parse("@14:30:00.500", parse_timeonly=lambda t: str(t))
        assert "14:30:00.500000" in result

    def test_parse_timeonly_does_not_affect_datetime(self) -> None:
        result = parse("@2024-01-15", parse_timeonly=lambda t: "TIMEONLY")
        assert isinstance(result, datetime)


# ---------------------------------------------------------------------------
# parse_duration hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseDurationHook:
    def test_parse_duration_timedelta(self) -> None:
        result = parse("@PT30S", parse_duration=lambda d: str(d))
        assert result == "0:00:30"

    def test_parse_duration_string_fallback(self) -> None:
        """Y/M durations (returned as str) also go through the hook."""
        result = parse("@P1Y2M", parse_duration=lambda d: f"duration:{d}")
        assert result == "duration:P1Y2M"

    def test_parse_duration_does_not_affect_datetime(self) -> None:
        result = parse("@2024-01-15", parse_duration=lambda d: "DURATION")
        assert isinstance(result, datetime)


# ---------------------------------------------------------------------------
# parse_regexp hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseRegexpHook:
    def test_parse_regexp_hook(self) -> None:
        result = parse("/test/i", parse_regexp=lambda p: p.pattern)
        assert result == "test"

    def test_parse_regexp_returns_custom(self) -> None:
        result = parse("/^abc$/", parse_regexp=lambda p: {"pattern": p.pattern, "flags": p.flags})
        assert result["pattern"] == "^abc$"


# ---------------------------------------------------------------------------
# parse_binary hook (Task 9)
# ---------------------------------------------------------------------------

class TestParseBinaryHook:
    def test_parse_binary_b64(self) -> None:
        result = parse('b"SGVsbG8="', parse_binary=lambda b: b.decode("utf-8"))
        assert result == "Hello"

    def test_parse_binary_hex(self) -> None:
        result = parse('x"48656C6C6F"', parse_binary=lambda b: b.decode("utf-8"))
        assert result == "Hello"

    def test_parse_binary_empty_b64(self) -> None:
        result = parse('b""', parse_binary=lambda b: len(b))
        assert result == 0

    def test_parse_binary_empty_hex(self) -> None:
        result = parse('x""', parse_binary=lambda b: len(b))
        assert result == 0


# ---------------------------------------------------------------------------
# object_hook (Task 9)
# ---------------------------------------------------------------------------

class TestObjectHook:
    def test_object_hook_simple(self) -> None:
        result = parse('{"a": 1}', object_hook=lambda d: SimpleNamespace(**d))
        assert isinstance(result, SimpleNamespace)
        assert result.a == 1

    def test_object_hook_nested(self) -> None:
        """object_hook is called for each nested object."""
        result = parse('{"outer": {"inner": 42}}', object_hook=lambda d: SimpleNamespace(**d))
        assert isinstance(result, SimpleNamespace)
        assert isinstance(result.outer, SimpleNamespace)
        assert result.outer.inner == 42

    def test_object_hook_empty_object(self) -> None:
        result = parse("{}", object_hook=lambda d: "empty")
        assert result == "empty"

    def test_object_hook_not_called_for_array(self) -> None:
        called = []
        result = parse("[1, 2]", object_hook=lambda d: called.append(d))
        assert result == [1, 2]
        assert called == []


# ---------------------------------------------------------------------------
# object_pairs_hook (Task 9)
# ---------------------------------------------------------------------------

class TestObjectPairsHook:
    def test_object_pairs_hook_ordered_dict(self) -> None:
        result = parse('{"b": 2, "a": 1}', object_pairs_hook=OrderedDict)
        assert isinstance(result, OrderedDict)
        assert list(result.keys()) == ["b", "a"]
        assert list(result.values()) == [2, 1]

    def test_object_pairs_hook_empty(self) -> None:
        result = parse("{}", object_pairs_hook=OrderedDict)
        assert isinstance(result, OrderedDict)
        assert len(result) == 0

    def test_object_pairs_hook_receives_list_of_tuples(self) -> None:
        received = []
        parse('{"x": 1, "y": 2}', object_pairs_hook=lambda pairs: received.extend(pairs) or dict(pairs))
        assert received == [("x", 1), ("y", 2)]

    def test_object_pairs_hook_priority_over_object_hook(self) -> None:
        """object_pairs_hook takes priority over object_hook."""
        result = parse(
            '{"a": 1}',
            object_hook=lambda d: "OBJECT_HOOK",
            object_pairs_hook=lambda pairs: "PAIRS_HOOK",
        )
        assert result == "PAIRS_HOOK"

    def test_object_pairs_hook_nested(self) -> None:
        result = parse('{"a": {"b": 1}}', object_pairs_hook=OrderedDict)
        assert isinstance(result, OrderedDict)
        assert isinstance(result["a"], OrderedDict)


# ---------------------------------------------------------------------------
# Trailing content error (Task 9)
# ---------------------------------------------------------------------------

class TestTrailingContent:
    def test_trailing_content_string(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected data after value"):
            parse('"hello" "world"')

    def test_trailing_content_number(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected data after value"):
            parse("42 43")

    def test_trailing_content_after_object(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected data after value"):
            parse('{"a": 1} {"b": 2}')
