# Tech Design: rdn-go-marshal

## 1. Summary

This feature adds reflection-based `Marshal` and `Unmarshal` to the `rdn-go` package, bridging the gap between the low-level `Value` API and idiomatic Go structs/maps/slices. The architecture is two-pass: `Marshal` converts a Go value to an intermediate `Value` via reflection, then stringifies it; `Unmarshal` parses bytes into a `Value`, then populates a Go value via reflection. This mirrors `encoding/json`'s patterns (struct tags, interfaces, cached type encoders) while extending them to cover RDN-only types like Sets, Tuples, Maps with non-string keys, DateTimes, BigInts, and binary data. Thin generic wrapper types (`Set[T]`, `Tuple`, `OrderedMap[K,V]`) provide ergonomic round-trip fidelity for RDN-only collection types.

## 2. Current State

The existing API operates entirely on the `Value` intermediate representation:

- **Parse/Stringify**: `Parse([]byte) (Value, error)`, `Stringify(Value) ([]byte, error)`, `StringifyIndent`, `ParseZeroCopy`, `Valid`
- **Value type**: 64-byte compact struct with 15 `ValueKind` variants, constructors (`Null()`, `Bool()`, `NumberVal()`, etc.), and accessors (`Kind()`, `Float64()`, `Str()`, `Array()`, `Object()`, `Map()`, `Time()`, etc.)
- **Supporting types**: `KeyValue`, `MapEntry`, `TimeOnly`, `Duration`, `RegExp`, `Number`, `RawMessage`
- **Streaming**: `Decoder.Decode(*Value)`, `Encoder.Encode(Value)`
- **HTTP** (`rdnhttp/`): Content negotiation, `ReadRequest`, `WriteResponse`, `HandleRDN` -- all operate on `rdn.Value`
- **Errors**: `SyntaxError{msg, Offset}`

Users currently must manually construct `Value` trees from Go types and manually extract Go types from `Value` trees. This feature automates that process.

## 3. To-Be Behavior

### 3.1 Public API

```go
// ── Top-level functions (rdn.go) ────────────────────────────────────

// Marshal returns the RDN encoding of v.
func Marshal(v any) ([]byte, error)

// MarshalIndent is like Marshal but applies indentation.
func MarshalIndent(v any, prefix, indent string) ([]byte, error)

// MarshalValue converts a Go value to an rdn.Value without serialization.
func MarshalValue(v any) (Value, error)

// Unmarshal parses the RDN-encoded data and stores the result
// in the value pointed to by v. If v is nil or not a pointer,
// Unmarshal returns an InvalidUnmarshalError.
func Unmarshal(data []byte, v any) error

// UnmarshalValue stores an rdn.Value into the Go value pointed to by v.
func UnmarshalValue(val Value, v any) error

// ── Interfaces (marshal.go) ─────────────────────────────────────────

// Marshaler is implemented by types that can marshal themselves into an RDN Value.
type Marshaler interface {
    MarshalRDN() (Value, error)
}

// Unmarshaler is implemented by types that can unmarshal an RDN Value into themselves.
type Unmarshaler interface {
    UnmarshalRDN(Value) error
}

// ── Stream additions (stream.go) ────────────────────────────────────

// EncodeValue marshals v to a Value and writes it to the stream.
func (enc *Encoder) EncodeValue(v any) error

// DecodeValue reads a Value from the stream and unmarshals it into v.
func (dec *Decoder) DecodeValue(v any) error
```

### 3.2 Wrapper Types

```go
// ── Set[T] (wrappers.go) ────────────────────────────────────────────

// Set represents an RDN Set. It marshals to KindSet and unmarshals from KindSet.
type Set[T any] []T

// MarshalRDN implements Marshaler.
func (s Set[T]) MarshalRDN() (Value, error)

// UnmarshalRDN implements Unmarshaler.
func (s *Set[T]) UnmarshalRDN(v Value) error

// ── Tuple (wrappers.go) ─────────────────────────────────────────────

// Tuple represents an RDN Tuple of heterogeneous values.
// It marshals to KindTuple and unmarshals from KindTuple.
type Tuple []any

// MarshalRDN implements Marshaler.
func (t Tuple) MarshalRDN() (Value, error)

// UnmarshalRDN implements Unmarshaler.
func (t *Tuple) UnmarshalRDN(v Value) error

// ── OrderedMap[K, V] (wrappers.go) ──────────────────────────────────

// OrderedMap represents an RDN Map that preserves insertion order.
// It marshals to KindMap and unmarshals from KindMap.
type OrderedMap[K comparable, V any] struct {
    entries []OrderedMapEntry[K, V]
}

// OrderedMapEntry is a key-value pair in an OrderedMap.
type OrderedMapEntry[K comparable, V any] struct {
    Key   K
    Value V
}

// MarshalRDN implements Marshaler.
func (m OrderedMap[K, V]) MarshalRDN() (Value, error)

// UnmarshalRDN implements Unmarshaler.
func (m *OrderedMap[K, V]) UnmarshalRDN(v Value) error

// Entries returns the ordered entries.
func (m OrderedMap[K, V]) Entries() []OrderedMapEntry[K, V]

// Set inserts or updates an entry. If the key already exists, it updates in place.
func (m *OrderedMap[K, V]) Set(key K, value V)

// Get returns the value for key and whether it was found.
func (m OrderedMap[K, V]) Get(key K) (V, bool)

// Len returns the number of entries.
func (m OrderedMap[K, V]) Len() int
```

### 3.3 Error Types

