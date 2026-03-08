package rdn

import (
	"bytes"
	"io"
	"strings"
	"testing"
	"time"
)

func TestDecoderBasic(t *testing.T) {
	r := strings.NewReader(`{"name": "Alice", "age": 30}`)
	dec := NewDecoder(r)

	var v Value
	if err := dec.Decode(&v); err != nil {
		t.Fatalf("Decode failed: %v", err)
	}
	if v.Kind() != KindObject {
		t.Fatalf("expected KindObject, got %v", v.Kind())
	}
	pairs := v.Object()
	if len(pairs) != 2 {
		t.Fatalf("expected 2 pairs, got %d", len(pairs))
	}
	if pairs[0].Key != "name" || pairs[0].Value.Str() != "Alice" {
		t.Errorf("unexpected first pair: %s = %v", pairs[0].Key, pairs[0].Value)
	}
}

func TestDecoderRDNTypes(t *testing.T) {
	input := `{"created": @2024-01-15T10:30:00.000Z, "count": 42n, "tags": Set{"go", "rdn"}}`
	dec := NewDecoder(strings.NewReader(input))

	var v Value
	if err := dec.Decode(&v); err != nil {
		t.Fatalf("Decode failed: %v", err)
	}
	if v.Kind() != KindObject {
		t.Fatalf("expected KindObject, got %v", v.Kind())
	}
	pairs := v.Object()
	if len(pairs) != 3 {
		t.Fatalf("expected 3 pairs, got %d", len(pairs))
	}
	if pairs[0].Value.Kind() != KindDateTime {
		t.Errorf("expected KindDateTime, got %v", pairs[0].Value.Kind())
	}
	if pairs[1].Value.Kind() != KindBigInt {
		t.Errorf("expected KindBigInt, got %v", pairs[1].Value.Kind())
	}
	if pairs[2].Value.Kind() != KindSet {
		t.Errorf("expected KindSet, got %v", pairs[2].Value.Kind())
	}
}

func TestDecoderInvalid(t *testing.T) {
	dec := NewDecoder(strings.NewReader(`{invalid`))
	var v Value
	if err := dec.Decode(&v); err == nil {
		t.Fatal("expected error for invalid RDN")
	}
}

func TestDecoderReaderError(t *testing.T) {
	dec := NewDecoder(&failReader{})
	var v Value
	if err := dec.Decode(&v); err == nil {
		t.Fatal("expected error from failing reader")
	}
}

type failReader struct{}

func (f *failReader) Read([]byte) (int, error) {
	return 0, io.ErrUnexpectedEOF
}

func TestEncoderBasic(t *testing.T) {
	var buf bytes.Buffer
	enc := NewEncoder(&buf)

	v := ObjectVal([]KeyValue{
		{Key: "name", Value: StringVal("Alice")},
		{Key: "age", Value: NumberVal(30)},
	})
	if err := enc.Encode(v); err != nil {
		t.Fatalf("Encode failed: %v", err)
	}

	got := buf.String()
	expected := `{"name":"Alice","age":30}` + "\n"
	if got != expected {
		t.Errorf("got %q, expected %q", got, expected)
	}
}

func TestEncoderIndent(t *testing.T) {
	var buf bytes.Buffer
	enc := NewEncoder(&buf)
	enc.SetIndent("", "  ")

	v := ArrayVal([]Value{NumberVal(1), NumberVal(2), NumberVal(3)})
	if err := enc.Encode(v); err != nil {
		t.Fatalf("Encode failed: %v", err)
	}

	got := buf.String()
	expected := "[\n  1,\n  2,\n  3\n]\n"
	if got != expected {
		t.Errorf("got %q, expected %q", got, expected)
	}
}

func TestEncoderMultipleWrites(t *testing.T) {
	var buf bytes.Buffer
	enc := NewEncoder(&buf)

	if err := enc.Encode(NumberVal(1)); err != nil {
		t.Fatalf("first Encode failed: %v", err)
	}
	if err := enc.Encode(StringVal("hello")); err != nil {
		t.Fatalf("second Encode failed: %v", err)
	}
	if err := enc.Encode(Bool(true)); err != nil {
		t.Fatalf("third Encode failed: %v", err)
	}

	got := buf.String()
	expected := "1\n\"hello\"\ntrue\n"
	if got != expected {
		t.Errorf("got %q, expected %q", got, expected)
	}
}

