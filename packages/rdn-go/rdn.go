// Package rdn implements parsing and serialization for RDN (Rich Data Notation),
// a JSON superset with native support for dates, BigInts, regular expressions,
// binary data, Maps, Sets, tuples, and more.
//
// The API mirrors encoding/json's Parse/Stringify pattern, operating on the
// Value type which can represent any RDN value.
//
// Basic usage:
//
//	v, err := rdn.Parse([]byte(`{"created": @2024-01-15T10:30:00.000Z, "count": 42n}`))
//	if err != nil { log.Fatal(err) }
//
//	data, err := rdn.Stringify(v)
//	if err != nil { log.Fatal(err) }
package rdn

// Parse parses RDN-encoded data and returns the corresponding Value.
func Parse(data []byte) (Value, error) {
	return parseRoot(data)
}

// ParseZeroCopy parses RDN-encoded data using zero-copy string optimization.
// Strings without escape sequences will reference the input buffer directly
// instead of copying. The returned Value must not be used after the input
// byte slice is modified or freed.
func ParseZeroCopy(data []byte) (Value, error) {
	return parseRootZeroCopy(data)
}

// Stringify returns the compact RDN encoding of a Value.
func Stringify(v Value) ([]byte, error) {
	return stringify(v, "", "")
}

// StringifyIndent is like Stringify but applies indentation for readability.
// Each element in an array, object, map, set, or tuple begins on a new line
// indented by one or more copies of indent according to nesting depth.
// prefix is prepended to each line.
func StringifyIndent(v Value, prefix, indent string) ([]byte, error) {
	return stringify(v, prefix, indent)
}

// Valid reports whether data is valid RDN.
func Valid(data []byte) bool {
	_, err := Parse(data)
	return err == nil
}
