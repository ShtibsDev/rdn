"""Tests for the RDNModel mixin."""

from __future__ import annotations

import re
from datetime import datetime, time, timedelta, timezone
from typing import Optional

import pytest
import rdn
from pydantic import ConfigDict, Field

from rdn_pydantic import (
    PydanticRDNBigInt,
    PydanticRDNDateTime,
    PydanticRDNDuration,
    PydanticRDNSet,
    PydanticRDNTimeOnly,
    RDNModel,
)


# ── Simple model ──────────────────────────────────────────────────────────────


class SimpleModel(RDNModel):
    name: str
    age: int


class TestSimpleModel:
    def test_model_dump_rdn(self):
        m = SimpleModel(name="Alice", age=30)
        result = m.model_dump_rdn()
        assert isinstance(result, str)
        parsed = rdn.loads(result)
        assert parsed["name"] == "Alice"
        assert parsed["age"] == 30

    def test_model_validate_rdn(self):
        rdn_str = '{"name": "Bob", "age": 25}'
        m = SimpleModel.model_validate_rdn(rdn_str)
        assert m.name == "Bob"
        assert m.age == 25

    def test_roundtrip(self):
        m1 = SimpleModel(name="Charlie", age=42)
        rdn_str = m1.model_dump_rdn()
        m2 = SimpleModel.model_validate_rdn(rdn_str)
        assert m1.name == m2.name
        assert m1.age == m2.age


# ── Model with RDN types ─────────────────────────────────────────────────────


class RDNTypesModel(RDNModel):
    big_id: PydanticRDNBigInt
    created_at: PydanticRDNDateTime
    wake_time: PydanticRDNTimeOnly
    duration: PydanticRDNDuration
    tags: PydanticRDNSet


class TestRDNTypesModel:
    def test_model_dump_rdn_with_rdn_types(self):
        m = RDNTypesModel(
            big_id=12345678901234567890,
            created_at=datetime(2024, 1, 15, 12, 0, 0, tzinfo=timezone.utc),
            wake_time=time(7, 30, 0),
            duration=timedelta(days=3, hours=4),
            tags={1, 2, 3},
        )
        result = m.model_dump_rdn()
        assert isinstance(result, str)
        # The result should be parseable RDN
        parsed = rdn.loads(result)
        assert parsed["big_id"] == 12345678901234567890

    def test_model_validate_rdn_with_rdn_types(self):
        m = RDNTypesModel(
            big_id=42,
            created_at=datetime(2024, 6, 1, 0, 0, 0, tzinfo=timezone.utc),
            wake_time=time(8, 0, 0),
            duration=timedelta(hours=1),
            tags={10, 20},
        )
        rdn_str = m.model_dump_rdn()
        m2 = RDNTypesModel.model_validate_rdn(rdn_str)
        assert m2.big_id == m.big_id


# ── Indent ────────────────────────────────────────────────────────────────────


class TestIndent:
    def test_model_dump_rdn_indent(self):
        m = SimpleModel(name="Alice", age=30)
        result = m.model_dump_rdn(indent=2)
        assert isinstance(result, str)
        # Indented output should contain newlines
        assert "\n" in result
        # Should still parse back correctly
        parsed = rdn.loads(result)
        assert parsed["name"] == "Alice"
        assert parsed["age"] == 30

    def test_model_dump_rdn_no_indent(self):
        m = SimpleModel(name="Alice", age=30)
        result = m.model_dump_rdn()
        # Compact output should not have newlines (for this simple case)
        # It might have spaces around separators though
        parsed = rdn.loads(result)
        assert parsed["name"] == "Alice"


# ── by_alias ──────────────────────────────────────────────────────────────────


class AliasModel(RDNModel):
    model_config = ConfigDict(populate_by_name=True)

    user_name: str = Field(alias="userName")
    user_id: PydanticRDNBigInt = Field(alias="userId")


class TestByAlias:
    def test_model_dump_rdn_by_alias(self):
        m = AliasModel(user_name="Alice", user_id=42)
        result = m.model_dump_rdn(by_alias=True)
        parsed = rdn.loads(result)
        assert "userName" in parsed
        assert "userId" in parsed
        assert parsed["userName"] == "Alice"
        assert parsed["userId"] == 42

    def test_model_dump_rdn_no_alias(self):
        m = AliasModel(user_name="Bob", user_id=99)
        result = m.model_dump_rdn(by_alias=False)
        parsed = rdn.loads(result)
        assert "user_name" in parsed
        assert "user_id" in parsed


