package rdn

import (
	"math"
	"math/big"
	"testing"
	"time"
)

// ── Helpers ──────────────────────────────────────────────────────────────

func mustMarshalValue(t *testing.T, v any) Value {
	t.Helper()
	val, err := MarshalValue(v)
	if err != nil {
		t.Fatalf("MarshalValue(%v) unexpected error: %v", v, err)
	}
	return val
}

func assertValueEqual(t *testing.T, got, want Value) {
	t.Helper()
	if !got.Equal(want) {
		t.Errorf("got %s (%v), want %s (%v)", got.Kind(), got, want.Kind(), want)
	}
}

// ── Primitives ───────────────────────────────────────────────────────────

func TestMarshalBool(t *testing.T) {
	assertValueEqual(t, mustMarshalValue(t, true), Bool(true))
	assertValueEqual(t, mustMarshalValue(t, false), Bool(false))
}

func TestMarshalInt(t *testing.T) {
	assertValueEqual(t, mustMarshalValue(t, 42), NumberVal(42))
	assertValueEqual(t, mustMarshalValue(t, int64(100)), NumberVal(100))
}

func TestMarshalUint64(t *testing.T) {
	assertValueEqual(t, mustMarshalValue(t, uint64(42)), NumberVal(42))
}

func TestMarshalFloat64(t *testing.T) {
	assertValueEqual(t, mustMarshalValue(t, 3.14), NumberVal(3.14))
}

func TestMarshalString(t *testing.T) {
	assertValueEqual(t, mustMarshalValue(t, "hello"), StringVal("hello"))
}

func TestMarshalNil(t *testing.T) {
	assertValueEqual(t, mustMarshalValue(t, nil), Null())
}

// ── Special numbers ─────────────────────────────────────────────────────

func TestMarshalNaN(t *testing.T) {
	val := mustMarshalValue(t, math.NaN())
	if !val.IsNaN() {
		t.Error("expected NaN")
	}
}

func TestMarshalInfinity(t *testing.T) {
	val := mustMarshalValue(t, math.Inf(1))
	if !val.IsInf() || val.Float64() != math.Inf(1) {
		t.Error("expected +Infinity")
	}
	val = mustMarshalValue(t, math.Inf(-1))
	if !val.IsInf() || val.Float64() != math.Inf(-1) {
		t.Error("expected -Infinity")
	}
}

func TestMarshalUint64BigInt(t *testing.T) {
	bigU := uint64(1<<53 + 1)
	val := mustMarshalValue(t, bigU)
	if val.Kind() != KindBigInt {
		t.Errorf("expected BigInt, got %s", val.Kind())
	}
}

func TestMarshalInt64BigInt(t *testing.T) {
	bigI := int64(1<<53 + 1)
	val := mustMarshalValue(t, bigI)
	if val.Kind() != KindBigInt {
		t.Errorf("expected BigInt for large positive int64, got %s", val.Kind())
	}

	negI := int64(-(1<<53 + 1))
	val = mustMarshalValue(t, negI)
	if val.Kind() != KindBigInt {
		t.Errorf("expected BigInt for large negative int64, got %s", val.Kind())
	}
}

// ── Special types ───────────────────────────────────────────────────────

func TestMarshalTime(t *testing.T) {
	tm := time.Date(2024, 1, 15, 10, 30, 0, 0, time.UTC)
	val := mustMarshalValue(t, tm)
	if val.Kind() != KindDateTime {
		t.Fatalf("expected DateTime, got %s", val.Kind())
	}
	if !val.Time().Equal(tm) {
		t.Errorf("time mismatch: got %v, want %v", val.Time(), tm)
	}
}

func TestMarshalBigInt(t *testing.T) {
	bi := big.NewInt(999999999999999999)
	val := mustMarshalValue(t, bi)
	if val.Kind() != KindBigInt {
		t.Fatalf("expected BigInt, got %s", val.Kind())
	}

	// Nil *big.Int
	var nilBi *big.Int
	val = mustMarshalValue(t, nilBi)
	assertValueEqual(t, val, Null())
}

