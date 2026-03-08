package rdn

import (
	"testing"
)

// ── Set tests ────────────────────────────────────────────────────────────

func TestSetIntRoundTrip(t *testing.T) {
	s := Set[int]{1, 2, 3}
	val, err := MarshalValue(s)
	if err != nil {
		t.Fatalf("MarshalValue(Set[int]) error: %v", err)
	}
	if val.Kind() != KindSet {
		t.Fatalf("expected KindSet, got %s", val.Kind())
	}
	elems := val.Array()
	if len(elems) != 3 {
		t.Fatalf("expected 3 elements, got %d", len(elems))
	}

	var got Set[int]
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatalf("UnmarshalValue(Set[int]) error: %v", err)
	}
	if len(got) != 3 || got[0] != 1 || got[1] != 2 || got[2] != 3 {
		t.Errorf("got %v, want [1 2 3]", got)
	}
}

func TestSetStringRoundTrip(t *testing.T) {
	s := Set[string]{"a", "b"}
	val, err := MarshalValue(s)
	if err != nil {
		t.Fatalf("MarshalValue(Set[string]) error: %v", err)
	}
	if val.Kind() != KindSet {
		t.Fatalf("expected KindSet, got %s", val.Kind())
	}

	var got Set[string]
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatalf("UnmarshalValue(Set[string]) error: %v", err)
	}
	if len(got) != 2 || got[0] != "a" || got[1] != "b" {
		t.Errorf("got %v, want [a b]", got)
	}
}

func TestSetUnmarshalFromArray(t *testing.T) {
	arr := ArrayVal([]Value{NumberVal(10), NumberVal(20)})
	var got Set[int]
	if err := UnmarshalValue(arr, &got); err != nil {
		t.Fatalf("UnmarshalValue(Set[int] from Array) error: %v", err)
	}
	if len(got) != 2 || got[0] != 10 || got[1] != 20 {
		t.Errorf("got %v, want [10 20]", got)
	}
}

func TestSetWrongKindError(t *testing.T) {
	str := StringVal("not a set")
	var got Set[int]
	err := UnmarshalValue(str, &got)
	if err == nil {
		t.Fatal("expected error unmarshaling string into Set[int]")
	}
}

// ── Tuple tests ──────────────────────────────────────────────────────────

func TestTupleRoundTrip(t *testing.T) {
	tup := Tuple{"hello", 42, true}
	val, err := MarshalValue(tup)
	if err != nil {
		t.Fatalf("MarshalValue(Tuple) error: %v", err)
	}
	if val.Kind() != KindTuple {
		t.Fatalf("expected KindTuple, got %s", val.Kind())
	}
	elems := val.Array()
	if len(elems) != 3 {
		t.Fatalf("expected 3 elements, got %d", len(elems))
	}

	var got Tuple
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatalf("UnmarshalValue(Tuple) error: %v", err)
	}
	if len(got) != 3 {
		t.Fatalf("expected 3 elements, got %d", len(got))
	}
	if got[0] != "hello" {
		t.Errorf("got[0] = %v, want \"hello\"", got[0])
	}
	if got[1] != float64(42) {
		t.Errorf("got[1] = %v, want 42", got[1])
	}
	if got[2] != true {
		t.Errorf("got[2] = %v, want true", got[2])
	}
}

func TestTupleUnmarshalFromArray(t *testing.T) {
	arr := ArrayVal([]Value{StringVal("x"), NumberVal(99)})
	var got Tuple
	if err := UnmarshalValue(arr, &got); err != nil {
		t.Fatalf("UnmarshalValue(Tuple from Array) error: %v", err)
	}
	if len(got) != 2 || got[0] != "x" || got[1] != float64(99) {
		t.Errorf("got %v, want [x 99]", got)
	}
}

func TestTupleWrongKindError(t *testing.T) {
	str := StringVal("not a tuple")
	var got Tuple
	err := UnmarshalValue(str, &got)
	if err == nil {
		t.Fatal("expected error unmarshaling string into Tuple")
	}
}

// ── OrderedMap tests ─────────────────────────────────────────────────────

func TestOrderedMapStringIntRoundTrip(t *testing.T) {
	var m OrderedMap[string, int]
	m.Set("a", 1)
	m.Set("b", 2)
	m.Set("c", 3)

	val, err := MarshalValue(&m)
	if err != nil {
		t.Fatalf("MarshalValue(OrderedMap) error: %v", err)
	}
	if val.Kind() != KindMap {
		t.Fatalf("expected KindMap, got %s", val.Kind())
	}

	var got OrderedMap[string, int]
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatalf("UnmarshalValue(OrderedMap) error: %v", err)
	}
	if got.Len() != 3 {
		t.Fatalf("expected 3 entries, got %d", got.Len())
	}
	entries := got.Entries()
	if entries[0].Key != "a" || entries[0].Value != 1 {
		t.Errorf("entry[0] = %v, want {a 1}", entries[0])
	}
	if entries[1].Key != "b" || entries[1].Value != 2 {
		t.Errorf("entry[1] = %v, want {b 2}", entries[1])
	}
	if entries[2].Key != "c" || entries[2].Value != 3 {
		t.Errorf("entry[2] = %v, want {c 3}", entries[2])
	}
}

