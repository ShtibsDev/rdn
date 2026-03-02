package rdn

import (
	"math"
	"math/big"
	"testing"
	"time"
	"unsafe"
)

func TestValueConstructorsAndAccessors(t *testing.T) {
	t.Run("Null", func(t *testing.T) {
		v := Null()
		if v.Kind() != KindNull {
			t.Errorf("expected KindNull, got %v", v.Kind())
		}
		if !v.IsNull() {
			t.Error("expected IsNull")
		}
	})

	t.Run("Bool", func(t *testing.T) {
		v := Bool(true)
		if v.Kind() != KindBool {
			t.Errorf("expected KindBool, got %v", v.Kind())
		}
		if !v.BoolVal() {
			t.Error("expected true")
		}
	})

	t.Run("Number", func(t *testing.T) {
		v := NumberVal(3.14)
		if v.Kind() != KindNumber {
			t.Errorf("expected KindNumber, got %v", v.Kind())
		}
		if v.Float64() != 3.14 {
			t.Errorf("expected 3.14, got %v", v.Float64())
		}
	})

	t.Run("NaN", func(t *testing.T) {
		v := NumberVal(math.NaN())
		if !v.IsNaN() {
			t.Error("expected IsNaN")
		}
	})

	t.Run("Infinity", func(t *testing.T) {
		v := NumberVal(math.Inf(1))
		if !v.IsInf() {
			t.Error("expected IsInf")
		}
	})

	t.Run("String", func(t *testing.T) {
		v := StringVal("hello")
		if v.Kind() != KindString {
			t.Errorf("expected KindString, got %v", v.Kind())
		}
		if v.Str() != "hello" {
			t.Errorf("expected hello, got %v", v.Str())
		}
	})

	t.Run("BigInt", func(t *testing.T) {
		v := BigIntVal("999999999999999999")
		if v.Kind() != KindBigInt {
			t.Errorf("expected KindBigInt, got %v", v.Kind())
		}
		if v.Str() != "999999999999999999" {
			t.Errorf("expected 999999999999999999, got %v", v.Str())
		}
	})

	t.Run("BigIntFromGo", func(t *testing.T) {
		bi := new(big.Int)
		bi.SetString("12345678901234567890", 10)
		v := BigIntFromGo(bi)
		if v.Str() != "12345678901234567890" {
			t.Errorf("expected 12345678901234567890, got %v", v.Str())
		}
	})

	t.Run("DateTime", func(t *testing.T) {
		now := time.Date(2024, 1, 15, 10, 30, 0, 0, time.UTC)
		v := DateTimeVal(now)
		if v.Kind() != KindDateTime {
			t.Errorf("expected KindDateTime, got %v", v.Kind())
		}
		if !v.Time().Equal(now) {
			t.Errorf("expected %v, got %v", now, v.Time())
		}
	})

	t.Run("Array", func(t *testing.T) {
		v := ArrayVal([]Value{NumberVal(1), NumberVal(2)})
		if v.Len() != 2 {
			t.Errorf("expected len 2, got %d", v.Len())
		}
	})

	t.Run("Object", func(t *testing.T) {
		v := ObjectVal([]KeyValue{{Key: "a", Value: NumberVal(1)}})
		if v.Len() != 1 {
			t.Errorf("expected len 1, got %d", v.Len())
		}
	})

	t.Run("Map", func(t *testing.T) {
		v := MapVal([]MapEntry{{Key: StringVal("a"), Value: NumberVal(1)}})
		if v.Len() != 1 {
			t.Errorf("expected len 1, got %d", v.Len())
		}
	})

	t.Run("Binary", func(t *testing.T) {
		v := BinaryVal([]byte{1, 2, 3})
		if v.Len() != 3 {
			t.Errorf("expected len 3, got %d", v.Len())
		}
	})
}

func TestValueStructSize(t *testing.T) {
	size := unsafe.Sizeof(Value{})
	if size > 80 {
		t.Errorf("Value struct size %d bytes exceeds 80-byte target (was 224 before optimization)", size)
	}
	t.Logf("Value: %d bytes, KeyValue: %d bytes, MapEntry: %d bytes", size, unsafe.Sizeof(KeyValue{}), unsafe.Sizeof(MapEntry{}))
}

func TestParseZeroCopy(t *testing.T) {
	data := []byte(`{"name":"Alice","age":30}`)
	v, err := ParseZeroCopy(data)
	if err != nil {
		t.Fatal(err)
	}
	obj := v.Object()
	if len(obj) != 2 {
		t.Fatalf("expected 2 keys, got %d", len(obj))
	}
	if obj[0].Key != "name" || obj[0].Value.Str() != "Alice" {
		t.Errorf("unexpected first pair: %q=%q", obj[0].Key, obj[0].Value.Str())
	}
}

func TestValueEqual(t *testing.T) {
	tests := []struct {
		name  string
		a, b  Value
		equal bool
	}{
		{"null_null", Null(), Null(), true},
		{"null_bool", Null(), Bool(false), false},
		{"bool_same", Bool(true), Bool(true), true},
		{"bool_diff", Bool(true), Bool(false), false},
		{"num_same", NumberVal(42), NumberVal(42), true},
		{"num_diff", NumberVal(42), NumberVal(43), false},
		{"nan_nan", NumberVal(math.NaN()), NumberVal(math.NaN()), true},
		{"str_same", StringVal("hi"), StringVal("hi"), true},
		{"str_diff", StringVal("hi"), StringVal("bye"), false},
		{"arr_same", ArrayVal([]Value{NumberVal(1)}), ArrayVal([]Value{NumberVal(1)}), true},
		{"arr_diff_len", ArrayVal([]Value{NumberVal(1)}), ArrayVal([]Value{}), false},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.a.Equal(tt.b); got != tt.equal {
				t.Errorf("Equal() = %v, want %v", got, tt.equal)
			}
		})
	}
}

func TestValueString(t *testing.T) {
	tests := []struct {
		v    Value
		want string
	}{
		{Null(), "null"},
		{Bool(true), "true"},
		{Bool(false), "false"},
		{NumberVal(42), "42"},
		{NumberVal(math.NaN()), "NaN"},
		{NumberVal(math.Inf(1)), "Infinity"},
		{NumberVal(math.Inf(-1)), "-Infinity"},
		{BigIntVal("42"), "42n"},
		{StringVal("hello"), `"hello"`},
	}

	for _, tt := range tests {
		if got := tt.v.String(); got != tt.want {
			t.Errorf("String() = %q, want %q", got, tt.want)
		}
	}
}