func TestMarshalBigIntValue(t *testing.T) {
	bi := *big.NewInt(42)
	val := mustMarshalValue(t, bi)
	if val.Kind() != KindBigInt {
		t.Fatalf("expected BigInt, got %s", val.Kind())
	}
	if val.Str() != "42" {
		t.Errorf("expected 42, got %s", val.Str())
	}
}

func TestMarshalTimeOnly(t *testing.T) {
	to := TimeOnly{Hours: 14, Minutes: 30, Seconds: 0}
	val := mustMarshalValue(t, to)
	if val.Kind() != KindTimeOnly {
		t.Fatalf("expected TimeOnly, got %s", val.Kind())
	}
	if val.TimeOnlyValue() != to {
		t.Errorf("TimeOnly mismatch")
	}
}

func TestMarshalDuration(t *testing.T) {
	d := Duration{ISO: "P1Y2M3D"}
	val := mustMarshalValue(t, d)
	if val.Kind() != KindDuration {
		t.Fatalf("expected Duration, got %s", val.Kind())
	}
	if val.Str() != "P1Y2M3D" {
		t.Errorf("expected P1Y2M3D, got %s", val.Str())
	}
}

func TestMarshalRegExp(t *testing.T) {
	re := RegExp{Source: "abc", Flags: "gi"}
	val := mustMarshalValue(t, re)
	if val.Kind() != KindRegExp {
		t.Fatalf("expected RegExp, got %s", val.Kind())
	}
	got := val.RegExpValue()
	if got.Source != "abc" || got.Flags != "gi" {
		t.Errorf("RegExp mismatch: %+v", got)
	}
}

func TestMarshalNumber(t *testing.T) {
	// Float number
	val := mustMarshalValue(t, Number("3.14"))
	if val.Kind() != KindNumber || val.Float64() != 3.14 {
		t.Errorf("expected Number 3.14, got %s %v", val.Kind(), val.Float64())
	}

	// Integer number that fits in float64
	val = mustMarshalValue(t, Number("42"))
	if val.Kind() != KindNumber || val.Float64() != 42 {
		t.Errorf("expected Number 42, got %s %v", val.Kind(), val.Float64())
	}

	// BigInt suffix
	val = mustMarshalValue(t, Number("99999999999999999n"))
	if val.Kind() != KindBigInt {
		t.Errorf("expected BigInt, got %s", val.Kind())
	}

	// Large integer → BigInt
	val = mustMarshalValue(t, Number("99999999999999999"))
	if val.Kind() != KindBigInt {
		t.Errorf("expected BigInt for large integer, got %s", val.Kind())
	}

	// Empty number
	val = mustMarshalValue(t, Number(""))
	if val.Kind() != KindNumber || val.Float64() != 0 {
		t.Errorf("expected Number 0 for empty, got %s", val.Kind())
	}
}

func TestMarshalRawMessage(t *testing.T) {
	raw := RawMessage(`{"key":"value"}`)
	val := mustMarshalValue(t, raw)
	if val.Kind() != KindObject {
		t.Fatalf("expected Object, got %s", val.Kind())
	}
	pairs := val.Object()
	if len(pairs) != 1 || pairs[0].Key != "key" {
		t.Errorf("unexpected object content")
	}

	// Nil RawMessage
	val = mustMarshalValue(t, RawMessage(nil))
	assertValueEqual(t, val, Null())
}

func TestMarshalValuePassthrough(t *testing.T) {
	orig := StringVal("hello")
	val := mustMarshalValue(t, orig)
	assertValueEqual(t, val, orig)
}

func TestMarshalBytes(t *testing.T) {
	data := []byte{0xDE, 0xAD, 0xBE, 0xEF}
	val := mustMarshalValue(t, data)
	if val.Kind() != KindBinary {
		t.Fatalf("expected Binary, got %s", val.Kind())
	}
	got := val.Bytes()
	if len(got) != len(data) {
		t.Fatalf("length mismatch: got %d, want %d", len(got), len(data))
	}
	for i := range data {
		if got[i] != data[i] {
			t.Errorf("byte %d: got %x, want %x", i, got[i], data[i])
		}
	}

	// Nil bytes
	val = mustMarshalValue(t, []byte(nil))
	assertValueEqual(t, val, Null())
}

