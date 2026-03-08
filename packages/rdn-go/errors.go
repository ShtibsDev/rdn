package rdn

import (
	"fmt"
	"reflect"
)

// SyntaxError is the error type returned by Parse when the input is not valid RDN.
type SyntaxError struct {
	msg    string
	Offset int64
}

func (e *SyntaxError) Error() string {
	return fmt.Sprintf("rdn: %s at position %d", e.msg, e.Offset)
}

// MarshalError describes an error that occurred during marshaling.
type MarshalError struct {
	Type reflect.Type
	Err  error
}

func (e *MarshalError) Error() string {
	return "rdn: error marshaling type " + e.Type.String() + ": " + e.Err.Error()
}

func (e *MarshalError) Unwrap() error { return e.Err }

// UnmarshalTypeError describes a type mismatch during unmarshaling.
type UnmarshalTypeError struct {
	Value  string
	Type   reflect.Type
	Struct string
	Field  string
}

func (e *UnmarshalTypeError) Error() string {
	if e.Struct != "" {
		return "rdn: cannot unmarshal " + e.Value + " into Go struct field " + e.Struct + "." + e.Field + " of type " + e.Type.String()
	}
	return "rdn: cannot unmarshal " + e.Value + " into Go value of type " + e.Type.String()
}

// InvalidUnmarshalError describes an invalid argument to Unmarshal.
type InvalidUnmarshalError struct {
	Type reflect.Type
}

func (e *InvalidUnmarshalError) Error() string {
	if e.Type == nil {
		return "rdn: Unmarshal(nil)"
	}
	if e.Type.Kind() != reflect.Pointer {
		return "rdn: Unmarshal(non-pointer " + e.Type.String() + ")"
	}
	return "rdn: Unmarshal(nil " + e.Type.String() + ")"
}
