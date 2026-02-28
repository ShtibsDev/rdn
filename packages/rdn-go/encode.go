package rdn

import (
	"fmt"
	"math"
	"strconv"
	"time"
)

const maxEncodeDepth = 128

// encoder holds the state for a single stringify operation.
type encoder struct {
	buf    *encodeState
	indent string
	prefix string
	depth  int
}

func newEncoder(indent, prefix string) *encoder {
	return &encoder{
		buf:    getEncodeState(),
		indent: indent,
		prefix: prefix,
	}
}

func (enc *encoder) writeIndent() {
	if enc.indent == "" {
		return
	}
	enc.buf.WriteByte('\n')
	enc.buf.WriteString(enc.prefix)
	for i := 0; i < enc.depth; i++ {
		enc.buf.WriteString(enc.indent)
	}
}

func (enc *encoder) writeSep() {
	if enc.indent != "" {
		enc.buf.WriteString(": ")
	} else {
		enc.buf.WriteByte(':')
	}
}

func (enc *encoder) writeArrow() {
	if enc.indent != "" {
		enc.buf.WriteString(" => ")
	} else {
		enc.buf.WriteString("=>")
	}
}

func (enc *encoder) enterDepth() error {
	enc.depth++
	if enc.depth > maxEncodeDepth {
		return fmt.Errorf("rdn: maximum encoding depth exceeded (%d)", maxEncodeDepth)
	}
	return nil
}

// ── String escaping ─────────────────────────────────────────────────────

func (enc *encoder) escapeString(s string) {
	// Fast scan: check if any char needs escaping
	needsEscape := false
	for i := 0; i < len(s); i++ {
		c := s[i]
		if c < 0x20 || c == '"' || c == '\\' {
			needsEscape = true
			break
		}
	}
	if !needsEscape {
		enc.buf.WriteByte('"')
		enc.buf.WriteString(s)
		enc.buf.WriteByte('"')
		return
	}

	// Slow path
	enc.buf.WriteByte('"')
	start := 0
	for i := 0; i < len(s); i++ {
		c := s[i]
		if c < 0x20 || c == '"' || c == '\\' {
			if i > start {
				enc.buf.WriteString(s[start:i])
			}
			enc.buf.WriteString(escapeTable[c])
			start = i + 1
		}
	}
	if start < len(s) {
		enc.buf.WriteString(s[start:])
	}
	enc.buf.WriteByte('"')
}

// ── RegExp source escaping ──────────────────────────────────────────────

func (enc *encoder) escapeRegExpSource(s string) {
	start := 0
	for i := 0; i < len(s); i++ {
		if s[i] == '/' {
			enc.buf.WriteString(s[start:i])
			enc.buf.WriteString("\\/")
			start = i + 1
		} else if s[i] == '\\' && i+1 < len(s) {
			i++ // skip escaped char (already escaped, don't touch)
		}
	}
	if start < len(s) {
		enc.buf.WriteString(s[start:])
	}
}

// ── Date formatting ─────────────────────────────────────────────────────

func (enc *encoder) formatDate(t time.Time) {
	t = t.UTC()
	y, mon, d := t.Date()
	h, min, sec := t.Clock()
	ms := t.Nanosecond() / 1_000_000

	enc.buf.WriteByte('@')
	// Year — handle negative and > 9999 safely
	if y < 0 {
		enc.buf.WriteByte('-')
		y = -y
	}
	if y > 9999 {
		enc.buf.WriteString(strconv.Itoa(y))
	} else {
		enc.buf.WriteString(digitPairs[y/100])
		enc.buf.WriteString(digitPairs[y%100])
	}
	enc.buf.WriteByte('-')
	enc.buf.WriteString(digitPairs[int(mon)])
	enc.buf.WriteByte('-')
	enc.buf.WriteString(digitPairs[d])
	enc.buf.WriteByte('T')
	enc.buf.WriteString(digitPairs[h])
	enc.buf.WriteByte(':')
	enc.buf.WriteString(digitPairs[min])
	enc.buf.WriteByte(':')
	enc.buf.WriteString(digitPairs[sec])
	enc.buf.WriteByte('.')
	// Milliseconds: 3 digits
	enc.buf.WriteByte(byte('0' + ms/100))
	enc.buf.WriteByte(byte('0' + (ms/10)%10))
	enc.buf.WriteByte(byte('0' + ms%10))
	enc.buf.WriteByte('Z')
}

// ── TimeOnly formatting ─────────────────────────────────────────────────

func (enc *encoder) formatTimeOnly(t TimeOnly) {
	enc.buf.WriteByte('@')
	// Bounds-safe: clamp to 0-99 range for digitPairs
	enc.buf.WriteString(digitPairs[clamp99(t.Hours)])
	enc.buf.WriteByte(':')
	enc.buf.WriteString(digitPairs[clamp99(t.Minutes)])
	enc.buf.WriteByte(':')
	enc.buf.WriteString(digitPairs[clamp99(t.Seconds)])
	if t.Milliseconds > 0 {
		ms := t.Milliseconds
		if ms > 999 {
			ms = 999
		}
		enc.buf.WriteByte('.')
		enc.buf.WriteByte(byte('0' + ms/100))
		enc.buf.WriteByte(byte('0' + (ms/10)%10))
		enc.buf.WriteByte(byte('0' + ms%10))
	}
}

func clamp99(v int) int {
	if v < 0 {
		return 0
	}
	if v > 99 {
		return 99
	}
	return v
}

// ── Base64 encoding ─────────────────────────────────────────────────────