// ── Collections ─────────────────────────────────────────────────────────

func TestMarshalSlice(t *testing.T) {
	val := mustMarshalValue(t, []int{1, 2, 3})
	if val.Kind() != KindArray {
		t.Fatalf("expected Array, got %s", val.Kind())
	}
	arr := val.Array()
	if len(arr) != 3 {
		t.Fatalf("expected 3 elements, got %d", len(arr))
	}
	for i, want := range []float64{1, 2, 3} {
		if arr[i].Float64() != want {
			t.Errorf("element %d: got %v, want %v", i, arr[i].Float64(), want)
		}
	}
}

func TestMarshalArray(t *testing.T) {
	val := mustMarshalValue(t, [3]int{10, 20, 30})
	if val.Kind() != KindArray {
		t.Fatalf("expected Array, got %s", val.Kind())
	}
	arr := val.Array()
	if len(arr) != 3 {
		t.Fatalf("expected 3 elements, got %d", len(arr))
	}
}

func TestMarshalStringMap(t *testing.T) {
	val := mustMarshalValue(t, map[string]int{"b": 2, "a": 1, "c": 3})
	if val.Kind() != KindObject {
		t.Fatalf("expected Object, got %s", val.Kind())
	}
	pairs := val.Object()
	if len(pairs) != 3 {
		t.Fatalf("expected 3 pairs, got %d", len(pairs))
	}
	// Keys should be sorted
	if pairs[0].Key != "a" || pairs[1].Key != "b" || pairs[2].Key != "c" {
		t.Errorf("keys not sorted: %s, %s, %s", pairs[0].Key, pairs[1].Key, pairs[2].Key)
	}
}

func TestMarshalNonStringMap(t *testing.T) {
	val := mustMarshalValue(t, map[int]string{1: "one", 2: "two"})
	if val.Kind() != KindMap {
		t.Fatalf("expected Map, got %s", val.Kind())
	}
	entries := val.Map()
	if len(entries) != 2 {
		t.Fatalf("expected 2 entries, got %d", len(entries))
	}
}

func TestMarshalNestedStruct(t *testing.T) {
	type Inner struct {
		X int `rdn:"x"`
	}
	type Outer struct {
		Name  string `rdn:"name"`
		Inner Inner  `rdn:"inner"`
	}
	val := mustMarshalValue(t, Outer{Name: "test", Inner: Inner{X: 42}})
	if val.Kind() != KindObject {
		t.Fatalf("expected Object, got %s", val.Kind())
	}
	pairs := val.Object()
	if len(pairs) != 2 {
		t.Fatalf("expected 2 pairs, got %d", len(pairs))
	}
	if pairs[0].Key != "name" || pairs[0].Value.Str() != "test" {
		t.Errorf("unexpected first pair: %+v", pairs[0])
	}
	if pairs[1].Key != "inner" || pairs[1].Value.Kind() != KindObject {
		t.Errorf("unexpected second pair: %+v", pairs[1])
	}
}

// ── Struct tags ──────────────────────────────────────────────────────────

func TestMarshalStructTags(t *testing.T) {
	type Tagged struct {
		Name    string `rdn:"name"`
		Ignored string `rdn:"-"`
		Default string
	}
	val := mustMarshalValue(t, Tagged{Name: "hello", Ignored: "skip", Default: "def"})
	pairs := val.Object()

	// Should have "name" and "Default", not "Ignored"
	names := make(map[string]bool)
	for _, p := range pairs {
		names[p.Key] = true
	}
	if !names["name"] {
		t.Error("missing 'name' field")
	}
	if names["Ignored"] || names["-"] {
		t.Error("should not include ignored field")
	}
	if !names["Default"] {
		t.Error("missing 'Default' field (no tag → use field name)")
	}
}