func TestEncoderRDNTypes(t *testing.T) {
	var buf bytes.Buffer
	enc := NewEncoder(&buf)

	v := SetVal([]Value{StringVal("a"), StringVal("b")})
	if err := enc.Encode(v); err != nil {
		t.Fatalf("Encode failed: %v", err)
	}

	got := strings.TrimSpace(buf.String())
	expected := `Set{"a","b"}`
	if got != expected {
		t.Errorf("got %q, expected %q", got, expected)
	}
}

func TestStreamRoundtrip(t *testing.T) {
	original := ObjectVal([]KeyValue{
		{Key: "created", Value: DateTimeVal(mustParseTime(t, "2024-01-15T10:30:00.000Z"))},
		{Key: "count", Value: BigIntVal("42")},
		{Key: "pattern", Value: RegExpVal("^hello$", "i")},
	})

	// Encode to buffer
	var buf bytes.Buffer
	enc := NewEncoder(&buf)
	if err := enc.Encode(original); err != nil {
		t.Fatalf("Encode failed: %v", err)
	}

	// Decode back
	dec := NewDecoder(&buf)
	var decoded Value
	if err := dec.Decode(&decoded); err != nil {
		t.Fatalf("Decode failed: %v", err)
	}

	if !original.Equal(decoded) {
		t.Errorf("roundtrip mismatch:\n  original: %v\n  decoded:  %v", original, decoded)
	}
}

func TestEncoderEncodeValue(t *testing.T) {
	// Test struct encoding through stream
	type Item struct {
		Name  string `rdn:"name"`
		Count int    `rdn:"count"`
	}
	var buf bytes.Buffer
	enc := NewEncoder(&buf)
	err := enc.EncodeValue(Item{Name: "test", Count: 42})
	if err != nil {
		t.Fatal(err)
	}
	// Verify output is valid RDN (parse it back)
	output := strings.TrimSpace(buf.String())
	val, err := Parse([]byte(output))
	if err != nil {
		t.Fatalf("output is not valid RDN: %v\noutput: %s", err, output)
	}
	if val.Kind() != KindObject {
		t.Fatalf("expected KindObject, got %v", val.Kind())
	}
}

func TestEncoderEncodeValueWithIndent(t *testing.T) {
	var buf bytes.Buffer
	enc := NewEncoder(&buf)
	enc.SetIndent("", "  ")
	err := enc.EncodeValue(map[string]int{"a": 1})
	if err != nil {
		t.Fatal(err)
	}
	output := buf.String()
	if !strings.Contains(output, "\n") {
		t.Errorf("expected indented output, got: %s", output)
	}
}

func TestDecoderDecodeValue(t *testing.T) {
	type Item struct {
		Name  string `rdn:"name"`
		Count int    `rdn:"count"`
	}
	input := `{"name": "test", "count": 42}`
	dec := NewDecoder(strings.NewReader(input))
	var item Item
	err := dec.DecodeValue(&item)
	if err != nil {
		t.Fatal(err)
	}
	if item.Name != "test" {
		t.Errorf("expected name 'test', got %q", item.Name)
	}
	if item.Count != 42 {
		t.Errorf("expected count 42, got %d", item.Count)
	}
}

func TestDecoderDecodeValueError(t *testing.T) {
	dec := NewDecoder(strings.NewReader("invalid!!!"))
	var x int
	err := dec.DecodeValue(&x)
	if err == nil {
		t.Fatal("expected error for invalid RDN")
	}
}

func mustParseTime(t *testing.T, s string) time.Time {
	t.Helper()
	v, err := Parse([]byte("@" + s))
	if err != nil {
		t.Fatalf("failed to parse time %q: %v", s, err)
	}
	return v.Time()
}
