package rdn

import (
	"encoding"
	"errors"
	"fmt"
	"math"
	"math/big"
	"reflect"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"
	"unsafe"
)

// Marshaler is the interface implemented by types that can marshal themselves
// into an RDN Value.
type Marshaler interface {
	MarshalRDN() (Value, error)
}

// MarshalValue converts a Go value into an RDN Value.
func MarshalValue(v any) (Value, error) {
	if v == nil {
		return Null(), nil
	}
	ms := &marshalState{visited: make(map[unsafe.Pointer]bool)}
	return ms.marshalValue(reflect.ValueOf(v))
}

// Marshal returns the RDN encoding of v as bytes.
func Marshal(v any) ([]byte, error) {
	val, err := MarshalValue(v)
	if err != nil {
		return nil, err
	}
	return Stringify(val)
}

// MarshalIndent is like Marshal but applies indentation for readability.
func MarshalIndent(v any, prefix, indent string) ([]byte, error) {
	val, err := MarshalValue(v)
	if err != nil {
		return nil, err
	}
	return StringifyIndent(val, prefix, indent)
}

// ── Type vars (marshal-specific) ─────────────────────────────────────────
// Shared type vars (timeType, bigIntType, etc.) are defined in unmarshal.go.

var (
	marshalerType     = reflect.TypeOf((*Marshaler)(nil)).Elem()
	textMarshalerType = reflect.TypeOf((*encoding.TextMarshaler)(nil)).Elem()
)

// ── Marshal state ────────────────────────────────────────────────────────

type marshalState struct {
	visited map[unsafe.Pointer]bool
}

func (ms *marshalState) checkCycle(v reflect.Value) error {
	if v.IsNil() {
		return nil
	}
	ptr := unsafe.Pointer(v.Pointer())
	if ms.visited[ptr] {
		return &MarshalError{Type: v.Type(), Err: errors.New("circular reference detected")}
	}
	ms.visited[ptr] = true
	return nil
}

func (ms *marshalState) removeCycle(v reflect.Value) {
	if !v.IsNil() {
		delete(ms.visited, unsafe.Pointer(v.Pointer()))
	}
}

func (ms *marshalState) marshalValue(v reflect.Value) (Value, error) {
	// Handle invalid (nil interface)
	if !v.IsValid() {
		return Null(), nil
	}

	// Dereference interfaces
	if v.Kind() == reflect.Interface {
		if v.IsNil() {
			return Null(), nil
		}
		v = v.Elem()
	}

	// Dereference pointers
	if v.Kind() == reflect.Pointer {
		if v.IsNil() {
			return Null(), nil
		}
		if err := ms.checkCycle(v); err != nil {
			return Value{}, err
		}
		defer ms.removeCycle(v)
		v = v.Elem()
	}

	// Look up cached encoder
	enc := cachedEncoder(v.Type())
	return enc(ms, v)
}

// ── Encoder cache ────────────────────────────────────────────────────────

type encoderFunc func(ms *marshalState, v reflect.Value) (Value, error)

var encoderCache sync.Map // map[reflect.Type]encoderFunc

func cachedEncoder(t reflect.Type) encoderFunc {
	if v, ok := encoderCache.Load(t); ok {
		return v.(encoderFunc)
	}
	enc := newEncoder2(t)
	v, _ := encoderCache.LoadOrStore(t, enc)
	return v.(encoderFunc)
}

func newEncoder2(t reflect.Type) encoderFunc {
	// 1. Marshaler interface (value receiver)
	if t.Implements(marshalerType) {
		return marshalerEncoder
	}
	// Marshaler interface (pointer receiver, if addressable)
	if reflect.PointerTo(t).Implements(marshalerType) {
		return addrMarshalerEncoder
	}

	// 2. Special types by identity
	switch t {
	case timeType:
		return timeEncoder
	case bigIntType:
		return bigIntEncoder
	case bigIntPtrType:
		return bigIntPtrEncoder
	case valueType:
		return valuePassthroughEncoder
	case timeOnlyType:
		return timeOnlyEncoder
	case durationType:
		return durationEncoder
	case regexpType:
		return regexpEncoder
	case numberType:
		return numberEncoder
	case rawMessageType:
		return rawMessageEncoder
	}

	// 3. []byte special case
	if t == byteSliceType {
		return bytesEncoder
	}

	// 4. TextMarshaler interface
	if t.Implements(textMarshalerType) {
		return textMarshalerEncoder
	}
	if reflect.PointerTo(t).Implements(textMarshalerType) {
		return addrTextMarshalerEncoder
	}

	// 5. Kind switch
	switch t.Kind() {
	case reflect.Bool:
		return boolEncoder
	case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
		return intEncoder
	case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64:
		return uintEncoder
	case reflect.Float32, reflect.Float64:
		return floatEncoder
	case reflect.String:
		return stringEncoder
	case reflect.Slice:
		return newSliceEncoder(t)
	case reflect.Array:
		return newArrayEncoder(t)
	case reflect.Map:
		return newMapEncoder(t)
	case reflect.Struct:
		return newStructEncoder(t)
	default:
		return unsupportedEncoder
	}
}

