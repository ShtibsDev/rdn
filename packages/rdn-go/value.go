package rdn

import (
	"fmt"
	"math"
	"math/big"
	"time"
)

// ValueKind identifies the type of value stored in a Value.
type ValueKind int

const (
	KindNull     ValueKind = iota
	KindBool               // bool
	KindNumber             // float64 — includes NaN, ±Infinity
	KindBigInt             // stored as string of digits (possibly with leading -)
	KindString             // string
	KindArray              // []Value
	KindObject             // []KeyValue — ordered
	KindDateTime           // time.Time
	KindTimeOnly           // TimeOnly
	KindDuration           // Duration (ISO string)
	KindRegExp             // RegExp
	KindBinary             // []byte
	KindMap                // []MapEntry
	KindSet                // []Value
	KindTuple              // []Value
)

var kindNames = [...]string{
	KindNull: "Null", KindBool: "Bool", KindNumber: "Number", KindBigInt: "BigInt",
	KindString: "String", KindArray: "Array", KindObject: "Object", KindDateTime: "DateTime",
	KindTimeOnly: "TimeOnly", KindDuration: "Duration", KindRegExp: "RegExp", KindBinary: "Binary",
	KindMap: "Map", KindSet: "Set", KindTuple: "Tuple",
}

func (k ValueKind) String() string {
	if int(k) < len(kindNames) {
		return kindNames[k]
	}
	return fmt.Sprintf("ValueKind(%d)", int(k))
}

// KeyValue is a key-value pair in an RDN object.
type KeyValue struct {
	Key   string
	Value Value
}

// MapEntry is a key-value pair in an RDN Map where keys can be any Value.
type MapEntry struct {
	Key   Value
	Value Value
}

// Value represents any RDN value. It is a concrete struct using union-style
// storage — only the fields relevant to Kind() are meaningful.
type Value struct {
	kind    ValueKind
	str     string    // String, BigInt digits, Duration ISO
	num     float64   // Number
	boolean bool      // Bool
	arr     []Value   // Array, Tuple, Set
	obj     []KeyValue // Object
	mapV    []MapEntry // Map
	timeVal time.Time  // DateTime
	timeO   TimeOnly   // TimeOnly
	regexpV RegExp     // RegExp
	binary  []byte     // Binary
}

// ── Constructors ─────────────────────────────────────────────────────────

// Null returns a null Value.
func Null() Value { return Value{kind: KindNull} }

// Bool returns a boolean Value.
func Bool(b bool) Value { return Value{kind: KindBool, boolean: b} }

// NumberVal returns a numeric Value. Accepts NaN and ±Infinity.
func NumberVal(f float64) Value { return Value{kind: KindNumber, num: f} }

// StringVal returns a string Value.
func StringVal(s string) Value { return Value{kind: KindString, str: s} }

// BigIntVal returns a BigInt Value from a decimal digit string (may have leading -).
func BigIntVal(s string) Value { return Value{kind: KindBigInt, str: s} }

// BigIntFromGo returns a BigInt Value from a *big.Int.
func BigIntFromGo(v *big.Int) Value { return Value{kind: KindBigInt, str: v.String()} }

// ArrayVal returns an array Value.
func ArrayVal(elems []Value) Value { return Value{kind: KindArray, arr: elems} }

// ObjectVal returns an object Value with ordered key-value pairs.
func ObjectVal(pairs []KeyValue) Value { return Value{kind: KindObject, obj: pairs} }

// DateTimeVal returns a DateTime Value.
func DateTimeVal(t time.Time) Value { return Value{kind: KindDateTime, timeVal: t} }

// TimeOnlyVal returns a TimeOnly Value.
func TimeOnlyVal(t TimeOnly) Value { return Value{kind: KindTimeOnly, timeO: t} }

// DurationVal returns a Duration Value from an ISO 8601 duration string.
func DurationVal(iso string) Value { return Value{kind: KindDuration, str: iso} }

// RegExpVal returns a RegExp Value.
func RegExpVal(source, flags string) Value { return Value{kind: KindRegExp, regexpV: RegExp{Source: source, Flags: flags}} }

// BinaryVal returns a Binary Value.
func BinaryVal(data []byte) Value { return Value{kind: KindBinary, binary: data} }

// MapVal returns a Map Value.
func MapVal(entries []MapEntry) Value { return Value{kind: KindMap, mapV: entries} }

// SetVal returns a Set Value.
func SetVal(elems []Value) Value { return Value{kind: KindSet, arr: elems} }

// TupleVal returns a Tuple Value.
func TupleVal(elems []Value) Value { return Value{kind: KindTuple, arr: elems} }

// ── Accessors ────────────────────────────────────────────────────────────

// Kind returns the type of the stored value.
func (v Value) Kind() ValueKind { return v.kind }

// IsNull reports whether the value is null.
func (v Value) IsNull() bool { return v.kind == KindNull }