```go
// ── errors.go additions ─────────────────────────────────────────────

// MarshalError describes an error encountered while marshaling a Go value.
type MarshalError struct {
    Type reflect.Type
    Err  error
}

func (e *MarshalError) Error() string
// → "rdn: error marshaling type <Type>: <Err>"

func (e *MarshalError) Unwrap() error

// UnmarshalTypeError describes an RDN value that was not appropriate
// for a value of a specific Go type.
type UnmarshalTypeError struct {
    Value  string       // description of RDN value: "number", "string", "array", etc.
    Type   reflect.Type // type of Go value it could not be assigned to
    Struct string       // name of the struct containing the field (empty if not in struct)
    Field  string       // the full field path (empty if not in struct)
}

func (e *UnmarshalTypeError) Error() string
// → "rdn: cannot unmarshal <Value> into Go value of type <Type>"
// or "rdn: cannot unmarshal <Value> into Go struct field <Struct>.<Field> of type <Type>"

// InvalidUnmarshalError describes an invalid argument passed to Unmarshal/UnmarshalValue.
// (The argument to Unmarshal must be a non-nil pointer.)
type InvalidUnmarshalError struct {
    Type reflect.Type
}

func (e *InvalidUnmarshalError) Error() string
// → "rdn: Unmarshal(nil)"
// or "rdn: Unmarshal(non-pointer <Type>)"
// or "rdn: Unmarshal(nil <Type>)"
```

### 3.4 Struct Tag Syntax

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

**`omitempty` zero-value rules:**
- `bool`: `false`
- integers/floats: `0`
- `string`: `""`
- pointers, interfaces, slices, maps: `nil`
- `time.Time`: zero time (`time.Time{}`)
- `*big.Int`: `nil`
- `rdn.TimeOnly`: `TimeOnly{}`
- `rdn.Duration`: `Duration{ISO: ""}`
- `rdn.RegExp`: `RegExp{Source: "", Flags: ""}`
- `rdn.Value`: zero Value (`Value{}`, i.e. KindNull with no data)
- arrays: never considered empty
- structs (other than above): never considered empty

## 4. Design Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | Architecture | Two-pass (Go value -> Value -> []byte and reverse) | Reuses existing parser/encoder, keeps code paths unified, simpler to maintain. Single-pass optimization can be added later. |
| 2 | Wrapper types | Provide `Set[T]`, `Tuple`, `OrderedMap[K,V]` | Without wrappers, users cannot round-trip Sets/Tuples/Maps through Marshal. Generics make them ergonomic. |
| 3 | KindMap into `interface{}` | `[]rdn.MapEntry` | Preserves insertion order. Avoids panics from unhashable keys in `map[any]any`. |
| 4 | KindSet into `interface{}` | `Set[any]` | Uses the wrapper type, making it distinguishable from a plain slice (KindArray). |
| 5 | KindTuple into `interface{}` | `Tuple` (i.e. `[]any`) | Uses the wrapper type, making it distinguishable from a plain slice (KindArray). |
| 6 | Map key sorting | Sort `map[string]V` keys lexicographically | Deterministic output, matches `encoding/json` behavior. |
| 7 | `omitempty` semantics | Go zero-value semantics | Consistent with `encoding/json`. No custom "empty" interface. |
| 8 | `DisallowUnknownFields` | Deferred (not in this task) | Start simple; unknown fields are silently ignored on unmarshal. |
| 9 | Naming | `MarshalValue`/`UnmarshalValue` | Clear that these operate on `Value` not `[]byte`. |
| 10 | Interface precedence | `Marshaler` > `TextMarshaler` > reflection; `Unmarshaler` > `TextUnmarshaler` > reflection | RDN-specific interfaces take priority; text interfaces provide interop with stdlib types. |
| 11 | Circular reference detection | Pointer-address tracking via `map[unsafe.Pointer]bool` | Matches `encoding/json` behavior. Only applies to pointer, map, and slice types. |
| 12 | uint64/int64 BigInt overflow | Values with absolute magnitude > 2^53 marshal as KindBigInt | float64 cannot represent them precisely. |
| 13 | Non-string map keys (marshal) | `map[K]V` where K is not `string` marshals to KindMap | Natural fit: RDN Map supports any-typed keys. |
| 14 | `rdn.Number` marshal | Parse the string: if it contains `n` suffix or is a pure integer string, produce KindBigInt; otherwise KindNumber | Preserves the original intent of the Number value. |
| 15 | nil slice/map marshal | `null` (not empty collection) | Matches `encoding/json` behavior. |
| 16 | Stream `any` overloads | Add `EncodeValue(v any)` / `DecodeValue(v any)` | Ergonomic shorthand for handlers that want to skip the Value intermediate step. |

## 5. Interfaces & Models

### 5.1 Marshaler/Unmarshaler Interfaces

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

### 5.2 Internal Types

```go
// encoderFunc converts a reflect.Value to an rdn.Value.
type encoderFunc func(v reflect.Value) (Value, error)

// decoderFunc populates a reflect.Value from an rdn.Value.
type decoderFunc func(val Value, v reflect.Value) error

// field holds pre-computed metadata for a single struct field.
type field struct {
    name      string       // RDN key name (from tag or field name)
    index     []int        // reflect field index path (supports embedded structs)
    typ       reflect.Type // field type
    omitempty bool         // ",omitempty" tag option
    quoted    bool         // ",string" tag option
    encoder   encoderFunc  // cached encoder for this field's type
    decoder   decoderFunc  // cached decoder for this field's type
}

// structFields is the pre-computed field metadata for a struct type.
type structFields struct {
    list      []field          // ordered fields
    nameIndex map[string]int   // RDN key name -> index in list
}

// marshalState holds per-call state for cycle detection.
type marshalState struct {
    visited map[unsafe.Pointer]bool
}

// Caches
var encoderCache sync.Map // map[reflect.Type]encoderFunc
var decoderCache sync.Map // map[reflect.Type]decoderFunc
var fieldCache   sync.Map // map[reflect.Type]*structFields
```

## 6. Implementation Details

### 6.1 marshal.go

**File layout**: `Marshal`, `MarshalIndent`, `MarshalValue`, `Marshaler` interface, `marshalState`, `marshalValue` recursive function, encoder cache lookup, type-specific encoder constructors.

**MarshalValue flow:**

```go
func MarshalValue(v any) (Value, error) {
    if v == nil {
        return Null(), nil
    }
    ms := &marshalState{visited: make(map[unsafe.Pointer]bool)}
    return ms.marshalValue(reflect.ValueOf(v))
}

func Marshal(v any) ([]byte, error) {
    val, err := MarshalValue(v)
    if err != nil {
        return nil, err
    }
    return Stringify(val)
}

func MarshalIndent(v any, prefix, indent string) ([]byte, error) {
    val, err := MarshalValue(v)
    if err != nil {
        return nil, err
    }
    return StringifyIndent(val, prefix, indent)
}
```

