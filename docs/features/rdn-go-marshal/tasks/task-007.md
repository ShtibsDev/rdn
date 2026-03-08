# Task 7: Top-Level Functions in rdn.go

**Status:** pending
**File(s):** `packages/rdn-go/rdn.go`, `packages/rdn-go/rdn_test.go`
**Depends on:** task-003 (marshal), task-004 (unmarshal)
**Blocks:** nothing

## Description

Add the top-level convenience functions to `rdn.go`. These are thin wrappers that compose `MarshalValue`/`UnmarshalValue` with `Stringify`/`Parse`.

### Functions to Add

```go
// Marshal returns the RDN encoding of v.
func Marshal(v any) ([]byte, error) {
    val, err := MarshalValue(v)
    if err != nil {
        return nil, err
    }
    return Stringify(val)
}

// MarshalIndent is like Marshal but applies indentation.
func MarshalIndent(v any, prefix, indent string) ([]byte, error) {
    val, err := MarshalValue(v)
    if err != nil {
        return nil, err
    }
    return StringifyIndent(val, prefix, indent)
}

// Unmarshal parses the RDN-encoded data and stores the result in the value pointed to by v.
func Unmarshal(data []byte, v any) error {
    val, err := Parse(data)
    if err != nil {
        return err
    }
    return UnmarshalValue(val, v)
}
```

**Note:** `MarshalValue` and `UnmarshalValue` are already defined in `marshal.go`/`unmarshal.go`. The functions in `rdn.go` are `Marshal`, `MarshalIndent`, and `Unmarshal` — the top-level entry points that include the serialization/parsing step.

If `Marshal`/`MarshalIndent`/`Unmarshal` were already defined in `marshal.go`/`unmarshal.go` during task 3/4, then this task just adds roundtrip integration tests to `rdn_test.go`.

### Integration Tests

Add roundtrip tests that exercise the full pipeline (`Marshal` → bytes → `Unmarshal`):
- Struct with various field types (string, int, bool, time.Time, *big.Int, []byte)
- Nested struct with slices and maps
- Wrapper types (Set, Tuple, OrderedMap) through Marshal/Unmarshal
- nil values
- `interface{}` destination

## Acceptance Criteria
- [ ] `Marshal`/`MarshalIndent`/`Unmarshal` are exported from the package
- [ ] Full roundtrip: `Marshal(v)` → `Unmarshal(data, &v2)` → `v == v2`
- [ ] Roundtrip tests added to `rdn_test.go`
- [ ] `go test ./...` passes

## References
- [tech-design.md §3.1](../tech-design.md) — Public API
