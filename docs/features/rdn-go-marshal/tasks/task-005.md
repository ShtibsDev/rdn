# Task 5: Wrapper Types

**Status:** pending
**File(s):** `packages/rdn-go/wrappers.go` (new), `packages/rdn-go/wrappers_test.go` (new)
**Depends on:** task-003 (marshal), task-004 (unmarshal)
**Blocks:** nothing

## Description

Create `wrappers.go` with generic wrapper types for RDN-only collection types that have no direct Go equivalent.

### Set[T]

```go
// Set represents an RDN Set. Marshals to KindSet, unmarshals from KindSet.
type Set[T any] []T

func (s Set[T]) MarshalRDN() (Value, error)    // marshal each element, return SetVal
func (s *Set[T]) UnmarshalRDN(v Value) error   // accept KindSet or KindArray, unmarshal elements
```

### Tuple

```go
// Tuple represents an RDN Tuple of heterogeneous values.
type Tuple []any

func (t Tuple) MarshalRDN() (Value, error)     // marshal each element, return TupleVal
func (t *Tuple) UnmarshalRDN(v Value) error    // accept KindTuple or KindArray, unmarshal via defaultGoValue
```

### OrderedMap[K, V]

```go
type OrderedMapEntry[K comparable, V any] struct {
    Key   K
    Value V
}

type OrderedMap[K comparable, V any] struct {
    entries []OrderedMapEntry[K, V]
}

// Marshaler/Unmarshaler
func (m OrderedMap[K, V]) MarshalRDN() (Value, error)   // marshal entries, return MapVal
func (m *OrderedMap[K, V]) UnmarshalRDN(v Value) error  // accept KindMap or KindObject

// Utility methods
func (m *OrderedMap[K, V]) Set(key K, value V)           // insert or update
func (m OrderedMap[K, V]) Get(key K) (V, bool)           // lookup
func (m *OrderedMap[K, V]) Delete(key K)                  // remove
func (m OrderedMap[K, V]) Len() int                       // count
func (m OrderedMap[K, V]) Entries() []OrderedMapEntry[K, V] // all entries
func (m OrderedMap[K, V]) Keys() []K                      // all keys
func (m OrderedMap[K, V]) Values() []V                    // all values
```

### Implementation Notes
- `Set.MarshalRDN` and `Tuple.MarshalRDN` call `MarshalValue` for each element
- `OrderedMap.MarshalRDN` calls `MarshalValue` for both keys and values
- Unmarshal methods call `UnmarshalValue` for element population
- `Set.UnmarshalRDN` accepts both KindSet and KindArray (graceful degradation)
- `Tuple.UnmarshalRDN` accepts both KindTuple and KindArray
- `OrderedMap.UnmarshalRDN` accepts both KindMap and KindObject

## Acceptance Criteria
- [ ] `Set[int]{1, 2, 3}` round-trips through Marshal/Unmarshal
- [ ] `Tuple{"hello", 42, true}` round-trips
- [ ] `OrderedMap[string, int]` round-trips with preserved order
- [ ] OrderedMap utility methods (Set, Get, Delete, Len, Keys, Values) work correctly
- [ ] Wrapper types accept compatible kinds on unmarshal (Set←Array, Tuple←Array, Map←Object)
- [ ] Error cases: wrong kind, element type mismatch
- [ ] `wrappers_test.go` covers round-trips, methods, error cases
- [ ] `go test ./...` passes

## References
- [tech-design.md §3.2](../tech-design.md) — Wrapper type definitions
- [tech-design.md §6.4](../tech-design.md) — wrappers.go implementation details
