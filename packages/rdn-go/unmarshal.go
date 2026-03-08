package rdn

import (
	"encoding"
	"math/big"
	"reflect"
	"strconv"
	"sync"
	"time"
)

// Unmarshaler is implemented by types that can unmarshal an RDN Value into themselves.
type Unmarshaler interface {
	UnmarshalRDN(Value) error
}

// type reflection vars — shared across marshal.go and unmarshal.go.
// If marshal.go defines these later in the same package, these will need to be
// deduplicated (move to a shared file or remove from one side).
var (
	unmarshalerType     = reflect.TypeOf((*Unmarshaler)(nil)).Elem()
	textUnmarshalerType = reflect.TypeOf((*encoding.TextUnmarshaler)(nil)).Elem()
	timeType            = reflect.TypeOf(time.Time{})
	bigIntType          = reflect.TypeOf(big.Int{})
	bigIntPtrType       = reflect.TypeOf((*big.Int)(nil))
	valueType           = reflect.TypeOf(Value{})
	timeOnlyType        = reflect.TypeOf(TimeOnly{})
	durationType        = reflect.TypeOf(Duration{})
	regexpType          = reflect.TypeOf(RegExp{})
	numberType          = reflect.TypeOf(Number(""))
	rawMessageType      = reflect.TypeOf(RawMessage(nil))
	byteSliceType       = reflect.TypeOf([]byte(nil))
)

// decoderFunc decodes a Value into a reflect.Value.
type decoderFunc func(val Value, v reflect.Value) error

var decoderCache sync.Map // map[reflect.Type]decoderFunc

// Unmarshal parses the RDN-encoded data and stores the result in the value pointed to by v.
func Unmarshal(data []byte, v any) error {
	val, err := Parse(data)
	if err != nil {
		return err
	}
	return UnmarshalValue(val, v)
}

// UnmarshalValue stores the RDN Value in the value pointed to by v.
// v must be a non-nil pointer.
func UnmarshalValue(val Value, v any) error {
	rv := reflect.ValueOf(v)
	if rv.Kind() != reflect.Pointer || rv.IsNil() {
		return &InvalidUnmarshalError{Type: reflect.TypeOf(v)}
	}
	return unmarshalInto(val, rv.Elem())
}

// unmarshalInto dispatches decoding of val into the reflect.Value dst.
func unmarshalInto(val Value, dst reflect.Value) error {
	// 1. Check Unmarshaler interface
	if dst.CanAddr() {
		pv := dst.Addr()
		if pv.Type().Implements(unmarshalerType) {
			return pv.Interface().(Unmarshaler).UnmarshalRDN(val)
		}
	}
	if dst.Type().Implements(unmarshalerType) {
		if dst.Kind() == reflect.Pointer && dst.IsNil() {
			dst.Set(reflect.New(dst.Type().Elem()))
		}
		return dst.Interface().(Unmarshaler).UnmarshalRDN(val)
	}

	// 2. Pointer handling
	if dst.Kind() == reflect.Pointer {
		if val.Kind() == KindNull {
			dst.Set(reflect.Zero(dst.Type()))
			return nil
		}
		if dst.IsNil() {
			dst.Set(reflect.New(dst.Type().Elem()))
		}
		return unmarshalInto(val, dst.Elem())
	}

	// 3. interface{} with 0 methods
	if dst.Kind() == reflect.Interface && dst.NumMethod() == 0 {
		goVal := defaultGoValue(val)
		if goVal == nil {
			dst.Set(reflect.Zero(dst.Type()))
		} else {
			dst.Set(reflect.ValueOf(goVal))
		}
		return nil
	}

	// 4. Cached decoder
	dec := cachedDecoder(dst.Type())
	return dec(val, dst)
}

// cachedDecoder returns a cached decoder for the given type.
func cachedDecoder(t reflect.Type) decoderFunc {
	if v, ok := decoderCache.Load(t); ok {
		return v.(decoderFunc)
	}
	dec := newDecoder(t)
	v, _ := decoderCache.LoadOrStore(t, dec)
	return v.(decoderFunc)
}

