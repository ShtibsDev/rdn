package rdn

import (
	"encoding"
	"math/big"
	"reflect"
	"testing"
	"time"
)

// ── Primitives ──────────────────────────────────────────────────────────

func TestUnmarshalBool(t *testing.T) {
	var b bool
	if err := UnmarshalValue(Bool(true), &b); err != nil {
		t.Fatal(err)
	}
	if !b {
		t.Error("expected true")
	}
}

func TestUnmarshalFloat64(t *testing.T) {
	var f float64
	if err := UnmarshalValue(NumberVal(3.14), &f); err != nil {
		t.Fatal(err)
	}
	if f != 3.14 {
		t.Errorf("expected 3.14, got %v", f)
	}
}

func TestUnmarshalInt(t *testing.T) {
	var n int
	if err := UnmarshalValue(NumberVal(42), &n); err != nil {
		t.Fatal(err)
	}
	if n != 42 {
		t.Errorf("expected 42, got %d", n)
	}
}

func TestUnmarshalUint(t *testing.T) {
	var n uint
	if err := UnmarshalValue(NumberVal(7), &n); err != nil {
		t.Fatal(err)
	}
	if n != 7 {
		t.Errorf("expected 7, got %d", n)
	}
}

func TestUnmarshalString(t *testing.T) {
	var s string
	if err := UnmarshalValue(StringVal("hello"), &s); err != nil {
		t.Fatal(err)
	}
	if s != "hello" {
		t.Errorf("expected hello, got %s", s)
	}
}

func TestUnmarshalNullPointer(t *testing.T) {
	n := 42
	p := &n
	if err := UnmarshalValue(Null(), &p); err != nil {
		t.Fatal(err)
	}
	if p != nil {
		t.Error("expected nil pointer")
	}
}

// ── Number precision ────────────────────────────────────────────────────

func TestUnmarshalIntOverflowInt8(t *testing.T) {
	var n int8
	err := UnmarshalValue(NumberVal(200), &n)
	if err == nil {
		t.Fatal("expected overflow error")
	}
	if _, ok := err.(*UnmarshalTypeError); !ok {
		t.Fatalf("expected UnmarshalTypeError, got %T", err)
	}
}

func TestUnmarshalUintNegative(t *testing.T) {
	var n uint
	err := UnmarshalValue(NumberVal(-1), &n)
	if err == nil {
		t.Fatal("expected error for negative uint")
	}
}

func TestUnmarshalNonIntegerToInt(t *testing.T) {
	var n int
	err := UnmarshalValue(NumberVal(3.5), &n)
	if err == nil {
		t.Fatal("expected error for non-integer float")
	}
}

func TestUnmarshalBigIntOverflowInt64(t *testing.T) {
	var n int64
	err := UnmarshalValue(BigIntVal("99999999999999999999"), &n)
	if err == nil {
		t.Fatal("expected overflow error")
	}
}

func TestUnmarshalBigIntSuccess(t *testing.T) {
	var bi *big.Int
	if err := UnmarshalValue(BigIntVal("12345678901234567890"), &bi); err != nil {
		t.Fatal(err)
	}
	expected, _ := new(big.Int).SetString("12345678901234567890", 10)
	if bi.Cmp(expected) != 0 {
		t.Errorf("expected %s, got %s", expected, bi)
	}
}

func TestUnmarshalBigIntValue(t *testing.T) {
	var bi big.Int
	if err := UnmarshalValue(BigIntVal("999"), &bi); err != nil {
		t.Fatal(err)
	}
	if bi.Int64() != 999 {
		t.Errorf("expected 999, got %s", bi.String())
	}
}

// ── Special types ───────────────────────────────────────────────────────

func TestUnmarshalDateTime(t *testing.T) {
	now := time.Date(2024, 1, 15, 10, 30, 0, 0, time.UTC)
	var got time.Time
	if err := UnmarshalValue(DateTimeVal(now), &got); err != nil {
		t.Fatal(err)
	}
	if !got.Equal(now) {
		t.Errorf("expected %v, got %v", now, got)
	}
}

