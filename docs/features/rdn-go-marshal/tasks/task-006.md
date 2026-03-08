# Task 6: Stream Methods

**Status:** pending
**File(s):** `packages/rdn-go/stream.go`, `packages/rdn-go/stream_test.go`
**Depends on:** task-003 (marshal), task-004 (unmarshal)
**Blocks:** nothing

## Description

Add `EncodeValue` and `DecodeValue` methods to the existing `Encoder` and `Decoder` types in `stream.go`.

### Encoder Addition

```go
// EncodeValue marshals v to a Value and writes it as RDN to the stream.
func (enc *Encoder) EncodeValue(v any) error {
    val, err := MarshalValue(v)
    if err != nil {
        return err
    }
    return enc.Encode(val)
}
```

### Decoder Addition

```go
// DecodeValue reads a single RDN value from the stream and stores it
// in the Go value pointed to by v.
func (dec *Decoder) DecodeValue(v any) error {
    var val Value
    if err := dec.Decode(&val); err != nil {
        return err
    }
    return UnmarshalValue(val, v)
}
```

## Acceptance Criteria
- [ ] `Encoder.EncodeValue(myStruct)` writes valid RDN to the writer
- [ ] `Decoder.DecodeValue(&myStruct)` reads RDN and populates the struct
- [ ] Indentation settings on Encoder are respected by EncodeValue
- [ ] Error propagation from MarshalValue/UnmarshalValue works correctly
- [ ] Tests added to `stream_test.go`: `TestEncoderEncodeValue`, `TestDecoderDecodeValue`
- [ ] `go test ./...` passes

## References
- [tech-design.md §6.6](../tech-design.md) — Stream modifications
