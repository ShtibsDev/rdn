"""Tests for rdn_pydantic custom Pydantic types."""

from __future__ import annotations

import re
from datetime import datetime, time, timedelta, timezone

import pytest
from pydantic import BaseModel, ValidationError

from rdn_pydantic.types import (
    PydanticRDNBigInt,
    PydanticRDNBinary,
    PydanticRDNDateTime,
    PydanticRDNDuration,
    PydanticRDNRegExp,
    PydanticRDNSet,
    PydanticRDNTimeOnly,
)


# ── BigInt ────────────────────────────────────────────────────────────────────


class BigIntModel(BaseModel):
    value: PydanticRDNBigInt


class TestPydanticRDNBigInt:
    def test_validates_int(self):
        m = BigIntModel(value=42)
        assert m.value == 42

    def test_validates_large_int(self):
        big = 12345678901234567890
        m = BigIntModel(value=big)
        assert m.value == big

    def test_validates_string_int(self):
        m = BigIntModel(value="123")
        assert m.value == 123

    def test_rejects_bool(self):
        with pytest.raises(ValidationError):
            BigIntModel(value=True)

    def test_rejects_float(self):
        with pytest.raises(ValidationError):
            BigIntModel(value=3.14)

    def test_rejects_non_numeric_string(self):
        with pytest.raises(ValidationError):
            BigIntModel(value="abc")

    def test_serialization(self):
        m = BigIntModel(value=42)
        data = m.model_dump()
        assert data["value"] == 42

    def test_negative_int(self):
        m = BigIntModel(value=-99)
        assert m.value == -99

    def test_zero(self):
        m = BigIntModel(value=0)
        assert m.value == 0


# ── DateTime ──────────────────────────────────────────────────────────────────


class DateTimeModel(BaseModel):
    value: PydanticRDNDateTime


class TestPydanticRDNDateTime:
    def test_validates_datetime(self):
        dt = datetime(2024, 1, 15, 12, 0, 0, tzinfo=timezone.utc)
        m = DateTimeModel(value=dt)
        assert m.value == dt

    def test_validates_iso_string(self):
        m = DateTimeModel(value="2024-01-15T12:00:00+00:00")
        assert m.value.year == 2024
        assert m.value.month == 1
        assert m.value.day == 15

    def test_rejects_invalid_string(self):
        with pytest.raises(ValidationError):
            DateTimeModel(value="not-a-date")

    def test_rejects_int(self):
        with pytest.raises(ValidationError):
            DateTimeModel(value=12345)

    def test_serialization(self):
        dt = datetime(2024, 6, 15, 10, 30, 0, tzinfo=timezone.utc)
        m = DateTimeModel(value=dt)
        data = m.model_dump()
        assert isinstance(data["value"], datetime)
        assert data["value"] == dt

    def test_naive_datetime(self):
        dt = datetime(2024, 1, 15, 12, 0, 0)
        m = DateTimeModel(value=dt)
        assert m.value == dt


# ── TimeOnly ──────────────────────────────────────────────────────────────────


class TimeOnlyModel(BaseModel):
    value: PydanticRDNTimeOnly


class TestPydanticRDNTimeOnly:
    def test_validates_time(self):
        t = time(14, 30, 0)
        m = TimeOnlyModel(value=t)
        assert m.value == t

    def test_validates_time_string(self):
        m = TimeOnlyModel(value="14:30:00")
        assert m.value.hour == 14
        assert m.value.minute == 30

    def test_rejects_datetime(self):
        """datetime is a subclass of time's date, but not time itself;
        however datetime IS a subclass of date. Our validator explicitly
        rejects datetime since it is not a plain time."""
        with pytest.raises(ValidationError):
            TimeOnlyModel(value=datetime(2024, 1, 15, 12, 0, 0))

    def test_rejects_int(self):
        with pytest.raises(ValidationError):
            TimeOnlyModel(value=42)

    def test_rejects_invalid_string(self):
        with pytest.raises(ValidationError):
            TimeOnlyModel(value="not-a-time")

    def test_serialization(self):
        t = time(8, 15, 30)
        m = TimeOnlyModel(value=t)
        data = m.model_dump()
        assert isinstance(data["value"], time)
        assert data["value"] == t

    def test_midnight(self):
        t = time(0, 0, 0)
        m = TimeOnlyModel(value=t)
        assert m.value == t


# ── Duration ──────────────────────────────────────────────────────────────────


class DurationModel(BaseModel):
    value: PydanticRDNDuration