func TestUnmarshalTimeOnly(t *testing.T) {
	to := TimeOnly{Hours: 14, Minutes: 30, Seconds: 15, Milliseconds: 500}
	var got TimeOnly
	if err := UnmarshalValue(TimeOnlyVal(to), &got); err != nil {
		t.Fatal(err)
	}
	if got != to {
		t.Errorf("expected %v, got %v", to, got)
	}
}

func TestUnmarshalDuration(t *testing.T) {
	var got Duration
	if err := UnmarshalValue(DurationVal("P1Y2M3D"), &got); err != nil {
		t.Fatal(err)
	}
	if got.ISO != "P1Y2M3D" {
		t.Errorf("expected P1Y2M3D, got %s", got.ISO)
	}
}

func TestUnmarshalRegExp(t *testing.T) {
	var got RegExp
	if err := UnmarshalValue(RegExpVal("abc", "gi"), &got); err != nil {
		t.Fatal(err)
	}
	if got.Source != "abc" || got.Flags != "gi" {
		t.Errorf("expected /abc/gi, got %v", got)
	}
}

func TestUnmarshalBinary(t *testing.T) {
	src := []byte{0xDE, 0xAD, 0xBE, 0xEF}
	var got []byte
	if err := UnmarshalValue(BinaryVal(src), &got); err != nil {
		t.Fatal(err)
	}
	if len(got) != len(src) {
		t.Fatalf("expected len %d, got %d", len(src), len(got))
	}
	for i := range src {
		if got[i] != src[i] {
			t.Errorf("byte %d: expected %x, got %x", i, src[i], got[i])
		}
	}
}

// ── Collections ─────────────────────────────────────────────────────────

func TestUnmarshalArrayToSlice(t *testing.T) {
	val := ArrayVal([]Value{NumberVal(1), NumberVal(2), NumberVal(3)})
	var got []int
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if len(got) != 3 || got[0] != 1 || got[1] != 2 || got[2] != 3 {
		t.Errorf("expected [1 2 3], got %v", got)
	}
}

func TestUnmarshalTupleToSlice(t *testing.T) {
	val := TupleVal([]Value{NumberVal(10), NumberVal(20)})
	var got []int
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if len(got) != 2 || got[0] != 10 || got[1] != 20 {
		t.Errorf("expected [10 20], got %v", got)
	}
}

func TestUnmarshalSetToSlice(t *testing.T) {
	val := SetVal([]Value{NumberVal(5), NumberVal(6)})
	var got []int
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if len(got) != 2 || got[0] != 5 || got[1] != 6 {
		t.Errorf("expected [5 6], got %v", got)
	}
}

func TestUnmarshalObjectToStruct(t *testing.T) {
	type Person struct {
		Name string `rdn:"name"`
		Age  int    `rdn:"age"`
	}
	val := ObjectVal([]KeyValue{
		{Key: "name", Value: StringVal("Alice")},
		{Key: "age", Value: NumberVal(30)},
	})
	var got Person
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got.Name != "Alice" || got.Age != 30 {
		t.Errorf("expected {Alice 30}, got %+v", got)
	}
}

func TestUnmarshalObjectToMap(t *testing.T) {
	val := ObjectVal([]KeyValue{
		{Key: "a", Value: NumberVal(1)},
		{Key: "b", Value: NumberVal(2)},
	})
	var got map[string]int
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got["a"] != 1 || got["b"] != 2 {
		t.Errorf("expected map[a:1 b:2], got %v", got)
	}
}

func TestUnmarshalMapToGoMap(t *testing.T) {
	val := MapVal([]MapEntry{
		{Key: NumberVal(1), Value: StringVal("one")},
		{Key: NumberVal(2), Value: StringVal("two")},
	})
	var got map[int]string
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got[1] != "one" || got[2] != "two" {
		t.Errorf("expected map[1:one 2:two], got %v", got)
	}
}