// ── Individual encoders ──────────────────────────────────────────────────

func marshalerEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	m := v.Interface().(Marshaler)
	return m.MarshalRDN()
}

func addrMarshalerEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	if !v.CanAddr() {
		// Fall back: create a copy and take its address
		cp := reflect.New(v.Type())
		cp.Elem().Set(v)
		m := cp.Interface().(Marshaler)
		return m.MarshalRDN()
	}
	m := v.Addr().Interface().(Marshaler)
	return m.MarshalRDN()
}

func timeEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	return DateTimeVal(v.Interface().(time.Time)), nil
}

func bigIntEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	bi := v.Interface().(big.Int)
	return BigIntFromGo(&bi), nil
}

func bigIntPtrEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	if v.IsNil() {
		return Null(), nil
	}
	bi := v.Interface().(*big.Int)
	return BigIntFromGo(bi), nil
}

func valuePassthroughEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	return v.Interface().(Value), nil
}

func timeOnlyEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	return TimeOnlyVal(v.Interface().(TimeOnly)), nil
}

func durationEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	d := v.Interface().(Duration)
	return DurationVal(d.ISO), nil
}

func regexpEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	re := v.Interface().(RegExp)
	return RegExpVal(re.Source, re.Flags), nil
}

func numberEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	n := v.Interface().(Number)
	s := string(n)
	if s == "" {
		return NumberVal(0), nil
	}
	// Check if it's a BigInt: contains 'n' suffix or is a pure integer beyond float64 precision
	if strings.HasSuffix(s, "n") {
		return BigIntVal(s[:len(s)-1]), nil
	}
	// If it contains '.', 'e', 'E', it's a float
	if strings.ContainsAny(s, ".eE") {
		f, err := strconv.ParseFloat(s, 64)
		if err != nil {
			return Value{}, &MarshalError{Type: numberType, Err: fmt.Errorf("invalid number %q: %w", s, err)}
		}
		return NumberVal(f), nil
	}
	// Check special values
	switch s {
	case "NaN":
		return NumberVal(math.NaN()), nil
	case "Infinity":
		return NumberVal(math.Inf(1)), nil
	case "-Infinity":
		return NumberVal(math.Inf(-1)), nil
	}
	// Pure integer — check if it fits in float64
	if isIntegerBeyondFloat64(s) {
		return BigIntVal(s), nil
	}
	f, err := strconv.ParseFloat(s, 64)
	if err != nil {
		return Value{}, &MarshalError{Type: numberType, Err: fmt.Errorf("invalid number %q: %w", s, err)}
	}
	return NumberVal(f), nil
}

// isIntegerBeyondFloat64 reports whether a decimal integer string exceeds float64 safe integer range.
func isIntegerBeyondFloat64(s string) bool {
	digits := s
	if len(digits) > 0 && digits[0] == '-' {
		digits = digits[1:]
	}
	// More than 16 digits is certainly beyond float64
	if len(digits) > 16 {
		return true
	}
	if len(digits) < 16 {
		return false
	}
	// Exactly 16 digits: compare with 2^53 = 9007199254740992
	const maxSafe = "9007199254740992"
	return digits >= maxSafe
}

func rawMessageEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	raw := v.Interface().(RawMessage)
	if raw == nil {
		return Null(), nil
	}
	return Parse([]byte(raw))
}

func bytesEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	b := v.Bytes()
	if b == nil {
		return Null(), nil
	}
	return BinaryVal(b), nil
}

func textMarshalerEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	m := v.Interface().(encoding.TextMarshaler)
	b, err := m.MarshalText()
	if err != nil {
		return Value{}, &MarshalError{Type: v.Type(), Err: err}
	}
	return StringVal(string(b)), nil
}

func addrTextMarshalerEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	if !v.CanAddr() {
		cp := reflect.New(v.Type())
		cp.Elem().Set(v)
		m := cp.Interface().(encoding.TextMarshaler)
		b, err := m.MarshalText()
		if err != nil {
			return Value{}, &MarshalError{Type: v.Type(), Err: err}
		}
		return StringVal(string(b)), nil
	}
	m := v.Addr().Interface().(encoding.TextMarshaler)
	b, err := m.MarshalText()
	if err != nil {
		return Value{}, &MarshalError{Type: v.Type(), Err: err}
	}
	return StringVal(string(b)), nil
}

func boolEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	return Bool(v.Bool()), nil
}

func intEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	i := v.Int()
	if i > (1<<53) || i < -(1<<53) {
		return BigIntVal(strconv.FormatInt(i, 10)), nil
	}
	return NumberVal(float64(i)), nil
}

func uintEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	u := v.Uint()
	if u > (1 << 53) {
		return BigIntVal(strconv.FormatUint(u, 10)), nil
	}
	return NumberVal(float64(u)), nil
}

func floatEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	return NumberVal(v.Float()), nil
}

func stringEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	return StringVal(v.String()), nil
}

func newSliceEncoder(t reflect.Type) encoderFunc {
	elemEnc := cachedEncoder(t.Elem())
	return func(ms *marshalState, v reflect.Value) (Value, error) {
		if v.IsNil() {
			return Null(), nil
		}
		if err := ms.checkCycle(v); err != nil {
			return Value{}, err
		}
		defer ms.removeCycle(v)
		n := v.Len()
		elems := make([]Value, n)
		for i := 0; i < n; i++ {
			val, err := elemEnc(ms, v.Index(i))
			if err != nil {
				return Value{}, err
			}
			elems[i] = val
		}
		return ArrayVal(elems), nil
	}
}

func newArrayEncoder(t reflect.Type) encoderFunc {
	elemEnc := cachedEncoder(t.Elem())
	return func(ms *marshalState, v reflect.Value) (Value, error) {
		n := v.Len()
		elems := make([]Value, n)
		for i := 0; i < n; i++ {
			val, err := elemEnc(ms, v.Index(i))
			if err != nil {
				return Value{}, err
			}
			elems[i] = val
		}
		return ArrayVal(elems), nil
	}
}

func newMapEncoder(t reflect.Type) encoderFunc {
	keyKind := t.Key().Kind()
	valEnc := cachedEncoder(t.Elem())

	if keyKind == reflect.String {
		// String-keyed maps → ObjectVal with sorted keys
		return func(ms *marshalState, v reflect.Value) (Value, error) {
			if v.IsNil() {
				return Null(), nil
			}
			keys := v.MapKeys()
			sort.Slice(keys, func(i, j int) bool { return keys[i].String() < keys[j].String() })
			pairs := make([]KeyValue, len(keys))
			for i, k := range keys {
				val, err := valEnc(ms, v.MapIndex(k))
				if err != nil {
					return Value{}, err
				}
				pairs[i] = KeyValue{Key: k.String(), Value: val}
			}
			return ObjectVal(pairs), nil
		}
	}

	// Non-string-keyed maps → MapVal
	keyEnc := cachedEncoder(t.Key())
	return func(ms *marshalState, v reflect.Value) (Value, error) {
		if v.IsNil() {
			return Null(), nil
		}
		keys := v.MapKeys()
		entries := make([]MapEntry, len(keys))
		for i, k := range keys {
			kv, err := keyEnc(ms, k)
			if err != nil {
				return Value{}, err
			}
			vv, err := valEnc(ms, v.MapIndex(k))
			if err != nil {
				return Value{}, err
			}
			entries[i] = MapEntry{Key: kv, Value: vv}
		}
		return MapVal(entries), nil
	}
}

func newStructEncoder(t reflect.Type) encoderFunc {
	sf := cachedStructFields(t)
	return func(ms *marshalState, v reflect.Value) (Value, error) {
		pairs := make([]KeyValue, 0, len(sf.list))
		for _, f := range sf.list {
			fv := fieldByIndex(v, f.index)
			if !fv.IsValid() {
				continue
			}
			if f.omitempty && isEmptyValue(fv) {
				continue
			}
			val, err := ms.marshalValue(fv)
			if err != nil {
				return Value{}, err
			}
			if f.quoted {
				val = wrapStringQuoted(val)
			}
			pairs = append(pairs, KeyValue{Key: f.name, Value: val})
		}
		return ObjectVal(pairs), nil
	}
}

// fieldByIndex is defined in unmarshal.go and shared across marshal/unmarshal.

// wrapStringQuoted converts a Value to its string representation for the ",string" tag option.
func wrapStringQuoted(v Value) Value {
	switch v.Kind() {
	case KindBool:
		if v.BoolVal() {
			return StringVal("true")
		}
		return StringVal("false")
	case KindNumber:
		return StringVal(v.String())
	case KindBigInt:
		return StringVal(v.Str() + "n")
	case KindString:
		return v // already a string
	default:
		return v
	}
}

func unsupportedEncoder(_ *marshalState, v reflect.Value) (Value, error) {
	return Value{}, &MarshalError{Type: v.Type(), Err: fmt.Errorf("unsupported type: %s", v.Type())}
}
