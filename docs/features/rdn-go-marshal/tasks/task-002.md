# Task 2: Struct Tag Parsing

**Status:** pending
**File(s):** `packages/rdn-go/tags.go` (new), `packages/rdn-go/tags_test.go` (new)
**Depends on:** nothing
**Blocks:** tasks 3, 4

## Description

Create `tags.go` with struct field analysis and tag parsing, cached per `reflect.Type`.

### Tag Format
`rdn:"name,omitempty"` — same semantics as `encoding/json`:
- `rdn:"name"` — custom key name
- `rdn:"-"` — skip field
- `rdn:"-,"` — literal key name `-`
- `rdn:",omitempty"` — omit zero values
- `rdn:",string"` — quote numbers/bools as strings
- No `rdn` tag → fall back to `json` tag → fall back to exported field name

### Key Types

```go
type tagOptions string
func (o tagOptions) Contains(name string) bool

func parseTag(tag string) (name string, opts tagOptions)
```

### Struct Field Analysis

```go
type field struct {
    name      string       // RDN key name
    index     []int        // reflect field index path (embedded support)
    typ       reflect.Type
    omitempty bool
    quoted    bool         // ",string" option
}

type structFields struct {
    list      []field
    nameIndex map[string]int // key name → index in list
}

func analyzeStructFields(t reflect.Type) *structFields
func cachedStructFields(t reflect.Type) *structFields // sync.Map cached
```

### Embedded Struct Rules (match encoding/json)
1. Collect all exported fields from struct + anonymous embedded structs recursively
2. `rdn` tag takes priority over `json` tag over field name
3. Shallower fields win over deeper fields with the same name
4. Same-depth same-name fields: both excluded (ambiguity)
5. Unexported anonymous struct fields: still promote their exported fields

### isEmptyValue Helper
```go
func isEmptyValue(v reflect.Value) bool
```
Returns true for zero values: false, 0, "", nil pointer/slice/map/interface, zero-value TimeOnly/Duration/RegExp.

## Acceptance Criteria
- [ ] `parseTag` handles all tag variations correctly
- [ ] `tagOptions.Contains` works for comma-separated options
- [ ] `analyzeStructFields` resolves embedded/promoted fields with correct conflict resolution
- [ ] `cachedStructFields` returns same pointer on repeated calls (sync.Map cache)
- [ ] `json` tag fallback works when no `rdn` tag present
- [ ] `isEmptyValue` covers all Go kinds + RDN special types
- [ ] `tags_test.go` covers: basic tags, omitempty, string option, skip, embedded structs (single/multi-level, conflicts), json fallback
- [ ] `go test ./...` passes

## References
- [tech-design.md §3.4](../tech-design.md) — Struct tag syntax
- [tech-design.md §6.3](../tech-design.md) — tags.go implementation details