// ── interface{} defaults ────────────────────────────────────────────────

func TestUnmarshalDefaultGoValues(t *testing.T) {
	tests := []struct {
		name string
		val  Value
		chk  func(any) bool
	}{
		{"null", Null(), func(v any) bool { return v == nil }},
		{"bool", Bool(true), func(v any) bool { b, ok := v.(bool); return ok && b }},
		{"number", NumberVal(42), func(v any) bool { f, ok := v.(float64); return ok && f == 42 }},
		{"bigint", BigIntVal("99"), func(v any) bool { bi, ok := v.(*big.Int); return ok && bi.Int64() == 99 }},
		{"string", StringVal("hi"), func(v any) bool { s, ok := v.(string); return ok && s == "hi" }},
		{"array", ArrayVal([]Value{NumberVal(1)}), func(v any) bool { a, ok := v.([]any); return ok && len(a) == 1 }},
		{"object", ObjectVal([]KeyValue{{Key: "k", Value: NumberVal(1)}}), func(v any) bool { m, ok := v.(map[string]any); return ok && m["k"].(float64) == 1 }},
		{"datetime", DateTimeVal(time.Date(2024, 1, 1, 0, 0, 0, 0, time.UTC)), func(v any) bool { _, ok := v.(time.Time); return ok }},
		{"timeonly", TimeOnlyVal(TimeOnly{Hours: 12}), func(v any) bool { _, ok := v.(TimeOnly); return ok }},
		{"duration", DurationVal("P1D"), func(v any) bool { d, ok := v.(Duration); return ok && d.ISO == "P1D" }},
		{"regexp", RegExpVal("abc", "g"), func(v any) bool { r, ok := v.(RegExp); return ok && r.Source == "abc" }},
		{"binary", BinaryVal([]byte{1, 2}), func(v any) bool { b, ok := v.([]byte); return ok && len(b) == 2 }},
		{"map", MapVal([]MapEntry{{Key: NumberVal(1), Value: StringVal("a")}}), func(v any) bool { _, ok := v.([]MapEntry); return ok }},
		{"set", SetVal([]Value{NumberVal(1)}), func(v any) bool { a, ok := v.(Set[any]); return ok && len(a) == 1 }},
		{"tuple", TupleVal([]Value{NumberVal(1)}), func(v any) bool { a, ok := v.(Tuple); return ok && len(a) == 1 }},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			var got any
			if err := UnmarshalValue(tt.val, &got); err != nil {
				t.Fatal(err)
			}
			if !tt.chk(got) {
				t.Errorf("check failed for %s: got %v (%T)", tt.name, got, got)
			}
		})
	}
}

// ── Struct field matching ───────────────────────────────────────────────

func TestUnmarshalStructTagName(t *testing.T) {
	type S struct {
		X int `rdn:"x_val"`
	}
	val := ObjectVal([]KeyValue{{Key: "x_val", Value: NumberVal(5)}})
	var got S
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got.X != 5 {
		t.Errorf("expected 5, got %d", got.X)
	}
}

func TestUnmarshalStructUnknownFieldsIgnored(t *testing.T) {
	type S struct {
		A int `rdn:"a"`
	}
	val := ObjectVal([]KeyValue{
		{Key: "a", Value: NumberVal(1)},
		{Key: "unknown", Value: StringVal("ignored")},
	})
	var got S
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got.A != 1 {
		t.Errorf("expected 1, got %d", got.A)
	}
}

func TestUnmarshalStructEmbedded(t *testing.T) {
	type Base struct {
		ID int `rdn:"id"`
	}
	type Derived struct {
		Base
		Name string `rdn:"name"`
	}
	val := ObjectVal([]KeyValue{
		{Key: "id", Value: NumberVal(42)},
		{Key: "name", Value: StringVal("test")},
	})
	var got Derived
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got.ID != 42 || got.Name != "test" {
		t.Errorf("expected {42 test}, got %+v", got)
	}
}

