package rdn

import "fmt"

// SyntaxError is the error type returned by Parse when the input is not valid RDN.
type SyntaxError struct {
	msg    string
	Offset int64
}

func (e *SyntaxError) Error() string {
	return fmt.Sprintf("rdn: %s at position %d", e.msg, e.Offset)
}