// newDecoder constructs a decoderFunc for the given type.
func newDecoder(t reflect.Type) decoderFunc {
	// 1. Unmarshaler interface (pointer receiver)
	if reflect.PointerTo(t).Implements(unmarshalerType) {
		return unmarshalerDecoder
	}

	// 2. Special types
	switch t {
	case valueType:
		return valueDecoder
	case rawMessageType:
		return rawMessageDecoder
	case timeType:
		return timeDecoder
	case bigIntPtrType:
		return bigIntPtrDecoder
	case bigIntType:
		return bigIntDecoder
	case timeOnlyType:
		return timeOnlyDecoder
	case durationType:
		return durationDecoder
	case regexpType:
		return regexpDecoder
	case numberType:
		return numberDecoder
	}

	// 3. []byte special case
	if t == byteSliceType {
		return bytesDecoder
	}

	// 4. TextUnmarshaler
	if reflect.PointerTo(t).Implements(textUnmarshalerType) {
		return textUnmarshalerDecoder
	}

	// 5. Kind switch
	switch t.Kind() {
	case reflect.Bool:
		return boolDecoder
	case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
		return intDecoder
	case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64:
		return uintDecoder
	case reflect.Float32, reflect.Float64:
		return floatDecoder
	case reflect.String:
		return stringDecoder
	case reflect.Slice:
		return newSliceDecoder(t)
	case reflect.Array:
		return newArrayDecoder(t)
	case reflect.Map:
		return newMapDecoder(t)
	case reflect.Struct:
		return newStructDecoder(t)
	case reflect.Pointer:
		return newPointerDecoder(t)
	case reflect.Interface:
		return interfaceDecoder
	default:
		return func(val Value, v reflect.Value) error {
			return &UnmarshalTypeError{Value: val.Kind().String(), Type: t}
		}
	}
}

// ── Individual decoders ─────────────────────────────────────────────────

func unmarshalerDecoder(val Value, v reflect.Value) error {
	if v.CanAddr() {
		return v.Addr().Interface().(Unmarshaler).UnmarshalRDN(val)
	}
	// Value receiver
	return v.Interface().(Unmarshaler).UnmarshalRDN(val)
}

func valueDecoder(val Value, v reflect.Value) error {
	v.Set(reflect.ValueOf(val))
	return nil
}

func rawMessageDecoder(val Value, v reflect.Value) error {
	data, err := Stringify(val)
	if err != nil {
		return err
	}
	v.SetBytes(data)
	return nil
}

func timeDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.Set(reflect.Zero(timeType))
		return nil
	}
	if val.Kind() != KindDateTime {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: timeType}
	}
	v.Set(reflect.ValueOf(val.Time()))
	return nil
}

func bigIntPtrDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.Set(reflect.Zero(bigIntPtrType))
		return nil
	}
	if val.Kind() != KindBigInt {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: bigIntPtrType}
	}
	bi := new(big.Int)
	if _, ok := bi.SetString(val.Str(), 10); !ok {
		return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: bigIntPtrType}
	}
	v.Set(reflect.ValueOf(bi))
	return nil
}

func bigIntDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.Set(reflect.Zero(bigIntType))
		return nil
	}
	if val.Kind() != KindBigInt {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: bigIntType}
	}
	bi := new(big.Int)
	if _, ok := bi.SetString(val.Str(), 10); !ok {
		return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: bigIntType}
	}
	v.Set(reflect.ValueOf(*bi))
	return nil
}

func timeOnlyDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.Set(reflect.Zero(timeOnlyType))
		return nil
	}
	if val.Kind() != KindTimeOnly {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: timeOnlyType}
	}
	v.Set(reflect.ValueOf(val.TimeOnlyValue()))
	return nil
}

func durationDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.Set(reflect.Zero(durationType))
		return nil
	}
	if val.Kind() != KindDuration {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: durationType}
	}
	v.Set(reflect.ValueOf(Duration{ISO: val.Str()}))
	return nil
}

func regexpDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.Set(reflect.Zero(regexpType))
		return nil
	}
	if val.Kind() != KindRegExp {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: regexpType}
	}
	v.Set(reflect.ValueOf(val.RegExpValue()))
	return nil
}

func numberDecoder(val Value, v reflect.Value) error {
	switch val.Kind() {
	case KindNumber:
		v.Set(reflect.ValueOf(Number(strconv.FormatFloat(val.Float64(), 'g', -1, 64))))
		return nil
	case KindBigInt:
		v.Set(reflect.ValueOf(Number(val.Str())))
		return nil
	case KindNull:
		v.Set(reflect.Zero(numberType))
		return nil
	}
	return &UnmarshalTypeError{Value: val.Kind().String(), Type: numberType}
}

func bytesDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.SetBytes(nil)
		return nil
	}
	if val.Kind() != KindBinary {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: byteSliceType}
	}
	src := val.Bytes()
	dst := make([]byte, len(src))
	copy(dst, src)
	v.SetBytes(dst)
	return nil
}

