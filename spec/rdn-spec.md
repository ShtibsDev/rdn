# RDN (Rich Data Notation) — Technical Specification

**Version:** 1.0 (as implemented in V8)
**File extension:** `.rdn`
**Encoding:** UTF-8 (one-byte) or UTF-16 (two-byte)

## 1. Overview

RDN is a **strict superset of JSON** that extends the format with native representations for common programming types: dates, BigInts, regular expressions, binary data, Maps, Sets, and special numeric values. Any valid JSON document is a valid RDN document. RDN adds no comments, no trailing commas, and no unquoted keys — it stays close to JSON's simplicity while closing its type gaps.

## 2. Grammar (ABNF-style)

```
rdn-text     = ws value ws

value        = null / boolean / number / bigint / string
             / array / tuple / object / map / set
             / datetime / time-only / duration
             / regexp / binary

ws           = *( %x09 / %x0A / %x0D / %x20 )    ; tab / LF / CR / space
```

### 2.1 JSON-Compatible Types

```
null         = "null"
boolean      = "true" / "false"

number       = [ "-" ] int [ frac ] [ exp ]
             / "NaN"
             / "Infinity"
             / "-Infinity"
int          = "0" / ( %x31-39 *DIGIT )
frac         = "." 1*DIGIT
exp          = ( "e" / "E" ) [ "+" / "-" ] 1*DIGIT

string       = %x22 *char %x22              ; "..."
char         = unescaped / escaped
unescaped    = %x20-21 / %x23-5B / %x5D-10FFFF
escaped      = "\" escape-char
escape-char  = %x22 / %x5C / %x2F / "b" / "f" / "n" / "r" / "t"
             / "u" 4HEXDIG

array        = "[" ws "]"
             / "[" ws value *( ws "," ws value ) ws "]"

object       = "{" ws "}"
             / "{" ws member *( ws "," ws member ) ws "}"
member       = string ws ":" ws value
```

### 2.2 RDN-Specific Types

```
bigint       = [ "-" ] 1*DIGIT "n"

datetime     = "@" datetime-body
datetime-body = iso-full / iso-no-ms / date-only / unix-ts
iso-full     = YYYY "-" MM "-" DD "T" HH ":" mm ":" ss "." mmm "Z"
iso-no-ms    = YYYY "-" MM "-" DD "T" HH ":" mm ":" ss "Z"
date-only    = YYYY "-" MM "-" DD
unix-ts      = 1*DIGIT                       ; seconds or milliseconds

time-only    = "@" HH ":" mm ":" ss [ "." mmm ]

duration     = "@" "P" duration-body
duration-body = *( 1*DIGIT ( "Y" / "M" / "D" ) )
                [ "T" *( 1*DIGIT ( "H" / "M" / "S" ) ) ]

regexp       = "/" regexp-body "/" regexp-flags
regexp-body  = 1*( regexp-char / "\/" )
regexp-char  = %x01-2E / %x30-10FFFF        ; any char except "/" and NUL
regexp-flags = *( "d" / "g" / "i" / "m" / "s" / "u" / "v" / "y" )

binary       = binary-b64 / binary-hex
binary-b64   = "b" %x22 *base64-char [ "=" / "==" ] %x22
binary-hex   = "x" %x22 *HEXDIG %x22
base64-char  = ALPHA / DIGIT / "+" / "/"

map          = explicit-map / implicit-map
explicit-map = "Map{" ws "}"
             / "Map{" ws map-entry *( ws "," ws map-entry ) ws "}"
implicit-map = "{" ws map-entry *( ws "," ws map-entry ) ws "}"
map-entry    = value ws "=>" ws value

set          = explicit-set / implicit-set
explicit-set = "Set{" ws "}"
             / "Set{" ws value *( ws "," ws value ) ws "}"
implicit-set = "{" ws value ws "}"           ; single-element
             / "{" ws value ws "," ws value *( ws "," ws value ) ws "}"

tuple        = "(" ws ")"
             / "(" ws value *( ws "," ws value ) ws ")"
```

## 3. Type Catalog