**Type dispatch logic in `marshalValue`:**

```go
func (ms *marshalState) marshalValue(v reflect.Value) (Value, error) {
    // 1. Handle invalid (nil interface)
    if !v.IsValid() {
        return Null(), nil
    }

    // 2. Dereference interface
    if v.Kind() == reflect.Interface {
        if v.IsNil() {
            return Null(), nil
        }
        v = v.Elem()
    }

    // 3. Dereference pointers (with nil check)
    for v.Kind() == reflect.Pointer {
        if v.IsNil() {
            return Null(), nil
        }
        v = v.Elem()
    }

    // 4. Look up cached encoder
    enc := cachedEncoder(v.Type())
    return enc(v)
}
```

**Cached encoder construction** (called once per type, cached in `sync.Map`):

```go
func newEncoder(t reflect.Type) encoderFunc {
    // Check Marshaler interface (on value or pointer receiver)
    if t.Implements(marshalerType) {
        return marshalerEncoder
    }
    if reflect.PointerTo(t).Implements(marshalerType) {
        return addrMarshalerEncoder
    }

    // Special types (checked by identity, not interface)
    switch t {
    case timeType:
        return timeEncoder
    case bigIntType:
        return bigIntEncoder // note: *big.Int already deref'd to big.Int
    case bigIntPtrType:
        return bigIntPtrEncoder
    case valueType:
        return valuePassthroughEncoder
    case timeOnlyType:
        return timeOnlyEncoder
    case durationType:
        return durationEncoder
    case regexpType:
        return regexpEncoder
    case numberType:
        return numberEncoder
    case rawMessageType:
        return rawMessageEncoder
    }

    // []byte special case (before slice)
    if t == byteSliceType {
        return bytesEncoder
    }

    // TextMarshaler
    if t.Implements(textMarshalerType) {
        return textMarshalerEncoder
    }
    if reflect.PointerTo(t).Implements(textMarshalerType) {
        return addrTextMarshalerEncoder
    }

    // reflect.Kind switch
    switch t.Kind() {
    case reflect.Bool:
        return boolEncoder
    case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
        return intEncoder
    case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64:
        return uintEncoder
    case reflect.Float32, reflect.Float64:
        return floatEncoder
    case reflect.String:
        return stringEncoder
    case reflect.Slice:
        return newSliceEncoder(t)
    case reflect.Array:
        return newArrayEncoder(t)
    case reflect.Map:
        return newMapEncoder(t)
    case reflect.Struct:
        return newStructEncoder(t)
    default:
        return unsupportedEncoder
    }
}
```

**Circular reference detection** (for pointers, slices, maps):

```go
func (ms *marshalState) checkCycle(v reflect.Value) error {
    ptr := unsafe.Pointer(v.Pointer())
    if ms.visited[ptr] {
        return &MarshalError{Type: v.Type(), Err: errors.New("circular reference detected")}
    }
    ms.visited[ptr] = true
    return nil
}
// Called at the start of slice/map/pointer encoding; removed after encoding completes.
```

**uint64 BigInt overflow:**

```go
func uintEncoder(v reflect.Value) (Value, error) {
    u := v.Uint()
    // 2^53 = 9007199254740992
    if u > 1<<53 {
        return BigIntVal(strconv.FormatUint(u, 10)), nil
    }
    return NumberVal(float64(u)), nil
}

func intEncoder(v reflect.Value) (Value, error) {
    i := v.Int()
    if i > 1<<53 || i < -(1<<53) {
        return BigIntVal(strconv.FormatInt(i, 10)), nil
    }
    return NumberVal(float64(i)), nil
}
```

**Map key sorting:**

```go
func newMapEncoder(t reflect.Type) encoderFunc {
    keyType := t.Key()
    if keyType.Kind() == reflect.String {
        // String keys → KindObject with sorted keys
        return func(v reflect.Value) (Value, error) {
            if v.IsNil() {
                return Null(), nil
            }
            keys := v.MapKeys()
            sort.Slice(keys, func(i, j int) bool {
                return keys[i].String() < keys[j].String()
            })
            pairs := make([]KeyValue, len(keys))
            for i, k := range keys {
                val, err := ms.marshalValue(v.MapIndex(k))
                if err != nil {
                    return Value{}, err
                }
                pairs[i] = KeyValue{Key: k.String(), Value: val}
            }
            return ObjectVal(pairs), nil
        }
    }
    // Non-string keys → KindMap
    return func(v reflect.Value) (Value, error) { /* ... marshal keys and values ... */ }
}
```

### 6.2 unmarshal.go

**File layout**: `Unmarshal`, `UnmarshalValue`, `Unmarshaler` interface, `unmarshalValue` recursive function, decoder cache lookup, type-specific decoder constructors.

**UnmarshalValue flow:**

```go
func UnmarshalValue(val Value, v any) error {
    rv := reflect.ValueOf(v)
    if rv.Kind() != reflect.Pointer || rv.IsNil() {
        return &InvalidUnmarshalError{Type: reflect.TypeOf(v)}
    }
    return unmarshalValue(val, rv.Elem())
}

func Unmarshal(data []byte, v any) error {
    val, err := Parse(data)
    if err != nil {
        return err
    }
    return UnmarshalValue(val, v)
}
```

**Type dispatch logic in `unmarshalValue`:**

```go
func unmarshalValue(val Value, v reflect.Value) error {
    // 1. Check Unmarshaler interface
    if v.CanAddr() {
        if u, ok := v.Addr().Interface().(Unmarshaler); ok {
            return u.UnmarshalRDN(val)
        }
    }
    if u, ok := v.Interface().(Unmarshaler); ok {
        return u.UnmarshalRDN(val)
    }

    // 2. Pointer handling
    if v.Kind() == reflect.Pointer {
        if val.Kind() == KindNull {
            v.Set(reflect.Zero(v.Type()))
            return nil
        }
        if v.IsNil() {
            v.Set(reflect.New(v.Type().Elem()))
        }
        return unmarshalValue(val, v.Elem())
    }

    // 3. Interface{} handling
    if v.Kind() == reflect.Interface && v.NumMethod() == 0 {
        v.Set(reflect.ValueOf(defaultGoValue(val)))
        return nil
    }

    // 4. Look up cached decoder
    dec := cachedDecoder(v.Type())
    return dec(val, v)
}
```

