package rdn

import (
	"math"
	"testing"
	"time"
)

func TestParseStrings(t *testing.T) {
	tests := []struct {
		input string
		want  string
	}{
		{`"hello"`, "hello"},
		{`""`, ""},
		{`"with \"quotes\""`, `with "quotes"`},
		{`"back\\slash"`, `back\slash`},
		{`"new\nline"`, "new\nline"},
		{`"tab\there"`, "tab\there"},
		{`"unicode \u0041"`, "unicode A"},
		{`"slash\/"`, "slash/"},
		{`"emoji \uD83D\uDE00"`, "emoji 😀"},
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			v, err := Parse([]byte(tt.input))
			if err != nil {
				t.Fatalf("Parse error: %v", err)
			}
			if v.Kind() != KindString || v.Str() != tt.want {
				t.Errorf("got %q, want %q", v.Str(), tt.want)
			}
		})
	}
}

func TestParseStringErrors(t *testing.T) {
	tests := []string{
		`"unterminated`,
		`"bad escape \q"`,
		"\"control \x01 char\"",
	}
	for _, input := range tests {
		t.Run(input, func(t *testing.T) {
			_, err := Parse([]byte(input))
			if err == nil {
				t.Error("expected error")
			}
		})
	}
}

func TestParseNumbers(t *testing.T) {
	tests := []struct {
		input string
		want  float64
	}{
		{"0", 0},
		{"42", 42},
		{"-7", -7},
		{"3.14", 3.14},
		{"1e10", 1e10},
		{"1E10", 1e10},
		{"1.5e2", 150},
		{"-0", 0}, // -0 is valid JSON
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			v, err := Parse([]byte(tt.input))
			if err != nil {
				t.Fatalf("Parse error: %v", err)
			}
			if v.Kind() != KindNumber {
				t.Fatalf("expected KindNumber, got %v", v.Kind())
			}
			if v.Float64() != tt.want {
				t.Errorf("got %v, want %v", v.Float64(), tt.want)
			}
		})
	}
}

func TestParseNumberErrors(t *testing.T) {
	tests := []string{
		"01",       // leading zero
		"1.",       // trailing dot
		"1e",       // incomplete exponent
		"3.14n",    // bigint with decimal
		"1e10n",    // bigint with exponent
	}
	for _, input := range tests {
		t.Run(input, func(t *testing.T) {
			_, err := Parse([]byte(input))
			if err == nil {
				t.Error("expected error")
			}
		})
	}
}

func TestParseBigInt(t *testing.T) {
	tests := []struct {
		input string
		want  string
	}{
		{"0n", "0"},
		{"42n", "42"},
		{"-99n", "-99"},
		{"999999999999999999n", "999999999999999999"},
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			v, err := Parse([]byte(tt.input))
			if err != nil {
				t.Fatalf("Parse error: %v", err)
			}
			if v.Kind() != KindBigInt {
				t.Fatalf("expected KindBigInt, got %v", v.Kind())
			}
			if v.Str() != tt.want {
				t.Errorf("got %q, want %q", v.Str(), tt.want)
			}
		})
	}
}

func TestParseSpecialNumbers(t *testing.T) {
	v, err := Parse([]byte("NaN"))
	if err != nil {
		t.Fatal(err)
	}
	if !math.IsNaN(v.Float64()) {
		t.Error("expected NaN")
	}

	v, err = Parse([]byte("Infinity"))
	if err != nil {
		t.Fatal(err)
	}
	if !math.IsInf(v.Float64(), 1) {
		t.Error("expected +Infinity")
	}

	v, err = Parse([]byte("-Infinity"))
	if err != nil {
		t.Fatal(err)
	}
	if !math.IsInf(v.Float64(), -1) {
		t.Error("expected -Infinity")
	}
}