// BoolVal returns the boolean, or false if not KindBool.
func (v Value) BoolVal() bool { return v.boolean }

// Float64 returns the float64, or 0 if not KindNumber.
func (v Value) Float64() float64 { return v.num }

// Int64 returns the number truncated to int64. Only meaningful for KindNumber.
func (v Value) Int64() int64 { return int64(v.num) }

// Str returns the string content. Meaningful for KindString, KindBigInt, KindDuration.
func (v Value) Str() string { return v.str }

// Array returns the element slice. Meaningful for KindArray, KindTuple, KindSet.
func (v Value) Array() []Value { return v.arr }

// Object returns the ordered key-value pairs. Meaningful for KindObject.
func (v Value) Object() []KeyValue { return v.obj }

// Map returns the map entries. Meaningful for KindMap.
func (v Value) Map() []MapEntry { return v.mapV }

// Time returns the time.Time. Meaningful for KindDateTime.
func (v Value) Time() time.Time { return v.timeVal }

// TimeOnlyValue returns the TimeOnly. Meaningful for KindTimeOnly.
func (v Value) TimeOnlyValue() TimeOnly { return v.timeO }

// RegExpValue returns the RegExp. Meaningful for KindRegExp.
func (v Value) RegExpValue() RegExp { return v.regexpV }

// Bytes returns the binary data. Meaningful for KindBinary.
func (v Value) Bytes() []byte { return v.binary }

// Len returns the length of the contained collection (array, object, map, set, tuple, binary).
func (v Value) Len() int {
	switch v.kind {
	case KindArray, KindTuple, KindSet:
		return len(v.arr)
	case KindObject:
		return len(v.obj)
	case KindMap:
		return len(v.mapV)
	case KindBinary:
		return len(v.binary)
	case KindString:
		return len(v.str)
	default:
		return 0
	}
}

// IsNaN reports whether the value is KindNumber and NaN.
func (v Value) IsNaN() bool { return v.kind == KindNumber && math.IsNaN(v.num) }

// IsInf reports whether the value is KindNumber and ±Infinity.
func (v Value) IsInf() bool { return v.kind == KindNumber && math.IsInf(v.num, 0) }

// Equal reports whether two Values are deeply equal. NaN == NaN is true for this comparison.
func (v Value) Equal(other Value) bool {
	if v.kind != other.kind {
		return false
	}
	switch v.kind {
	case KindNull:
		return true
	case KindBool:
		return v.boolean == other.boolean
	case KindNumber:
		if math.IsNaN(v.num) && math.IsNaN(other.num) {
			return true
		}
		return v.num == other.num
	case KindBigInt, KindString, KindDuration:
		return v.str == other.str
	case KindArray, KindTuple, KindSet:
		if len(v.arr) != len(other.arr) {
			return false
		}
		for i := range v.arr {
			if !v.arr[i].Equal(other.arr[i]) {
				return false
			}
		}
		return true
	case KindObject:
		if len(v.obj) != len(other.obj) {
			return false
		}
		for i := range v.obj {
			if v.obj[i].Key != other.obj[i].Key || !v.obj[i].Value.Equal(other.obj[i].Value) {
				return false
			}
		}
		return true
	case KindMap:
		if len(v.mapV) != len(other.mapV) {
			return false
		}
		for i := range v.mapV {
			if !v.mapV[i].Key.Equal(other.mapV[i].Key) || !v.mapV[i].Value.Equal(other.mapV[i].Value) {
				return false
			}
		}
		return true
	case KindDateTime:
		return v.timeVal.Equal(other.timeVal)
	case KindTimeOnly:
		return v.timeO == other.timeO
	case KindRegExp:
		return v.regexpV == other.regexpV
	case KindBinary:
		if len(v.binary) != len(other.binary) {
			return false
		}
		for i := range v.binary {
			if v.binary[i] != other.binary[i] {
				return false
			}
		}
		return true
	default:
		return false
	}
}

// String returns a human-readable representation of the Value for debugging.
func (v Value) String() string {
	switch v.kind {
	case KindNull:
		return "null"
	case KindBool:
		if v.boolean {
			return "true"
		}
		return "false"
	case KindNumber:
		if math.IsNaN(v.num) {
			return "NaN"
		}
		if math.IsInf(v.num, 1) {
			return "Infinity"
		}
		if math.IsInf(v.num, -1) {
			return "-Infinity"
		}
		return fmt.Sprintf("%g", v.num)
	case KindBigInt:
		return v.str + "n"
	case KindString:
		return fmt.Sprintf("%q", v.str)
	case KindDateTime:
		return "@" + v.timeVal.UTC().Format("2006-01-02T15:04:05.000Z")
	case KindTimeOnly:
		return "@" + v.timeO.String()
	case KindDuration:
		return "@" + v.str
	case KindRegExp:
		return v.regexpV.String()
	default:
		return fmt.Sprintf("%s(...)", v.kind)
	}
}
