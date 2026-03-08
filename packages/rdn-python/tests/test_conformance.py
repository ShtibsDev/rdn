"""Conformance test runner for the shared language-agnostic RDN test suite.

Runs every file in ``test-suite/valid/``, ``test-suite/invalid/``, and
``test-suite/roundtrip/`` as a parametrized pytest case.
"""

from __future__ import annotations

import base64
import json
import math
import re
from datetime import datetime, time, timedelta, timezone
from pathlib import Path
from typing import Any

import pytest

import rdn

# ---------------------------------------------------------------------------
# Locate the shared test suite (lives at the monorepo root)
# ---------------------------------------------------------------------------

SUITE_DIR = Path(__file__).resolve().parent.parent.parent.parent / "test-suite"

VALID_DIR = SUITE_DIR / "valid"
INVALID_DIR = SUITE_DIR / "invalid"
ROUNDTRIP_DIR = SUITE_DIR / "roundtrip"

MAX_SAFE_INTEGER = 2**53 - 1

# ---------------------------------------------------------------------------
# Collect test files
# ---------------------------------------------------------------------------


def _collect_rdn_files(directory: Path) -> list[Path]:
    """Return a sorted list of ``.rdn`` files inside *directory*."""
    if not directory.is_dir():
        return []
    return sorted(directory.glob("*.rdn"))


def _stem_ids(paths: list[Path]) -> list[str]:
    """Return the stem (filename without extension) for each path, used as pytest IDs."""
    return [p.stem for p in paths]


VALID_FILES = _collect_rdn_files(VALID_DIR)
INVALID_FILES = _collect_rdn_files(INVALID_DIR)
ROUNDTRIP_FILES = _collect_rdn_files(ROUNDTRIP_DIR)


# ---------------------------------------------------------------------------
# RegExp flag reconstruction
# ---------------------------------------------------------------------------

# Mapping from Python re flags back to RDN/JS single-letter flags
_PY_FLAG_TO_RDN: list[tuple[re.RegexFlag, str]] = [
    (re.IGNORECASE, "g"),  # placeholder — see below
    (re.IGNORECASE, "i"),
    (re.MULTILINE, "m"),
    (re.DOTALL, "s"),
]


def _reconstruct_flags(py_flags: re.RegexFlag) -> str:
    """Reconstruct the RDN/JS flag string from a Python ``re.RegexFlag``.

    Only ``i``, ``m``, and ``s`` can be represented in Python; other flags
    (``g``, ``d``, ``v``, ``y``) are dropped during parsing and cannot be
    recovered.  The returned flags are sorted alphabetically to match the
    expected JSON convention.
    """
    chars: list[str] = []
    if py_flags & re.IGNORECASE:
        chars.append("i")
    if py_flags & re.MULTILINE:
        chars.append("m")
    if py_flags & re.DOTALL:
        chars.append("s")
    return "".join(sorted(chars))


# ---------------------------------------------------------------------------
# Duration ISO 8601 reconstruction from timedelta
# ---------------------------------------------------------------------------


def _timedelta_to_iso(td: timedelta) -> str:
    """Convert a ``timedelta`` back to an ISO 8601 duration string like ``PT2H30M``."""
    total_seconds = int(td.total_seconds())
    if total_seconds < 0:
        total_seconds = -total_seconds
    days = total_seconds // 86400
    remainder = total_seconds % 86400
    hours = remainder // 3600
    remainder %= 3600
    minutes = remainder // 60
    seconds = remainder % 60

    parts: list[str] = ["P"]
    if days:
        parts.append(f"{days}D")
    if hours or minutes or seconds:
        parts.append("T")
        if hours:
            parts.append(f"{hours}H")
        if minutes:
            parts.append(f"{minutes}M")
        if seconds:
            parts.append(f"{seconds}S")
    if parts == ["P"]:
        parts.append("T0S")
    return "".join(parts)


# ---------------------------------------------------------------------------
# Normalization: Python values -> $type-tagged dicts
# ---------------------------------------------------------------------------