**Pointer allocation:**

```go
// For pointer destinations, allocate if nil before recursing:
if v.Kind() == reflect.Pointer {
    if val.Kind() == KindNull {
        v.Set(reflect.Zero(v.Type()))
        return nil
    }
    if v.IsNil() {
        v.Set(reflect.New(v.Type().Elem()))
    }
    return unmarshalValue(val, v.Elem())
}
```

**`interface{}` default type mapping:**

```go
func defaultGoValue(val Value) any {
    switch val.Kind() {
    case KindNull:
        return nil
    case KindBool:
        return val.BoolVal()
    case KindNumber:
        return val.Float64()
    case KindBigInt:
        bi := new(big.Int)
        bi.SetString(val.Str(), 10)
        return bi
    case KindString:
        return val.Str()
    case KindArray:
        elems := val.Array()
        result := make([]any, len(elems))
        for i, e := range elems {
            result[i] = defaultGoValue(e)
        }
        return result
    case KindObject:
        pairs := val.Object()
        result := make(map[string]any, len(pairs))
        for _, kv := range pairs {
            result[kv.Key] = defaultGoValue(kv.Value)
        }
        return result
    case KindDateTime:
        return val.Time()
    case KindTimeOnly:
        return val.TimeOnlyValue()
    case KindDuration:
        return Duration{ISO: val.Str()}
    case KindRegExp:
        return val.RegExpValue()
    case KindBinary:
        return val.Bytes()
    case KindMap:
        entries := val.Map()
        result := make([]MapEntry, len(entries))
        for i, e := range entries {
            result[i] = MapEntry{
                Key:   valueFromDefault(defaultGoValue(e.Key)),
                Value: valueFromDefault(defaultGoValue(e.Value)),
            }
        }
        return result
    case KindSet:
        elems := val.Array()
        result := make(Set[any], len(elems))
        for i, e := range elems {
            result[i] = defaultGoValue(e)
        }
        return result
    case KindTuple:
        elems := val.Array()
        result := make(Tuple, len(elems))
        for i, e := range elems {
            result[i] = defaultGoValue(e)
        }
        return result
    }
    return nil
}
```

**Number precision handling:**

```go
func intDecoder(val Value, v reflect.Value) error {
    switch val.Kind() {
    case KindNumber:
        f := val.Float64()
        n := int64(f)
        if float64(n) != f {
            return &UnmarshalTypeError{Value: "number " + strconv.FormatFloat(f, 'g', -1, 64), Type: v.Type()}
        }
        if v.OverflowInt(n) {
            return &UnmarshalTypeError{Value: "number " + strconv.FormatFloat(f, 'g', -1, 64), Type: v.Type()}
        }
        v.SetInt(n)
        return nil
    case KindBigInt:
        n, err := strconv.ParseInt(val.Str(), 10, 64)
        if err != nil {
            return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: v.Type()}
        }
        if v.OverflowInt(n) {
            return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: v.Type()}
        }
        v.SetInt(n)
        return nil
    }
    return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
}
```

### 6.3 tags.go

**Tag parsing:**

```go
// tagOptions is a string following the name in a struct tag (e.g. "omitempty,string").
type tagOptions string

// parseTag splits a struct field's "rdn" tag into a name and options.
func parseTag(tag string) (string, tagOptions) {
    idx := strings.IndexByte(tag, ',')
    if idx == -1 {
        return tag, ""
    }
    return tag[:idx], tagOptions(tag[idx+1:])
}

// Contains reports whether opts contains the named option.
func (o tagOptions) Contains(name string) bool {
    for o != "" {
        var opt string
        idx := strings.IndexByte(string(o), ',')
        if idx == -1 {
            opt, o = string(o), ""
        } else {
            opt, o = string(o[:idx]), o[idx+1:]
        }
        if opt == name {
            return true
        }
    }
    return false
}
```

**Struct field analysis (cached per type):**

```go
func cachedStructFields(t reflect.Type) *structFields {
    if f, ok := fieldCache.Load(t); ok {
        return f.(*structFields)
    }
    f := analyzeStructFields(t)
    fieldCache.Store(t, f)
    return f
}

func analyzeStructFields(t reflect.Type) *structFields {
    var fields []field
    var visit func(t reflect.Type, index []int, depth int)
    visit = func(t reflect.Type, index []int, depth int) {
        for i := 0; i < t.NumField(); i++ {
            sf := t.Field(i)
            // Skip unexported non-embedded fields
            if !sf.IsExported() && !sf.Anonymous {
                continue
            }
            tag := sf.Tag.Get("rdn")
            // If no rdn tag, fall back to json tag for compatibility
            if tag == "" {
                tag = sf.Tag.Get("json")
            }
            name, opts := parseTag(tag)
            if name == "-" && !opts.Contains("") {
                // "-" without trailing comma means skip
                // Check: tag == "-" exactly
                if tag == "-" {
                    continue
                }
            }
            // Handle anonymous (embedded) structs
            if sf.Anonymous {
                ft := sf.Type
                if ft.Kind() == reflect.Pointer {
                    ft = ft.Elem()
                }
                if ft.Kind() == reflect.Struct && name == "" {
                    // Promote embedded fields
                    visit(ft, append(append([]int{}, index...), i), depth+1)
                    continue
                }
            }
            if name == "" || name == "-" {
                if tag == "-," {
                    name = "-"
                } else if name == "" {
                    name = sf.Name
                } else {
                    continue // skip "-" tagged
                }
            }
            fieldIndex := append(append([]int{}, index...), i)
            fields = append(fields, field{
                name:      name,
                index:     fieldIndex,
                typ:       sf.Type,
                omitempty: opts.Contains("omitempty"),
                quoted:    opts.Contains("string"),
            })
        }
    }
    visit(t, nil, 0)

    // Resolve conflicts: same name at different depths, shallower wins.
    // Same depth → both excluded.
    seen := make(map[string]int) // name -> index in fields
    var resolved []field
    for _, f := range fields {
        if prev, ok := seen[f.name]; ok {
            prevDepth := len(resolved[prev].index)
            curDepth := len(f.index)
            if curDepth < prevDepth {
                resolved[prev] = f
            } else if curDepth == prevDepth {
                // Ambiguous: mark for removal by setting name to ""
                resolved[prev].name = ""
            }
            // curDepth > prevDepth: keep previous (shallower)
        } else {
            seen[f.name] = len(resolved)
            resolved = append(resolved, f)
        }
    }

    // Filter out ambiguous fields and build name index
    var final []field
    nameIdx := make(map[string]int)
    for _, f := range resolved {
        if f.name == "" {
            continue
        }
        nameIdx[f.name] = len(final)
        final = append(final, f)
    }

    return &structFields{list: final, nameIndex: nameIdx}
}
```

