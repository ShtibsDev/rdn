"""Benchmark: rdn (pure-Python & native) vs json stdlib.

For a fair comparison, parsing and serialization are measured on
*pure JSON* payloads (which are valid RDN too) so both libraries
process identical input.  RDN-only fixtures are benchmarked separately
to show extended-type overhead.
"""

import json
import sys
import timeit
from datetime import datetime, timezone

# ---------------------------------------------------------------------------
# Ensure the local source tree is importable
# ---------------------------------------------------------------------------
sys.path.insert(0, "src")

import rdn
from rdn._parser import parse as _py_parse
from rdn._serializer import stringify as _py_stringify

try:
    from rdn._native import parse as _native_parse, stringify as _native_stringify
    HAS_NATIVE = True
except ImportError:
    HAS_NATIVE = False

# ---------------------------------------------------------------------------
# Fixtures — pure JSON (parsable by both json & rdn)
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
# RDN-only fixtures (extended types — json cannot parse these)
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
        "sessionLog": Map{@2025-09-07 => @PT2H30M, @2025-09-08 => @PT1H15M},
        "namePattern": /^[A-Za-z\\s'-]+$/
      }
    ]
  }
}"""

# ---------------------------------------------------------------------------
# Objects for stringify benchmarks
# ---------------------------------------------------------------------------
SMALL_OBJ = json.loads(SMALL_JSON)
MEDIUM_OBJ = json.loads(MEDIUM_JSON)
LARGE_OBJ = json.loads(LARGE_JSON)

# RDN objects with extended types
MEDIUM_RDN_OBJ = rdn.loads(MEDIUM_RDN)
LARGE_RDN_OBJ = rdn.loads(LARGE_RDN)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
ITERATIONS = {"small": 20_000, "medium": 5_000, "large": 1_000}

def bench(label: str, fn, iterations: int) -> float:
    """Return ops/sec for *fn* over *iterations* calls."""
    total = timeit.timeit(fn, number=iterations)
    return iterations / total

def fmt(ops: float) -> str:
    if ops >= 1_000_000:
        return f"{ops/1_000_000:>7.2f}M"
    if ops >= 1_000:
        return f"{ops/1_000:>7.1f}K"
    return f"{ops:>7.0f} "

def pct(test: float, base: float) -> str:
    ratio = test / base
    diff = (ratio - 1) * 100
    if diff >= 0:
        return f"\033[32m+{diff:.0f}%\033[0m"
    return f"\033[31m{diff:.0f}%\033[0m"

# ---------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------
def main() -> None:
    print(f"Python {sys.version}")
    print(f"Native extension: {'YES' if HAS_NATIVE else 'NO'}")
    print(f"Payload sizes — small: {len(SMALL_JSON)}B  medium: {len(MEDIUM_JSON)}B  large: {len(LARGE_JSON)}B")
    print(f"RDN-only      — medium: {len(MEDIUM_RDN)}B  large: {len(LARGE_RDN)}B")
    print()

    # ── PARSE — pure JSON payloads ────────────────────────────────────────
    print("=" * 78)
    print("  PARSE  (JSON payloads — json vs rdn-python vs rdn-native)")
    print("=" * 78)
    header = f"{'Payload':<12} {'json':>10} {'rdn-py':>10} {'vs json':>10}"
    if HAS_NATIVE:
        header += f" {'rdn-native':>12} {'vs json':>10} {'vs rdn-py':>10}"
    print(header)
    print("-" * len(header))

    for name, payload, iters in [
        ("small", SMALL_JSON, ITERATIONS["small"]),
        ("medium", MEDIUM_JSON, ITERATIONS["medium"]),
        ("large", LARGE_JSON, ITERATIONS["large"]),
    ]:
        json_ops = bench("json", lambda p=payload: json.loads(p), iters)
        rdn_py_ops = bench("rdn-py", lambda p=payload: _py_parse(p), iters)
        line = f"{name:<12} {fmt(json_ops)} ops/s {fmt(rdn_py_ops)} ops/s {pct(rdn_py_ops, json_ops):>16}"
        if HAS_NATIVE:
            native_ops = bench("rdn-native", lambda p=payload: _native_parse(p), iters)
            line += f"  {fmt(native_ops)} ops/s {pct(native_ops, json_ops):>16} {pct(native_ops, rdn_py_ops):>16}"
        print(line)

    # ── PARSE — RDN-only payloads ─────────────────────────────────────────
    print()
    print("=" * 78)
    print("  PARSE  (RDN-only payloads — extended types)")
    print("=" * 78)
    header2 = f"{'Payload':<12} {'rdn-py':>10} "
    if HAS_NATIVE:
        header2 += f"{'rdn-native':>12} {'speedup':>10}"
    print(header2)
    print("-" * len(header2))

    for name, payload, iters in [
        ("medium-rdn", MEDIUM_RDN, ITERATIONS["medium"]),
        ("large-rdn", LARGE_RDN, ITERATIONS["large"]),
    ]:
        rdn_py_ops = bench("rdn-py", lambda p=payload: _py_parse(p), iters)
        line = f"{name:<12} {fmt(rdn_py_ops)} ops/s "
        if HAS_NATIVE:
            native_ops = bench("rdn-native", lambda p=payload: _native_parse(p), iters)
            line += f" {fmt(native_ops)} ops/s {pct(native_ops, rdn_py_ops):>16}"
        print(line)

    # ── STRINGIFY — pure JSON objects ─────────────────────────────────────
    print()
    print("=" * 78)
    print("  STRINGIFY  (JSON-compatible objects — json vs rdn-python vs rdn-native)")
    print("=" * 78)
    header3 = f"{'Payload':<12} {'json':>10} {'rdn-py':>10} {'vs json':>10}"
    if HAS_NATIVE:
        header3 += f" {'rdn-native':>12} {'vs json':>10} {'vs rdn-py':>10}"
    print(header3)
    print("-" * len(header3))

    for name, obj, iters in [
        ("small", SMALL_OBJ, ITERATIONS["small"]),
        ("medium", MEDIUM_OBJ, ITERATIONS["medium"]),
        ("large", LARGE_OBJ, ITERATIONS["large"]),
    ]:
        json_ops = bench("json", lambda o=obj: json.dumps(o), iters)
        rdn_py_ops = bench("rdn-py", lambda o=obj: _py_stringify(o), iters)
        line = f"{name:<12} {fmt(json_ops)} ops/s {fmt(rdn_py_ops)} ops/s {pct(rdn_py_ops, json_ops):>16}"
        if HAS_NATIVE:
            native_ops = bench("rdn-native", lambda o=obj: _native_stringify(o), iters)
            line += f"  {fmt(native_ops)} ops/s {pct(native_ops, json_ops):>16} {pct(native_ops, rdn_py_ops):>16}"
        print(line)

    # ── STRINGIFY — RDN extended-type objects ──────────────────────────────
    print()
    print("=" * 78)
    print("  STRINGIFY  (RDN extended-type objects)")
    print("=" * 78)
    header4 = f"{'Payload':<12} {'rdn-py':>10} "
    if HAS_NATIVE:
        header4 += f"{'rdn-native':>12} {'speedup':>10}"
    print(header4)
    print("-" * len(header4))

    for name, obj, iters in [
        ("medium-rdn", MEDIUM_RDN_OBJ, ITERATIONS["medium"]),
        ("large-rdn", LARGE_RDN_OBJ, ITERATIONS["large"]),
    ]:
        try:
            _py_stringify(obj)  # warm-up / verify it works
            rdn_py_ops = bench("rdn-py", lambda o=obj: _py_stringify(o), iters)
            line = f"{name:<12} {fmt(rdn_py_ops)} ops/s "
        except TypeError:
            line = f"{name:<12}     (skip — Map with non-string keys) "
            print(line)
            continue
        if HAS_NATIVE:
            native_ops = bench("rdn-native", lambda o=obj: _native_stringify(o), iters)
            line += f" {fmt(native_ops)} ops/s {pct(native_ops, rdn_py_ops):>16}"
        print(line)

    print()


if __name__ == "__main__":
    main()