func TestMarshalOmitempty(t *testing.T) {
	type OE struct {
		A string `rdn:"a,omitempty"`
		B int    `rdn:"b,omitempty"`
		C string `rdn:"c,omitempty"`
	}
	val := mustMarshalValue(t, OE{A: "", B: 0, C: "present"})
	pairs := val.Object()
	if len(pairs) != 1 {
		t.Fatalf("expected 1 pair (only c), got %d", len(pairs))
	}
	if pairs[0].Key != "c" {
		t.Errorf("expected key 'c', got %s", pairs[0].Key)
	}
}

func TestMarshalStringTag(t *testing.T) {
	type Quoted struct {
		Num  int  `rdn:"num,string"`
		Flag bool `rdn:"flag,string"`
	}
	val := mustMarshalValue(t, Quoted{Num: 42, Flag: true})
	pairs := val.Object()
	if pairs[0].Value.Kind() != KindString {
		t.Errorf("expected string for num, got %s", pairs[0].Value.Kind())
	}
	if pairs[0].Value.Str() != "42" {
		t.Errorf("expected '42', got %s", pairs[0].Value.Str())
	}
	if pairs[1].Value.Kind() != KindString {
		t.Errorf("expected string for flag, got %s", pairs[1].Value.Kind())
	}
	if pairs[1].Value.Str() != "true" {
		t.Errorf("expected 'true', got %s", pairs[1].Value.Str())
	}
}

func TestMarshalJSONTagFallback(t *testing.T) {
	type Fallback struct {
		Name string `json:"json_name"`
	}
	val := mustMarshalValue(t, Fallback{Name: "hello"})
	pairs := val.Object()
	if pairs[0].Key != "json_name" {
		t.Errorf("expected 'json_name' from json tag fallback, got %s", pairs[0].Key)
	}
}

// ── Embedded structs ────────────────────────────────────────────────────

func TestMarshalEmbeddedStruct(t *testing.T) {
	type Base struct {
		ID int `rdn:"id"`
	}
	type Extended struct {
		Base
		Name string `rdn:"name"`
	}
	val := mustMarshalValue(t, Extended{Base: Base{ID: 1}, Name: "test"})
	pairs := val.Object()
	names := make(map[string]bool)
	for _, p := range pairs {
		names[p.Key] = true
	}
	if !names["id"] {
		t.Error("embedded field 'id' not promoted")
	}
	if !names["name"] {
		t.Error("missing 'name' field")
	}
}

// ── Marshaler interface ─────────────────────────────────────────────────

type customMarshaler struct {
	Val string
}

func (c customMarshaler) MarshalRDN() (Value, error) {
	return StringVal("custom:" + c.Val), nil
}

func TestMarshalCustomMarshaler(t *testing.T) {
	val := mustMarshalValue(t, customMarshaler{Val: "test"})
	assertValueEqual(t, val, StringVal("custom:test"))
}

// ── TextMarshaler interface ─────────────────────────────────────────────

type textMarshaled struct {
	Val string
}

func (tm textMarshaled) MarshalText() ([]byte, error) {
	return []byte("text:" + tm.Val), nil
}

func TestMarshalTextMarshaler(t *testing.T) {
	val := mustMarshalValue(t, textMarshaled{Val: "hello"})
	assertValueEqual(t, val, StringVal("text:hello"))
}

// ── Marshaler takes precedence over TextMarshaler ───────────────────────

type bothMarshaler struct {
	Val string
}

func (b bothMarshaler) MarshalRDN() (Value, error) {
	return StringVal("rdn:" + b.Val), nil
}

func (b bothMarshaler) MarshalText() ([]byte, error) {
	return []byte("text:" + b.Val), nil
}

func TestMarshalPrecedence(t *testing.T) {
	val := mustMarshalValue(t, bothMarshaler{Val: "test"})
	assertValueEqual(t, val, StringVal("rdn:test"))
}

// ── Nil handling ────────────────────────────────────────────────────────

func TestMarshalNilPointer(t *testing.T) {
	var p *int
	assertValueEqual(t, mustMarshalValue(t, p), Null())
}

func TestMarshalNilSlice(t *testing.T) {
	var s []int
	assertValueEqual(t, mustMarshalValue(t, s), Null())
}

