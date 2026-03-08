# Task 4: Unmarshal Implementation

**Status:** pending
**File(s):** `packages/rdn-go/unmarshal.go` (new), `packages/rdn-go/unmarshal_test.go` (new)
**Depends on:** task-001 (errors), task-002 (tags)
**Blocks:** tasks 5, 6, 7

## Description

Create `unmarshal.go` with the full unmarshaling pipeline: `[]byte` → `Value` → Go values.

### Public API

```go
type Unmarshaler interface {
    UnmarshalRDN(Value) error
}

func UnmarshalValue(val Value, v any) error
func Unmarshal(data []byte, v any) error
```

### Internal Architecture

**decoderFunc** — cached per `reflect.Type`:
```go
type decoderFunc func(val Value, v reflect.Value) error
var decoderCache sync.Map // map[reflect.Type]decoderFunc
```

### Type Dispatch Order (in unmarshalValue)
1. Validate target: must be non-nil pointer → `InvalidUnmarshalError`
2. Check `rdn.Unmarshaler` → call `UnmarshalRDN(val)`
3. Check special types: `rdn.Value` (assign directly), `rdn.RawMessage` (re-stringify), `time.Time`, `*big.Int`, `rdn.TimeOnly`, `rdn.Duration`, `rdn.RegExp`
4. Handle pointer targets: KindNull → set nil; otherwise allocate and recurse
5. Handle `interface{}` targets → `defaultGoValue(val)` (see §4.2 mapping)
6. `Value.Kind()` switch with target `reflect.Kind` validation

### defaultGoValue Mapping (into interface{})
| RDN Kind | Go Default |
|----------|-----------|
| KindNull | nil |
| KindBool | bool |
| KindNumber | float64 |
| KindBigInt | *big.Int |
| KindString | string |
| KindArray | []any (recursive) |
| KindObject | map[string]any (recursive) |
| KindDateTime | time.Time |
| KindTimeOnly | rdn.TimeOnly |
| KindDuration | rdn.Duration |
| KindRegExp | rdn.RegExp |
| KindBinary | []byte |
| KindMap | []rdn.MapEntry (preserves order, avoids unhashable key panic) |
| KindSet | rdn.Set[any] |
| KindTuple | rdn.Tuple |

### Key Implementation Details
- **Number precision**: Unmarshal KindNumber into int types → truncate, check `float64(int64(f)) != f` for precision loss. Return `UnmarshalTypeError` if not representable.
- **BigInt into int64/uint64**: Parse digit string, check overflow, return error if too large.
- **Struct field matching**: Use `cachedStructFields(t).nameIndex` for O(1) lookup by key name. Unknown fields are silently ignored.
- **Slice pre-allocation**: `make([]T, 0, val.Len())` using Value's length hint.
- **KindTuple into [N]T**: Verify length matches array size, error if mismatch.
- **TextUnmarshaler**: For KindString values, check if target implements `encoding.TextUnmarshaler`.

## Acceptance Criteria
- [ ] `Unmarshal` into `any`/`interface{}` uses correct default types for all 15 ValueKinds
- [ ] `Unmarshal` into typed structs matches fields by tag/name
- [ ] `Unmarshaler` interface is checked (including pointer receivers)
- [ ] `encoding.TextUnmarshaler` fallback works for string values
- [ ] Number precision validation (float→int, BigInt→int64 overflow)
- [ ] Pointer allocation for non-nil values, nil for KindNull
- [ ] `InvalidUnmarshalError` for nil/non-pointer targets
- [ ] `UnmarshalTypeError` for type mismatches
- [ ] KindSet → `Set[any]`, KindTuple → `Tuple`, KindMap → `[]MapEntry` (for interface{})
- [ ] Unknown struct fields silently ignored
- [ ] `unmarshal_test.go` covers all type combinations, edge cases, errors
- [ ] `go test ./...` passes

## References
- [tech-design.md §6.2](../tech-design.md) — unmarshal.go implementation details
- [tech-design.md §7.2, §7.3](../tech-design.md) — RDN → Go type mapping tables
- [tech-design.md §8](../tech-design.md) — Edge cases