### 6.4 wrappers.go

```go
// Set[T] marshals to KindSet.
type Set[T any] []T

func (s Set[T]) MarshalRDN() (Value, error) {
    elems := make([]Value, len(s))
    for i, item := range s {
        v, err := MarshalValue(item)
        if err != nil {
            return Value{}, err
        }
        elems[i] = v
    }
    return SetVal(elems), nil
}

func (s *Set[T]) UnmarshalRDN(v Value) error {
    if v.Kind() != KindSet {
        return &UnmarshalTypeError{Value: v.Kind().String(), Type: reflect.TypeOf(s).Elem()}
    }
    arr := v.Array()
    result := make(Set[T], len(arr))
    for i, elem := range arr {
        if err := UnmarshalValue(elem, &result[i]); err != nil {
            return err
        }
    }
    *s = result
    return nil
}

// Tuple marshals to KindTuple.
type Tuple []any

func (t Tuple) MarshalRDN() (Value, error) {
    elems := make([]Value, len(t))
    for i, item := range t {
        v, err := MarshalValue(item)
        if err != nil {
            return Value{}, err
        }
        elems[i] = v
    }
    return TupleVal(elems), nil
}

func (t *Tuple) UnmarshalRDN(v Value) error {
    if v.Kind() != KindTuple {
        return &UnmarshalTypeError{Value: v.Kind().String(), Type: reflect.TypeOf(t).Elem()}
    }
    arr := v.Array()
    result := make(Tuple, len(arr))
    for i, elem := range arr {
        result[i] = defaultGoValue(elem)
    }
    *t = result
    return nil
}

// OrderedMap[K, V] marshals to KindMap.
type OrderedMapEntry[K comparable, V any] struct {
    Key   K
    Value V
}

type OrderedMap[K comparable, V any] struct {
    entries []OrderedMapEntry[K, V]
}

func (m OrderedMap[K, V]) Entries() []OrderedMapEntry[K, V] { return m.entries }
func (m OrderedMap[K, V]) Len() int                         { return len(m.entries) }

func (m *OrderedMap[K, V]) Set(key K, value V) {
    for i := range m.entries {
        if m.entries[i].Key == key {
            m.entries[i].Value = value
            return
        }
    }
    m.entries = append(m.entries, OrderedMapEntry[K, V]{Key: key, Value: value})
}

func (m OrderedMap[K, V]) Get(key K) (V, bool) {
    for _, e := range m.entries {
        if e.Key == key {
            return e.Value, true
        }
    }
    var zero V
    return zero, false
}

func (m OrderedMap[K, V]) MarshalRDN() (Value, error) {
    entries := make([]MapEntry, len(m.entries))
    for i, e := range m.entries {
        k, err := MarshalValue(e.Key)
        if err != nil {
            return Value{}, err
        }
        v, err := MarshalValue(e.Value)
        if err != nil {
            return Value{}, err
        }
        entries[i] = MapEntry{Key: k, Value: v}
    }
    return MapVal(entries), nil
}

func (m *OrderedMap[K, V]) UnmarshalRDN(v Value) error {
    if v.Kind() != KindMap {
        return &UnmarshalTypeError{Value: v.Kind().String(), Type: reflect.TypeOf(m).Elem()}
    }
    mapEntries := v.Map()
    m.entries = make([]OrderedMapEntry[K, V], len(mapEntries))
    for i, me := range mapEntries {
        if err := UnmarshalValue(me.Key, &m.entries[i].Key); err != nil {
            return err
        }
        if err := UnmarshalValue(me.Value, &m.entries[i].Value); err != nil {
            return err
        }
    }
    return nil
}
```

### 6.5 errors.go (modifications)

Add the three new error types after the existing `SyntaxError`:

```go
// MarshalError describes an error that occurred during marshaling.
type MarshalError struct {
    Type reflect.Type
    Err  error
}

func (e *MarshalError) Error() string {
    return "rdn: error marshaling type " + e.Type.String() + ": " + e.Err.Error()
}

func (e *MarshalError) Unwrap() error { return e.Err }

// UnmarshalTypeError describes a type mismatch during unmarshaling.
type UnmarshalTypeError struct {
    Value  string
    Type   reflect.Type
    Struct string
    Field  string
}

func (e *UnmarshalTypeError) Error() string {
    if e.Struct != "" {
        return "rdn: cannot unmarshal " + e.Value + " into Go struct field " + e.Struct + "." + e.Field + " of type " + e.Type.String()
    }
    return "rdn: cannot unmarshal " + e.Value + " into Go value of type " + e.Type.String()
}

// InvalidUnmarshalError describes an invalid argument to Unmarshal.
type InvalidUnmarshalError struct {
    Type reflect.Type
}

func (e *InvalidUnmarshalError) Error() string {
    if e.Type == nil {
        return "rdn: Unmarshal(nil)"
    }
    if e.Type.Kind() != reflect.Pointer {
        return "rdn: Unmarshal(non-pointer " + e.Type.String() + ")"
    }
    return "rdn: Unmarshal(nil " + e.Type.String() + ")"
}
```