func TestMarshalNilMap(t *testing.T) {
	var m map[string]int
	assertValueEqual(t, mustMarshalValue(t, m), Null())
}

func TestMarshalNilInterface(t *testing.T) {
	var i any
	assertValueEqual(t, mustMarshalValue(t, i), Null())
}

// ── Empty vs nil ────────────────────────────────────────────────────────

func TestMarshalEmptySlice(t *testing.T) {
	val := mustMarshalValue(t, []int{})
	if val.Kind() != KindArray {
		t.Fatalf("expected Array for empty slice, got %s", val.Kind())
	}
	if val.Len() != 0 {
		t.Errorf("expected empty array, got length %d", val.Len())
	}
}

// ── Circular reference ──────────────────────────────────────────────────

func TestMarshalCircularReference(t *testing.T) {
	type Node struct {
		Name string `rdn:"name"`
		Next *Node  `rdn:"next"`
	}
	a := &Node{Name: "a"}
	b := &Node{Name: "b"}
	a.Next = b
	b.Next = a // cycle!

	_, err := MarshalValue(a)
	if err == nil {
		t.Fatal("expected error for circular reference")
	}
	me, ok := err.(*MarshalError)
	if !ok {
		t.Fatalf("expected *MarshalError, got %T", err)
	}
	if me.Err.Error() != "circular reference detected" {
		t.Errorf("unexpected error message: %s", me.Err.Error())
	}
}

// ── Unsupported types ───────────────────────────────────────────────────

func TestMarshalUnsupportedChan(t *testing.T) {
	ch := make(chan int)
	_, err := MarshalValue(ch)
	if err == nil {
		t.Fatal("expected error for chan type")
	}
	if _, ok := err.(*MarshalError); !ok {
		t.Fatalf("expected *MarshalError, got %T", err)
	}
}

func TestMarshalUnsupportedFunc(t *testing.T) {
	fn := func() {}
	_, err := MarshalValue(fn)
	if err == nil {
		t.Fatal("expected error for func type")
	}
	if _, ok := err.(*MarshalError); !ok {
		t.Fatalf("expected *MarshalError, got %T", err)
	}
}

// ── Marshal output (roundtrip) ──────────────────────────────────────────

func TestMarshalOutput(t *testing.T) {
	type Obj struct {
		Name string `rdn:"name"`
		Age  int    `rdn:"age"`
	}
	data, err := Marshal(Obj{Name: "Alice", Age: 30})
	if err != nil {
		t.Fatalf("Marshal error: %v", err)
	}
	// Parse back
	val, err := Parse(data)
	if err != nil {
		t.Fatalf("Parse error on marshaled output: %v\nData: %s", err, string(data))
	}
	if val.Kind() != KindObject {
		t.Fatalf("expected Object, got %s", val.Kind())
	}
	pairs := val.Object()
	if len(pairs) != 2 {
		t.Fatalf("expected 2 pairs, got %d", len(pairs))
	}
}

// ── MarshalIndent ───────────────────────────────────────────────────────

func TestMarshalIndent(t *testing.T) {
	type Obj struct {
		A int `rdn:"a"`
		B int `rdn:"b"`
	}
	data, err := MarshalIndent(Obj{A: 1, B: 2}, "", "  ")
	if err != nil {
		t.Fatalf("MarshalIndent error: %v", err)
	}
	s := string(data)
	// Should contain newlines and indentation
	if len(s) == 0 {
		t.Fatal("empty output")
	}
	// Parse back to verify validity
	_, err = Parse(data)
	if err != nil {
		t.Fatalf("Parse error on indented output: %v\nData: %s", err, s)
	}
	// Check it actually contains indentation
	if !contains(s, "\n") {
		t.Error("indented output should contain newlines")
	}
	if !contains(s, "  ") {
		t.Error("indented output should contain indentation")
	}
}

func contains(s, substr string) bool {
	for i := 0; i <= len(s)-len(substr); i++ {
		if s[i:i+len(substr)] == substr {
			return true
		}
	}
	return false
}
