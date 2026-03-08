package rdn

import (
	"bytes"
	"testing"
	"time"
)

func TestParseStringifyRoundtrip(t *testing.T) {
	tests := []struct {
		name  string
		input string
	}{
		{"null", "null"},
		{"true", "true"},
		{"false", "false"},
		{"integer", "42"},
		{"negative", "-7"},
		{"float", "3.14"},
		{"string", `"hello world"`},
		{"empty_string", `""`},
		{"escaped_string", `"he said \"hi\""`},
		{"nan", "NaN"},
		{"infinity", "Infinity"},
		{"neg_infinity", "-Infinity"},
		{"bigint", "42n"},
		{"neg_bigint", "-99n"},
		{"datetime_full", "@2024-01-15T10:30:00.123Z"},
		{"datetime_no_ms", "@2024-01-15T10:30:00.000Z"},
		{"date_only", "@2024-01-15T00:00:00.000Z"},
		{"time_only", "@14:30:00"},
		{"time_only_ms", "@23:59:59.999"},
		{"duration", "@P1Y2M3DT4H5M6S"},
		{"regexp", "/test/gi"},
		{"regexp_anchored", "/^[a-z]+$/i"},
		{"binary_b64", `b"SGVsbG8="`},
		{"binary_empty", `b""`},
		{"empty_array", "[]"},
		{"array", "[1,2,3]"},
		{"empty_object", "{}"},
		{"object", `{"a":1,"b":2}`},
		{"empty_map", "Map{}"},
		{"map", `Map{"a"=>1,"b"=>2}`},
		{"empty_set", "Set{}"},
		{"set", "Set{1,2,3}"},
		{"empty_tuple", "()"},
		{"tuple", `(1,"two",true)`},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			v, err := Parse([]byte(tt.input))
			if err != nil {
				t.Fatalf("Parse(%q): %v", tt.input, err)
			}
			out, err := Stringify(v)
			if err != nil {
				t.Fatalf("Stringify: %v", err)
			}
			v2, err := Parse(out)
			if err != nil {
				t.Fatalf("Re-parse(%q): %v", out, err)
			}
			if !v.Equal(v2) {
				t.Errorf("Roundtrip mismatch: %q → %q", tt.input, out)
			}
		})
	}
}

func TestValid(t *testing.T) {
	tests := []struct {
		input string
		valid bool
	}{
		{`null`, true},
		{`"hello"`, true},
		{`42`, true},
		{`[1, 2, 3]`, true},
		{`{"key": "value"}`, true},
		{``, false},
		{`{`, false},
		{`{key: "value"}`, false},
		{`[1, 2,]`, false},
		{`'hello'`, false},
	}

	for _, tt := range tests {
		if got := Valid([]byte(tt.input)); got != tt.valid {
			t.Errorf("Valid(%q) = %v, want %v", tt.input, got, tt.valid)
		}
	}
}

func TestStringifyIndent(t *testing.T) {
	v := ObjectVal([]KeyValue{
		{Key: "a", Value: NumberVal(1)},
		{Key: "b", Value: ArrayVal([]Value{NumberVal(2), NumberVal(3)})},
	})
	out, err := StringifyIndent(v, "", "  ")
	if err != nil {
		t.Fatal(err)
	}
	expected := "{\n  \"a\": 1,\n  \"b\": [\n    2,\n    3\n  ]\n}"
	if string(out) != expected {
		t.Errorf("StringifyIndent:\n  got:      %s\n  expected: %s", out, expected)
	}
}

func TestMarshalUnmarshalRoundtrip(t *testing.T) {
	type Inner struct {
		Value int `rdn:"value"`
	}
	type Outer struct {
		Name    string    `rdn:"name"`
		Score   float64   `rdn:"score"`
		Active  bool      `rdn:"active"`
		Created time.Time `rdn:"created"`
		Tags    []string  `rdn:"tags"`
		Inner   Inner     `rdn:"inner"`
	}

	now := time.Date(2024, 6, 15, 10, 30, 0, 0, time.UTC)
	original := Outer{
		Name:    "Alice",
		Score:   99.5,
		Active:  true,
		Created: now,
		Tags:    []string{"admin", "editor"},
		Inner:   Inner{Value: 42},
	}

	data, err := Marshal(original)
	if err != nil {
		t.Fatal(err)
	}

	// Verify it's valid RDN
	if !Valid(data) {
		t.Fatalf("Marshal output is not valid RDN: %s", data)
	}

	var restored Outer
	if err := Unmarshal(data, &restored); err != nil {
		t.Fatalf("Unmarshal failed: %v", err)
	}

	if restored.Name != original.Name {
		t.Errorf("Name: got %q, want %q", restored.Name, original.Name)
	}
	if restored.Score != original.Score {
		t.Errorf("Score: got %v, want %v", restored.Score, original.Score)
	}
	if restored.Active != original.Active {
		t.Errorf("Active: got %v, want %v", restored.Active, original.Active)
	}
	if !restored.Created.Equal(original.Created) {
		t.Errorf("Created: got %v, want %v", restored.Created, original.Created)
	}
	if len(restored.Tags) != len(original.Tags) {
		t.Errorf("Tags length: got %d, want %d", len(restored.Tags), len(original.Tags))
	}
	if restored.Inner.Value != original.Inner.Value {
		t.Errorf("Inner.Value: got %d, want %d", restored.Inner.Value, original.Inner.Value)
	}
}

func TestMarshalUnmarshalNil(t *testing.T) {
	data, err := Marshal(nil)
	if err != nil {
		t.Fatal(err)
	}
	var result any
	if err := Unmarshal(data, &result); err != nil {
		t.Fatal(err)
	}
	if result != nil {
		t.Errorf("expected nil, got %v", result)
	}
}

func TestMarshalUnmarshalMap(t *testing.T) {
	original := map[string]int{"alpha": 1, "beta": 2, "gamma": 3}
	data, err := Marshal(original)
	if err != nil {
		t.Fatal(err)
	}
	var restored map[string]int
	if err := Unmarshal(data, &restored); err != nil {
		t.Fatal(err)
	}
	for k, v := range original {
		if restored[k] != v {
			t.Errorf("key %q: got %d, want %d", k, restored[k], v)
		}
	}
}

func TestMarshalUnmarshalInterface(t *testing.T) {
	// Marshal a struct, unmarshal into interface{}
	type Item struct {
		X int `rdn:"x"`
	}
	data, err := Marshal(Item{X: 5})
	if err != nil {
		t.Fatal(err)
	}
	var result any
	if err := Unmarshal(data, &result); err != nil {
		t.Fatal(err)
	}
	m, ok := result.(map[string]any)
	if !ok {
		t.Fatalf("expected map[string]any, got %T", result)
	}
	if m["x"] != float64(5) {
		t.Errorf("expected x=5, got %v", m["x"])
	}
}

func TestMarshalIndentRoundtrip(t *testing.T) {
	data, err := MarshalIndent(map[string]int{"a": 1}, "", "  ")
	if err != nil {
		t.Fatal(err)
	}
	// Should contain newlines
	if !bytes.Contains(data, []byte("\n")) {
		t.Errorf("expected indented output with newlines, got: %s", data)
	}
	// Should still be valid RDN
	var restored map[string]int
	if err := Unmarshal(data, &restored); err != nil {
		t.Fatal(err)
	}
	if restored["a"] != 1 {
		t.Errorf("expected a=1, got %v", restored["a"])
	}
}