The `errors.go` file will need a new import for `reflect`.

### 6.6 stream.go (modifications)

Add two new methods to the existing `Encoder` and `Decoder`:

```go
// EncodeValue marshals v into an rdn.Value and writes it to the stream.
func (enc *Encoder) EncodeValue(v any) error {
    val, err := MarshalValue(v)
    if err != nil {
        return err
    }
    return enc.Encode(val)
}

// DecodeValue reads a Value from the stream and unmarshals it into v.
func (dec *Decoder) DecodeValue(v any) error {
    var val Value
    if err := dec.Decode(&val); err != nil {
        return err
    }
    return UnmarshalValue(val, v)
}
```

### 6.7 README.md (documentation updates)

**New section after "Streaming" and before "HTTP Support":**

````markdown
### Marshal / Unmarshal

Reflection-based serialization mirroring `encoding/json`:

```go
type User struct {
    Name    string    `rdn:"name"`
    Created time.Time `rdn:"created"`
    Score   int       `rdn:"score,omitempty"`
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

// Work with Value directly (no serialization)
val, err := rdn.MarshalValue(user)  // Go → Value
err = rdn.UnmarshalValue(val, &user) // Value → Go
```

**Struct tags:** `rdn:"name,omitempty"` — same syntax as `encoding/json`. Supports `omitempty` and `string` options. Falls back to `json` tags if no `rdn` tag is present.

**Custom types:** Implement `rdn.Marshaler` / `rdn.Unmarshaler` for full control:

```go
type Marshaler interface {
    MarshalRDN() (Value, error)
}
type Unmarshaler interface {
    UnmarshalRDN(Value) error
}
```

**Wrapper types** for RDN-only collections:

| Type | Marshals to | Description |
|------|-------------|-------------|
| `Set[T]` | `Set{...}` | Ordered set of homogeneous values |
| `Tuple` | `(...)` | Heterogeneous tuple (`[]any` under the hood) |
| `OrderedMap[K,V]` | `Map{k => v}` | Insertion-ordered map with any key type |
```
````

**Update "Streaming" section** to add `EncodeValue`/`DecodeValue` example:

````markdown
```go
// Encode/Decode Go values directly (combines Marshal + stream)
enc := rdn.NewEncoder(writer)
enc.EncodeValue(myStruct)  // Go value → RDN bytes