| RDN Literal | JavaScript Result | Example |
|---|---|---|
| `null` | `null` | `null` |
| `true` / `false` | `Boolean` | `true` |
| `42`, `3.14`, `1e10` | `Number` | `3.14` |
| `NaN` | `Number` (NaN) | `NaN` |
| `Infinity`, `-Infinity` | `Number` | `-Infinity` |
| `42n` | `BigInt` | `999999999999999999n` |
| `"hello"` | `String` | `"escaped \"quotes\""` |
| `[...]` | `Array` | `[1, "two", true]` |
| `{key: val, ...}` | `Object` (plain) | `{"a": 1, "b": 2}` |
| `@2024-01-15T10:30:00.000Z` | `Date` | `@2024-01-15` |
| `@14:30:00.500` | `Object` `{hours, minutes, seconds, milliseconds, __type__: "TimeOnly"}` | `@00:00:00` |
| `@P1Y2M3DT4H5M6S` | `Object` `{iso, __type__: "Duration"}` | `@PT1H` |
| `/^[a-z]+$/i` | `RegExp` | `/test/gi` |
| `b"SGVsbG8="` | `Uint8Array` | `b""` |
| `x"48656C6C6F"` | `Uint8Array` | `x"FF00AB"` |
| `Map{"a" => 1}` | `Map` | `{"key" => "val"}` |
| `Set{1, 2, 3}` | `Set` | `{"a", "b"}` |
| `(1, 2, 3)` | `Array` | `("x", "y")` |

## 4. Detailed Type Semantics

### 4.1 Numbers

Standard JSON numbers. Additionally:
- `NaN` — IEEE 754 Not-a-Number
- `Infinity` / `-Infinity` — IEEE 754 infinities
- Numbers are parsed Smi-first (up to 9 digits accumulated as int32) to avoid `strtod` overhead.

### 4.2 BigInt

An integer literal suffixed with `n`. Arbitrary precision. No decimal point or exponent allowed.

```
42n
-123456789012345678901234567890n
0n
```

### 4.3 DateTime

Prefixed with `@`. The parser recognizes four formats by length/content:

| Format | Length | Example | Notes |
|---|---|---|---|
| Full ISO 8601 | 24 chars | `@2024-01-15T10:30:00.123Z` | Must end with `Z` (UTC) |
| ISO without millis | 20 chars | `@2024-01-15T10:30:00Z` | Must end with `Z` (UTC) |
| Date only | 10 chars | `@2024-01-15` | Time is midnight UTC |
| Unix timestamp | Variable | `@1705312200` or `@1705312200000` | Auto-detected: <=10 digits = seconds, >10 = milliseconds |

All DateTime values parse to JavaScript `Date` objects. The stringifier always outputs the full 24-character ISO format: `@YYYY-MM-DDTHH:mm:ss.sssZ`.

### 4.4 TimeOnly

A time-of-day literal starting with `@` followed by `HH:MM:SS` with optional `.mmm` milliseconds. Distinguished from DateTime by the presence of `:` as the 3rd character (vs `-` for dates).

```
@14:30:00        → {hours: 14, minutes: 30, seconds: 0, milliseconds: 0, __type__: "TimeOnly"}
@23:59:59.999    → {hours: 23, minutes: 59, seconds: 59, milliseconds: 999, __type__: "TimeOnly"}
```

### 4.5 Duration

An ISO 8601 duration literal starting with `@P`. The raw ISO string is preserved.

```
@P1Y2M3DT4H5M6S → {iso: "P1Y2M3DT4H5M6S", __type__: "Duration"}
@PT1H            → {iso: "PT1H", __type__: "Duration"}
@P1D             → {iso: "P1D", __type__: "Duration"}
```

### 4.6 RegExp

Slash-delimited pattern with optional flags. Backslash-escaped `/` characters are supported inside the pattern.

```
/^[a-z]+$/i
/test/gi
/^https?:\/\/[\w.-]+\.[a-z]{2,}\/?$/gi
```

Valid flags: `d` `g` `i` `m` `s` `u` `v` `y`

### 4.7 Binary Data

Two encodings, both producing `Uint8Array`:

