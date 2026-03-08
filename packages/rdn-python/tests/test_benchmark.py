"""Comprehensive pytest-benchmark tests for rdn-python.

Covers parse and stringify across five payload categories
(small/medium/large JSON, medium/large RDN) plus micro-benchmarks
for string-heavy, number-heavy, and nested-object workloads.

Run with:
    pytest tests/test_benchmark.py --benchmark-enable --benchmark-min-rounds=100

Benchmarks are disabled by default (see pyproject.toml addopts).
"""

from __future__ import annotations

import json
from typing import Any

import pytest

import rdn
from rdn._parser import parse as _py_parse
from rdn._serializer import stringify as _py_stringify

# ---------------------------------------------------------------------------
# Optional native extension
# ---------------------------------------------------------------------------
try:
    from rdn._native import parse as _native_parse, stringify as _native_stringify
    HAS_NATIVE = True
except ImportError:
    HAS_NATIVE = False

native_only = pytest.mark.skipif(not HAS_NATIVE, reason="rdn._native not installed")

# ---------------------------------------------------------------------------
# Fixtures — pure JSON payloads (valid RDN too)
# ---------------------------------------------------------------------------
SMALL_JSON = '{"name": "test", "value": 42, "active": true}'

MEDIUM_JSON = json.dumps({
    "users": [
        {"id": i, "name": f"user_{i}", "email": f"user{i}@example.com", "active": i % 2 == 0, "score": i * 1.5, "tags": ["a", "b", "c"]}
        for i in range(20)
    ],
    "meta": {"total": 20, "page": 1, "perPage": 20, "hasMore": False},
})

LARGE_JSON = json.dumps({
    "apiVersion": "2.1.0",
    "requestId": "c7d83aef-cf17-42e1-baef-00004539f5f8",
    "data": {
        "users": [
            {
                "id": f"usr_{i:05d}",
                "email": f"user{i}@example.com",
                "profile": {"firstName": f"First{i}", "lastName": f"Last{i}", "bio": f"Bio text for user {i} " * 5},
                "preferences": {"theme": "dark" if i % 2 else "light", "notifications": {"email": True, "push": False, "sms": i % 3 == 0}},
                "roles": ["admin", "editor"] if i % 5 == 0 else ["viewer"],
                "scores": [round(i * 1.1 + j * 0.7, 2) for j in range(10)],
                "metadata": {f"key_{j}": f"value_{j}" for j in range(5)},
            }
            for i in range(50)
        ]
    },
    "pagination": {"total": 50, "page": 1, "perPage": 50, "hasMore": False},
})

# ---------------------------------------------------------------------------
# Fixtures — RDN-only payloads (extended types)
# ---------------------------------------------------------------------------
MEDIUM_RDN = """{
  "users": [
    {"id": 1, "name": "Alice", "created": @2024-01-15T10:30:00.000Z, "tags": {"admin", "editor"}},
    {"id": 2, "name": "Bob", "created": @2024-02-20T08:00:00.000Z, "tags": {"viewer"}},
    {"id": 3, "name": "Charlie", "created": @2024-03-10T14:15:00.000Z, "tags": {"editor"}}
  ],
  "meta": {"total": 3, "generatedAt": @2024-06-01T00:00:00.000Z}
}"""

LARGE_RDN = """{
  "meta": {
    "apiVersion": "2.1.0",
    "requestId": "c7d83aef-cf17-42e1-baef-00004539f5f8",
    "timestamp": @2025-08-15T14:32:07.123Z,
    "rateLimit": {"remaining": 4982, "resetAt": @2025-08-15T15:00:00.000Z}
  },
  "data": {
    "users": [
      {
        "id": "usr_00001",
        "externalId": 900000001338n,
        "email": "jack.thompson12@gmail.com",
        "createdAt": @2024-11-05T10:34:33.000Z,
        "lastLogin": @2025-09-08T01:25:47.000Z,
        "preferences": {"theme": "light", "notifications": {"email": true, "push": false}},
        "avatar": b"t41AP44NzIXj1CFhg2UR7RE7SjxEBwlkoOVKGVLdHeTgEJAKaQ==",
        "roles": {"admin", "editor"},
        "sessionLog": {"2025-09-07": @PT2H30M, "2025-09-08": @PT1H15M},
        "namePattern": /^[A-Za-z\\s'-]+$/
      }
    ]
  }
}"""