func TestOrderedMapIntStringRoundTrip(t *testing.T) {
	var m OrderedMap[int, string]
	m.Set(10, "ten")
	m.Set(20, "twenty")

	val, err := MarshalValue(&m)
	if err != nil {
		t.Fatalf("MarshalValue(OrderedMap[int,string]) error: %v", err)
	}
	if val.Kind() != KindMap {
		t.Fatalf("expected KindMap, got %s", val.Kind())
	}

	var got OrderedMap[int, string]
	if err := UnmarshalValue(val, &got); err != nil {
		t.Fatalf("UnmarshalValue(OrderedMap[int,string]) error: %v", err)
	}
	if got.Len() != 2 {
		t.Fatalf("expected 2 entries, got %d", got.Len())
	}
	v1, ok := got.Get(10)
	if !ok || v1 != "ten" {
		t.Errorf("Get(10) = (%v, %v), want (ten, true)", v1, ok)
	}
	v2, ok := got.Get(20)
	if !ok || v2 != "twenty" {
		t.Errorf("Get(20) = (%v, %v), want (twenty, true)", v2, ok)
	}
}

func TestOrderedMapUnmarshalFromObject(t *testing.T) {
	obj := ObjectVal([]KeyValue{
		{Key: "x", Value: NumberVal(10)},
		{Key: "y", Value: NumberVal(20)},
	})
	var got OrderedMap[string, int]
	if err := UnmarshalValue(obj, &got); err != nil {
		t.Fatalf("UnmarshalValue(OrderedMap from Object) error: %v", err)
	}
	if got.Len() != 2 {
		t.Fatalf("expected 2 entries, got %d", got.Len())
	}
	v, ok := got.Get("x")
	if !ok || v != 10 {
		t.Errorf("Get(x) = (%v, %v), want (10, true)", v, ok)
	}
	v, ok = got.Get("y")
	if !ok || v != 20 {
		t.Errorf("Get(y) = (%v, %v), want (20, true)", v, ok)
	}
}

func TestOrderedMapUtilityMethods(t *testing.T) {
	var m OrderedMap[string, int]
	m.Set("a", 1)
	m.Set("b", 2)
	m.Set("c", 3)

	// Len
	if m.Len() != 3 {
		t.Errorf("Len() = %d, want 3", m.Len())
	}

	// Keys
	keys := m.Keys()
	if len(keys) != 3 || keys[0] != "a" || keys[1] != "b" || keys[2] != "c" {
		t.Errorf("Keys() = %v, want [a b c]", keys)
	}

	// Values
	vals := m.Values()
	if len(vals) != 3 || vals[0] != 1 || vals[1] != 2 || vals[2] != 3 {
		t.Errorf("Values() = %v, want [1 2 3]", vals)
	}

	// Get existing
	v, ok := m.Get("b")
	if !ok || v != 2 {
		t.Errorf("Get(b) = (%v, %v), want (2, true)", v, ok)
	}

	// Get missing
	v, ok = m.Get("z")
	if ok || v != 0 {
		t.Errorf("Get(z) = (%v, %v), want (0, false)", v, ok)
	}

	// Delete
	m.Delete("b")
	if m.Len() != 2 {
		t.Errorf("Len() after Delete = %d, want 2", m.Len())
	}
	_, ok = m.Get("b")
	if ok {
		t.Error("Get(b) after Delete should return false")
	}

	// Delete non-existent (should not panic)
	m.Delete("z")
	if m.Len() != 2 {
		t.Errorf("Len() after deleting non-existent = %d, want 2", m.Len())
	}
}

func TestOrderedMapSetUpdate(t *testing.T) {
	var m OrderedMap[string, int]
	m.Set("a", 1)
	m.Set("b", 2)
	m.Set("a", 99) // update existing

	if m.Len() != 2 {
		t.Fatalf("Len() = %d, want 2 (update should not add new entry)", m.Len())
	}
	v, ok := m.Get("a")
	if !ok || v != 99 {
		t.Errorf("Get(a) = (%v, %v), want (99, true)", v, ok)
	}
	// Verify order preserved: a should still be first
	entries := m.Entries()
	if entries[0].Key != "a" || entries[0].Value != 99 {
		t.Errorf("entry[0] = %v, want {a 99}", entries[0])
	}
	if entries[1].Key != "b" || entries[1].Value != 2 {
		t.Errorf("entry[1] = %v, want {b 2}", entries[1])
	}
}