**Base64** (prefix `b"`):
```
b"SGVsbG8="       → Uint8Array [72, 101, 108, 108, 111]
b""               → Uint8Array []
```

**Hexadecimal** (prefix `x"`):
```
x"48656C6C6F"     → Uint8Array [72, 101, 108, 108, 111]
x"FF00AB"         → Uint8Array [255, 0, 171]
```

Hex digits are case-insensitive. The stringifier always uses base64 (`b"..."`).

### 4.8 Map

Ordered key-value collection. Keys can be any RDN value (not restricted to strings).

**Explicit syntax** (always valid):
```
Map{}
Map{"a" => 1, "b" => 2}
Map{1 => "one", @2024-01-01 => "new year"}
```

**Implicit syntax** (brace-disambiguated):
```
{"a" => 1, "b" => 2}
```

The `=>` separator distinguishes Map entries from Object members (`:`) and Set elements (`,`).

### 4.9 Set

Ordered collection of unique values.

**Explicit syntax** (always valid):
```
Set{}
Set{1, 2, 3}
Set{"hello", "world"}
```

**Implicit syntax** (brace-disambiguated):
```
{"a", "b", "c"}    → Set with 3 elements
{"only"}            → Set with 1 element
```

### 4.10 Tuple

Parenthesis-delimited list. Parses to a plain JavaScript `Array` (no distinct tuple type).

```
(1, 2, 3)          → [1, 2, 3]
("x", "y")         → ["x", "y"]
```

## 5. Brace Disambiguation

The `{` character can start an Object, Map, or Set. The parser disambiguates by reading the first value and inspecting the separator that follows:

| After first value | Separator | Result |
|---|---|---|
| Any | `:` | **Object** (first value must be a string key) |
| Any | `=>` | **Map** |
| Any | `,` | **Set** |
| Any | `}` | **Set** (single-element) |
| (nothing) | `}` immediately | **Object** (empty `{}`) |

This means:
- `{}` → empty Object
- `{"a": 1}` → Object
- `{"a" => 1}` → Map
- `{"a", "b"}` → Set
- `{"a"}` → Set (single-element)

## 6. API

### 6.1 `RDN.parse(text [, reviver])`

- **text** (`string`): RDN-formatted string to parse.
- **reviver** (`function`, optional): `(key, value) => newValue`. Called bottom-up (leaf values first). Returning `undefined` deletes the property/entry/element. Works on Object properties, Array elements, Map entries (key passed as the Map key), and Set elements.
- **Returns**: Parsed JavaScript value.
- **Throws**: `SyntaxError` on malformed input.
- **`.length`**: `2`

### 6.2 `RDN.stringify(value [, replacer])`

- **value** (`any`): JavaScript value to serialize.
- **replacer** (`function`, optional): `(key, value) => newValue`. Returning `undefined` omits the property/entry/element. Applied to Object properties, Array elements, Map entries, and Set elements.
- **Returns**: RDN string, or `undefined` for non-serializable root values.
- **`.length`**: `2`

**Serialization rules:**

| JavaScript Type | RDN Output |
|---|---|
| `null` | `null` |
| `Boolean` | `true` / `false` |
| `Number` (finite) | Numeric literal |
| `Number` (NaN) | `NaN` |
| `Number` (±Infinity) | `Infinity` / `-Infinity` |
| `BigInt` | `42n` |
| `String` | `"escaped"` |
| `Date` | `@YYYY-MM-DDTHH:mm:ss.sssZ` |
| `Date` (invalid) | `null` |
| `RegExp` | `/pattern/flags` |
| `Uint8Array` / `ArrayBuffer` | `b"base64..."` |
| `Array` | `[...]` |
| `Object` | `{...}` |
| `Map` (non-empty) | `Map{k => v, ...}` |
| `Map` (empty) | `Map{}` |
| `Set` (non-empty) | `Set{v, ...}` |
| `Set` (empty) | `Set{}` |
| `undefined`, `Function`, `Symbol` | Omitted from objects; `null` in arrays |

**Non-callable replacer values** (null, numbers, strings) are silently ignored.

## 7. Escape Sequences

Identical to JSON (RFC 8259 §7):

