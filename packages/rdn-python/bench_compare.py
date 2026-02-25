"""Comprehensive benchmark: rdn-python vs json (stdlib) vs orjson.

Three categories:
  1. Pure JSON        — identical payloads, all three libraries
  2. Realistic API    — typical API responses with a few RDN types (dates, bigints)
  3. Heavy RDN        — dense use of extended types (sets, maps, tuples, regex, binary, durations)

For categories 2 & 3, json/orjson cannot parse/produce RDN syntax, so we
measure the *equivalent* work: json/orjson serialize the stdlib objects that
RDN would produce (datetime→isoformat string, bytes→base64 string, etc.)
so that the output size and information content is comparable.
"""

import base64
import json
import re
import sys
import timeit
from datetime import datetime, time, timedelta, timezone

sys.path.insert(0, "src")

import orjson

import rdn
from rdn._parser import parse as _py_parse
from rdn._serializer import stringify as _py_stringify

try:
    from rdn._native import parse as _native_parse, stringify as _native_stringify
    HAS_NATIVE = True
except ImportError:
    HAS_NATIVE = False

# ── helpers ───────────────────────────────────────────────────────────────

def bench(fn, iterations: int) -> float:
    """Return ops/sec."""
    total = timeit.timeit(fn, number=iterations)
    return iterations / total

def fmt(ops: float) -> str:
    if ops >= 1_000_000:
        return f"{ops/1_000_000:.2f}M"
    if ops >= 1_000:
        return f"{ops/1_000:.1f}K"
    return f"{ops:.0f}"

def ratio_str(test: float, base: float) -> str:
    r = test / base
    if r >= 1:
        return f"\033[32m{r:.2f}x faster\033[0m"
    return f"\033[31m{1/r:.2f}x slower\033[0m"

def pct_str(test: float, base: float) -> str:
    diff = ((test / base) - 1) * 100
    if diff >= 0:
        return f"\033[32m+{diff:.0f}%\033[0m"
    return f"\033[31m{diff:.0f}%\033[0m"

SEP = "─" * 90

# ═══════════════════════════════════════════════════════════════════════════
# CATEGORY 1: Pure JSON
# ═══════════════════════════════════════════════════════════════════════════

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

SMALL_OBJ = json.loads(SMALL_JSON)
MEDIUM_OBJ = json.loads(MEDIUM_JSON)
LARGE_OBJ = json.loads(LARGE_JSON)

# ═══════════════════════════════════════════════════════════════════════════
# CATEGORY 2: Realistic API  (typical REST response with some RDN types)
# ═══════════════════════════════════════════════════════════════════════════

# RDN string — what a client would actually receive over the wire
REALISTIC_RDN = """{
  "apiVersion": "2.1.0",
  "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": @2025-08-15T14:32:07.123Z,
  "data": {
    "users": [
      {
        "id": 1,
        "externalId": 9007199254740993n,
        "name": "Alice Johnson",
        "email": "alice@example.com",
        "createdAt": @2024-01-15T10:30:00.000Z,
        "lastLogin": @2025-08-14T22:15:33.000Z,
        "score": 98.5,
        "active": true,
        "roles": ["admin", "editor"],
        "preferences": {"theme": "dark", "notifications": true, "language": "en"}
      },
      {
        "id": 2,
        "externalId": 9007199254740994n,
        "name": "Bob Smith",
        "email": "bob@example.com",
        "createdAt": @2024-02-20T08:00:00.000Z,
        "lastLogin": @2025-08-15T09:42:11.000Z,
        "score": 87.3,
        "active": true,
        "roles": ["viewer"],
        "preferences": {"theme": "light", "notifications": false, "language": "es"}
      },
      {
        "id": 3,
        "externalId": 9007199254740995n,
        "name": "Charlie Davis",
        "email": "charlie@example.com",
        "createdAt": @2024-03-10T14:15:00.000Z,
        "lastLogin": @2025-08-13T17:08:55.000Z,
        "score": 92.1,
        "active": false,
        "roles": ["editor"],
        "preferences": {"theme": "dark", "notifications": true, "language": "fr"}
      }
    ],
    "pagination": {"total": 3, "page": 1, "perPage": 20, "hasMore": false}
  }
}"""

