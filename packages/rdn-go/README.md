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

// ParseZeroCopy parses RDN-encoded data using zero-copy string optimization.
// Strings without escape sequences reference the input buffer directly.
// The returned Value must not be used after the input slice is modified.
func ParseZeroCopy(data []byte) (Value, error)

// Stringify returns the compact RDN encoding of a Value.
func Stringify(v Value) ([]byte, error)

// StringifyIndent is like Stringify but applies indentation.
func StringifyIndent(v Value, prefix, indent string) ([]byte, error)

// Valid reports whether data is valid RDN.
func Valid(data []byte) bool
```

### Value Type

`Value` is a compact 64-byte struct using `unsafe.Pointer` for collection and rare-type storage. Use `Kind()` to determine which accessor to call.

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

### Marshal / Unmarshal

Reflection-based serialization mirroring `encoding/json`:

```go
type User struct {
    Name    string          `rdn:"name"`
    Created time.Time       `rdn:"created"`
    Score   int             `rdn:"score,omitempty"`
    Tags    rdn.Set[string] `rdn:"tags"`
}

// Marshal Go struct → RDN bytes
data, err := rdn.Marshal(User{
    Name:    "Alice",
    Created: time.Now(),
    Tags:    rdn.Set[string]{"admin", "editor"},
})

// Unmarshal RDN bytes → Go struct
var user User
err = rdn.Unmarshal(data, &user)

// Work with Value directly (no serialization step)
val, err := rdn.MarshalValue(user)   // Go → Value
err = rdn.UnmarshalValue(val, &user) // Value → Go
```

#### Struct Tags

Format: `rdn:"name,option1,option2"`

| Tag | Behavior |
|-----|----------|
| `rdn:"name"` | Use `name` as the object key |
| `rdn:"-"` | Skip this field entirely |
| `rdn:"-,"` | Use literal key name `-` |
| `rdn:",omitempty"` | Omit if the field is the Go zero value |
| `rdn:"name,omitempty"` | Custom name + omitempty |
| `rdn:",string"` | Quote numbers/bools as RDN strings |
| (no tag) | Use the exported field name as-is |

If no `rdn` tag is present, the `json` tag is used as a fallback.

#### Custom Marshaling

Implement `Marshaler` / `Unmarshaler` for full control over how a type is converted:

```go
type Marshaler interface {
    MarshalRDN() (Value, error)
}

type Unmarshaler interface {
    UnmarshalRDN(Value) error
}
```

Types implementing `encoding.TextMarshaler` / `encoding.TextUnmarshaler` are also supported as a fallback.

#### Wrapper Types

Thin generic wrappers for RDN-only collection types:

| Type | Marshals to | Description |
|------|-------------|-------------|
| `Set[T]` | `Set{...}` | Ordered set of homogeneous values |
| `Tuple` | `(...)` | Heterogeneous tuple (`[]any` under the hood) |
| `OrderedMap[K,V]` | `Map{k => v}` | Insertion-ordered map with any key type |

```go
tags := rdn.Set[string]{"admin", "editor"}
point := rdn.Tuple{1, 2, "label"}

m := &rdn.OrderedMap[int, string]{}
m.Set(1, "one")
m.Set(2, "two")
```

#### Type Mapping (Go to RDN)

| Go Type | RDN Kind | Notes |
|---------|----------|-------|
| `nil` | null | |
| `bool` | Bool | |
| `int`, `int8`..`int64` | Number | BigInt if abs > 2^53 |
| `uint`, `uint8`..`uint64` | Number | BigInt if > 2^53 |
| `float32`, `float64` | Number | NaN and Inf preserved |
| `string` | String | |
| `[]byte` | Binary | base64 encoded |
| `*big.Int` | BigInt | nil produces null |
| `time.Time` | DateTime | |
| `rdn.TimeOnly` | TimeOnly | |
| `rdn.Duration` | Duration | |
| `rdn.RegExp` | RegExp | |
| `rdn.Value` | (passthrough) | |
| `rdn.Set[T]` | Set | via Marshaler |
| `rdn.Tuple` | Tuple | via Marshaler |
| `rdn.OrderedMap[K,V]` | Map | via Marshaler |
| `[]T` | Array | nil produces null |
| `map[string]V` | Object | nil produces null, keys sorted |
| `map[K]V` (non-string K) | Map | nil produces null |
| `struct` | Object | field tags respected |

#### Type Mapping (RDN to `interface{}`)

| RDN Kind | Go Type |
|----------|---------|
| null | `nil` |
| Bool | `bool` |
| Number | `float64` |
| BigInt | `*big.Int` |
| String | `string` |
| Array | `[]any` |
| Object | `map[string]any` |
| DateTime | `time.Time` |
| TimeOnly | `rdn.TimeOnly` |
| Duration | `rdn.Duration` |
| RegExp | `rdn.RegExp` |
| Binary | `[]byte` |
| Map | `[]rdn.MapEntry` |
| Set | `rdn.Set[any]` |
| Tuple | `rdn.Tuple` |

### Streaming

`Encoder` and `Decoder` mirror `encoding/json`'s streaming API.

```go
// Decode from an io.Reader
dec := rdn.NewDecoder(reader)
var v rdn.Value
if err := dec.Decode(&v); err != nil { ... }

// Encode to an io.Writer (appends newline after each value)
enc := rdn.NewEncoder(writer)
enc.SetIndent("", "  ") // optional pretty-print
if err := enc.Encode(v); err != nil { ... }

