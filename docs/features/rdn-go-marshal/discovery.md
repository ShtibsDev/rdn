# Discovery: rdn-go-marshal

## 1. Feature Overview

Add reflection-based `Marshal` and `Unmarshal` functions to the `rdn-go` package, mirroring Go's `encoding/json` patterns. This bridges the gap between the current low-level `Value`-based API (parse RDN text into `Value`, stringify `Value` back to RDN text) and idiomatic Go usage where developers work directly with structs, maps, and slices.

**What we're building:**
- `Marshal(v any) ([]byte, error)` -- serialize any Go value to RDN bytes
- `Unmarshal(data []byte, v any) error` -- parse RDN bytes into a Go value
- `MarshalValue(v any) (Value, error)` -- convert Go value to `Value` (no serialization)
- `UnmarshalValue(val Value, v any) error` -- convert `Value` into a Go value (no parsing)
- `Marshaler` / `Unmarshaler` interfaces for custom type hooks
- Struct tag support: `rdn:"name,omitempty"`

**Why:** The README roadmap explicitly lists this as the next feature. Without Marshal/Unmarshal, users must manually construct `Value` trees from Go types and vice versa, which is tedious and error-prone.

## 2. Current State

### 2.1 Value Type System

The `Value` struct (defined in `value.go`) is a compact 64-byte struct using `unsafe.Pointer` for collection and rare-type storage:

```go
type Value struct {
    kind    ValueKind      // 8 bytes
    num     float64        // 8 bytes
    boolean bool           // 1 byte (+7 pad)
    str     string         // 16 bytes
    ptr     unsafe.Pointer // 8 bytes -- backing array or heap-allocated rare type
    ptrLen  int            // 8 bytes -- length for slice-backed collections
    ptrCap  int            // 8 bytes -- capacity for slice-backed collections
}
```

`ValueKind` is an int enum with 15 variants:

| Kind | Storage | Constructor |
|------|---------|-------------|
| `KindNull` | (none) | `Null()` |
| `KindBool` | `boolean` field | `Bool(b)` |
| `KindNumber` | `num` field (float64, incl. NaN/Inf) | `NumberVal(f)` |
| `KindBigInt` | `str` field (digit string) | `BigIntVal(s)`, `BigIntFromGo(v)` |
| `KindString` | `str` field | `StringVal(s)` |
| `KindArray` | `ptr`/`ptrLen`/`ptrCap` -> `[]Value` | `ArrayVal(elems)` |
| `KindObject` | `ptr`/`ptrLen`/`ptrCap` -> `[]KeyValue` | `ObjectVal(pairs)` |
| `KindDateTime` | `ptr` -> `*time.Time` | `DateTimeVal(t)` |
| `KindTimeOnly` | `ptr` -> `*TimeOnly` | `TimeOnlyVal(t)` |
| `KindDuration` | `str` field (ISO string) | `DurationVal(iso)` |
| `KindRegExp` | `ptr` -> `*RegExp` | `RegExpVal(src, flags)` |
| `KindBinary` | `ptr` -> `*[]byte` | `BinaryVal(data)` |
| `KindMap` | `ptr`/`ptrLen`/`ptrCap` -> `[]MapEntry` | `MapVal(entries)` |
| `KindSet` | `ptr`/`ptrLen`/`ptrCap` -> `[]Value` | `SetVal(elems)` |
| `KindTuple` | `ptr`/`ptrLen`/`ptrCap` -> `[]Value` | `TupleVal(elems)` |

Supporting types:
- `KeyValue{Key string, Value Value}` -- object entries
- `MapEntry{Key Value, Value Value}` -- map entries with any-typed keys

Accessors: `Kind()`, `IsNull()`, `BoolVal()`, `Float64()`, `Int64()`, `Str()`, `Array()`, `Object()`, `Map()`, `Time()`, `TimeOnlyValue()`, `RegExpValue()`, `Bytes()`, `Len()`, `IsNaN()`, `IsInf()`, `Equal()`, `String()`.

### 2.2 Type Representations

From `types.go`:

```go
type TimeOnly struct { Hours, Minutes, Seconds, Milliseconds int }
type Duration struct { ISO string }
type RegExp struct { Source, Flags string }
type Number string      // preserves original text; has Float64(), Int64(), BigInt() methods
type RawMessage []byte  // raw RDN bytes, delays parsing
```

### 2.3 Existing API Surface

**Core (rdn.go):**
- `Parse(data []byte) (Value, error)` -- parse RDN text to Value
- `ParseZeroCopy(data []byte) (Value, error)` -- zero-copy variant
- `Stringify(v Value) ([]byte, error)` -- compact serialization
- `StringifyIndent(v Value, prefix, indent string) ([]byte, error)` -- pretty-print
- `Valid(data []byte) bool` -- validity check

**Streaming (stream.go):**
- `NewDecoder(r io.Reader) *Decoder` / `Decoder.Decode(v *Value) error`
- `NewEncoder(w io.Writer) *Encoder` / `Encoder.SetIndent()` / `Encoder.Encode(v Value) error`

**Errors (errors.go):**
- `SyntaxError{msg string, Offset int64}` -- parse errors

**HTTP (rdnhttp/):**
- Content negotiation, request/response helpers, middleware (separate sub-package)

**How Marshal/Unmarshal complements existing APIs:**
The current APIs operate on the `Value` intermediate representation. Marshal/Unmarshal will add a higher-level path:
- `Marshal`: `Go value` -> `Value` -> `[]byte` (combines MarshalValue + Stringify)
- `Unmarshal`: `[]byte` -> `Value` -> `Go value` (combines Parse + UnmarshalValue)

The intermediate `MarshalValue`/`UnmarshalValue` functions allow users to work with `Value` trees directly when needed (e.g., rdnhttp handlers that already have a `Value`).

## 3. Go Type -> RDN Type Mapping (Marshal)

| Go Type | RDN ValueKind | Notes |
|---------|---------------|-------|
| `bool` | `KindBool` | |
| `int`, `int8`..`int64` | `KindNumber` | Cast to `float64` |
| `uint`, `uint8`..`uint64` | `KindNumber` | Cast to `float64`; `uint64` values > 2^53 should use BigInt |
| `float32`, `float64` | `KindNumber` | NaN and Inf preserved natively |
| `string` | `KindString` | |
| `[]byte` | `KindBinary` | Special case: base64-encoded |
| `*big.Int` | `KindBigInt` | Via `BigIntFromGo()` |
| `time.Time` | `KindDateTime` | Via `DateTimeVal()` |
| `rdn.TimeOnly` | `KindTimeOnly` | Direct passthrough |
| `rdn.Duration` | `KindDuration` | Direct passthrough |
| `rdn.RegExp` | `KindRegExp` | Direct passthrough |
| `rdn.Number` | `KindNumber` or `KindBigInt` | Parse the string to determine kind |
| `rdn.RawMessage` | (raw passthrough) | Parse then re-embed |
| `rdn.Value` | (direct) | Already a Value -- pass through |
| `[]T` (any slice) | `KindArray` | Recursively marshal elements |
| `[N]T` (array) | `KindArray` | Same as slice |
| `map[string]V` | `KindObject` | String keys -> Object |
| `map[K]V` (non-string K) | `KindMap` | Non-string keys -> Map with `=>` syntax |
| `struct` | `KindObject` | Field name/tag -> key, field value -> value |
| `*T` (pointer) | (deref) | Marshal `*T` as `T`; nil -> `KindNull` |
| `interface{}` / `any` | (dynamic) | Inspect runtime type |
| `nil` | `KindNull` | |
| `Marshaler` implementor | (custom) | Call `MarshalRDN()` |
| `encoding.TextMarshaler` | `KindString` | Fallback: marshal as string |

## 4. RDN Type -> Go Type Mapping (Unmarshal)

### 4.1 Into Typed Destinations

When the destination type is known (e.g., `*MyStruct`):