# ── exclude_none ──────────────────────────────────────────────────────────────


class OptionalModel(RDNModel):
    name: str
    email: Optional[str] = None
    age: Optional[int] = None


class TestExcludeNone:
    def test_model_dump_rdn_exclude_none(self):
        m = OptionalModel(name="Alice")
        result = m.model_dump_rdn(exclude_none=True)
        parsed = rdn.loads(result)
        assert "name" in parsed
        assert "email" not in parsed
        assert "age" not in parsed

    def test_model_dump_rdn_include_none(self):
        m = OptionalModel(name="Bob")
        result = m.model_dump_rdn(exclude_none=False)
        parsed = rdn.loads(result)
        assert "name" in parsed
        assert "email" in parsed
        assert parsed["email"] is None

    def test_model_dump_rdn_partial_none(self):
        m = OptionalModel(name="Charlie", email="charlie@example.com")
        result = m.model_dump_rdn(exclude_none=True)
        parsed = rdn.loads(result)
        assert parsed["name"] == "Charlie"
        assert parsed["email"] == "charlie@example.com"
        assert "age" not in parsed


# ── Nested models ────────────────────────────────────────────────────────────


class Address(RDNModel):
    street: str
    city: str


class Person(RDNModel):
    name: str
    age: int
    address: Address


class TestNestedModels:
    def test_model_dump_rdn_nested(self):
        m = Person(name="Alice", age=30, address=Address(street="123 Main St", city="Springfield"))
        result = m.model_dump_rdn()
        parsed = rdn.loads(result)
        assert parsed["name"] == "Alice"
        assert parsed["address"]["street"] == "123 Main St"
        assert parsed["address"]["city"] == "Springfield"

    def test_model_validate_rdn_nested(self):
        rdn_str = '{"name": "Bob", "age": 25, "address": {"street": "456 Oak Ave", "city": "Shelbyville"}}'
        m = Person.model_validate_rdn(rdn_str)
        assert m.name == "Bob"
        assert m.age == 25
        assert m.address.street == "456 Oak Ave"
        assert m.address.city == "Shelbyville"

    def test_nested_roundtrip(self):
        m1 = Person(name="Charlie", age=42, address=Address(street="789 Elm Blvd", city="Capital City"))
        rdn_str = m1.model_dump_rdn()
        m2 = Person.model_validate_rdn(rdn_str)
        assert m1.name == m2.name
        assert m1.address.street == m2.address.street

    def test_nested_indent(self):
        m = Person(name="Diana", age=28, address=Address(street="101 Pine Rd", city="Ogdenville"))
        result = m.model_dump_rdn(indent=2)
        assert "\n" in result
        parsed = rdn.loads(result)
        assert parsed["address"]["city"] == "Ogdenville"


# ── Bytes input ───────────────────────────────────────────────────────────────


class TestBytesInput:
    def test_model_validate_rdn_bytes(self):
        rdn_bytes = b'{"name": "Alice", "age": 30}'
        m = SimpleModel.model_validate_rdn(rdn_bytes)
        assert m.name == "Alice"
        assert m.age == 30


# ── Combined features ────────────────────────────────────────────────────────


class FullModel(RDNModel):
    model_config = ConfigDict(populate_by_name=True)

    name: str = Field(alias="displayName")
    score: Optional[int] = None
    active: bool = True


class TestCombinedFeatures:
    def test_alias_and_exclude_none(self):
        m = FullModel(name="Test", active=True)
        result = m.model_dump_rdn(by_alias=True, exclude_none=True)
        parsed = rdn.loads(result)
        assert "displayName" in parsed
        assert "score" not in parsed
        assert parsed["active"] is True

    def test_alias_exclude_none_and_indent(self):
        m = FullModel(name="Test", score=100)
        result = m.model_dump_rdn(by_alias=True, exclude_none=False, indent=2)
        assert "\n" in result
        parsed = rdn.loads(result)
        assert parsed["displayName"] == "Test"
        assert parsed["score"] == 100