// Encode/Decode Go values directly (combines Marshal + stream)
enc.EncodeValue(myStruct)  // Go value → RDN bytes
dec.DecodeValue(&myStruct) // RDN bytes → Go value
```

### HTTP Support (`rdnhttp` sub-package)

The `rdnhttp` sub-package provides content-type negotiation, request/response helpers, and middleware for serving RDN over HTTP. It lives in a separate package to avoid pulling `net/http` into every consumer.

```go
import "github.com/ShtibsDev/rdn/packages/rdn-go/rdnhttp"
```

**Constants & Types:**

| Export | Description |
|--------|-------------|
| `MediaTypeRDN` | `"application/rdn"` |
| `MediaTypeJSON` | `"application/json"` |
| `FormatRDN` / `FormatJSON` | Response format enum |
| `Options` | JSONFallback, Indent, Prefix, MaxBodySize |

**Content Negotiation:**

| Function | Purpose |
|----------|---------|
| `NegotiateFormat(r, opts)` | Parse Accept header, pick response format |
| `DetectContentType(r)` | Parse Content-Type header, identify request format |
| `AcceptsRDN(r)` | Quick check: does Accept include `application/rdn`? |
| `IsRDNContentType(r)` | Quick check: is Content-Type `application/rdn`? |

**HTTP Helpers & Middleware:**

| Function | Purpose |
|----------|---------|
| `ReadRequest(r, v, opts...)` | Read + parse request body into `*rdn.Value` |
| `WriteResponse(w, r, v, opts...)` | Negotiate format + write response |
| `Negotiate(next, opts...)` | Middleware: sets negotiated format in context |
| `NegotiateFunc(opts...)` | Middleware chain variant `func(http.Handler) http.Handler` |
| `FormatFromContext(ctx)` | Retrieve negotiated format from context |
| `HandleRDN(fn, opts...)` | Full handler wrapper: read → process → write |

**Example — HTTP handler:**

```go
handler := rdnhttp.HandleRDN(func(r *http.Request, v rdn.Value) (rdn.Value, error) {
    // v is the parsed request body; return the response value
    return rdn.ObjectVal([]rdn.KeyValue{
        {Key: "status", Value: rdn.StringVal("ok")},
    }), nil
})
http.Handle("/api/data", handler)
```

**Example — Middleware:**

```go
mux := http.NewServeMux()
mux.HandleFunc("/api/items", func(w http.ResponseWriter, r *http.Request) {
    format := rdnhttp.FormatFromContext(r.Context())
    // ... use format to decide response encoding
})
wrapped := rdnhttp.Negotiate(mux, rdnhttp.Options{JSONFallback: true})
http.ListenAndServe(":8080", wrapped)
```

JSON fallback converts only the JSON-compatible subset of RDN values (null, bool, number, string, array, object). Extended types (BigInt, DateTime, RegExp, etc.) return an error when JSON output is requested.

### Errors

```go
// SyntaxError is returned when the input is not valid RDN.
type SyntaxError struct {
    msg    string
    Offset int64  // byte offset in input
}

// MarshalError describes an error encountered while marshaling a Go value.
type MarshalError struct {
    Type reflect.Type
    Err  error
}

// UnmarshalTypeError describes an RDN value that was not appropriate
// for a value of a specific Go type.
type UnmarshalTypeError struct {
    Value  string       // "number", "string", "array", etc.
    Type   reflect.Type // Go type it could not be assigned to
    Struct string       // containing struct name (if applicable)
    Field  string       // full field path (if applicable)
}

// InvalidUnmarshalError describes an invalid argument passed to Unmarshal
// (must be a non-nil pointer).
type InvalidUnmarshalError struct {
    Type reflect.Type
}
```

## Performance

Benchmarks on Apple M3 Pro (`go test -bench=. -benchmem`):

| Benchmark | Speed | Throughput | Bytes/op | Allocs |
|-----------|-------|------------|----------|--------|
| Parse/Primitives | 436 ns/op | 160 MB/s | 1,984 B | 11 |
| Parse/Nested | 909 ns/op | 202 MB/s | 3,808 B | 23 |
| Parse/RDNHeavy | 822 ns/op | 220 MB/s | 2,168 B | 22 |
| Parse/LargeArray1K | 20.4 µs/op | 191 MB/s | 145,536 B | 8 |
| Parse/StringHeavy | 857 ns/op | 295 MB/s | 2,264 B | 16 |
| ParseZeroCopy/StringHeavy | 800 ns/op | 316 MB/s | 2,072 B | 11 |
| Stringify/Primitives | 179 ns/op | 391 MB/s | 80 B | 1 |
| Stringify/Nested | 354 ns/op | 520 MB/s | 192 B | 1 |
| Stringify/RDNHeavy | 277 ns/op | 653 MB/s | 192 B | 1 |
| Stringify/LargeArray1K | 33.1 µs/op | 118 MB/s | 4,097 B | 1 |
| Stringify/StringHeavy | 265 ns/op | 954 MB/s | 256 B | 1 |

Key optimizations:
- Compact 64-byte `Value` struct (down from 224B) using `unsafe.Pointer` for collections
- 256-entry dispatch table for O(1) first-character token lookup
- Deferred string materialization (fast path avoids allocation for escape-free strings)
- Reusable scratch buffer for escaped string materialization
- Object key interning for repeated keys across collections
- Zero-copy string parsing via `unsafe.String` (`ParseZeroCopy`)
- Smi-first number parsing (int64 accumulation for up to 15 digits)
- Pre-computed base64/hex decode tables (inline decoding without `encoding/base64`)
- sync.Pool buffer reuse for serialization
- Pre-computed digit-pair and escape tables

## Roadmap

- `DisallowUnknownFields` option for strict struct unmarshaling
- Single-pass optimization (bypass `Value` intermediate for Marshal/Unmarshal)

## License

See the [repository root](https://github.com/ShtibsDev/rdn) for license information.