# Equivalent JSON string — same data but dates as ISO strings, bigints as strings
REALISTIC_JSON = json.dumps({
    "apiVersion": "2.1.0",
    "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "timestamp": "2025-08-15T14:32:07.123Z",
    "data": {
        "users": [
            {
                "id": 1,
                "externalId": "9007199254740993",
                "name": "Alice Johnson",
                "email": "alice@example.com",
                "createdAt": "2024-01-15T10:30:00.000Z",
                "lastLogin": "2025-08-14T22:15:33.000Z",
                "score": 98.5,
                "active": True,
                "roles": ["admin", "editor"],
                "preferences": {"theme": "dark", "notifications": True, "language": "en"},
            },
            {
                "id": 2,
                "externalId": "9007199254740994",
                "name": "Bob Smith",
                "email": "bob@example.com",
                "createdAt": "2024-02-20T08:00:00.000Z",
                "lastLogin": "2025-08-15T09:42:11.000Z",
                "score": 87.3,
                "active": True,
                "roles": ["viewer"],
                "preferences": {"theme": "light", "notifications": False, "language": "es"},
            },
            {
                "id": 3,
                "externalId": "9007199254740995",
                "name": "Charlie Davis",
                "email": "charlie@example.com",
                "createdAt": "2024-03-10T14:15:00.000Z",
                "lastLogin": "2025-08-13T17:08:55.000Z",
                "score": 92.1,
                "active": False,
                "roles": ["editor"],
                "preferences": {"theme": "dark", "notifications": True, "language": "fr"},
            },
        ],
        "pagination": {"total": 3, "page": 1, "perPage": 20, "hasMore": False},
    },
})

# Pre-parse the realistic objects for stringify benchmarks
REALISTIC_RDN_OBJ = rdn.loads(REALISTIC_RDN)
REALISTIC_JSON_OBJ = json.loads(REALISTIC_JSON)

# ═══════════════════════════════════════════════════════════════════════════
# CATEGORY 3: Heavy RDN (dense extended types)
# ═══════════════════════════════════════════════════════════════════════════

HEAVY_RDN = """{
  "meta": {
    "generatedAt": @2025-08-15T14:32:07.123Z,
    "processingTime": @PT0.847S,
    "requestPattern": /^\\/api\\/v[0-9]+\\/users/i
  },
  "schedule": {
    "morningStart": @08:30:00,
    "morningEnd": @12:00:00,
    "afternoonStart": @13:00:00,
    "afternoonEnd": @17:30:00,
    "breakDuration": @PT30M,
    "shiftDuration": @PT8H
  },
  "sessions": Map{
    @2025-08-13 => {"duration": @PT2H30M, "score": 94.2, "tags": {"focus", "productive"}},
    @2025-08-14 => {"duration": @PT1H45M, "score": 88.7, "tags": {"review", "meetings"}},
    @2025-08-15 => {"duration": @PT3H10M, "score": 97.1, "tags": {"coding", "productive", "flow"}}
  },
  "users": [
    {
      "id": 9007199254740993n,
      "created": @2024-01-15T10:30:00.000Z,
      "lastActive": @2025-08-15T14:30:00.000Z,
      "avatar": b"SGVsbG8gV29ybGQhIFRoaXMgaXMgdGVzdCBhdmF0YXIgZGF0YSBmb3IgYmVuY2htYXJraW5nIHB1cnBvc2VzLg==",
      "namePattern": /^[A-Za-z\\s'-]+$/,
      "permissions": {"admin", "write", "read", "execute"},
      "loginHistory": (@2025-08-13T09:00:00.000Z, @2025-08-14T08:30:00.000Z, @2025-08-15T07:45:00.000Z),
      "quotas": Map{"cpu" => @PT4H, "memory" => 8589934592n, "storage" => 107374182400n}
    },
    {
      "id": 9007199254740994n,
      "created": @2024-02-20T08:00:00.000Z,
      "lastActive": @2025-08-15T12:15:00.000Z,
      "avatar": b"QW5vdGhlciBhdmF0YXIgcGF5bG9hZCBmb3IgdGhlIHNlY29uZCB1c2VyIGluIG91ciB0ZXN0IGRhdGEu",
      "namePattern": /^[A-Za-z\\s'-]+$/,
      "permissions": {"write", "read"},
      "loginHistory": (@2025-08-10T10:00:00.000Z, @2025-08-12T09:15:00.000Z, @2025-08-15T08:30:00.000Z),
      "quotas": Map{"cpu" => @PT2H, "memory" => 4294967296n, "storage" => 53687091200n}
    }
  ],
  "config": {
    "allowedOrigins": {"https://app.example.com", "https://admin.example.com", "https://api.example.com"},
    "rateLimits": Map{"free" => 100, "pro" => 10000, "enterprise" => Infinity},
    "featureFlags": Map{"darkMode" => true, "betaApi" => false, "newDashboard" => true},
    "retryDelays": (@PT1S, @PT5S, @PT30S, @PT5M)
  }
}"""