def normalize_for_comparison(value: Any, *, expected: Any = None) -> Any:
    """Convert a parsed Python value to the ``$type``-tagged dict convention
    used in the expected JSON files so that the two can be compared directly.

    Parameters
    ----------
    value:
        The Python value produced by ``rdn.loads()``.
    expected:
        The corresponding expected-JSON structure.  Used to disambiguate
        cases where the Python type is ambiguous (e.g. ``int`` could be a
        regular integer *or* a BigInt).
    """
    if value is None:
        return None

    if isinstance(value, bool):
        return value

    if isinstance(value, float):
        if math.isnan(value):
            return {"$type": "Number", "value": "NaN"}
        if value == float("inf"):
            return {"$type": "Number", "value": "Infinity"}
        if value == float("-inf"):
            return {"$type": "Number", "value": "-Infinity"}
        return value

    if isinstance(value, int):
        # BigInts in RDN parse to plain ``int``.  The expected JSON tags them
        # as ``{"$type": "BigInt", ...}``.  We look at *expected* to decide.
        if isinstance(expected, dict) and expected.get("$type") == "BigInt":
            return {"$type": "BigInt", "value": str(value)}
        return value

    if isinstance(value, str):
        # Duration year/month fallback strings (e.g. "P1Y2M3D") are plain
        # strings in Python.  The expected JSON tags them as Duration.
        if isinstance(expected, dict) and expected.get("$type") == "Duration":
            return {"$type": "Duration", "value": value}
        return value

    if isinstance(value, datetime):
        ms = value.microsecond // 1000
        return {"$type": "Date", "value": value.strftime("%Y-%m-%dT%H:%M:%S.") + f"{ms:03d}Z"}

    if isinstance(value, time):
        return {"$type": "TimeOnly", "value": {"hours": value.hour, "minutes": value.minute, "seconds": value.second, "milliseconds": value.microsecond // 1000}}

    if isinstance(value, timedelta):
        return {"$type": "Duration", "value": _timedelta_to_iso(value)}

    if isinstance(value, re.Pattern):
        return {"$type": "RegExp", "value": {"source": value.pattern, "flags": _reconstruct_flags(value.flags)}}

    if isinstance(value, (bytes, bytearray)):
        return {"$type": "Binary", "value": base64.b64encode(value).decode("ascii")}

    if isinstance(value, dict):
        # If expected is a $type: "Map" tagged structure, normalize as Map
        if isinstance(expected, dict) and expected.get("$type") == "Map":
            expected_entries = expected.get("value", [])
            normalized_entries: list[list[Any]] = []
            for i, (k, v) in enumerate(value.items()):
                exp_pair = expected_entries[i] if i < len(expected_entries) else [None, None]
                nk = normalize_for_comparison(k, expected=exp_pair[0] if isinstance(exp_pair, list) and len(exp_pair) > 0 else None)
                nv = normalize_for_comparison(v, expected=exp_pair[1] if isinstance(exp_pair, list) and len(exp_pair) > 1 else None)
                normalized_entries.append([nk, nv])
            return {"$type": "Map", "value": normalized_entries}

        # Regular object — recursively normalize values
        if isinstance(expected, dict):
            return {k: normalize_for_comparison(v, expected=expected.get(k)) for k, v in value.items()}
        return {k: normalize_for_comparison(v) for k, v in value.items()}

    if isinstance(value, (set, frozenset)):
        normalized_items = [normalize_for_comparison(v) for v in value]
        return {"$type": "Set", "value": sorted(normalized_items, key=_sort_key)}

    if isinstance(value, tuple):
        if isinstance(expected, list):
            return [normalize_for_comparison(v, expected=expected[i] if i < len(expected) else None) for i, v in enumerate(value)]
        return [normalize_for_comparison(v) for v in value]

    if isinstance(value, list):
        if isinstance(expected, list):
            return [normalize_for_comparison(v, expected=expected[i] if i < len(expected) else None) for i, v in enumerate(value)]
        return [normalize_for_comparison(v) for v in value]

    raise TypeError(f"Cannot normalize {type(value).__name__}")


def _sort_key(val: Any) -> tuple[str, str]:
    """Produce a stable sort key for normalized set elements.

    Returns ``(type_name, str_representation)`` so that different types don't
    cause comparison errors.
    """
    return (type(val).__name__, str(val))


# ---------------------------------------------------------------------------
# Normalization for expected JSON (sort set values for comparison)
# ---------------------------------------------------------------------------


def _strip_non_python_flags(flags: str) -> str:
    """Remove regexp flags that have no Python ``re`` equivalent.

    Python only supports ``i`` (IGNORECASE), ``m`` (MULTILINE), and ``s``
    (DOTALL).  Flags ``g``, ``d``, ``v``, and ``y`` exist in JS/RDN but
    are dropped during parsing.
    """
    return "".join(sorted(ch for ch in flags if ch in "ims"))


def normalize_expected(expected: Any) -> Any:
    """Normalize the expected JSON structure so that Set values are sorted
    and RegExp flags are reduced to the Python-representable subset,
    enabling stable comparison with the normalized parsed result."""
    if isinstance(expected, dict):
        if expected.get("$type") == "Set":
            items = expected.get("value", [])
            normalized_items = [normalize_expected(v) for v in items]
            return {"$type": "Set", "value": sorted(normalized_items, key=_sort_key)}
        if expected.get("$type") == "Map":
            entries = expected.get("value", [])
            return {"$type": "Map", "value": [[normalize_expected(k), normalize_expected(v)] for k, v in entries]}
        if expected.get("$type") == "RegExp":
            value = expected["value"]
            return {"$type": "RegExp", "value": {"source": value["source"], "flags": _strip_non_python_flags(value.get("flags", ""))}}
        return {k: normalize_expected(v) for k, v in expected.items()}
    if isinstance(expected, list):
        return [normalize_expected(v) for v in expected]
    return expected


# ---------------------------------------------------------------------------
# Valid tests
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("rdn_path", VALID_FILES, ids=_stem_ids(VALID_FILES))
def test_valid(rdn_path: Path) -> None:
    """Parse a valid ``.rdn`` file and compare with its ``.expected.json``."""
    expected_path = rdn_path.with_suffix(".expected.json")
    assert expected_path.exists(), f"Missing expected file: {expected_path}"

    rdn_text = rdn_path.read_text(encoding="utf-8")
    expected_json = json.loads(expected_path.read_text(encoding="utf-8"))

    parsed = rdn.loads(rdn_text)
    normalized = normalize_for_comparison(parsed, expected=expected_json)
    expected_normalized = normalize_expected(expected_json)

    assert normalized == expected_normalized, (
        f"Mismatch for {rdn_path.name}:\n"
        f"  parsed (normalized) = {normalized!r}\n"
        f"  expected            = {expected_normalized!r}"
    )


# ---------------------------------------------------------------------------
# Invalid tests
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("rdn_path", INVALID_FILES, ids=_stem_ids(INVALID_FILES))
def test_invalid(rdn_path: Path) -> None:
    """Assert that parsing an invalid ``.rdn`` file raises ``RDNDecodeError``."""
    rdn_text = rdn_path.read_text(encoding="utf-8")

    with pytest.raises(rdn.RDNDecodeError):
        rdn.loads(rdn_text)


# ---------------------------------------------------------------------------
# Roundtrip tests
# ---------------------------------------------------------------------------


def normalize_for_roundtrip(value: Any) -> Any:
    """Normalize a value for roundtrip comparison.

    Like ``normalize_for_comparison`` but without an ``expected`` hint, so
    BigInts are detected by magnitude and Maps are indistinguishable from
    Objects.
    """
    if value is None:
        return None
    if isinstance(value, bool):
        return value
    if isinstance(value, float):
        if math.isnan(value):
            return {"$type": "Number", "value": "NaN"}
        if value == float("inf"):
            return {"$type": "Number", "value": "Infinity"}
        if value == float("-inf"):
            return {"$type": "Number", "value": "-Infinity"}
        return value
    if isinstance(value, int):
        if abs(value) > MAX_SAFE_INTEGER:
            return {"$type": "BigInt", "value": str(value)}
        return value
    if isinstance(value, str):
        return value
    if isinstance(value, datetime):
        ms = value.microsecond // 1000
        return {"$type": "Date", "value": value.strftime("%Y-%m-%dT%H:%M:%S.") + f"{ms:03d}Z"}
    if isinstance(value, time):
        return {"$type": "TimeOnly", "value": {"hours": value.hour, "minutes": value.minute, "seconds": value.second, "milliseconds": value.microsecond // 1000}}
    if isinstance(value, timedelta):
        return {"$type": "Duration", "value": _timedelta_to_iso(value)}
    if isinstance(value, re.Pattern):
        return {"$type": "RegExp", "value": {"source": value.pattern, "flags": _reconstruct_flags(value.flags)}}
    if isinstance(value, (bytes, bytearray)):
        return {"$type": "Binary", "value": base64.b64encode(value).decode("ascii")}
    if isinstance(value, dict):
        return {k: normalize_for_roundtrip(v) for k, v in value.items()}
    if isinstance(value, (set, frozenset)):
        return {"$type": "Set", "value": sorted([normalize_for_roundtrip(v) for v in value], key=_sort_key)}
    if isinstance(value, tuple):
        return [normalize_for_roundtrip(v) for v in value]
    if isinstance(value, list):
        return [normalize_for_roundtrip(v) for v in value]
    raise TypeError(f"Cannot normalize {type(value).__name__}")


@pytest.mark.parametrize("rdn_path", ROUNDTRIP_FILES, ids=_stem_ids(ROUNDTRIP_FILES))
def test_roundtrip(rdn_path: Path) -> None:
    """Parse -> stringify -> parse and verify identity."""
    rdn_text = rdn_path.read_text(encoding="utf-8")

    first_parse = rdn.loads(rdn_text)
    serialized = rdn.dumps(first_parse)
    second_parse = rdn.loads(serialized)

    norm1 = normalize_for_roundtrip(first_parse)
    norm2 = normalize_for_roundtrip(second_parse)

    assert norm1 == norm2, (
        f"Roundtrip mismatch for {rdn_path.name}:\n"
        f"  first  = {norm1!r}\n"
        f"  second = {norm2!r}\n"
        f"  serialized = {serialized!r}"
    )
