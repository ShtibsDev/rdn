package rdn

import (
	"testing"
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