| RDN ValueKind | Target Go Type | Behavior |
|---------------|----------------|----------|
| `KindNull` | any pointer | Set to `nil` |
| `KindNull` | `interface{}` | Set to `nil` |
| `KindBool` | `bool` | Direct |
| `KindNumber` | `float64`, `float32` | Direct |
| `KindNumber` | `int`, `int8`..`int64` | Truncate; error if not representable |
| `KindNumber` | `uint`, `uint8`..`uint64` | Truncate; error if negative or too large |
| `KindBigInt` | `*big.Int` | Parse digit string |
| `KindBigInt` | `int64`, `uint64` | Parse digit string; error if overflow |
| `KindString` | `string` | Direct |
| `KindString` | `encoding.TextUnmarshaler` | Call `UnmarshalText()` |
| `KindArray` | `[]T` | Recursively unmarshal elements |
| `KindTuple` | `[]T` | Same as array (tuple -> slice) |
| `KindTuple` | `[N]T` | Fixed-size array; error if length mismatch |
| `KindSet` | `[]T` | Same as array (set -> slice, order not guaranteed) |
| `KindObject` | `struct` | Match keys to field names/tags |
| `KindObject` | `map[string]V` | Populate map |
| `KindMap` | `map[K]V` | Recursively unmarshal keys and values |
| `KindDateTime` | `time.Time` | Direct |
| `KindTimeOnly` | `TimeOnly` | Direct |
| `KindDuration` | `Duration` | Direct |
| `KindRegExp` | `RegExp` | Direct |
| `KindBinary` | `[]byte` | Direct |
| Any | `rdn.Value` | Assign the `Value` directly (no conversion) |
| Any | `rdn.RawMessage` | Re-stringify the Value to raw bytes |
| Any | `interface{}` | See 4.2 below |

### 4.2 Into `interface{}` (Untyped Destination)

When unmarshaling into `any`/`interface{}`, we need default Go types:

| RDN ValueKind | Default Go Type |
|---------------|----------------|
| `KindNull` | `nil` |
| `KindBool` | `bool` |
| `KindNumber` | `float64` |
| `KindBigInt` | `*big.Int` |
| `KindString` | `string` |
| `KindArray` | `[]any` |
| `KindObject` | `map[string]any` |
| `KindDateTime` | `time.Time` |
| `KindTimeOnly` | `rdn.TimeOnly` |
| `KindDuration` | `rdn.Duration` |
| `KindRegExp` | `rdn.RegExp` |
| `KindBinary` | `[]byte` |
| `KindMap` | `map[any]any` (or `[]rdn.MapEntry` -- see Open Questions) |
| `KindSet` | `[]any` (see Open Questions) |
| `KindTuple` | `[]any` |

## 5. Interface Design

### 5.1 Marshaler / Unmarshaler

```go
// Marshaler is implemented by types that can marshal themselves into an RDN Value.
type Marshaler interface {
    MarshalRDN() (Value, error)
}

// Unmarshaler is implemented by types that can unmarshal an RDN Value into themselves.
type Unmarshaler interface {
    UnmarshalRDN(Value) error
}
```

**Precedence order (Marshal):**
1. `rdn.Marshaler` -- highest priority
2. `encoding.TextMarshaler` -- fallback, produces KindString
3. Reflection-based encoding

**Precedence order (Unmarshal):**
1. `rdn.Unmarshaler` -- highest priority
2. `encoding.TextUnmarshaler` -- fallback, only for KindString values
3. Reflection-based decoding

### 5.2 Struct Tags

Format: `rdn:"name,option1,option2"`

| Tag | Behavior |
|-----|----------|
| `rdn:"name"` | Use `name` as the object key |
| `rdn:"-"` | Skip this field entirely |
| `rdn:",omitempty"` | Omit if the field is the zero value |
| `rdn:"-,"` | Use literal key name `-` |
| `rdn:"name,omitempty"` | Both: custom name + omitempty |
| `rdn:",string"` | Quote numbers/bools as RDN strings (mirrors encoding/json) |
| (no tag) | Use the exported field name as-is |

**Struct tag parsing** should follow `encoding/json` conventions exactly:
- Only exported fields are included
- Anonymous (embedded) struct fields are promoted (flattened)
- If two promoted fields have the same name, the outer one wins
- If both are at the same depth, both are excluded (ambiguity)

### 5.3 encoding.TextMarshaler Support

Yes, support `encoding.TextMarshaler` / `encoding.TextUnmarshaler` as a fallback. This ensures interop with types like `net.IP`, `url.URL`, `time.Time` (though time.Time gets special handling), and user-defined types that already implement these interfaces.