func textUnmarshalerDecoder(val Value, v reflect.Value) error {
	if val.Kind() != KindString {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
	}
	if v.CanAddr() {
		return v.Addr().Interface().(encoding.TextUnmarshaler).UnmarshalText([]byte(val.Str()))
	}
	return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
}

func boolDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.SetBool(false)
		return nil
	}
	if val.Kind() != KindBool {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
	}
	v.SetBool(val.BoolVal())
	return nil
}

func intDecoder(val Value, v reflect.Value) error {
	switch val.Kind() {
	case KindNumber:
		f := val.Float64()
		n := int64(f)
		if float64(n) != f {
			return &UnmarshalTypeError{Value: "number " + strconv.FormatFloat(f, 'g', -1, 64), Type: v.Type()}
		}
		if v.OverflowInt(n) {
			return &UnmarshalTypeError{Value: "number " + strconv.FormatFloat(f, 'g', -1, 64), Type: v.Type()}
		}
		v.SetInt(n)
		return nil
	case KindBigInt:
		n, err := strconv.ParseInt(val.Str(), 10, 64)
		if err != nil {
			return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: v.Type()}
		}
		if v.OverflowInt(n) {
			return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: v.Type()}
		}
		v.SetInt(n)
		return nil
	case KindNull:
		v.SetInt(0)
		return nil
	}
	return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
}

func uintDecoder(val Value, v reflect.Value) error {
	switch val.Kind() {
	case KindNumber:
		f := val.Float64()
		if f < 0 {
			return &UnmarshalTypeError{Value: "number " + strconv.FormatFloat(f, 'g', -1, 64), Type: v.Type()}
		}
		n := uint64(f)
		if float64(n) != f {
			return &UnmarshalTypeError{Value: "number " + strconv.FormatFloat(f, 'g', -1, 64), Type: v.Type()}
		}
		if v.OverflowUint(n) {
			return &UnmarshalTypeError{Value: "number " + strconv.FormatFloat(f, 'g', -1, 64), Type: v.Type()}
		}
		v.SetUint(n)
		return nil
	case KindBigInt:
		n, err := strconv.ParseUint(val.Str(), 10, 64)
		if err != nil {
			return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: v.Type()}
		}
		if v.OverflowUint(n) {
			return &UnmarshalTypeError{Value: "bigint " + val.Str(), Type: v.Type()}
		}
		v.SetUint(n)
		return nil
	case KindNull:
		v.SetUint(0)
		return nil
	}
	return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
}

func floatDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.SetFloat(0)
		return nil
	}
	if val.Kind() != KindNumber {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
	}
	v.SetFloat(val.Float64())
	return nil
}

func stringDecoder(val Value, v reflect.Value) error {
	if val.Kind() == KindNull {
		v.SetString("")
		return nil
	}
	if val.Kind() != KindString {
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
	}
	v.SetString(val.Str())
	return nil
}

func interfaceDecoder(val Value, v reflect.Value) error {
	if v.NumMethod() == 0 {
		goVal := defaultGoValue(val)
		if goVal == nil {
			v.Set(reflect.Zero(v.Type()))
		} else {
			v.Set(reflect.ValueOf(goVal))
		}
		return nil
	}
	return &UnmarshalTypeError{Value: val.Kind().String(), Type: v.Type()}
}

// ── Collection decoders ─────────────────────────────────────────────────

func newSliceDecoder(t reflect.Type) decoderFunc {
	elemDec := cachedDecoder(t.Elem())
	return func(val Value, v reflect.Value) error {
		if val.Kind() == KindNull {
			v.Set(reflect.Zero(t))
			return nil
		}
		switch val.Kind() {
		case KindArray, KindTuple, KindSet:
		default:
			return &UnmarshalTypeError{Value: val.Kind().String(), Type: t}
		}
		elems := val.Array()
		slice := reflect.MakeSlice(t, len(elems), len(elems))
		for i, e := range elems {
			if err := elemDec(e, slice.Index(i)); err != nil {
				return err
			}
		}
		v.Set(slice)
		return nil
	}
}

func newArrayDecoder(t reflect.Type) decoderFunc {
	elemDec := cachedDecoder(t.Elem())
	return func(val Value, v reflect.Value) error {
		switch val.Kind() {
		case KindArray, KindTuple:
		default:
			return &UnmarshalTypeError{Value: val.Kind().String(), Type: t}
		}
		elems := val.Array()
		arrLen := t.Len()
		for i := 0; i < arrLen && i < len(elems); i++ {
			if err := elemDec(elems[i], v.Index(i)); err != nil {
				return err
			}
		}
		// Zero remaining elements if input is shorter
		for i := len(elems); i < arrLen; i++ {
			v.Index(i).Set(reflect.Zero(t.Elem()))
		}
		return nil
	}
}

