package rdn

import (
	"reflect"
	"testing"
	"time"
)

func TestParseTag(t *testing.T) {
	tests := []struct {
		tag      string
		wantName string
		wantOpts tagOptions
	}{
		{"myField", "myField", ""},
		{"myField,omitempty", "myField", "omitempty"},
		{",omitempty", "", "omitempty"},
		{"-", "-", ""},
		{"-,", "-", ""},
		{"", "", ""},
		{"name,string,omitempty", "name", "string,omitempty"},
	}
	for _, tt := range tests {
		name, opts := parseTag(tt.tag)
		if name != tt.wantName {
			t.Errorf("parseTag(%q) name = %q, want %q", tt.tag, name, tt.wantName)
		}
		if opts != tt.wantOpts {
			t.Errorf("parseTag(%q) opts = %q, want %q", tt.tag, opts, tt.wantOpts)
		}
	}
}

func TestTagOptionsContains(t *testing.T) {
	tests := []struct {
		opts tagOptions
		name string
		want bool
	}{
		{"omitempty", "omitempty", true},
		{"string,omitempty", "omitempty", true},
		{"string,omitempty", "string", true},
		{"", "omitempty", false},
		{"omitempty", "string", false},
		{"omit", "omitempty", false},
	}
	for _, tt := range tests {
		if got := tt.opts.Contains(tt.name); got != tt.want {
			t.Errorf("tagOptions(%q).Contains(%q) = %v, want %v", tt.opts, tt.name, got, tt.want)
		}
	}
}

func TestAnalyzeStructFieldsSimple(t *testing.T) {
	type S struct {
		Name  string `rdn:"name"`
		Age   int    `rdn:"age,omitempty"`
		Email string `rdn:"email,string"`
		Plain string
	}
	sf := analyzeStructFields(reflect.TypeOf(S{}))
	if len(sf.list) != 4 {
		t.Fatalf("expected 4 fields, got %d", len(sf.list))
	}
	// Check field names
	names := make(map[string]bool)
	for _, f := range sf.list {
		names[f.name] = true
	}
	for _, want := range []string{"name", "age", "email", "Plain"} {
		if !names[want] {
			t.Errorf("missing field %q", want)
		}
	}
	// Check options
	for _, f := range sf.list {
		if f.name == "age" && !f.omitempty {
			t.Error("age should have omitempty")
		}
		if f.name == "email" && !f.quoted {
			t.Error("email should have quoted=true")
		}
	}
	// Check nameIndex
	for _, f := range sf.list {
		idx, ok := sf.nameIndex[f.name]
		if !ok {
			t.Errorf("nameIndex missing %q", f.name)
		}
		if sf.list[idx].name != f.name {
			t.Errorf("nameIndex[%q] points to wrong field", f.name)
		}
	}
}

func TestAnalyzeStructFieldsSkip(t *testing.T) {
	type S struct {
		Visible string `rdn:"visible"`
		Hidden  string `rdn:"-"`
	}
	sf := analyzeStructFields(reflect.TypeOf(S{}))
	if len(sf.list) != 1 {
		t.Fatalf("expected 1 field, got %d", len(sf.list))
	}
	if sf.list[0].name != "visible" {
		t.Errorf("expected field name 'visible', got %q", sf.list[0].name)
	}
}

func TestAnalyzeStructFieldsLiteralDash(t *testing.T) {
	type S struct {
		Dash string `rdn:"-,"`
	}
	sf := analyzeStructFields(reflect.TypeOf(S{}))
	if len(sf.list) != 1 {
		t.Fatalf("expected 1 field, got %d", len(sf.list))
	}
	if sf.list[0].name != "-" {
		t.Errorf("expected field name '-', got %q", sf.list[0].name)
	}
}

func TestAnalyzeStructFieldsJSONFallback(t *testing.T) {
	type S struct {
		Name string `json:"json_name"`
	}
	sf := analyzeStructFields(reflect.TypeOf(S{}))
	if len(sf.list) != 1 {
		t.Fatalf("expected 1 field, got %d", len(sf.list))
	}
	if sf.list[0].name != "json_name" {
		t.Errorf("expected field name 'json_name', got %q", sf.list[0].name)
	}
}

func TestAnalyzeStructFieldsRDNPriority(t *testing.T) {
	type S struct {
		Name string `rdn:"rdn_name" json:"json_name"`
	}
	sf := analyzeStructFields(reflect.TypeOf(S{}))
	if len(sf.list) != 1 {
		t.Fatalf("expected 1 field, got %d", len(sf.list))
	}
	if sf.list[0].name != "rdn_name" {
		t.Errorf("expected field name 'rdn_name', got %q", sf.list[0].name)
	}
}