// ── Unmarshaler interface ───────────────────────────────────────────────

type customUnmarshal struct {
	Data string
}

func (c *customUnmarshal) UnmarshalRDN(val Value) error {
	c.Data = "custom:" + val.Str()
	return nil
}

// compile-time check
var _ Unmarshaler = (*customUnmarshal)(nil)

func TestUnmarshalCustomUnmarshaler(t *testing.T) {
	var got customUnmarshal
	if err := UnmarshalValue(StringVal("hello"), &got); err != nil {
		t.Fatal(err)
	}
	if got.Data != "custom:hello" {
		t.Errorf("expected custom:hello, got %s", got.Data)
	}
}

// ── TextUnmarshaler ─────────────────────────────────────────────────────

type textType struct {
	Val string
}

func (tt *textType) UnmarshalText(data []byte) error {
	tt.Val = "text:" + string(data)
	return nil
}

// compile-time check
var _ encoding.TextUnmarshaler = (*textType)(nil)

func TestUnmarshalTextUnmarshaler(t *testing.T) {
	var got textType
	if err := UnmarshalValue(StringVal("world"), &got); err != nil {
		t.Fatal(err)
	}
	if got.Val != "text:world" {
		t.Errorf("expected text:world, got %s", got.Val)
	}
}

// ── Pointer allocation ──────────────────────────────────────────────────

func TestUnmarshalPointerAllocation(t *testing.T) {
	var p *int
	if err := UnmarshalValue(NumberVal(99), &p); err != nil {
		t.Fatal(err)
	}
	if p == nil {
		t.Fatal("expected non-nil pointer")
	}
	if *p != 99 {
		t.Errorf("expected 99, got %d", *p)
	}
}

// ── Errors ──────────────────────────────────────────────────────────────

func TestUnmarshalInvalidUnmarshalErrorNil(t *testing.T) {
	err := UnmarshalValue(Null(), nil)
	if err == nil {
		t.Fatal("expected error")
	}
	if _, ok := err.(*InvalidUnmarshalError); !ok {
		t.Fatalf("expected InvalidUnmarshalError, got %T", err)
	}
}

func TestUnmarshalInvalidUnmarshalErrorNonPointer(t *testing.T) {
	var n int
	err := UnmarshalValue(NumberVal(1), n)
	if err == nil {
		t.Fatal("expected error")
	}
	if _, ok := err.(*InvalidUnmarshalError); !ok {
		t.Fatalf("expected InvalidUnmarshalError, got %T", err)
	}
}

func TestUnmarshalTypeMismatch(t *testing.T) {
	var b bool
	err := UnmarshalValue(StringVal("hello"), &b)
	if err == nil {
		t.Fatal("expected type mismatch error")
	}
	if _, ok := err.(*UnmarshalTypeError); !ok {
		t.Fatalf("expected UnmarshalTypeError, got %T: %v", err, err)
	}
}

// ── Value target ────────────────────────────────────────────────────────

func TestUnmarshalIntoValue(t *testing.T) {
	src := ArrayVal([]Value{NumberVal(1), StringVal("two")})
	var got Value
	if err := UnmarshalValue(src, &got); err != nil {
		t.Fatal(err)
	}
	if !got.Equal(src) {
		t.Errorf("expected %v, got %v", src, got)
	}
}

// ── RawMessage target ───────────────────────────────────────────────────

func TestUnmarshalIntoRawMessage(t *testing.T) {
	src := NumberVal(42)
	var got RawMessage
	if err := UnmarshalValue(src, &got); err != nil {
		t.Fatal(err)
	}
	if string(got) != "42" {
		t.Errorf("expected 42, got %s", string(got))
	}
}

// ── Full Unmarshal (integration) ────────────────────────────────────────