func (enc *encoder) encodeBase64(data []byte) {
	enc.buf.WriteString(`b"`)
	n := len(data)
	i := 0
	for ; i+2 < n; i += 3 {
		a := data[i]
		b := data[i+1]
		c := data[i+2]
		enc.buf.WriteByte(b64Encode[a>>2])
		enc.buf.WriteByte(b64Encode[((a&0x03)<<4)|(b>>4)])
		enc.buf.WriteByte(b64Encode[((b&0x0F)<<2)|(c>>6)])
		enc.buf.WriteByte(b64Encode[c&0x3F])
	}
	if i < n {
		a := data[i]
		enc.buf.WriteByte(b64Encode[a>>2])
		if i+1 < n {
			b := data[i+1]
			enc.buf.WriteByte(b64Encode[((a&0x03)<<4)|(b>>4)])
			enc.buf.WriteByte(b64Encode[(b&0x0F)<<2])
			enc.buf.WriteByte('=')
		} else {
			enc.buf.WriteByte(b64Encode[(a&0x03)<<4])
			enc.buf.WriteByte('=')
			enc.buf.WriteByte('=')
		}
	}
	enc.buf.WriteByte('"')
}

// ── Core stringification ────────────────────────────────────────────────

func (enc *encoder) encode(v Value) error {
	switch v.kind {
	case KindNull:
		enc.buf.WriteString("null")

	case KindBool:
		if v.boolean {
			enc.buf.WriteString("true")
		} else {
			enc.buf.WriteString("false")
		}

	case KindNumber:
		if math.IsNaN(v.num) {
			enc.buf.WriteString("NaN")
		} else if math.IsInf(v.num, 1) {
			enc.buf.WriteString("Infinity")
		} else if math.IsInf(v.num, -1) {
			enc.buf.WriteString("-Infinity")
		} else {
			enc.buf.WriteString(strconv.FormatFloat(v.num, 'g', -1, 64))
		}

	case KindBigInt:
		enc.buf.WriteString(v.str)
		enc.buf.WriteByte('n')

	case KindString:
		enc.escapeString(v.str)

	case KindDateTime:
		enc.formatDate(v.timeVal)

	case KindTimeOnly:
		enc.formatTimeOnly(v.timeO)

	case KindDuration:
		enc.buf.WriteByte('@')
		enc.buf.WriteString(v.str)

	case KindRegExp:
		enc.buf.WriteByte('/')
		enc.escapeRegExpSource(v.regexpV.Source)
		enc.buf.WriteByte('/')
		enc.buf.WriteString(v.regexpV.Flags)

	case KindBinary:
		enc.encodeBase64(v.binary)

	case KindArray:
		enc.buf.WriteByte('[')
		if len(v.arr) > 0 {
			if err := enc.enterDepth(); err != nil {
				return err
			}
			for i, elem := range v.arr {
				if i > 0 {
					enc.buf.WriteByte(',')
				}
				enc.writeIndent()
				if err := enc.encode(elem); err != nil {
					return err
				}
			}
			enc.depth--
			enc.writeIndent()
		}
		enc.buf.WriteByte(']')

	case KindObject:
		enc.buf.WriteByte('{')
		if len(v.obj) > 0 {
			if err := enc.enterDepth(); err != nil {
				return err
			}
			for i, kv := range v.obj {
				if i > 0 {
					enc.buf.WriteByte(',')
				}
				enc.writeIndent()
				enc.escapeString(kv.Key)
				enc.writeSep()
				if err := enc.encode(kv.Value); err != nil {
					return err
				}
			}
			enc.depth--
			enc.writeIndent()
		}
		enc.buf.WriteByte('}')

	case KindMap:
		if len(v.mapV) == 0 {
			enc.buf.WriteString("Map{}")
		} else {
			enc.buf.WriteString("Map{")
			if err := enc.enterDepth(); err != nil {
				return err
			}
			for i, entry := range v.mapV {
				if i > 0 {
					enc.buf.WriteByte(',')
				}
				enc.writeIndent()
				if err := enc.encode(entry.Key); err != nil {
					return err
				}
				enc.writeArrow()
				if err := enc.encode(entry.Value); err != nil {
					return err
				}
			}
			enc.depth--
			enc.writeIndent()
			enc.buf.WriteByte('}')
		}

	case KindSet:
		if len(v.arr) == 0 {
			enc.buf.WriteString("Set{}")
		} else {
			enc.buf.WriteString("Set{")
			if err := enc.enterDepth(); err != nil {
				return err
			}
			for i, elem := range v.arr {
				if i > 0 {
					enc.buf.WriteByte(',')
				}
				enc.writeIndent()
				if err := enc.encode(elem); err != nil {
					return err
				}
			}
			enc.depth--
			enc.writeIndent()
			enc.buf.WriteByte('}')
		}

	case KindTuple:
		enc.buf.WriteByte('(')
		if len(v.arr) > 0 {
			if err := enc.enterDepth(); err != nil {
				return err
			}
			for i, elem := range v.arr {
				if i > 0 {
					enc.buf.WriteByte(',')
				}
				enc.writeIndent()
				if err := enc.encode(elem); err != nil {
					return err
				}
			}
			enc.depth--
			enc.writeIndent()
		}
		enc.buf.WriteByte(')')

	default:
		return fmt.Errorf("rdn: unsupported value kind %v", v.kind)
	}
	return nil
}

// stringify performs the full encoding and returns the result.
func stringify(v Value, prefix, indent string) ([]byte, error) {
	enc := newEncoder(indent, prefix)
	defer putEncodeState(enc.buf)

	if err := enc.encode(v); err != nil {
		return nil, err
	}

	// Copy result out before returning buffer to pool
	result := make([]byte, enc.buf.Len())
	copy(result, enc.buf.Bytes())
	return result, nil
}