# Pre-parse the heavy RDN object
HEAVY_RDN_OBJ = rdn.loads(HEAVY_RDN)

# Equivalent JSON for the heavy payload — everything flattened to JSON types
def _heavy_json_equivalent():
    """Build a JSON-compatible dict that carries the same information."""
    return {
        "meta": {
            "generatedAt": "2025-08-15T14:32:07.123Z",
            "processingTime": "PT0.847S",
            "requestPattern": "^\\/api\\/v[0-9]+\\/users",
        },
        "schedule": {
            "morningStart": "08:30:00",
            "morningEnd": "12:00:00",
            "afternoonStart": "13:00:00",
            "afternoonEnd": "17:30:00",
            "breakDuration": "PT30M",
            "shiftDuration": "PT8H",
        },
        "sessions": {
            "2025-08-13": {"duration": "PT2H30M", "score": 94.2, "tags": ["focus", "productive"]},
            "2025-08-14": {"duration": "PT1H45M", "score": 88.7, "tags": ["review", "meetings"]},
            "2025-08-15": {"duration": "PT3H10M", "score": 97.1, "tags": ["coding", "productive", "flow"]},
        },
        "users": [
            {
                "id": "9007199254740993",
                "created": "2024-01-15T10:30:00.000Z",
                "lastActive": "2025-08-15T14:30:00.000Z",
                "avatar": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
                "namePattern": "^[A-Za-z\\s'-]+$",
                "permissions": ["admin", "write", "read", "execute"],
                "loginHistory": ["2025-08-13T09:00:00.000Z", "2025-08-14T08:30:00.000Z", "2025-08-15T07:45:00.000Z"],
                "quotas": {"cpu": "PT4H", "memory": "8589934592", "storage": "107374182400"},
            },
            {
                "id": "9007199254740994",
                "created": "2024-02-20T08:00:00.000Z",
                "lastActive": "2025-08-15T12:15:00.000Z",
                "avatar": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/58BHgAIBwJ6h2fMAAAABJRU5ErkJggg==",
                "namePattern": "^[A-Za-z\\s'-]+$",
                "permissions": ["write", "read"],
                "loginHistory": ["2025-08-10T10:00:00.000Z", "2025-08-12T09:15:00.000Z", "2025-08-15T08:30:00.000Z"],
                "quotas": {"cpu": "PT2H", "memory": "4294967296", "storage": "53687091200"},
            },
        ],
        "config": {
            "allowedOrigins": ["https://app.example.com", "https://admin.example.com", "https://api.example.com"],
            "rateLimits": {"free": 100, "pro": 10000, "enterprise": None},
            "featureFlags": {"darkMode": True, "betaApi": False, "newDashboard": True},
            "retryDelays": ["PT1S", "PT5S", "PT30S", "PT5M"],
        },
    }