func TestParseDateTime(t *testing.T) {
	tests := []struct {
		input string
		want  time.Time
	}{
		{"@2024-01-15T10:30:00.123Z", time.Date(2024, 1, 15, 10, 30, 0, 123_000_000, time.UTC)},
		{"@2024-01-15T10:30:00Z", time.Date(2024, 1, 15, 10, 30, 0, 0, time.UTC)},
		{"@2024-01-15", time.Date(2024, 1, 15, 0, 0, 0, 0, time.UTC)},
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			v, err := Parse([]byte(tt.input))
			if err != nil {
				t.Fatalf("Parse error: %v", err)
			}
			if v.Kind() != KindDateTime {
				t.Fatalf("expected KindDateTime, got %v", v.Kind())
			}
			if !v.Time().Equal(tt.want) {
				t.Errorf("got %v, want %v", v.Time(), tt.want)
			}
		})
	}
}

func TestParseTimeOnly(t *testing.T) {
	v, err := Parse([]byte("@14:30:00"))
	if err != nil {
		t.Fatal(err)
	}
	if v.Kind() != KindTimeOnly {
		t.Fatalf("expected KindTimeOnly, got %v", v.Kind())
	}
	to := v.TimeOnlyValue()
	if to.Hours != 14 || to.Minutes != 30 || to.Seconds != 0 || to.Milliseconds != 0 {
		t.Errorf("unexpected TimeOnly: %+v", to)
	}

	v, err = Parse([]byte("@23:59:59.999"))
	if err != nil {
		t.Fatal(err)
	}
	to = v.TimeOnlyValue()
	if to.Milliseconds != 999 {
		t.Errorf("expected ms=999, got %d", to.Milliseconds)
	}
}

func TestParseDuration(t *testing.T) {
	tests := []struct {
		input string
		want  string
	}{
		{"@P1Y2M3DT4H5M6S", "P1Y2M3DT4H5M6S"},
		{"@PT1H", "PT1H"},
		{"@PT1H30M", "PT1H30M"},
	}
	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			v, err := Parse([]byte(tt.input))
			if err != nil {
				t.Fatalf("Parse error: %v", err)
			}
			if v.Kind() != KindDuration {
				t.Fatalf("expected KindDuration, got %v", v.Kind())
			}
			if v.Str() != tt.want {
				t.Errorf("got %q, want %q", v.Str(), tt.want)
			}
		})
	}
}

func TestParseRegExp(t *testing.T) {
	v, err := Parse([]byte("/test/gi"))
	if err != nil {
		t.Fatal(err)
	}
	if v.Kind() != KindRegExp {
		t.Fatalf("expected KindRegExp, got %v", v.Kind())
	}
	r := v.RegExpValue()
	if r.Source != "test" || r.Flags != "gi" {
		t.Errorf("unexpected RegExp: %+v", r)
	}
}

func TestParseBinary(t *testing.T) {
	// base64
	v, err := Parse([]byte(`b"SGVsbG8="`))
	if err != nil {
		t.Fatal(err)
	}
	if v.Kind() != KindBinary {
		t.Fatalf("expected KindBinary, got %v", v.Kind())
	}
	if string(v.Bytes()) != "Hello" {
		t.Errorf("got %q, want %q", v.Bytes(), "Hello")
	}

	// hex
	v, err = Parse([]byte(`x"48656C6C6F"`))
	if err != nil {
		t.Fatal(err)
	}
	if string(v.Bytes()) != "Hello" {
		t.Errorf("got %q, want %q", v.Bytes(), "Hello")
	}

	// empty
	v, err = Parse([]byte(`b""`))
	if err != nil {
		t.Fatal(err)
	}
	if len(v.Bytes()) != 0 {
		t.Error("expected empty binary")
	}
}

