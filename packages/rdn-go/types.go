package rdn

import (
	"fmt"
	"math/big"
	"strconv"
)

// TimeOnly represents a time-of-day value without a date component.
type TimeOnly struct {
	Hours, Minutes, Seconds, Milliseconds int
}

// String returns the RDN representation of the TimeOnly value.
func (t TimeOnly) String() string {
	if t.Milliseconds > 0 {
		return fmt.Sprintf("%02d:%02d:%02d.%03d", t.Hours, t.Minutes, t.Seconds, t.Milliseconds)
	}
	return fmt.Sprintf("%02d:%02d:%02d", t.Hours, t.Minutes, t.Seconds)
}

// Duration represents an ISO 8601 duration string.
type Duration struct {
	ISO string
}

// String returns the ISO 8601 duration string.
func (d Duration) String() string { return d.ISO }

// RegExp represents a regular expression with its source pattern and flags.
type RegExp struct {
	Source string
	Flags  string
}

// String returns the RDN representation of the RegExp.
func (r RegExp) String() string { return "/" + r.Source + "/" + r.Flags }

// Number is a string representation of a JSON/RDN number, preserving the
// original textual form. It can be converted to standard Go numeric types.
type Number string

// Float64 returns the number as a float64.
func (n Number) Float64() (float64, error) { return strconv.ParseFloat(string(n), 64) }

// Int64 returns the number as an int64.
func (n Number) Int64() (int64, error) { return strconv.ParseInt(string(n), 10, 64) }

// BigInt returns the number as a *big.Int. Returns nil if the string is not a valid integer.
func (n Number) BigInt() (*big.Int, error) {
	v := new(big.Int)
	if _, ok := v.SetString(string(n), 10); !ok {
		return nil, fmt.Errorf("rdn: invalid big integer %q", string(n))
	}
	return v, nil
}

// String returns the string representation of the number.
func (n Number) String() string { return string(n) }

// RawMessage is a raw encoded RDN value. It can be used to delay parsing
// or to pass through RDN content unchanged.
type RawMessage []byte