func newMapDecoder(t reflect.Type) decoderFunc {
	keyType := t.Key()
	valType := t.Elem()
	return func(val Value, v reflect.Value) error {
		if val.Kind() == KindNull {
			v.Set(reflect.Zero(t))
			return nil
		}
		v.Set(reflect.MakeMap(t))
		if keyType.Kind() == reflect.String && val.Kind() == KindObject {
			valDec := cachedDecoder(valType)
			for _, kv := range val.Object() {
				mk := reflect.New(keyType).Elem()
				mk.SetString(kv.Key)
				mv := reflect.New(valType).Elem()
				if err := valDec(kv.Value, mv); err != nil {
					return err
				}
				v.SetMapIndex(mk, mv)
			}
			return nil
		}
		if val.Kind() == KindMap {
			keyDec := cachedDecoder(keyType)
			valDec := cachedDecoder(valType)
			for _, entry := range val.Map() {
				mk := reflect.New(keyType).Elem()
				if err := keyDec(entry.Key, mk); err != nil {
					return err
				}
				mv := reflect.New(valType).Elem()
				if err := valDec(entry.Value, mv); err != nil {
					return err
				}
				v.SetMapIndex(mk, mv)
			}
			return nil
		}
		return &UnmarshalTypeError{Value: val.Kind().String(), Type: t}
	}
}

func newStructDecoder(t reflect.Type) decoderFunc {
	sf := cachedStructFields(t)
	return func(val Value, v reflect.Value) error {
		if val.Kind() == KindNull {
			v.Set(reflect.Zero(t))
			return nil
		}
		if val.Kind() != KindObject {
			return &UnmarshalTypeError{Value: val.Kind().String(), Type: t}
		}
		for _, kv := range val.Object() {
			idx, ok := sf.nameIndex[kv.Key]
			if !ok {
				continue // unknown fields are ignored
			}
			f := sf.list[idx]
			fv := fieldByIndex(v, f.index)
			if err := unmarshalInto(kv.Value, fv); err != nil {
				return err
			}
		}
		return nil
	}
}

func newPointerDecoder(t reflect.Type) decoderFunc {
	return func(val Value, v reflect.Value) error {
		if val.Kind() == KindNull {
			v.Set(reflect.Zero(t))
			return nil
		}
		if v.IsNil() {
			v.Set(reflect.New(t.Elem()))
		}
		return unmarshalInto(val, v.Elem())
	}
}

// fieldByIndex navigates into a struct by index path, allocating intermediate
// pointers as needed.
func fieldByIndex(v reflect.Value, index []int) reflect.Value {
	for _, i := range index {
		if v.Kind() == reflect.Pointer {
			if v.IsNil() {
				v.Set(reflect.New(v.Type().Elem()))
			}
			v = v.Elem()
		}
		v = v.Field(i)
	}
	return v
}

// ── defaultGoValue ──────────────────────────────────────────────────────

// defaultGoValue maps a Value to the default Go type for interface{} targets.
func defaultGoValue(val Value) any {
	switch val.Kind() {
	case KindNull:
		return nil
	case KindBool:
		return val.BoolVal()
	case KindNumber:
		return val.Float64()
	case KindBigInt:
		bi := new(big.Int)
		bi.SetString(val.Str(), 10)
		return bi
	case KindString:
		return val.Str()
	case KindArray:
		elems := val.Array()
		result := make([]any, len(elems))
		for i, e := range elems {
			result[i] = defaultGoValue(e)
		}
		return result
	case KindObject:
		pairs := val.Object()
		result := make(map[string]any, len(pairs))
		for _, kv := range pairs {
			result[kv.Key] = defaultGoValue(kv.Value)
		}
		return result
	case KindDateTime:
		return val.Time()
	case KindTimeOnly:
		return val.TimeOnlyValue()
	case KindDuration:
		return Duration{ISO: val.Str()}
	case KindRegExp:
		return val.RegExpValue()
	case KindBinary:
		return val.Bytes()
	case KindMap:
		return val.Map()
	case KindSet:
		elems := val.Array()
		result := make(Set[any], len(elems))
		for i, e := range elems {
			result[i] = defaultGoValue(e)
		}
		return result
	case KindTuple:
		elems := val.Array()
		result := make(Tuple, len(elems))
		for i, e := range elems {
			result[i] = defaultGoValue(e)
		}
		return result
	}
	return nil
}
