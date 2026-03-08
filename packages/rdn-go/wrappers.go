package rdn

import "reflect"

// Set represents an RDN Set. It marshals to KindSet and unmarshals from KindSet.
type Set[T any] []T

func (s Set[T]) MarshalRDN() (Value, error) {
	elems := make([]Value, len(s))
	for i, item := range s {
		v, err := MarshalValue(item)
		if err != nil {
			return Value{}, err
		}
		elems[i] = v
	}
	return SetVal(elems), nil
}

func (s *Set[T]) UnmarshalRDN(v Value) error {
	if v.Kind() != KindSet && v.Kind() != KindArray {
		return &UnmarshalTypeError{Value: v.Kind().String(), Type: reflect.TypeOf(s).Elem()}
	}
	arr := v.Array()
	result := make(Set[T], len(arr))
	for i, elem := range arr {
		if err := UnmarshalValue(elem, &result[i]); err != nil {
			return err
		}
	}
	*s = result
	return nil
}

// Tuple represents an RDN Tuple of heterogeneous values.
type Tuple []any

func (t Tuple) MarshalRDN() (Value, error) {
	elems := make([]Value, len(t))
	for i, item := range t {
		v, err := MarshalValue(item)
		if err != nil {
			return Value{}, err
		}
		elems[i] = v
	}
	return TupleVal(elems), nil
}

func (t *Tuple) UnmarshalRDN(v Value) error {
	if v.Kind() != KindTuple && v.Kind() != KindArray {
		return &UnmarshalTypeError{Value: v.Kind().String(), Type: reflect.TypeOf(t).Elem()}
	}
	arr := v.Array()
	result := make(Tuple, len(arr))
	for i, elem := range arr {
		result[i] = defaultGoValue(elem)
	}
	*t = result
	return nil
}

// OrderedMapEntry is a key-value pair in an OrderedMap.
type OrderedMapEntry[K comparable, V any] struct {
	Key   K
	Value V
}

// OrderedMap represents an RDN Map that preserves insertion order.
type OrderedMap[K comparable, V any] struct {
	entries []OrderedMapEntry[K, V]
}

func (m OrderedMap[K, V]) Entries() []OrderedMapEntry[K, V] { return m.entries }
func (m OrderedMap[K, V]) Len() int                         { return len(m.entries) }
func (m OrderedMap[K, V]) Keys() []K {
	keys := make([]K, len(m.entries))
	for i, e := range m.entries {
		keys[i] = e.Key
	}
	return keys
}
func (m OrderedMap[K, V]) Values() []V {
	vals := make([]V, len(m.entries))
	for i, e := range m.entries {
		vals[i] = e.Value
	}
	return vals
}

func (m *OrderedMap[K, V]) Set(key K, value V) {
	for i := range m.entries {
		if m.entries[i].Key == key {
			m.entries[i].Value = value
			return
		}
	}
	m.entries = append(m.entries, OrderedMapEntry[K, V]{Key: key, Value: value})
}

func (m OrderedMap[K, V]) Get(key K) (V, bool) {
	for _, e := range m.entries {
		if e.Key == key {
			return e.Value, true
		}
	}
	var zero V
	return zero, false
}

func (m *OrderedMap[K, V]) Delete(key K) {
	for i := range m.entries {
		if m.entries[i].Key == key {
			m.entries = append(m.entries[:i], m.entries[i+1:]...)
			return
		}
	}
}

func (m OrderedMap[K, V]) MarshalRDN() (Value, error) {
	entries := make([]MapEntry, len(m.entries))
	for i, e := range m.entries {
		k, err := MarshalValue(e.Key)
		if err != nil {
			return Value{}, err
		}
		v, err := MarshalValue(e.Value)
		if err != nil {
			return Value{}, err
		}
		entries[i] = MapEntry{Key: k, Value: v}
	}
	return MapVal(entries), nil
}

func (m *OrderedMap[K, V]) UnmarshalRDN(v Value) error {
	if v.Kind() != KindMap && v.Kind() != KindObject {
		return &UnmarshalTypeError{Value: v.Kind().String(), Type: reflect.TypeOf(m).Elem()}
	}
	if v.Kind() == KindMap {
		mapEntries := v.Map()
		m.entries = make([]OrderedMapEntry[K, V], len(mapEntries))
		for i, me := range mapEntries {
			if err := UnmarshalValue(me.Key, &m.entries[i].Key); err != nil {
				return err
			}
			if err := UnmarshalValue(me.Value, &m.entries[i].Value); err != nil {
				return err
			}
		}
	} else { // KindObject
		objEntries := v.Object()
		m.entries = make([]OrderedMapEntry[K, V], len(objEntries))
		for i, kv := range objEntries {
			if err := UnmarshalValue(StringVal(kv.Key), &m.entries[i].Key); err != nil {
				return err
			}
			if err := UnmarshalValue(kv.Value, &m.entries[i].Value); err != nil {
				return err
			}
		}
	}
	return nil
}
