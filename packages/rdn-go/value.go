package rdn

import (
	"fmt"
	"math"
	"math/big"
	"time"
	"unsafe"
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

// Value represents any RDN value. It uses a compact layout with unsafe.Pointer
// for collection and rare-type storage to minimize per-element memory overhead.
//
// Only the fields relevant to Kind() are meaningful:
//   - KindNull: no fields
//   - KindBool: boolean
//   - KindNumber: num
//   - KindString, KindBigInt, KindDuration: str
//   - KindArray, KindTuple, KindSet: ptr (→ []Value via ptrLen/ptrCap)
//   - KindObject: ptr (→ []KeyValue via ptrLen/ptrCap)
//   - KindMap: ptr (→ []MapEntry via ptrLen/ptrCap)
//   - KindDateTime: ptr (→ *time.Time)
//   - KindTimeOnly: ptr (→ *TimeOnly)
//   - KindRegExp: ptr (→ *RegExp)
//   - KindBinary: ptr (→ *[]byte)
type Value struct {
	kind    ValueKind      // 8
	num     float64        // 8
	boolean bool           // 1 (+7 pad)
	str     string         // 16
	ptr     unsafe.Pointer // 8  → points to backing array or heap-allocated rare type
	ptrLen  int            // 8  → length for slice-backed collections
	ptrCap  int            // 8  → capacity for slice-backed collections
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
func ArrayVal(elems []Value) Value {
	sh := (*sliceHeader)(unsafe.Pointer(&elems))
	return Value{kind: KindArray, ptr: sh.Data, ptrLen: sh.Len, ptrCap: sh.Cap}
}

// ObjectVal returns an object Value with ordered key-value pairs.
func ObjectVal(pairs []KeyValue) Value {
	sh := (*sliceHeader)(unsafe.Pointer(&pairs))
	return Value{kind: KindObject, ptr: sh.Data, ptrLen: sh.Len, ptrCap: sh.Cap}
}

// DateTimeVal returns a DateTime Value.
func DateTimeVal(t time.Time) Value {
	p := new(time.Time)
	*p = t
	return Value{kind: KindDateTime, ptr: unsafe.Pointer(p)}
}

// TimeOnlyVal returns a TimeOnly Value.
func TimeOnlyVal(t TimeOnly) Value {
	p := new(TimeOnly)
	*p = t
	return Value{kind: KindTimeOnly, ptr: unsafe.Pointer(p)}
}

// DurationVal returns a Duration Value from an ISO 8601 duration string.
func DurationVal(iso string) Value { return Value{kind: KindDuration, str: iso} }

// RegExpVal returns a RegExp Value.
func RegExpVal(source, flags string) Value {
	p := new(RegExp)
	*p = RegExp{Source: source, Flags: flags}
	return Value{kind: KindRegExp, ptr: unsafe.Pointer(p)}
}

// BinaryVal returns a Binary Value.
func BinaryVal(data []byte) Value {
	p := new([]byte)
	*p = data
	return Value{kind: KindBinary, ptr: unsafe.Pointer(p)}
}

// MapVal returns a Map Value.
func MapVal(entries []MapEntry) Value {
	sh := (*sliceHeader)(unsafe.Pointer(&entries))
	return Value{kind: KindMap, ptr: sh.Data, ptrLen: sh.Len, ptrCap: sh.Cap}
}

// SetVal returns a Set Value.
func SetVal(elems []Value) Value {
	sh := (*sliceHeader)(unsafe.Pointer(&elems))
	return Value{kind: KindSet, ptr: sh.Data, ptrLen: sh.Len, ptrCap: sh.Cap}
}

// TupleVal returns a Tuple Value.
func TupleVal(elems []Value) Value {
	sh := (*sliceHeader)(unsafe.Pointer(&elems))
	return Value{kind: KindTuple, ptr: sh.Data, ptrLen: sh.Len, ptrCap: sh.Cap}
}

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
func (v Value) Array() []Value {
	if v.ptr == nil {
		return nil
	}
	return unsafe.Slice((*Value)(v.ptr), v.ptrLen)
}

// Object returns the ordered key-value pairs. Meaningful for KindObject.
func (v Value) Object() []KeyValue {
	if v.ptr == nil {
		return nil
	}
	return unsafe.Slice((*KeyValue)(v.ptr), v.ptrLen)
}

// Map returns the map entries. Meaningful for KindMap.
func (v Value) Map() []MapEntry {
	if v.ptr == nil {
		return nil
	}
	return unsafe.Slice((*MapEntry)(v.ptr), v.ptrLen)
}

// Time returns the time.Time. Meaningful for KindDateTime.
func (v Value) Time() time.Time {
	if v.ptr == nil {
		return time.Time{}
	}
	return *(*time.Time)(v.ptr)
}

// TimeOnlyValue returns the TimeOnly. Meaningful for KindTimeOnly.
func (v Value) TimeOnlyValue() TimeOnly {
	if v.ptr == nil {
		return TimeOnly{}
	}
	return *(*TimeOnly)(v.ptr)
}

// RegExpValue returns the RegExp. Meaningful for KindRegExp.
func (v Value) RegExpValue() RegExp {
	if v.ptr == nil {
		return RegExp{}
	}
	return *(*RegExp)(v.ptr)
}

// Bytes returns the binary data. Meaningful for KindBinary.
func (v Value) Bytes() []byte {
	if v.ptr == nil {
		return nil
	}
	return *(*[]byte)(v.ptr)
}

// Len returns the length of the contained collection (array, object, map, set, tuple, binary).
func (v Value) Len() int {
	switch v.kind {
	case KindArray, KindTuple, KindSet, KindObject, KindMap:
		return v.ptrLen
	case KindBinary:
		if v.ptr == nil {
			return 0
		}
		return len(*(*[]byte)(v.ptr))
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
		a, b := v.Array(), other.Array()
		if len(a) != len(b) {
			return false
		}
		for i := range a {
			if !a[i].Equal(b[i]) {
				return false
			}
		}
		return true
	case KindObject:
		a, b := v.Object(), other.Object()
		if len(a) != len(b) {
			return false
		}
		for i := range a {
			if a[i].Key != b[i].Key || !a[i].Value.Equal(b[i].Value) {
				return false
			}
		}
		return true
	case KindMap:
		a, b := v.Map(), other.Map()
		if len(a) != len(b) {
			return false
		}
		for i := range a {
			if !a[i].Key.Equal(b[i].Key) || !a[i].Value.Equal(b[i].Value) {
				return false
			}
		}
		return true
	case KindDateTime:
		return v.Time().Equal(other.Time())
	case KindTimeOnly:
		return v.TimeOnlyValue() == other.TimeOnlyValue()
	case KindRegExp:
		return v.RegExpValue() == other.RegExpValue()
	case KindBinary:
		ab, bb := v.Bytes(), other.Bytes()
		if len(ab) != len(bb) {
			return false
		}
		for i := range ab {
			if ab[i] != bb[i] {
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
		return "@" + v.Time().UTC().Format("2006-01-02T15:04:05.000Z")
	case KindTimeOnly:
		return "@" + v.TimeOnlyValue().String()
	case KindDuration:
		return "@" + v.str
	case KindRegExp:
		return v.RegExpValue().String()
	default:
		return fmt.Sprintf("%s(...)", v.kind)
	}
}

// sliceHeader mirrors reflect.SliceHeader but uses unsafe.Pointer for the data field.
type sliceHeader struct {
	Data unsafe.Pointer
	Len  int
	Cap  int
}

