# Task 8: README Documentation

**Status:** pending
**File(s):** `packages/rdn-go/README.md`
**Depends on:** tasks 1-7 (all code complete)
**Blocks:** nothing

## Description

Update `README.md` to document the new Marshal/Unmarshal API, struct tags, interfaces, wrapper types, and error types.

### Sections to Add/Update

1. **Marshal / Unmarshal section** (new, after Streaming section):
   - Basic struct example with `Marshal` and `Unmarshal`
   - `MarshalValue`/`UnmarshalValue` for working with `Value` directly
   - `MarshalIndent` for pretty output

2. **Struct Tags section** (new):
   - Tag format: `rdn:"name,omitempty"`
   - All options: name, `-`, omitempty, string
   - `json` tag fallback behavior
   - Example struct with various tags

3. **Custom Marshaling section** (new):
   - `Marshaler` / `Unmarshaler` interfaces
   - Example implementing both
   - `encoding.TextMarshaler` fallback note

4. **Wrapper Types section** (new):
   - `Set[T]` — usage example
   - `Tuple` — usage example
   - `OrderedMap[K,V]` — usage example with utility methods

5. **Streaming section** (update):
   - Add `EncodeValue`/`DecodeValue` examples for direct struct streaming

6. **Errors section** (update):
   - Add `MarshalError`, `UnmarshalTypeError`, `InvalidUnmarshalError`

7. **Type Mapping table** (new):
   - Go → RDN direction
   - RDN → Go direction (typed + interface{})

8. **Roadmap** (update):
   - Remove completed items (Marshal/Unmarshal, struct tags, interfaces)
   - Add any future items if applicable (e.g., DisallowUnknownFields, single-pass optimization)

## Acceptance Criteria
- [ ] All new API functions documented with examples
- [ ] Struct tag syntax fully documented
- [ ] Wrapper types documented with examples
- [ ] Type mapping tables present
- [ ] Roadmap updated (completed items removed)
- [ ] Examples are copy-pasteable and correct

## References
- [tech-design.md §6.7](../tech-design.md) — README updates
- [discovery.md §2.3](../discovery.md) — Current API surface for context