func TestAnalyzeStructFieldsEmbedded(t *testing.T) {
	type Inner struct {
		X int `rdn:"x"`
		Y int `rdn:"y"`
	}
	type Outer struct {
		Inner
		Z int `rdn:"z"`
	}
	sf := analyzeStructFields(reflect.TypeOf(Outer{}))
	names := make(map[string]bool)
	for _, f := range sf.list {
		names[f.name] = true
	}
	for _, want := range []string{"x", "y", "z"} {
		if !names[want] {
			t.Errorf("missing promoted field %q", want)
		}
	}
}

func TestAnalyzeStructFieldsPointerEmbedded(t *testing.T) {
	type Inner struct {
		X int `rdn:"x"`
	}
	type Outer struct {
		*Inner
		Z int `rdn:"z"`
	}
	sf := analyzeStructFields(reflect.TypeOf(Outer{}))
	names := make(map[string]bool)
	for _, f := range sf.list {
		names[f.name] = true
	}
	for _, want := range []string{"x", "z"} {
		if !names[want] {
			t.Errorf("missing field %q", want)
		}
	}
}

func TestAnalyzeStructFieldsConflictSameDepth(t *testing.T) {
	type A struct {
		X int `rdn:"x"`
	}
	type B struct {
		X int `rdn:"x"`
	}
	type S struct {
		A
		B
	}
	sf := analyzeStructFields(reflect.TypeOf(S{}))
	if _, ok := sf.nameIndex["x"]; ok {
		t.Error("ambiguous field 'x' should be excluded")
	}
}

func TestAnalyzeStructFieldsConflictShallowerWins(t *testing.T) {
	type Inner struct {
		X int `rdn:"x"`
	}
	type Outer struct {
		Inner
		X int `rdn:"x"`
	}
	sf := analyzeStructFields(reflect.TypeOf(Outer{}))
	idx, ok := sf.nameIndex["x"]
	if !ok {
		t.Fatal("field 'x' should exist (shallower wins)")
	}
	// The outer field should win (depth 0 vs depth 1), index should be [1]
	if len(sf.list[idx].index) != 1 {
		t.Errorf("expected shallower field (index len 1), got index len %d", len(sf.list[idx].index))
	}
}

func TestAnalyzeStructFieldsUnexported(t *testing.T) {
	type S struct {
		Exported   string `rdn:"exported"`
		unexported string `rdn:"unexported"`
	}
	_ = S{unexported: "suppress unused"}
	sf := analyzeStructFields(reflect.TypeOf(S{}))
	if len(sf.list) != 1 {
		t.Fatalf("expected 1 field, got %d", len(sf.list))
	}
	if sf.list[0].name != "exported" {
		t.Errorf("expected 'exported', got %q", sf.list[0].name)
	}
}

func TestCachedStructFields(t *testing.T) {
	type S struct {
		A string `rdn:"a"`
	}
	sf1 := cachedStructFields(reflect.TypeOf(S{}))
	sf2 := cachedStructFields(reflect.TypeOf(S{}))
	if sf1 != sf2 {
		t.Error("cachedStructFields should return the same pointer on repeated calls")
	}
}

func TestIsEmptyValue(t *testing.T) {
	tests := []struct {
		name string
		val  interface{}
		want bool
	}{
		{"false bool", false, true},
		{"true bool", true, false},
		{"zero int", int(0), true},
		{"nonzero int", int(42), false},
		{"zero float", float64(0), true},
		{"nonzero float", float64(3.14), false},
		{"empty string", "", true},
		{"nonzero string", "hello", false},
		{"nil slice", ([]int)(nil), true},
		{"nonempty slice", []int{1}, false},
		{"nil map", (map[string]int)(nil), true},
		{"nonempty map", map[string]int{"a": 1}, false},
		{"nil pointer", (*int)(nil), true},
		{"non-nil pointer", new(int), false},
		{"zero TimeOnly", TimeOnly{}, true},
		{"nonzero TimeOnly", TimeOnly{Hours: 12}, false},
		{"zero Duration", Duration{}, true},
		{"nonzero Duration", Duration{ISO: "PT1H"}, false},
		{"zero RegExp", RegExp{}, true},
		{"nonzero RegExp", RegExp{Source: "test"}, false},
		{"null Value", Null(), true},
		{"non-null Value", StringVal("hi"), false},
		{"zero time.Time", time.Time{}, true},
		{"nonzero time.Time", time.Now(), false},
		{"zero uint", uint(0), true},
		{"nonzero uint", uint(5), false},
	}
	for _, tt := range tests {
		v := reflect.ValueOf(tt.val)
		if got := isEmptyValue(v); got != tt.want {
			t.Errorf("isEmptyValue(%s) = %v, want %v", tt.name, got, tt.want)
		}
	}
}