class TestPydanticRDNDuration:
    def test_validates_timedelta(self):
        td = timedelta(days=3, hours=4)
        m = DurationModel(value=td)
        assert m.value == td

    def test_validates_numeric_seconds(self):
        m = DurationModel(value=3600)
        assert m.value == timedelta(seconds=3600)

    def test_validates_float_seconds(self):
        m = DurationModel(value=1.5)
        assert m.value == timedelta(seconds=1.5)

    def test_rejects_string(self):
        with pytest.raises(ValidationError):
            DurationModel(value="P3D")

    def test_rejects_list(self):
        with pytest.raises(ValidationError):
            DurationModel(value=[1, 2, 3])

    def test_serialization(self):
        td = timedelta(hours=2, minutes=30)
        m = DurationModel(value=td)
        data = m.model_dump()
        assert isinstance(data["value"], timedelta)
        assert data["value"] == td

    def test_zero_duration(self):
        m = DurationModel(value=timedelta(0))
        assert m.value == timedelta(0)


# ── RegExp ────────────────────────────────────────────────────────────────────


class RegExpModel(BaseModel):
    value: PydanticRDNRegExp


class TestPydanticRDNRegExp:
    def test_validates_compiled_pattern(self):
        p = re.compile(r"\d+")
        m = RegExpModel(value=p)
        assert m.value.pattern == r"\d+"

    def test_validates_string_pattern(self):
        m = RegExpModel(value=r"\w+")
        assert isinstance(m.value, re.Pattern)
        assert m.value.pattern == r"\w+"

    def test_rejects_invalid_regex(self):
        with pytest.raises(ValidationError):
            RegExpModel(value="[invalid")

    def test_rejects_int(self):
        with pytest.raises(ValidationError):
            RegExpModel(value=42)

    def test_serialization(self):
        p = re.compile(r"hello.*world")
        m = RegExpModel(value=p)
        data = m.model_dump()
        assert data["value"] == r"hello.*world"

    def test_flags_preserved(self):
        p = re.compile(r"test", re.IGNORECASE)
        m = RegExpModel(value=p)
        assert m.value.flags & re.IGNORECASE


# ── Binary ────────────────────────────────────────────────────────────────────


class BinaryModel(BaseModel):
    value: PydanticRDNBinary


class TestPydanticRDNBinary:
    def test_validates_bytes(self):
        m = BinaryModel(value=b"hello")
        assert m.value == b"hello"

    def test_validates_base64_string(self):
        import base64
        encoded = base64.b64encode(b"hello world").decode()
        m = BinaryModel(value=encoded)
        assert m.value == b"hello world"

    def test_rejects_int(self):
        with pytest.raises(ValidationError):
            BinaryModel(value=42)

    def test_rejects_invalid_base64(self):
        with pytest.raises(ValidationError):
            BinaryModel(value="not!valid!base64!!!")

    def test_serialization(self):
        m = BinaryModel(value=b"\x00\x01\x02")
        data = m.model_dump()
        assert isinstance(data["value"], bytes)
        assert data["value"] == b"\x00\x01\x02"

    def test_empty_bytes(self):
        m = BinaryModel(value=b"")
        assert m.value == b""


# ── Set ───────────────────────────────────────────────────────────────────────


class SetModel(BaseModel):
    value: PydanticRDNSet


class TestPydanticRDNSet:
    def test_validates_set(self):
        m = SetModel(value={1, 2, 3})
        assert m.value == {1, 2, 3}

    def test_validates_list(self):
        m = SetModel(value=[1, 2, 3])
        assert m.value == {1, 2, 3}

    def test_validates_tuple(self):
        m = SetModel(value=(4, 5, 6))
        assert m.value == {4, 5, 6}

    def test_validates_frozenset(self):
        m = SetModel(value=frozenset([7, 8]))
        assert m.value == {7, 8}

    def test_deduplicates(self):
        m = SetModel(value=[1, 1, 2, 2, 3])
        assert m.value == {1, 2, 3}

    def test_rejects_int(self):
        with pytest.raises(ValidationError):
            SetModel(value=42)

    def test_rejects_string(self):
        with pytest.raises(ValidationError):
            SetModel(value="abc")

    def test_serialization_is_list(self):
        m = SetModel(value={3, 1, 2})
        data = m.model_dump()
        assert isinstance(data["value"], list)
        assert sorted(data["value"]) == [1, 2, 3]

    def test_empty_set(self):
        m = SetModel(value=set())
        assert m.value == set()