func TestParseCollections(t *testing.T) {
	t.Run("array", func(t *testing.T) {
		v, err := Parse([]byte("[1, 2, 3]"))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindArray || v.Len() != 3 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("tuple", func(t *testing.T) {
		v, err := Parse([]byte(`(1, "two", true)`))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindTuple || v.Len() != 3 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("object", func(t *testing.T) {
		v, err := Parse([]byte(`{"a": 1, "b": 2}`))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindObject || v.Len() != 2 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("map_explicit", func(t *testing.T) {
		v, err := Parse([]byte(`Map{"a" => 1, "b" => 2}`))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindMap || v.Len() != 2 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("map_brace", func(t *testing.T) {
		v, err := Parse([]byte(`{"a" => 1, "b" => 2}`))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindMap || v.Len() != 2 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("set_explicit", func(t *testing.T) {
		v, err := Parse([]byte("Set{1, 2, 3}"))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindSet || v.Len() != 3 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("set_brace", func(t *testing.T) {
		v, err := Parse([]byte("{1, 2, 3}"))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindSet || v.Len() != 3 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("set_single", func(t *testing.T) {
		v, err := Parse([]byte("{1}"))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindSet || v.Len() != 1 {
			t.Errorf("unexpected: kind=%v len=%d", v.Kind(), v.Len())
		}
	})

	t.Run("empty_braces_is_object", func(t *testing.T) {
		v, err := Parse([]byte("{}"))
		if err != nil {
			t.Fatal(err)
		}
		if v.Kind() != KindObject {
			t.Errorf("expected KindObject, got %v", v.Kind())
		}
	})
}

func TestParseNestingDepth(t *testing.T) {
	// Build deeply nested array
	input := make([]byte, 0, 300)
	for i := 0; i < 129; i++ {
		input = append(input, '[')
	}
	input = append(input, '1')
	for i := 0; i < 129; i++ {
		input = append(input, ']')
	}
	_, err := Parse(input)
	if err == nil {
		t.Error("expected depth exceeded error")
	}
}

func TestParseTrailingContent(t *testing.T) {
	_, err := Parse([]byte(`42 "extra"`))
	if err == nil {
		t.Error("expected error for trailing content")
	}
}

func TestParseShortUnixTimestamps(t *testing.T) {
	for _, input := range []string{"@0", "@1", "@12"} {
		_, err := Parse([]byte(input))
		if err != nil {
			t.Errorf("Parse(%q) should succeed but got: %v", input, err)
		}
	}
}

func TestParseNegativeZero(t *testing.T) {
	v, err := Parse([]byte("-0"))
	if err != nil {
		t.Fatal(err)
	}
	if !math.Signbit(v.Float64()) {
		t.Error("-0 lost its sign")
	}
}

func TestParseEmptyRegex(t *testing.T) {
	_, err := Parse([]byte("//"))
	if err == nil {
		t.Error("Empty regex // should be rejected per spec")
	}
}

func TestParseNumberOverflow(t *testing.T) {
	// 1e999 overflows float64 → should parse as +Infinity, not error
	v, err := Parse([]byte("1e999"))
	if err != nil {
		t.Fatalf("Parse(1e999) should succeed but got: %v", err)
	}
	if !math.IsInf(v.Float64(), 1) {
		t.Errorf("expected +Inf, got %v", v.Float64())
	}

	// Negative overflow
	v, err = Parse([]byte("-1e999"))
	if err != nil {
		t.Fatalf("Parse(-1e999) should succeed but got: %v", err)
	}
	if !math.IsInf(v.Float64(), -1) {
		t.Errorf("expected -Inf, got %v", v.Float64())
	}
}

func TestRegExpSlashRoundtrip(t *testing.T) {
	v := RegExpVal("a/b", "g")
	out, err := Stringify(v)
	if err != nil {
		t.Fatal("stringify err:", err)
	}
	v2, err := Parse(out)
	if err != nil {
		t.Fatalf("roundtrip parse err: %v (encoded: %s)", err, out)
	}
	if !v.Equal(v2) {
		t.Errorf("roundtrip mismatch: %v != %v", v, v2)
	}
}
