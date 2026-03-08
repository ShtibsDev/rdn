package rdn

import (
	"reflect"
	"sort"
	"strings"
	"sync"
	"time"
)

// tagOptions is a string following the name in a struct tag.
type tagOptions string

// parseTag splits a struct field's tag into name and options.
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

// field describes a single struct field for RDN encoding/decoding.
type field struct {
	name      string       // RDN key name
	index     []int        // reflect field index path (embedded support)
	typ       reflect.Type
	omitempty bool
	quoted    bool // ",string" option
}

// structFields holds the analyzed fields and a name lookup index.
type structFields struct {
	list      []field
	nameIndex map[string]int // key name → index in list
}

var fieldCache sync.Map // map[reflect.Type]*structFields

// cachedStructFields returns the analyzed fields for the given struct type,
// using a cache to avoid repeated reflection work.
func cachedStructFields(t reflect.Type) *structFields {
	if v, ok := fieldCache.Load(t); ok {
		return v.(*structFields)
	}
	sf := analyzeStructFields(t)
	v, _ := fieldCache.LoadOrStore(t, sf)
	return v.(*structFields)
}

// analyzeStructFields recursively collects and resolves all exported fields
// from a struct type, handling embedded structs and tag-based naming.
func analyzeStructFields(t reflect.Type) *structFields {
	type fieldInfo struct {
		field
		depth int
	}
	var fields []fieldInfo
	var visit func(t reflect.Type, index []int, depth int)
	visit = func(t reflect.Type, index []int, depth int) {
		for i := 0; i < t.NumField(); i++ {
			sf := t.Field(i)
			if !sf.IsExported() && !sf.Anonymous {
				continue
			}
			tag := sf.Tag.Get("rdn")
			if tag == "" {
				tag = sf.Tag.Get("json")
			}
			// Handle anonymous (embedded) structs
			if sf.Anonymous {
				ft := sf.Type
				if ft.Kind() == reflect.Pointer {
					ft = ft.Elem()
				}
				if ft.Kind() == reflect.Struct {
					name, _ := parseTag(tag)
					if name == "" {
						// No explicit name → promote fields
						visit(ft, append(append([]int{}, index...), i), depth+1)
						continue
					}
				}
			}
			name, opts := parseTag(tag)
			// Skip "-" tagged fields (but "-," means literal dash)
			if name == "-" && tag != "-," {
				continue
			}
			if name == "" || (name == "-" && tag == "-,") {
				if tag == "-," {
					name = "-"
				} else {
					name = sf.Name
				}
			}
			fieldIndex := make([]int, len(index)+1)
			copy(fieldIndex, index)
			fieldIndex[len(index)] = i
			fields = append(fields, fieldInfo{
				field: field{
					name:      name,
					index:     fieldIndex,
					typ:       sf.Type,
					omitempty: opts.Contains("omitempty"),
					quoted:    opts.Contains("string"),
				},
				depth: depth,
			})
		}
	}
	visit(t, nil, 0)

	// Resolve conflicts
	type seenInfo struct {
		idx   int
		depth int
	}
	seen := make(map[string]seenInfo)
	ambiguous := make(map[string]bool)
	for i, f := range fields {
		if prev, ok := seen[f.name]; ok {
			if f.depth < prev.depth {
				seen[f.name] = seenInfo{idx: i, depth: f.depth}
				delete(ambiguous, f.name)
			} else if f.depth == prev.depth {
				ambiguous[f.name] = true
			}
		} else {
			seen[f.name] = seenInfo{idx: i, depth: f.depth}
		}
	}

	var final []field
	nameIdx := make(map[string]int)
	for name, info := range seen {
		if ambiguous[name] {
			continue
		}
		nameIdx[name] = len(final)
		final = append(final, fields[info.idx].field)
	}
	// Sort by index to maintain declaration order
	sort.Slice(final, func(i, j int) bool {
		for k := 0; k < len(final[i].index) && k < len(final[j].index); k++ {
			if final[i].index[k] != final[j].index[k] {
				return final[i].index[k] < final[j].index[k]
			}
		}
		return len(final[i].index) < len(final[j].index)
	})
	// Rebuild nameIndex after sorting
	nameIdx = make(map[string]int, len(final))
	for i, f := range final {
		nameIdx[f.name] = i
	}

	return &structFields{list: final, nameIndex: nameIdx}
}

// isEmptyValue reports whether the value should be considered empty for
// the purposes of omitempty.
func isEmptyValue(v reflect.Value) bool {
	switch v.Kind() {
	case reflect.Bool:
		return !v.Bool()
	case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
		return v.Int() == 0
	case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64:
		return v.Uint() == 0
	case reflect.Float32, reflect.Float64:
		return v.Float() == 0
	case reflect.String:
		return v.String() == ""
	case reflect.Slice, reflect.Map:
		return v.IsNil()
	case reflect.Pointer, reflect.Interface:
		return v.IsNil()
	case reflect.Struct:
		t := v.Type()
		switch t {
		case reflect.TypeOf(TimeOnly{}):
			return v.Interface().(TimeOnly) == TimeOnly{}
		case reflect.TypeOf(Duration{}):
			return v.Interface().(Duration) == Duration{}
		case reflect.TypeOf(RegExp{}):
			return v.Interface().(RegExp) == RegExp{}
		case reflect.TypeOf(Value{}):
			return v.Interface().(Value).Kind() == KindNull
		}
		if t == reflect.TypeOf(time.Time{}) {
			return v.Interface().(time.Time).IsZero()
		}
		return false
	case reflect.Array:
		return false
	default:
		return false
	}
}
