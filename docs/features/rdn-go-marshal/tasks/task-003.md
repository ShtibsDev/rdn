# Task 3: Marshal Implementation

**Status:** pending
**File(s):** `packages/rdn-go/marshal.go` (new), `packages/rdn-go/marshal_test.go` (new)
**Depends on:** task-001 (errors), task-002 (tags)
**Blocks:** tasks 5, 6, 7

## Description

Create `marshal.go` with the full marshaling pipeline: Go values → `Value` → `[]byte`.

### Public API

```go
type Marshaler interface {
    MarshalRDN() (Value, error)
}

func MarshalValue(v any) (Value, error)
func Marshal(v any) ([]byte, error)
func MarshalIndent(v any, prefix, indent string) ([]byte, error)
```

### Internal Architecture

**marshalState** — per-call state for cycle detection:
```go
type marshalState struct {
    visited map[unsafe.Pointer]bool
}
```

**encoderFunc** — cached per `reflect.Type`:
```go
type encoderFunc func(ms *marshalState, v reflect.Value) (Value, error)
var encoderCache sync.Map // map[reflect.Type]encoderFunc
```

### Type Dispatch Order (in marshalValue)
1. Check nil → `Null()`
2. Dereference pointers/interfaces
3. Check `rdn.Marshaler` → call `MarshalRDN()`
4. Check special types: `rdn.Value`, `rdn.RawMessage`, `time.Time`, `*big.Int`, `rdn.TimeOnly`, `rdn.Duration`, `rdn.RegExp`, `rdn.Number`, `[]byte`
5. Check `encoding.TextMarshaler` → `StringVal(text)`
6. `reflect.Kind` switch: Bool, Int*, Uint*, Float*, String, Slice, Array, Map, Struct, Ptr, Interface

### Key Implementation Details
- **Cycle detection**: Track pointer addresses in `marshalState.visited` for Ptr, Map, Slice kinds. Return `MarshalError` on cycle.
- **uint64/int64 BigInt overflow**: If `|value| > 1<<53`, marshal as `BigIntVal` instead of `NumberVal`
- **Map key sorting**: For `map[string]V`, sort keys with `sort.Strings`. For `map[K]V` where K implements `encoding.TextMarshaler` or is an integer type, sort lexicographically by the string representation.
- **Struct encoding**: Use `cachedStructFields(t)`, iterate fields, skip omitempty zeros, apply quoted option
- **Encoder caching**: `newEncoder(t reflect.Type) encoderFunc` builds and caches per-type encoder functions

## Acceptance Criteria
- [ ] `MarshalValue` correctly converts all Go primitive types
- [ ] Struct marshaling respects `rdn` tags (name, omitempty, string, skip)
- [ ] `Marshaler` interface is checked (including pointer receivers)
- [ ] `encoding.TextMarshaler` fallback works
- [ ] `time.Time` → DateTime, `*big.Int` → BigInt, `[]byte` → Binary
- [ ] `map[string]V` → Object with sorted keys; `map[K]V` → Map
- [ ] nil pointer/slice/map → null
- [ ] Circular reference detection returns `MarshalError`
- [ ] uint64 > 2^53 → BigInt
- [ ] `Marshal` produces valid RDN bytes
- [ ] `MarshalIndent` produces indented output
- [ ] `marshal_test.go` covers all types, edge cases, errors, struct tags
- [ ] `go test ./...` passes

## References
- [tech-design.md §6.1](../tech-design.md) — marshal.go implementation details
- [tech-design.md §7.1](../tech-design.md) — Go → RDN type mapping table
- [tech-design.md §8](../tech-design.md) — Edge cases