# ---------------------------------------------------------------------------
# Fixtures — micro-benchmark payloads
# ---------------------------------------------------------------------------
STRING_HEAVY_JSON = json.dumps({f"key_{i}": f"value string number {i} with some extra text to make it longer" for i in range(100)})

NUMBER_HEAVY_JSON = json.dumps({"numbers": [i * 1.23456789 for i in range(500)]})

NESTED_OBJECT_JSON = json.dumps({
    "level1": {
        f"a{i}": {
            f"b{j}": {
                f"c{k}": k
                for k in range(3)
            }
            for j in range(5)
        }
        for i in range(5)
    }
})

# ---------------------------------------------------------------------------
# Pre-parsed objects for stringify benchmarks
# ---------------------------------------------------------------------------
SMALL_OBJ: Any = json.loads(SMALL_JSON)
MEDIUM_OBJ: Any = json.loads(MEDIUM_JSON)
LARGE_OBJ: Any = json.loads(LARGE_JSON)

MEDIUM_RDN_OBJ: Any = rdn.loads(MEDIUM_RDN)
LARGE_RDN_OBJ: Any = rdn.loads(LARGE_RDN)

STRING_HEAVY_OBJ: Any = json.loads(STRING_HEAVY_JSON)
NUMBER_HEAVY_OBJ: Any = json.loads(NUMBER_HEAVY_JSON)
NESTED_OBJECT_OBJ: Any = json.loads(NESTED_OBJECT_JSON)


# ==========================================================================
# PARSE — Native path (rdn._native.parse)
# ==========================================================================

class TestParseNative:
    """Parse benchmarks using the Rust native extension."""

    @native_only
    def test_parse_small_json(self, benchmark: Any) -> None:
        benchmark(_native_parse, SMALL_JSON)

    @native_only
    def test_parse_medium_json(self, benchmark: Any) -> None:
        benchmark(_native_parse, MEDIUM_JSON)

    @native_only
    def test_parse_large_json(self, benchmark: Any) -> None:
        benchmark(_native_parse, LARGE_JSON)

    @native_only
    def test_parse_medium_rdn(self, benchmark: Any) -> None:
        benchmark(_native_parse, MEDIUM_RDN)

    @native_only
    def test_parse_large_rdn(self, benchmark: Any) -> None:
        benchmark(_native_parse, LARGE_RDN)


# ==========================================================================
# PARSE — Pure-Python path (rdn._parser.parse)
# ==========================================================================

class TestParsePurePython:
    """Parse benchmarks using the pure-Python parser."""

    def test_parse_small_json(self, benchmark: Any) -> None:
        benchmark(_py_parse, SMALL_JSON)

    def test_parse_medium_json(self, benchmark: Any) -> None:
        benchmark(_py_parse, MEDIUM_JSON)

    def test_parse_large_json(self, benchmark: Any) -> None:
        benchmark(_py_parse, LARGE_JSON)

    def test_parse_medium_rdn(self, benchmark: Any) -> None:
        benchmark(_py_parse, MEDIUM_RDN)

    def test_parse_large_rdn(self, benchmark: Any) -> None:
        benchmark(_py_parse, LARGE_RDN)


# ==========================================================================
# STRINGIFY — Native path (rdn._native.stringify)
# ==========================================================================

