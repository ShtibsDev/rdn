# Task 1: Add Error Types

**Status:** pending
**File(s):** `packages/rdn-go/errors.go`, `packages/rdn-go/errors_test.go` (new)
**Depends on:** nothing
**Blocks:** tasks 3, 4

## Description

Add three new error types to `errors.go` for the Marshal/Unmarshal layer:

### MarshalError
Returned when a value cannot be marshaled (e.g., unsupported type, circular reference).

```go
type MarshalError struct {
    Type    reflect.Type
    Message string
}
func (e *MarshalError) Error() string {
    return "rdn: cannot marshal " + e.Type.String() + ": " + e.Message
}
```

### UnmarshalTypeError
Returned when a Value cannot be stored into a Go type (type mismatch).

```go
type UnmarshalTypeError struct {
    Value  string       // description of the RDN value ("number", "string", etc.)
    Type   reflect.Type // Go type it could not be assigned to
    Offset int64        // byte offset (0 if from UnmarshalValue)
    Struct string       // struct type name, if applicable
    Field  string       // struct field name, if applicable
}
func (e *UnmarshalTypeError) Error() string // format: "rdn: cannot unmarshal <Value> into Go value of type <Type>"
```

### InvalidUnmarshalError
Returned when Unmarshal is called with a nil or non-pointer argument.

```go
type InvalidUnmarshalError struct {
    Type reflect.Type
}
func (e *InvalidUnmarshalError) Error() string // "rdn: Unmarshal(nil)" or "rdn: Unmarshal(non-pointer <Type>)"
```

## Acceptance Criteria
- [ ] All three error types added to `errors.go` with `import "reflect"`
- [ ] Each implements the `error` interface with descriptive messages
- [ ] `errors_test.go` verifies error messages for each type (nil type, pointer, struct context)
- [ ] `go test ./...` passes

## References
- [tech-design.md §3.3](../tech-design.md) — Error type definitions
- [tech-design.md §6.5](../tech-design.md) — Modifications to errors.go