HEAVY_JSON_STR = json.dumps(_heavy_json_equivalent())
HEAVY_JSON_OBJ = json.loads(HEAVY_JSON_STR)

# ═══════════════════════════════════════════════════════════════════════════
# Iteration counts (tuned to keep total runtime reasonable)
# ═══════════════════════════════════════════════════════════════════════════
ITERS = {"small": 20_000, "medium": 5_000, "large": 1_000, "realistic": 5_000, "heavy": 2_000}


def print_header(title: str):
    print()
    print(f"\033[1;36m{'═' * 90}\033[0m")
    print(f"\033[1;36m  {title}\033[0m")
    print(f"\033[1;36m{'═' * 90}\033[0m")


def print_row(label, json_ops, orjson_ops, rdn_py_ops, native_ops=None):
    """Print a comparison row with verbose analysis."""
    line = f"  {label:<14}"
    line += f"  json: {fmt(json_ops):>8} ops/s"
    line += f"  orjson: {fmt(orjson_ops):>8} ops/s"
    line += f"  rdn-py: {fmt(rdn_py_ops):>8} ops/s"
    if native_ops is not None:
        line += f"  rdn-native: {fmt(native_ops):>8} ops/s"
    print(line)

    # Verbose comparisons
    print(f"    {'rdn-py vs json:':>24}  {ratio_str(rdn_py_ops, json_ops)}  ({pct_str(rdn_py_ops, json_ops)})")
    print(f"    {'rdn-py vs orjson:':>24}  {ratio_str(rdn_py_ops, orjson_ops)}  ({pct_str(rdn_py_ops, orjson_ops)})")
    if native_ops is not None:
        print(f"    {'rdn-native vs json:':>24}  {ratio_str(native_ops, json_ops)}  ({pct_str(native_ops, json_ops)})")
        print(f"    {'rdn-native vs orjson:':>24}  {ratio_str(native_ops, orjson_ops)}  ({pct_str(native_ops, orjson_ops)})")
        print(f"    {'rdn-native vs rdn-py:':>24}  {ratio_str(native_ops, rdn_py_ops)}  ({pct_str(native_ops, rdn_py_ops)})")


def print_rdn_only_row(label, rdn_py_ops, native_ops=None):
    """Print a row for RDN-only benchmarks (no json/orjson comparison possible)."""
    line = f"  {label:<14}"
    line += f"  rdn-py: {fmt(rdn_py_ops):>8} ops/s"
    if native_ops is not None:
        line += f"  rdn-native: {fmt(native_ops):>8} ops/s"
        line += f"  → native is {ratio_str(native_ops, rdn_py_ops)}"
    print(line)