## 6. Implementation Approach

### 6.1 Marshal Flow

```
Marshal(v any) ([]byte, error)
    1. marshalValue(reflect.ValueOf(v)) -> Value
    2. Stringify(value) -> []byte
```

`marshalValue` recursively converts a `reflect.Value` to an `rdn.Value`:

1. **Nil check**: If pointer/interface/slice/map is nil, return `Null()`.
2. **Interface check**: If the type implements `Marshaler`, call `MarshalRDN()`.
3. **Special types**: Check for `time.Time`, `*big.Int`, `rdn.Value`, `rdn.TimeOnly`, `rdn.Duration`, `rdn.RegExp`, `rdn.Number`, `rdn.RawMessage`, `[]byte`.
4. **TextMarshaler check**: If it implements `encoding.TextMarshaler`, call `MarshalText()` and wrap in `StringVal()`.
5. **reflect.Kind switch**:
   - `Bool` -> `Bool(v.Bool())`
   - `Int*` -> `NumberVal(float64(v.Int()))`
   - `Uint*` -> `NumberVal(float64(v.Uint()))` (with BigInt overflow check for uint64)
   - `Float*` -> `NumberVal(v.Float())`
   - `String` -> `StringVal(v.String())`
   - `Slice` -> `ArrayVal(...)` (special case `[]byte` -> `BinaryVal`)
   - `Array` -> `ArrayVal(...)`
   - `Map` -> `ObjectVal(...)` if string keys, else `MapVal(...)`
   - `Struct` -> `ObjectVal(...)` using cached field analysis
   - `Ptr` -> dereference and recurse
   - `Interface` -> extract elem and recurse

### 6.2 Unmarshal Flow

```
Unmarshal(data []byte, v any) error
    1. Parse(data) -> Value
    2. unmarshalValue(value, reflect.ValueOf(v).Elem()) -> error
```

`unmarshalValue` recursively populates a `reflect.Value` from an `rdn.Value`:

1. **Interface check**: If the type implements `Unmarshaler`, call `UnmarshalRDN(val)`.
2. **Special type check**: `rdn.Value`, `rdn.RawMessage`, `time.Time`, `*big.Int`, `rdn.TimeOnly`, `rdn.Duration`, `rdn.RegExp`.
3. **Pointer handling**: If target is a pointer and value is null, set nil. Otherwise, allocate if needed and recurse into the pointed-to type.
4. **Kind switch on Value.Kind()**:
   - `KindNull` -> set zero value
   - `KindBool` -> set if target is bool
   - `KindNumber` -> set int/uint/float based on target kind
   - `KindBigInt` -> parse into `*big.Int` or int64/uint64
   - `KindString` -> set string, or call `TextUnmarshaler`
   - `KindArray`/`KindTuple`/`KindSet` -> populate slice/array
   - `KindObject` -> populate struct (field matching) or map[string]V
   - `KindMap` -> populate map[K]V
   - `KindDateTime`/`KindTimeOnly`/`KindDuration`/`KindRegExp`/`KindBinary` -> direct assignment to matching types
5. **Fallback to `interface{}`**: If the target is `interface{}`, use the default type mapping from section 4.2.

### 6.3 Type Encoder/Decoder Caching

Following `encoding/json`'s pattern, cache the encoder/decoder function per `reflect.Type` using a `sync.Map`:

```go
var encoderCache sync.Map // map[reflect.Type]encoderFunc
var decoderCache sync.Map // map[reflect.Type]decoderFunc

type encoderFunc func(v reflect.Value) (Value, error)
type decoderFunc func(val Value, v reflect.Value) error
```

The first call for a given type computes the encoder/decoder function (involves reflection to analyze struct fields, check interface implementations, etc.) and caches it. Subsequent calls do a single `sync.Map` load.

For structs, the cached function includes pre-computed field info:
- Field indices (for fast access without name lookup)
- Tag names
- Omitempty flags
- Per-field encoder/decoder functions

### 6.4 Struct Field Analysis

Create a `structFields` type that pre-computes all field metadata:

```go
type field struct {
    name      string       // RDN key name (from tag or field name)
    index     []int        // reflect field index path (supports embedded)
    omitempty bool
    quoted    bool         // ",string" option
    encoder   encoderFunc
    decoder   decoderFunc
}

type structFields struct {
    list    []field
    nameIndex map[string]int // key name -> index in list for fast unmarshal lookup
}
```

Cache `structFields` per `reflect.Type` in a `sync.Map`.

Anonymous (embedded) struct handling follows `encoding/json` rules:
1. Collect all fields from the struct and all anonymous embedded structs
2. Apply visibility and depth rules
3. Resolve conflicts (same name at different depths: shallower wins; same depth: both excluded)

## 7. Blast Radius

### New Files

| File | Purpose |
|------|---------|
| `marshal.go` | `Marshal`, `MarshalValue`, `MarshalIndent`, marshaler interface, marshalValue implementation |
| `unmarshal.go` | `Unmarshal`, `UnmarshalValue`, unmarshaler interface, unmarshalValue implementation |
| `tags.go` | Struct tag parsing (`parseTag`), struct field analysis, field cache |
| `marshal_test.go` | Tests for Marshal, MarshalValue, struct tags, custom marshalers |
| `unmarshal_test.go` | Tests for Unmarshal, UnmarshalValue, struct tags, custom unmarshalers |

### Existing Files That Need Modification

| File | Change |
|------|--------|
| `rdn.go` | Add doc comments referencing Marshal/Unmarshal; possibly add `MarshalIndent` top-level function |
| `types.go` | Add `Marshaler` and `Unmarshaler` interface definitions (or put in `marshal.go`) |
| `stream.go` | Update `Decoder.Decode` and `Encoder.Encode` to accept `any` in addition to `*Value` / `Value` (or add new methods) |
| `errors.go` | Add `MarshalError`, `UnmarshalError`, `UnmarshalTypeError` types |
| `README.md` | Document Marshal/Unmarshal API, struct tags, interfaces |

### Public API Additions

```go
// Top-level functions
func Marshal(v any) ([]byte, error)
func MarshalIndent(v any, prefix, indent string) ([]byte, error)
func MarshalValue(v any) (Value, error)
func Unmarshal(data []byte, v any) error
func UnmarshalValue(val Value, v any) error

// Interfaces
type Marshaler interface { MarshalRDN() (Value, error) }
type Unmarshaler interface { UnmarshalRDN(Value) error }

// Error types
type MarshalError struct { ... }
type UnmarshalTypeError struct { Value string; Type reflect.Type; Offset int64; Struct string; Field string }
type InvalidUnmarshalError struct { Type reflect.Type }

// Stream additions (optional -- could defer)
func (dec *Decoder) DecodeValue(v any) error   // sugar over Decode + UnmarshalValue
func (enc *Encoder) EncodeValue(v any) error   // sugar over MarshalValue + Encode
```

## 8. Edge Cases

### Circular References
- Marshal must detect circular references to avoid infinite recursion
- Track visited pointer addresses in a `map[unsafe.Pointer]bool` (or use a depth-based check similar to the existing `maxEncodeDepth = 128`)
- Return a `MarshalError` with a descriptive message when a cycle is detected
- `encoding/json` uses pointer tracking -- we should match that behavior

### nil Values
- `nil` pointer -> `KindNull`
- `nil` slice -> `KindNull` (matches `encoding/json` behavior: `null` not `[]`)
- `nil` map -> `KindNull`
- `nil` interface -> `KindNull`

### Unexported Fields
- Skip unexported fields entirely (same as `encoding/json`)
- Unexported embedded structs: still promote their exported fields (Go 1.22+ behavior)

### Anonymous / Embedded Structs
- Promoted fields are flattened into the parent object
- If the embedded type is a pointer, allocate on unmarshal if needed
- Conflict resolution follows `encoding/json` rules (depth-based, ambiguous fields excluded)

### RDN-Only Types (Set, Tuple, Map with Non-String Keys)

**Marshal direction:**
- No Go type naturally maps to Set or Tuple during marshal
- Users who need these must implement `Marshaler` and return `SetVal(...)` or `TupleVal(...)` or `MapVal(...)`
- Alternatively, we could provide wrapper types: `rdn.Set[T]`, `rdn.Tuple`, `rdn.Map[K,V]` -- but this adds complexity (see Open Questions)