| Sequence | Character | Code Point |
|---|---|---|
| `\"` | Quotation mark | U+0022 |
| `\\` | Reverse solidus | U+005C |
| `\/` | Solidus | U+002F |
| `\b` | Backspace | U+0008 |
| `\t` | Tab | U+0009 |
| `\n` | Line feed | U+000A |
| `\f` | Form feed | U+000C |
| `\r` | Carriage return | U+000D |
| `\uXXXX` | Unicode code point | U+0000–U+FFFF |

All control characters below U+0020 **must** be escaped.

## 8. Whitespace

Whitespace is insignificant and can appear between any tokens:
- Space (U+0020)
- Tab (U+0009)
- Line feed (U+000A)
- Carriage return (U+000D)

## 9. Circular Reference Detection

The stringifier maintains a stack-based cycle detection mechanism. If a reference cycle is detected (an object appears as its own descendant), a `TypeError` is thrown, consistent with `JSON.stringify` behavior.

## 10. Differences from JSON

| Aspect | JSON | RDN |
|---|---|---|
| `NaN` / `Infinity` | Serialized as `null` | Native `NaN`, `Infinity`, `-Infinity` |
| `BigInt` | Not supported (throws) | `42n` |
| `Date` | Must use string | `@2024-01-15T10:30:00.000Z` |
| `RegExp` | Not serializable | `/pattern/flags` |
| Binary data | Base64 string or number array | `b"..."` / `x"..."` |
| `Map` | Not directly supported | `Map{k => v}` |
| `Set` | Not directly supported | `Set{v, v}` |
| Tuple | Not supported | `(v, v)` → Array |
| TimeOnly | Not supported | `@HH:MM:SS.mmm` |
| Duration | Not supported | `@P...` (ISO 8601) |
| Comments | Not supported | Not supported |
| Trailing commas | Not allowed | Not allowed |
| Unquoted keys | Not allowed | Not allowed |
| `stringify` space param | 3rd argument | Not supported |

## 11. Example Document

```
{
  "meta": {
    "apiVersion": "2.1.0",
    "requestId": "c7d83aef-cf17-42e1-baef-00004539f5f8",
    "timestamp": @2025-08-15T14:32:07.123Z,
    "rateLimit": {
      "remaining": 4982,
      "resetAt": @2025-08-15T15:00:00.000Z
    }
  },
  "data": {
    "users": [
      {
        "id": "usr_00001",
        "externalId": 900000001338n,
        "email": "jack.thompson12@gmail.com",
        "createdAt": @2024-11-05T10:34:33.000Z,
        "lastLogin": @2025-09-08T01:25:47.000Z,
        "preferences": {
          "theme": "light",
          "notifications": {"email": true, "push": false}
        },
        "avatar": b"t41AP44NzIXj1CFhg2UR7RE7SjxEBwlkoOVKGVLdHeTgEJAKaQ==",
        "roles": {"admin", "editor"},
        "sessionLog": Map{
          @2025-09-07 => @PT2H30M,
          @2025-09-08 => @PT1H15M
        },
        "namePattern": /^[A-Za-z\s'-]+$/
      }
    ]
  }
}
```

## 12. Implementation Notes

- The parser is a recursive-descent parser templated on `Char` (uint8_t for one-byte, uint16_t for two-byte strings).
- Token dispatch uses a 256-entry constexpr lookup table for O(1) branching.
- Strings use deferred materialization — the parser scans and records position/length without allocating, only materializing a V8 String when needed.
- Object construction uses a 4-entry LRU map cache. When arrays contain objects of the same shape, subsequent objects skip all map transition lookups and write properties directly at known field offsets.
- The stringifier uses SWAR (SIMD Within A Register) for whole-string escape detection, scanning 4 bytes at a time.
- Date formatting uses a pre-computed digit-pair table for direct 2-char writes (~10ns vs ~100-200ns for snprintf).
- TimeOnly and Duration objects use cached maps with pre-computed `FieldIndex` values — the first parse builds the map, subsequent parses write properties directly.
- GC safety is maintained through a relocatable pointer callback and GC-traced `FixedArray` caches rather than raw `Handle<Map>` members.