# ═══════════════════════════════════════════════════════════════════════════
# RUN
# ═══════════════════════════════════════════════════════════════════════════
def main():
    print(f"\033[1mPython {sys.version}\033[0m")
    print(f"orjson {orjson.__version__}")
    print(f"rdn-native: {'YES' if HAS_NATIVE else 'NO'}")
    print()
    print("Payload sizes:")
    print(f"  Pure JSON — small: {len(SMALL_JSON):,}B  medium: {len(MEDIUM_JSON):,}B  large: {len(LARGE_JSON):,}B")
    print(f"  Realistic API (RDN): {len(REALISTIC_RDN):,}B  (JSON equiv): {len(REALISTIC_JSON):,}B")
    print(f"  Heavy RDN: {len(HEAVY_RDN):,}B  (JSON equiv): {len(HEAVY_JSON_STR):,}B")

    # ══════════════════════════════════════════════════════════════════════
    # 1. PURE JSON PARSE
    # ══════════════════════════════════════════════════════════════════════
    print_header("1. PARSE — Pure JSON payloads (all three libraries parse identical input)")

    for name, payload in [("small", SMALL_JSON), ("medium", MEDIUM_JSON), ("large", LARGE_JSON)]:
        iters = ITERS[name]
        j = bench(lambda p=payload: json.loads(p), iters)
        o = bench(lambda p=payload: orjson.loads(p), iters)
        r = bench(lambda p=payload: _py_parse(p), iters)
        n = bench(lambda p=payload: _native_parse(p), iters) if HAS_NATIVE else None
        print_row(name, j, o, r, n)
        print()

    # ══════════════════════════════════════════════════════════════════════
    # 2. PURE JSON STRINGIFY
    # ══════════════════════════════════════════════════════════════════════
    print_header("2. STRINGIFY — Pure JSON objects (all three libraries serialize identical objects)")

    for name, obj in [("small", SMALL_OBJ), ("medium", MEDIUM_OBJ), ("large", LARGE_OBJ)]:
        iters = ITERS[name]
        j = bench(lambda o=obj: json.dumps(o), iters)
        o_ops = bench(lambda o=obj: orjson.dumps(o), iters)
        r = bench(lambda o=obj: _py_stringify(o), iters)
        n = bench(lambda o=obj: _native_stringify(o), iters) if HAS_NATIVE else None
        print_row(name, j, o_ops, r, n)
        print()

    # ══════════════════════════════════════════════════════════════════════
    # 3. REALISTIC API PARSE
    # ══════════════════════════════════════════════════════════════════════
    print_header("3. PARSE — Realistic API response (dates + bigints in RDN vs string fields in JSON)")
    print("  \033[2mRDN parses dates/bigints natively; JSON keeps them as strings.\033[0m")
    print("  \033[2mThis measures end-to-end: RDN gives you rich types, JSON gives you strings to post-process.\033[0m")
    print()

    iters = ITERS["realistic"]
    j = bench(lambda: json.loads(REALISTIC_JSON), iters)
    o = bench(lambda: orjson.loads(REALISTIC_JSON), iters)
    r = bench(lambda: _py_parse(REALISTIC_RDN), iters)
    n = bench(lambda: _native_parse(REALISTIC_RDN), iters) if HAS_NATIVE else None
    print_row("realistic", j, o, r, n)
    print()

    # ══════════════════════════════════════════════════════════════════════
    # 4. REALISTIC API STRINGIFY
    # ══════════════════════════════════════════════════════════════════════
    print_header("4. STRINGIFY — Realistic API response")
    print("  \033[2mRDN serializes datetime/bigint objects natively.\033[0m")
    print("  \033[2mjson/orjson serialize the equivalent dict with pre-formatted strings.\033[0m")
    print()

    iters = ITERS["realistic"]
    j = bench(lambda: json.dumps(REALISTIC_JSON_OBJ), iters)
    o_ops = bench(lambda: orjson.dumps(REALISTIC_JSON_OBJ), iters)
    r = bench(lambda: _py_stringify(REALISTIC_RDN_OBJ), iters)
    n = bench(lambda: _native_stringify(REALISTIC_RDN_OBJ), iters) if HAS_NATIVE else None
    print_row("realistic", j, o_ops, r, n)
    print()

    # ══════════════════════════════════════════════════════════════════════
    # 5. HEAVY RDN PARSE
    # ══════════════════════════════════════════════════════════════════════
    print_header("5. PARSE — Heavy RDN features (sets, maps, tuples, regex, binary, durations)")
    print("  \033[2mjson/orjson parse the JSON equivalent (all extended types as strings/arrays).\033[0m")
    print("  \033[2mRDN parses into native Python types (set, dict w/ non-string keys, tuple, re.Pattern, bytes, timedelta).\033[0m")
    print()

    iters = ITERS["heavy"]
    j = bench(lambda: json.loads(HEAVY_JSON_STR), iters)
    o = bench(lambda: orjson.loads(HEAVY_JSON_STR), iters)
    r = bench(lambda: _py_parse(HEAVY_RDN), iters)
    n = bench(lambda: _native_parse(HEAVY_RDN), iters) if HAS_NATIVE else None
    print_row("heavy", j, o, r, n)
    print()

    # ══════════════════════════════════════════════════════════════════════
    # 6. HEAVY RDN STRINGIFY
    # ══════════════════════════════════════════════════════════════════════
    print_header("6. STRINGIFY — Heavy RDN features")
    print("  \033[2mjson/orjson serialize the JSON equivalent dict.\033[0m")
    print("  \033[2mRDN serializes the rich Python objects (sets, maps, tuples, regex, bytes, timedelta).\033[0m")
    print()

    iters = ITERS["heavy"]
    j = bench(lambda: json.dumps(HEAVY_JSON_OBJ), iters)
    o_ops = bench(lambda: orjson.dumps(HEAVY_JSON_OBJ), iters)
    # Pure-Python serializer doesn't support non-string Map keys (datetime keys in sessions Map)
    try:
        _py_stringify(HEAVY_RDN_OBJ)
        r = bench(lambda: _py_stringify(HEAVY_RDN_OBJ), iters)
    except TypeError:
        r = None
    if HAS_NATIVE:
        try:
            _native_stringify(HEAVY_RDN_OBJ)
            n = bench(lambda: _native_stringify(HEAVY_RDN_OBJ), iters)
        except TypeError:
            n = None
    else:
        n = None

    if r is not None:
        print_row("heavy", j, o_ops, r, n)
    else:
        # rdn serializers can't stringify Maps with non-string keys yet
        line = f"  {'heavy':<14}"
        line += f"  json: {fmt(j):>8} ops/s"
        line += f"  orjson: {fmt(o_ops):>8} ops/s"
        line += f"  rdn-py: \033[2m(skip — Map with datetime keys)\033[0m"
        if n is not None:
            line += f"  rdn-native: {fmt(n):>8} ops/s"
        else:
            line += f"  rdn-native: \033[2m(skip — Map with datetime keys)\033[0m"
        print(line)
        print(f"    \033[2mNote: RDN can parse Maps with non-string keys, but stringify doesn't support them yet.\033[0m")
        if n is not None:
            print(f"    {'rdn-native vs json:':>24}  {ratio_str(n, j)}  ({pct_str(n, j)})")
            print(f"    {'rdn-native vs orjson:':>24}  {ratio_str(n, o_ops)}  ({pct_str(n, o_ops)})")

    # ══════════════════════════════════════════════════════════════════════
    # SUMMARY
    # ══════════════════════════════════════════════════════════════════════
    print()
    print(f"\033[1;33m{'═' * 90}\033[0m")
    print(f"\033[1;33m  KEY TAKEAWAYS\033[0m")
    print(f"\033[1;33m{'═' * 90}\033[0m")
    print("""
  • json (stdlib)  — CPython's C-accelerated JSON, the universal baseline.
  • orjson          — Rust-based, fastest Python JSON library. The gold standard for speed.
  • rdn-py          — Pure Python recursive-descent parser. Handles all RDN extended types.
  • rdn-native      — Rust+PyO3 native extension. Same API, C-level speed.

  For pure JSON: rdn-py is slower than both json and orjson (expected — pure Python vs C/Rust).
                 rdn-native competes directly with json and approaches orjson territory.

  For realistic APIs: RDN gives you *typed* objects (datetime, int) out of the box.
                      With JSON you'd still need to post-process strings into types.
                      The "total cost of deserialization" favors RDN when you need rich types.

  For heavy RDN: No comparison — json/orjson simply can't represent sets, maps, tuples,
                 regex, binary, or durations natively. RDN eliminates the serialization layer.
""")


if __name__ == "__main__":
    main()