**Unmarshal direction:**
- `KindSet` -> `[]T` (loses set semantics but preserves data)
- `KindTuple` -> `[]T` or `[N]T` (loses tuple semantics)
- `KindMap` -> `map[K]V` (works naturally with non-string keys)
- All three can unmarshal into `rdn.Value` for full fidelity

### Empty vs nil Slices/Maps
- Marshal: `nil` slice -> `null`; empty slice (`[]int{}`) -> `[]`
- Marshal: `nil` map -> `null`; empty map (`map[string]int{}`) -> `{}`
- Unmarshal: `KindNull` -> nil slice/map; empty `KindArray` -> empty non-nil slice
- This matches `encoding/json` behavior

### RawMessage
- Marshal: parse the raw bytes to a `Value`, then embed it (validates the RDN)
- Unmarshal: re-stringify the `Value` to bytes
- Both directions match `json.RawMessage` behavior

### Number Precision
- `uint64` values > 2^53: marshal as `KindBigInt` (since float64 loses precision)
- `int64` values where `|v| > 2^53`: same treatment
- Unmarshal `KindNumber` into `int64`: truncate and verify no data loss

## 9. Performance Considerations

### Type Encoder Caching Strategy
- Use `sync.Map` for the encoder/decoder cache (lock-free reads, occasional writes)
- First-call overhead is acceptable since it's amortized over all subsequent calls
- Cache key is `reflect.Type` (already comparable and unique per type)
- Struct field analysis is the most expensive part -- cache the `structFields` result

### Allocation Reduction Techniques
- Reuse the existing `encodeState` pool for the stringify step in `Marshal`
- For `Unmarshal`, the `Parse` step already uses pooled scratch buffers
- Consider pre-allocating slice capacity based on the `Value.Len()` hint
- The intermediate `Value` step does add allocations compared to a direct reflection-based parser, but it simplifies the architecture significantly and keeps the parser/serializer unified

### Comparison with encoding/json Performance
- Our Marshal will be: `Go value -> Value -> []byte` (two passes)
- `encoding/json` does: `Go value -> []byte` (one pass)
- The extra `Value` intermediate step adds overhead, but:
  - It's architecturally simpler and more maintainable
  - The `Value` struct is compact (64 bytes) so the overhead is bounded
  - For most workloads, the reflection overhead dominates anyway
- If performance becomes critical, a future optimization could add a direct `Go value -> []byte` path that bypasses `Value` construction

### Benchmark Targets
- Marshal should be within 2x of `encoding/json.Marshal` for JSON-compatible data
- Unmarshal should be within 2x of `encoding/json.Unmarshal` for JSON-compatible data
- RDN-extended types (DateTime, BigInt, etc.) have no JSON equivalent to compare against

## 10. Decisions (Resolved)

1. **Set/Tuple/Map wrapper types**: ✅ **Yes** — Provide thin generic wrapper types (`rdn.Set[T]`, `rdn.Tuple`, `rdn.OrderedMap[K,V]`) for ergonomic marshaling.

2. **Default type for KindMap into `interface{}`**: ✅ **`[]rdn.MapEntry`** — Preserves order, avoids panics from unhashable keys.

3. **Default type for KindSet into `interface{}`**: ✅ **`rdn.Set[any]`** — Use the wrapper type since we're providing them.

4. **Stream `any` overloads**: ✅ **Yes** — Add `EncodeValue(v any)` / `DecodeValue(v any)` to streaming types.

5. **`omitempty` for RDN types**: ✅ Follow Go zero-value semantics (`TimeOnly{}`, `Duration{ISO: ""}`, `RegExp{Source: "", Flags: ""}` are empty).

6. **Map key sorting**: ✅ **Yes** — Sort `map[string]V` keys for deterministic output, matching `encoding/json`.

7. **DisallowUnknownFields**: ✅ **Deferred** — Start with default behavior (ignore unknown fields).

8. **Naming**: ✅ **`MarshalValue`/`UnmarshalValue`** — Consistent with Marshal/Unmarshal naming.