dec := rdn.NewDecoder(reader)
dec.DecodeValue(&myStruct) // RDN bytes → Go value
```
````

**Update "Errors" section** to document new error types.

**Remove "Roadmap" section** items that are now implemented.

## 7. Type Mapping Tables

### 7.1 Go -> RDN (Marshal)

| Go Type | RDN ValueKind | Notes |
|---------|---------------|-------|
| `nil` | `KindNull` | |
| `bool` | `KindBool` | |
| `int`, `int8`, `int16`, `int32`, `int64` | `KindNumber` | BigInt if abs > 2^53 |
| `uint`, `uint8`, `uint16`, `uint32`, `uint64` | `KindNumber` | BigInt if > 2^53 |
| `float32`, `float64` | `KindNumber` | NaN and Inf preserved |
| `string` | `KindString` | |
| `[]byte` | `KindBinary` | base64 encoded |
| `*big.Int` | `KindBigInt` | nil -> KindNull |
| `time.Time` | `KindDateTime` | |
| `rdn.TimeOnly` | `KindTimeOnly` | |
| `rdn.Duration` | `KindDuration` | |
| `rdn.RegExp` | `KindRegExp` | |
| `rdn.Number` | `KindNumber` or `KindBigInt` | Parsed from string |
| `rdn.RawMessage` | (parsed then embedded) | Validates the RDN |
| `rdn.Value` | (passthrough) | |
| `rdn.Set[T]` | `KindSet` | Via Marshaler |
| `rdn.Tuple` | `KindTuple` | Via Marshaler |
| `rdn.OrderedMap[K,V]` | `KindMap` | Via Marshaler |
| `[]T` | `KindArray` | nil -> KindNull |
| `[N]T` | `KindArray` | |
| `map[string]V` | `KindObject` | nil -> KindNull, keys sorted |
| `map[K]V` (non-string K) | `KindMap` | nil -> KindNull |
| `struct` | `KindObject` | Field tags respected |
| `*T` | (deref) | nil -> KindNull |
| `Marshaler` | (custom) | Highest priority |
| `encoding.TextMarshaler` | `KindString` | Fallback |

### 7.2 RDN -> Go (Unmarshal, typed destination)

| RDN ValueKind | Target Go Type | Behavior |
|---------------|----------------|----------|
| `KindNull` | any pointer | Set to `nil` |
| `KindNull` | `interface{}` | Set to `nil` |
| `KindNull` | slice, map | Set to `nil` |
| `KindBool` | `bool` | Direct |
| `KindNumber` | `float64`, `float32` | Direct |
| `KindNumber` | `int`, `int8`..`int64` | Truncate; error if not representable |
| `KindNumber` | `uint`, `uint8`..`uint64` | Truncate; error if negative/overflow |
| `KindBigInt` | `*big.Int` | Parse digit string |
| `KindBigInt` | `int64`, `uint64` | Parse; error if overflow |
| `KindString` | `string` | Direct |
| `KindString` | `encoding.TextUnmarshaler` | Call `UnmarshalText()` |
| `KindArray` | `[]T` | Recursively unmarshal |
| `KindTuple` | `[]T` | Same as array |
| `KindTuple` | `[N]T` | Error if length mismatch |
| `KindSet` | `[]T` | Same as array |
| `KindObject` | `struct` | Match keys to fields/tags |
| `KindObject` | `map[string]V` | Populate map |
| `KindMap` | `map[K]V` | Recursively unmarshal keys and values |
| `KindDateTime` | `time.Time` | Direct |
| `KindTimeOnly` | `rdn.TimeOnly` | Direct |
| `KindDuration` | `rdn.Duration` | Direct |
| `KindRegExp` | `rdn.RegExp` | Direct |
| `KindBinary` | `[]byte` | Direct (copy) |
| Any | `rdn.Value` | Assign directly |
| Any | `rdn.RawMessage` | Re-stringify to bytes |
| Any | `rdn.Number` | Convert to string form |
| Any | `Unmarshaler` | Call `UnmarshalRDN()` |

### 7.3 RDN -> Go (Unmarshal, `interface{}`)

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
| `KindMap` | `[]rdn.MapEntry` |
| `KindSet` | `rdn.Set[any]` |
| `KindTuple` | `rdn.Tuple` |

## 8. Edge Cases & Error Handling

| Edge Case | Expected Behavior |
|-----------|-------------------|
| Circular reference (pointer cycle) | Return `*MarshalError` with "circular reference detected" |
| `nil` pointer | Marshal as `KindNull` |
| `nil` slice | Marshal as `KindNull` (not empty array) |
| `nil` map | Marshal as `KindNull` (not empty object) |
| Empty slice (`[]int{}`) | Marshal as empty `KindArray` |
| Empty map (`map[string]int{}`) | Marshal as empty `KindObject` |
| `uint64` > 2^53 | Marshal as `KindBigInt` |
| `int64` abs > 2^53 | Marshal as `KindBigInt` |
| KindNumber -> `int8` overflow | Return `*UnmarshalTypeError` |
| KindNumber -> `uint64` negative | Return `*UnmarshalTypeError` |
| KindNumber -> `int64` non-integer (3.5) | Return `*UnmarshalTypeError` |
| KindBigInt -> `int64` overflow | Return `*UnmarshalTypeError` |
| Unmarshal into nil pointer | Return `*InvalidUnmarshalError` |
| Unmarshal into non-pointer | Return `*InvalidUnmarshalError` |
| Unknown struct fields on unmarshal | Silently ignored |
| Unexported struct fields | Skipped on both marshal and unmarshal |
| Embedded struct fields | Promoted (flattened) following `encoding/json` rules |
| Embedded pointer struct (nil) | Allocate on unmarshal; skip on marshal if nil + omitempty |
| Conflicting promoted field names (same depth) | Both excluded |
| Conflicting promoted field names (different depth) | Shallower wins |
| `rdn.RawMessage` marshal | Parse bytes to Value, embed as-is; error if invalid RDN |
| `rdn.RawMessage` unmarshal | Re-stringify the Value to bytes |
| `rdn.Number` marshal | Parse string: if integer, KindNumber or KindBigInt; if float, KindNumber |
| `rdn.Value` marshal | Passthrough (no conversion) |
| `rdn.Value` unmarshal | Assign directly (no conversion) |
| `encoding.TextMarshaler` | Call `MarshalText()`, wrap result as KindString |
| `encoding.TextUnmarshaler` | Only called if incoming value is KindString |
| `",string"` tag on non-numeric/bool field | Ignored (only applies to bool, int, uint, float, string) |
| Unsupported type (chan, func, complex) | Return `*MarshalError` with "unsupported type" |
| KindTuple -> `[3]int` with 4 elements | Return `*UnmarshalTypeError` (length mismatch) |
| Map with non-string, non-`TextMarshaler` keys | Marshal as KindMap (keys marshaled via reflection) |
| `json` tag fallback | If field has no `rdn` tag, use `json` tag (name and options) |

## 9. Testing Strategy

### marshal_test.go

- **Primitives**: bool, int variants, uint variants, float32/64, string, nil
- **Special numbers**: NaN, Infinity, -Infinity, -0, uint64 > 2^53, int64 abs > 2^53
- **Special types**: `time.Time`, `*big.Int`, `TimeOnly`, `Duration`, `RegExp`, `Number`, `RawMessage`, `Value`, `[]byte`
- **Collections**: slices, arrays, `map[string]V`, `map[int]V`, nested structs
- **Struct tags**: custom names, `-`, omitempty (all zero types), `string` option, no tag, `json` fallback
- **Embedded structs**: promotion, pointer embedding, conflict resolution
- **Interfaces**: `Marshaler`, `TextMarshaler`, precedence
- **Wrapper types**: `Set[string]`, `Set[int]`, `Tuple`, `OrderedMap[string,int]`, `OrderedMap[int,string]`
- **Nil handling**: nil pointer, nil slice, nil map, nil interface
- **Empty collections**: empty slice vs nil slice, empty map vs nil map
- **Circular references**: self-referencing pointer struct
- **Errors**: unsupported types (chan, func), circular reference
- **Determinism**: map key sorting

### unmarshal_test.go

- **Primitives**: KindBool->bool, KindNumber->float/int/uint, KindString->string, KindNull->pointer
- **Number precision**: KindNumber->int8 overflow, KindNumber->uint negative, KindBigInt->int64 overflow, KindBigInt->*big.Int
- **Special types**: KindDateTime->time.Time, KindTimeOnly->TimeOnly, KindDuration->Duration, KindRegExp->RegExp, KindBinary->[]byte, KindBigInt->*big.Int
- **Collections**: KindArray->[]T, KindTuple->[]T, KindTuple->[N]T (match and mismatch), KindSet->[]T, KindObject->struct, KindObject->map[string]V, KindMap->map[K]V
- **interface{} defaults**: each ValueKind into `any`, verify correct default types
- **Struct tags**: matching by name, omitempty (irrelevant for unmarshal), `-` skip, embedded promotion
- **Interfaces**: `Unmarshaler`, `TextUnmarshaler`, precedence
- **Wrapper types**: unmarshal into `Set[string]`, `Tuple`, `OrderedMap[string,int]`
- **Pointer allocation**: nil pointer target gets allocated
- **Errors**: `InvalidUnmarshalError` (nil, non-pointer), `UnmarshalTypeError` (type mismatch)
- **Value/RawMessage**: unmarshal any kind into `Value`, unmarshal into `RawMessage`

### wrappers_test.go

- `Set[T]` round-trip (marshal + unmarshal)
- `Tuple` round-trip with heterogeneous values
- `OrderedMap[K,V]` round-trip, `Set`/`Get`/`Len`/`Entries` methods
- Error cases: wrong kind for each wrapper

### tags_test.go

- `parseTag` function: name only, name+omitempty, omitempty only, `-`, `-,`
- `analyzeStructFields`: simple struct, embedded struct, pointer-embedded, conflict resolution, json fallback
- Field name index correctness

## 10. Performance

### Caching Strategy

- `encoderCache` (`sync.Map[reflect.Type]encoderFunc`): lock-free reads after first use of a type. First call incurs reflection cost to build the encoder function, then O(1) lookups.
- `decoderCache` (`sync.Map[reflect.Type]decoderFunc`): same pattern.
- `fieldCache` (`sync.Map[reflect.Type]*structFields`): struct field analysis is the most expensive operation; cached per struct type.
- All caches are process-global and never evicted (same as `encoding/json`).

### Allocation Targets

- **Marshal**: one `marshalState` per call (map allocation for cycle detection). For cycle-free value trees without pointers/maps/slices-of-pointers, could short-circuit to a stateless path in the future. The `Stringify` step reuses pooled `encodeState` buffers.
- **Unmarshal**: the `Parse` step already uses pooled scratch buffers. The `unmarshalValue` step allocates only what the target requires (slice backing arrays, map entries, struct fields).
- **Pre-allocate slice capacity**: use `Value.Len()` when unmarshaling arrays/sets/tuples into slices.

### Benchmark Plan

Add to the existing benchmark suite:

| Benchmark | Description |
|-----------|-------------|
| `BenchmarkMarshalPrimitives` | Struct with bool, int, float, string fields |
| `BenchmarkMarshalNested` | Struct with nested structs, slices, maps |
| `BenchmarkMarshalLargeSlice` | 1000-element `[]int` |
| `BenchmarkUnmarshalPrimitives` | Inverse of MarshalPrimitives |
| `BenchmarkUnmarshalNested` | Inverse of MarshalNested |
| `BenchmarkUnmarshalLargeSlice` | 1000-element array into `[]int` |
| `BenchmarkMarshalValue` | `MarshalValue` only (no serialization) |
| `BenchmarkUnmarshalValue` | `UnmarshalValue` only (no parsing) |

Target: within 2x of `encoding/json.Marshal`/`Unmarshal` for JSON-compatible data.

## 11. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Two-pass overhead (Value intermediate) | ~30-50% slower than single-pass for large payloads | Acceptable for v1; single-pass optimization can be added later as a non-breaking change |
| Reflection complexity for embedded structs | Subtle bugs in field conflict resolution | Port conflict resolution logic directly from `encoding/json` source; comprehensive test cases |
| Generics constraints on wrappers | `OrderedMap` requires `comparable` keys which excludes slices/maps as keys | Document limitation; users can use `Marshaler` interface for exotic key types |
| `sync.Map` memory growth | Caches grow unbounded for programs using many distinct types | Same behavior as `encoding/json`; not a concern for typical programs |
| `unsafe.Pointer` in cycle detection | Potential for false positives if GC moves objects | Go's GC does not move objects; `unsafe.Pointer` from `reflect.Value.Pointer()` is stable for the lifetime of the value |
| `json` tag fallback | Could cause surprise behavior if user expects different names from json vs rdn | Document clearly; `rdn` tag always takes priority; fallback is opt-in by absence |

## 12. Ordered Task List

1. **Add error types to `errors.go`**
   - Add `MarshalError`, `UnmarshalTypeError`, `InvalidUnmarshalError` to `errors.go`
   - Add `import "reflect"` to `errors.go`
   - Tests: verify error messages for each type in `errors_test.go`

2. **Implement struct tag parsing in `tags.go`**
   - Create `tags.go` with `parseTag`, `tagOptions.Contains`, `analyzeStructFields`, `cachedStructFields`
   - Handle: name extraction, omitempty, string option, `-` skip, `-,` literal, embedded promotion, conflict resolution, `json` fallback
   - Tests: `tags_test.go` covering all tag variations and embedded struct scenarios

3. **Implement `marshal.go` (MarshalValue + Marshal + MarshalIndent)**
   - Create `marshal.go` with `Marshaler` interface, `marshalState`, `marshalValue`, encoder cache, all type-specific encoders
   - Handle: nil, interface/pointer deref, cycle detection, all Go types, special RDN types, TextMarshaler, struct encoding via cached fields, map key sorting, uint64/int64 BigInt overflow
   - Tests: `marshal_test.go` covering all Go types, struct tags, edge cases, errors

4. **Implement `unmarshal.go` (UnmarshalValue + Unmarshal)**
   - Create `unmarshal.go` with `Unmarshaler` interface, `unmarshalValue`, decoder cache, all type-specific decoders, `defaultGoValue`
   - Handle: InvalidUnmarshalError, pointer allocation, interface{} defaults, all ValueKind->Go type combinations, TextUnmarshaler, struct field matching, number precision
   - Tests: `unmarshal_test.go` covering all type combinations, edge cases, errors

5. **Implement wrapper types in `wrappers.go`**
   - Create `wrappers.go` with `Set[T]`, `Tuple`, `OrderedMap[K,V]`, `OrderedMapEntry[K,V]`
   - Implement `MarshalRDN` and `UnmarshalRDN` on each, plus `OrderedMap` utility methods
   - Tests: `wrappers_test.go` covering round-trips, methods, error cases

6. **Add stream methods to `stream.go`**
   - Add `EncodeValue(v any)` to `Encoder` and `DecodeValue(v any)` to `Decoder`
   - Tests: add `TestEncoderEncodeValue` and `TestDecoderDecodeValue` to `stream_test.go`

7. **Add top-level functions to `rdn.go`**
   - Add `Marshal`, `MarshalIndent`, `MarshalValue`, `Unmarshal`, `UnmarshalValue` to `rdn.go`
   - These are thin wrappers that delegate to `marshal.go` and `unmarshal.go`
   - Tests: add roundtrip tests using `Marshal`/`Unmarshal` to `rdn_test.go`

8. **Update README.md**
   - Add Marshal/Unmarshal section with struct example, tag docs, interface docs, wrapper type table
   - Update Streaming section with `EncodeValue`/`DecodeValue` example
   - Update Errors section with new error types
   - Remove completed Roadmap items