func TestUnmarshalFull(t *testing.T) {
	type Config struct {
		Name    string  `rdn:"name"`
		Count   int     `rdn:"count"`
		Enabled bool    `rdn:"enabled"`
		Rate    float64 `rdn:"rate"`
	}
	data := []byte(`{"name": "test", "count": 5, "enabled": true, "rate": 0.75}`)
	var got Config
	if err := Unmarshal(data, &got); err != nil {
		t.Fatal(err)
	}
	if got.Name != "test" || got.Count != 5 || !got.Enabled || got.Rate != 0.75 {
		t.Errorf("unexpected result: %+v", got)
	}
}

func TestUnmarshalFullArray(t *testing.T) {
	data := []byte(`[1, 2, 3]`)
	var got []int
	if err := Unmarshal(data, &got); err != nil {
		t.Fatal(err)
	}
	if len(got) != 3 || got[0] != 1 || got[1] != 2 || got[2] != 3 {
		t.Errorf("expected [1 2 3], got %v", got)
	}
}

// ── Array (fixed-size) ──────────────────────────────────────────────────

func TestUnmarshalTupleToArray(t *testing.T) {
	val := TupleVal([]Value{NumberVal(10), NumberVal(20), NumberVal(30)})
	var got [3]int
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got != [3]int{10, 20, 30} {
		t.Errorf("expected [10 20 30], got %v", got)
	}
}

// ── Number type ─────────────────────────────────────────────────────────

func TestUnmarshalNumber(t *testing.T) {
	var got Number
	if err := UnmarshalValue(NumberVal(3.14), &got); err != nil {
		t.Fatal(err)
	}
	if string(got) != "3.14" {
		t.Errorf("expected 3.14, got %s", string(got))
	}
}

func TestUnmarshalNumberBigInt(t *testing.T) {
	var got Number
	if err := UnmarshalValue(BigIntVal("12345"), &got); err != nil {
		t.Fatal(err)
	}
	if string(got) != "12345" {
		t.Errorf("expected 12345, got %s", string(got))
	}
}

// ── Null to various types ───────────────────────────────────────────────

func TestUnmarshalNullToBool(t *testing.T) {
	b := true
	if err := UnmarshalValue(Null(), &b); err != nil {
		t.Fatal(err)
	}
	if b != false {
		t.Error("expected false after null")
	}
}

func TestUnmarshalNullToInt(t *testing.T) {
	n := 42
	if err := UnmarshalValue(Null(), &n); err != nil {
		t.Fatal(err)
	}
	if n != 0 {
		t.Errorf("expected 0, got %d", n)
	}
}

func TestUnmarshalNullToSlice(t *testing.T) {
	s := []int{1, 2, 3}
	if err := UnmarshalValue(Null(), &s); err != nil {
		t.Fatal(err)
	}
	if s != nil {
		t.Error("expected nil slice")
	}
}

// ── JSON tag fallback ───────────────────────────────────────────────────

func TestUnmarshalJSONTagFallback(t *testing.T) {
	type S struct {
		X int `json:"x_json"`
	}
	val := ObjectVal([]KeyValue{{Key: "x_json", Value: NumberVal(7)}})
	var got S
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatal(err)
	}
	if got.X != 7 {
		t.Errorf("expected 7, got %d", got.X)
	}
}

// ── BigInt to int (within range) ────────────────────────────────────────

func TestUnmarshalBigIntToInt(t *testing.T) {
	var n int
	if err := UnmarshalValue(BigIntVal("42"), &n); err != nil {
		t.Fatal(err)
	}
	if n != 42 {
		t.Errorf("expected 42, got %d", n)
	}
}

// ── Verify reflect types match ──────────────────────────────────────────

func TestUnmarshalTypeVars(t *testing.T) {
	// Ensure the type vars are correct
	if timeType != reflect.TypeOf(time.Time{}) {
		t.Error("timeType mismatch")
	}
	if bigIntPtrType != reflect.TypeOf((*big.Int)(nil)) {
		t.Error("bigIntPtrType mismatch")
	}
	if valueType != reflect.TypeOf(Value{}) {
		t.Error("valueType mismatch")
	}
}
