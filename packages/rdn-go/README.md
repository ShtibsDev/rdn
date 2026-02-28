# rdn-go

Pure Go parser and serializer for [RDN (Rich Data Notation)](https://github.com/ShtibsDev/rdn) — a JSON superset with native dates, BigInts, regular expressions, binary data, Maps, Sets, tuples, and more.

## Install

```bash
go get github.com/ShtibsDev/rdn/packages/rdn-go
```

## Quick Start

```go
package main

import (
    "fmt"
    "log"

    rdn "github.com/ShtibsDev/rdn/packages/rdn-go"
)

func main() {
    input := []byte(`{
        "created": @2024-01-15T10:30:00.000Z,
        "count": 42n,
        "tags": Set{"admin", "editor"},
        "pattern": /^hello$/i
    }`)

    v, err := rdn.Parse(input)
    if err != nil {
        log.Fatal(err)
    }

    fmt.Println(v.Kind()) // Object

    out, _ := rdn.Stringify(v)
    fmt.Println(string(out))
}
```

## API

### Core Functions

```go
// Parse parses RDN-encoded data and returns the corresponding Value.
func Parse(data []byte) (Value, error)

// Stringify returns the compact RDN encoding of a Value.
func Stringify(v Value) ([]byte, error)

// StringifyIndent is like Stringify but applies indentation.
func StringifyIndent(v Value, prefix, indent string) ([]byte, error)

// Valid reports whether data is valid RDN.
func Valid(data []byte) bool
```

### Value Type

`Value` is a concrete struct with union-style storage. Use `Kind()` to determine which accessor to call.

**Constructors:**

| Function | Creates |
|----------|---------|
| `Null()` | null value |
| `Bool(b)` | boolean |
| `NumberVal(f)` | float64 (including NaN, ±Infinity) |
| `BigIntVal(s)` | BigInt from digit string |
| `BigIntFromGo(v)` | BigInt from `*big.Int` |
| `StringVal(s)` | string |
| `ArrayVal(elems)` | array |
| `ObjectVal(pairs)` | ordered object |
| `DateTimeVal(t)` | datetime from `time.Time` |
| `TimeOnlyVal(t)` | time-of-day |
| `DurationVal(iso)` | ISO 8601 duration |
| `RegExpVal(src, flags)` | regular expression |
| `BinaryVal(data)` | binary data |
| `MapVal(entries)` | map with any-typed keys |
| `SetVal(elems)` | set |
| `TupleVal(elems)` | tuple |

**Accessors:**

| Method | Returns | Kinds |
|--------|---------|-------|
| `Kind()` | `ValueKind` | all |
| `IsNull()` | `bool` | all |
| `BoolVal()` | `bool` | Bool |
| `Float64()` | `float64` | Number |
| `Int64()` | `int64` | Number |
| `Str()` | `string` | String, BigInt, Duration |
| `Array()` | `[]Value` | Array, Tuple, Set |
| `Object()` | `[]KeyValue` | Object |
| `Map()` | `[]MapEntry` | Map |
| `Time()` | `time.Time` | DateTime |
| `TimeOnlyValue()` | `TimeOnly` | TimeOnly |
| `RegExpValue()` | `RegExp` | RegExp |
| `Bytes()` | `[]byte` | Binary |
| `Len()` | `int` | collections |
| `Equal(other)` | `bool` | all (deep equality, NaN == NaN) |

### Type Mapping

| RDN Type | Go Representation | ValueKind |
|----------|-------------------|-----------|
| `null` | zero Value | `KindNull` |
| `true` / `false` | `bool` | `KindBool` |
| `42`, `3.14` | `float64` | `KindNumber` |
| `NaN`, `Infinity` | `float64` (special) | `KindNumber` |
| `42n` | digit string | `KindBigInt` |
| `"hello"` | `string` | `KindString` |
| `[1, 2]` | `[]Value` | `KindArray` |
| `{"a": 1}` | `[]KeyValue` | `KindObject` |
| `@2024-01-15T...Z` | `time.Time` | `KindDateTime` |
| `@14:30:00` | `TimeOnly` | `KindTimeOnly` |
| `@P1Y2M3D` | `Duration` (ISO string) | `KindDuration` |
| `/pattern/flags` | `RegExp` | `KindRegExp` |
| `b"SGVsbG8="` | `[]byte` | `KindBinary` |
| `Map{k => v}` | `[]MapEntry` | `KindMap` |
| `Set{1, 2}` | `[]Value` | `KindSet` |
| `(1, 2)` | `[]Value` | `KindTuple` |

### Custom Types

```go
type TimeOnly struct {
    Hours, Minutes, Seconds, Milliseconds int
}

type Duration struct {
    ISO string // e.g. "P1Y2M3DT4H5M6S"
}

type RegExp struct {
    Source string // pattern without delimiters
    Flags  string // e.g. "gi"
}

type Number string   // preserves original text; has Float64(), Int64(), BigInt()
type RawMessage []byte
```

### Errors

```go
type SyntaxError struct {
    msg    string
    Offset int64  // byte offset in input
}
// Error() returns: "rdn: <msg> at position <offset>"
```

## Performance

Benchmarks on Apple M3 Pro (`go test -bench=. -benchmem`):

| Benchmark | Speed | Throughput | Allocs |
|-----------|-------|------------|--------|
| Parse/Primitives | 494 ns/op | 142 MB/s | 7 |
| Parse/Nested | 1.44 µs/op | 128 MB/s | 16 |
| Parse/RDNHeavy | 864 ns/op | 210 MB/s | 14 |
| Parse/LargeArray1K | 91.2 µs/op | 43 MB/s | 9 |
| Parse/StringHeavy | 874 ns/op | 290 MB/s | 12 |
| Stringify/Primitives | 262 ns/op | 267 MB/s | 6 |
| Stringify/Nested | 465 ns/op | 396 MB/s | 6 |
| Stringify/RDNHeavy | 366 ns/op | 494 MB/s | 6 |
| Stringify/LargeArray1K | 34.4 µs/op | 113 MB/s | 6 |
| Stringify/StringHeavy | 352 ns/op | 719 MB/s | 6 |

Key optimizations:
- 256-entry dispatch table for O(1) first-character token lookup
- Deferred string materialization (fast path avoids allocation for escape-free strings)
- Smi-first number parsing (int64 accumulation for up to 15 digits)
- Pre-computed base64/hex decode tables (inline decoding without `encoding/base64`)
- sync.Pool buffer reuse for serialization
- Pre-computed digit-pair and escape tables

## v0.2.0 Roadmap

- `Marshal(v any) ([]byte, error)` / `Unmarshal(data []byte, v any) error` — reflection-based
- Struct tags: `rdn:"name,omitempty"`
- Streaming: `NewEncoder(w io.Writer)` / `NewDecoder(r io.Reader)`
- `Marshaler` / `Unmarshaler` interfaces

## License

See the [repository root](https://github.com/ShtibsDev/rdn) for license information.
