package rdn

import (
	"math"
	"testing"
	"time"
)

func TestStringifyPrimitives(t *testing.T) {
	tests := []struct {
		name string
		v    Value
		want string
	}{
		{"null", Null(), "null"},
		{"true", Bool(true), "true"},
		{"false", Bool(false), "false"},
		{"int", NumberVal(42), "42"},
		{"float", NumberVal(3.14), "3.14"},
		{"negative", NumberVal(-7), "-7"},
		{"nan", NumberVal(math.NaN()), "NaN"},
		{"inf", NumberVal(math.Inf(1)), "Infinity"},
		{"neg_inf", NumberVal(math.Inf(-1)), "-Infinity"},
		{"bigint", BigIntVal("42"), "42n"},
		{"neg_bigint", BigIntVal("-99"), "-99n"},
		{"string", StringVal("hello"), `"hello"`},
		{"empty_string", StringVal(""), `""`},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			out, err := Stringify(tt.v)
			if err != nil {
				t.Fatalf("Stringify error: %v", err)
			}
			if string(out) != tt.want {
				t.Errorf("got %q, want %q", out, tt.want)
			}
		})
	}
}

func TestStringifyEscapes(t *testing.T) {
	tests := []struct {
		input string
		want  string
	}{
		{"hello", `"hello"`},
		{`say "hi"`, `"say \"hi\""`},
		{"back\\slash", `"back\\slash"`},
		{"new\nline", `"new\nline"`},
		{"tab\there", `"tab\there"`},
		{"\x00null", `"\u0000null"`},
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			out, err := Stringify(StringVal(tt.input))
			if err != nil {
				t.Fatal(err)
			}
			if string(out) != tt.want {
				t.Errorf("got %q, want %q", out, tt.want)
			}
		})
	}
}

func TestStringifyDateTime(t *testing.T) {
	v := DateTimeVal(time.Date(2024, 1, 15, 10, 30, 0, 123_000_000, time.UTC))
	out, err := Stringify(v)
	if err != nil {
		t.Fatal(err)
	}
	if string(out) != "@2024-01-15T10:30:00.123Z" {
		t.Errorf("got %q", out)
	}
}

func TestStringifyTimeOnly(t *testing.T) {
	v := TimeOnlyVal(TimeOnly{14, 30, 0, 0})
	out, err := Stringify(v)
	if err != nil {
		t.Fatal(err)
	}
	if string(out) != "@14:30:00" {
		t.Errorf("got %q", out)
	}

	v = TimeOnlyVal(TimeOnly{23, 59, 59, 999})
	out, err = Stringify(v)
	if err != nil {
		t.Fatal(err)
	}
	if string(out) != "@23:59:59.999" {
		t.Errorf("got %q", out)
	}
}

func TestStringifyDuration(t *testing.T) {
	v := DurationVal("P1Y2M3DT4H5M6S")
	out, err := Stringify(v)
	if err != nil {
		t.Fatal(err)
	}
	if string(out) != "@P1Y2M3DT4H5M6S" {
		t.Errorf("got %q", out)
	}
}

func TestStringifyRegExp(t *testing.T) {
	v := RegExpVal("test", "gi")
	out, err := Stringify(v)
	if err != nil {
		t.Fatal(err)
	}
	if string(out) != "/test/gi" {
		t.Errorf("got %q", out)
	}
}

func TestStringifyBinary(t *testing.T) {
	v := BinaryVal([]byte("Hello"))
	out, err := Stringify(v)
	if err != nil {
		t.Fatal(err)
	}
	if string(out) != `b"SGVsbG8="` {
		t.Errorf("got %q", out)
	}

	v = BinaryVal([]byte{})
	out, err = Stringify(v)
	if err != nil {
		t.Fatal(err)
	}
	if string(out) != `b""` {
		t.Errorf("got %q", out)
	}
}

func TestStringifyCollections(t *testing.T) {
	t.Run("array", func(t *testing.T) {
		v := ArrayVal([]Value{NumberVal(1), NumberVal(2), NumberVal(3)})
		out, err := Stringify(v)
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != "[1,2,3]" {
			t.Errorf("got %q", out)
		}
	})

	t.Run("empty_array", func(t *testing.T) {
		out, err := Stringify(ArrayVal([]Value{}))
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != "[]" {
			t.Errorf("got %q", out)
		}
	})

	t.Run("object", func(t *testing.T) {
		v := ObjectVal([]KeyValue{{Key: "a", Value: NumberVal(1)}, {Key: "b", Value: NumberVal(2)}})
		out, err := Stringify(v)
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != `{"a":1,"b":2}` {
			t.Errorf("got %q", out)
		}
	})

	t.Run("map", func(t *testing.T) {
		v := MapVal([]MapEntry{{Key: StringVal("a"), Value: NumberVal(1)}, {Key: StringVal("b"), Value: NumberVal(2)}})
		out, err := Stringify(v)
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != `Map{"a"=>1,"b"=>2}` {
			t.Errorf("got %q", out)
		}
	})

	t.Run("empty_map", func(t *testing.T) {
		out, err := Stringify(MapVal([]MapEntry{}))
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != "Map{}" {
			t.Errorf("got %q", out)
		}
	})

	t.Run("set", func(t *testing.T) {
		v := SetVal([]Value{NumberVal(1), NumberVal(2), NumberVal(3)})
		out, err := Stringify(v)
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != "Set{1,2,3}" {
			t.Errorf("got %q", out)
		}
	})

	t.Run("empty_set", func(t *testing.T) {
		out, err := Stringify(SetVal([]Value{}))
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != "Set{}" {
			t.Errorf("got %q", out)
		}
	})

	t.Run("tuple", func(t *testing.T) {
		v := TupleVal([]Value{NumberVal(1), StringVal("two"), Bool(true)})
		out, err := Stringify(v)
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != `(1,"two",true)` {
			t.Errorf("got %q", out)
		}
	})

	t.Run("empty_tuple", func(t *testing.T) {
		out, err := Stringify(TupleVal([]Value{}))
		if err != nil {
			t.Fatal(err)
		}
		if string(out) != "()" {
			t.Errorf("got %q", out)
		}
	})
}