class TestStringifyNative:
    """Stringify benchmarks using the Rust native extension."""

    @native_only
    def test_stringify_small_obj(self, benchmark: Any) -> None:
        benchmark(_native_stringify, SMALL_OBJ)

    @native_only
    def test_stringify_medium_obj(self, benchmark: Any) -> None:
        benchmark(_native_stringify, MEDIUM_OBJ)

    @native_only
    def test_stringify_large_obj(self, benchmark: Any) -> None:
        benchmark(_native_stringify, LARGE_OBJ)

    @native_only
    def test_stringify_medium_rdn_obj(self, benchmark: Any) -> None:
        benchmark(_native_stringify, MEDIUM_RDN_OBJ)

    @native_only
    def test_stringify_large_rdn_obj(self, benchmark: Any) -> None:
        benchmark(_native_stringify, LARGE_RDN_OBJ)


# ==========================================================================
# STRINGIFY — Pure-Python path (rdn._serializer.stringify)
# ==========================================================================

class TestStringifyPurePython:
    """Stringify benchmarks using the pure-Python serializer."""

    def test_stringify_small_obj(self, benchmark: Any) -> None:
        benchmark(_py_stringify, SMALL_OBJ)

    def test_stringify_medium_obj(self, benchmark: Any) -> None:
        benchmark(_py_stringify, MEDIUM_OBJ)

    def test_stringify_large_obj(self, benchmark: Any) -> None:
        benchmark(_py_stringify, LARGE_OBJ)

    def test_stringify_medium_rdn_obj(self, benchmark: Any) -> None:
        benchmark(_py_stringify, MEDIUM_RDN_OBJ)

    def test_stringify_large_rdn_obj(self, benchmark: Any) -> None:
        benchmark(_py_stringify, LARGE_RDN_OBJ)


# ==========================================================================
# MICRO — Parse, native path
# ==========================================================================

class TestMicroParseNative:
    """Micro-benchmarks targeting specific parse hot-paths (native)."""

    @native_only
    def test_parse_string_heavy(self, benchmark: Any) -> None:
        benchmark(_native_parse, STRING_HEAVY_JSON)

    @native_only
    def test_parse_number_heavy(self, benchmark: Any) -> None:
        benchmark(_native_parse, NUMBER_HEAVY_JSON)

    @native_only
    def test_parse_nested_object(self, benchmark: Any) -> None:
        benchmark(_native_parse, NESTED_OBJECT_JSON)


# ==========================================================================
# MICRO — Parse, pure-Python path
# ==========================================================================

class TestMicroParsePurePython:
    """Micro-benchmarks targeting specific parse hot-paths (pure Python)."""

    def test_parse_string_heavy(self, benchmark: Any) -> None:
        benchmark(_py_parse, STRING_HEAVY_JSON)

    def test_parse_number_heavy(self, benchmark: Any) -> None:
        benchmark(_py_parse, NUMBER_HEAVY_JSON)

    def test_parse_nested_object(self, benchmark: Any) -> None:
        benchmark(_py_parse, NESTED_OBJECT_JSON)


# ==========================================================================
# MICRO — Stringify, native path
# ==========================================================================

class TestMicroStringifyNative:
    """Micro-benchmarks targeting specific stringify hot-paths (native)."""

    @native_only
    def test_stringify_string_heavy(self, benchmark: Any) -> None:
        benchmark(_native_stringify, STRING_HEAVY_OBJ)

    @native_only
    def test_stringify_number_heavy(self, benchmark: Any) -> None:
        benchmark(_native_stringify, NUMBER_HEAVY_OBJ)

    @native_only
    def test_stringify_nested_object(self, benchmark: Any) -> None:
        benchmark(_native_stringify, NESTED_OBJECT_OBJ)


# ==========================================================================
# MICRO — Stringify, pure-Python path
# ==========================================================================

class TestMicroStringifyPurePython:
    """Micro-benchmarks targeting specific stringify hot-paths (pure Python)."""

    def test_stringify_string_heavy(self, benchmark: Any) -> None:
        benchmark(_py_stringify, STRING_HEAVY_OBJ)

    def test_stringify_number_heavy(self, benchmark: Any) -> None:
        benchmark(_py_stringify, NUMBER_HEAVY_OBJ)

    def test_stringify_nested_object(self, benchmark: Any) -> None:
        benchmark(_py_stringify, NESTED_OBJECT_OBJ)
