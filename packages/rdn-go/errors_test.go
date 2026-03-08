package rdn

import (
	"errors"
	"reflect"
	"testing"
)

func TestErrorMarshalError(t *testing.T) {
	inner := errors.New("unsupported value")
	err := &MarshalError{Type: reflect.TypeOf(0), Err: inner}

	want := "rdn: error marshaling type int: unsupported value"
	if got := err.Error(); got != want {
		t.Errorf("MarshalError.Error() = %q, want %q", got, want)
	}

	if unwrapped := err.Unwrap(); unwrapped != inner {
		t.Errorf("MarshalError.Unwrap() = %v, want %v", unwrapped, inner)
	}

	if !errors.Is(err, inner) {
		t.Error("errors.Is(MarshalError, inner) = false, want true")
	}
}

func TestErrorUnmarshalTypeErrorWithStruct(t *testing.T) {
	err := &UnmarshalTypeError{
		Value:  "string",
		Type:   reflect.TypeOf(0),
		Struct: "MyStruct",
		Field:  "Count",
	}

	want := "rdn: cannot unmarshal string into Go struct field MyStruct.Count of type int"
	if got := err.Error(); got != want {
		t.Errorf("UnmarshalTypeError.Error() = %q, want %q", got, want)
	}
}

func TestErrorUnmarshalTypeErrorWithoutStruct(t *testing.T) {
	err := &UnmarshalTypeError{
		Value: "number",
		Type:  reflect.TypeOf(""),
	}

	want := "rdn: cannot unmarshal number into Go value of type string"
	if got := err.Error(); got != want {
		t.Errorf("UnmarshalTypeError.Error() = %q, want %q", got, want)
	}
}

func TestErrorInvalidUnmarshalErrorNilType(t *testing.T) {
	err := &InvalidUnmarshalError{Type: nil}

	want := "rdn: Unmarshal(nil)"
	if got := err.Error(); got != want {
		t.Errorf("InvalidUnmarshalError.Error() = %q, want %q", got, want)
	}
}

func TestErrorInvalidUnmarshalErrorNonPointer(t *testing.T) {
	err := &InvalidUnmarshalError{Type: reflect.TypeOf(0)}

	want := "rdn: Unmarshal(non-pointer int)"
	if got := err.Error(); got != want {
		t.Errorf("InvalidUnmarshalError.Error() = %q, want %q", got, want)
	}
}

func TestErrorInvalidUnmarshalErrorNilPointer(t *testing.T) {
	var p *int
	err := &InvalidUnmarshalError{Type: reflect.TypeOf(p)}

	want := "rdn: Unmarshal(nil *int)"
	if got := err.Error(); got != want {
		t.Errorf("InvalidUnmarshalError.Error() = %q, want %q", got, want)
	}
}
