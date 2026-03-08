package rdn

import (
	"fmt"
	"strings"
	"testing"
)

var benchPrimitives = []byte(`{"null":null,"bool":true,"int":42,"float":3.14,"string":"hello world"}`)

var benchNested = []byte(`{"user":{"id":42n,"name":"Alice","created":@2024-01-15T10:30:00.000Z,"tags":Set{"admin","editor"},"sessions":Map{@2024-01-14T00:00:00.000Z=>@PT2H,@2024-01-15T00:00:00.000Z=>@PT1H30M}}}`)

var benchRDNHeavy = []byte(`{"date":@2024-06-15T12:00:00.000Z,"time":@14:30:00,"duration":@P1Y2M3DT4H5M6S,"bigint":999999999999999999n,"binary":b"SGVsbG8gV29ybGQh","regexp":/^test$/gi,"nan":NaN,"inf":Infinity}`)

func makeLargeArray(n int) []byte {
	var b strings.Builder
	b.WriteByte('[')
	for i := 0; i < n; i++ {
		if i > 0 {
			b.WriteByte(',')
		}
		fmt.Fprintf(&b, "%d", i)
	}
	b.WriteByte(']')
	return []byte(b.String())
}

func makeStringHeavy() []byte {
	return []byte(`{"key1":"The quick brown fox jumps over the lazy dog","key2":"Lorem ipsum dolor sit amet, consectetur adipiscing elit","key3":"Hello\nWorld\twith \"escapes\" and \\backslashes\\","key4":"short","key5":"another medium length string for testing purposes"}`)
}

var (
	benchLargeArray = makeLargeArray(1000)
	benchStringHeavy = makeStringHeavy()
)

// sink prevents compiler from optimizing away benchmark results
var sink Value
var sinkBytes []byte

func BenchmarkParse(b *testing.B) {
	benchmarks := []struct {
		name string
		data []byte
	}{
		{"Primitives", benchPrimitives},
		{"Nested", benchNested},
		{"RDNHeavy", benchRDNHeavy},
		{"LargeArray1K", benchLargeArray},
		{"StringHeavy", benchStringHeavy},
	}
	for _, bm := range benchmarks {
		b.Run(bm.name, func(b *testing.B) {
			b.SetBytes(int64(len(bm.data)))
			b.ReportAllocs()
			for i := 0; i < b.N; i++ {
				v, err := Parse(bm.data)
				if err != nil {
					b.Fatal(err)
				}
				sink = v
			}
		})
	}
}

func BenchmarkParseZeroCopy(b *testing.B) {
	benchmarks := []struct {
		name string
		data []byte
	}{
		{"Primitives", benchPrimitives},
		{"Nested", benchNested},
		{"RDNHeavy", benchRDNHeavy},
		{"LargeArray1K", benchLargeArray},
		{"StringHeavy", benchStringHeavy},
	}
	for _, bm := range benchmarks {
		b.Run(bm.name, func(b *testing.B) {
			b.SetBytes(int64(len(bm.data)))
			b.ReportAllocs()
			for i := 0; i < b.N; i++ {
				v, err := ParseZeroCopy(bm.data)
				if err != nil {
					b.Fatal(err)
				}
				sink = v
			}
		})
	}
}

func BenchmarkStringify(b *testing.B) {
	benchmarks := []struct {
		name string
		data []byte
	}{
		{"Primitives", benchPrimitives},
		{"Nested", benchNested},
		{"RDNHeavy", benchRDNHeavy},
		{"LargeArray1K", benchLargeArray},
		{"StringHeavy", benchStringHeavy},
	}
	for _, bm := range benchmarks {
		val, err := Parse(bm.data)
		if err != nil {
			b.Fatal(err)
		}
		b.Run(bm.name, func(b *testing.B) {
			b.SetBytes(int64(len(bm.data)))
			b.ReportAllocs()
			for i := 0; i < b.N; i++ {
				out, err := Stringify(val)
				if err != nil {
					b.Fatal(err)
				}
				sinkBytes = out
			}
		})
	}
}
