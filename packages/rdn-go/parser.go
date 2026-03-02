package rdn

import (
	"errors"
	"math"
	"strconv"
	"time"
	"unicode/utf16"
	"unsafe"
)

const maxBinarySize = 100 * 1024 * 1024 // 100 MB
const maxNumberLen = 1000               // max digits in a number literal

// ── String parsing with deferred materialization ────────────────────────

func (s *scanner) parseString() (string, error) {
	s.pos++ // skip opening "
	start := s.pos
	hasEscape := false
	for s.pos < s.len {
		c := s.data[s.pos]
		if c == '"' {
			if !hasEscape {
				var result string
				if s.zeroCopy {
					result = unsafeString(s.data[start:s.pos])
				} else {
					result = string(s.data[start:s.pos])
				}
				s.pos++ // skip closing "
				return result, nil
			}
			result, err := s.materializeString(start, s.pos)
			if err != nil {
				return "", err
			}
			s.pos++ // skip closing "
			return result, nil
		}
		if c == '\\' {
			hasEscape = true
			s.pos++ // skip backslash
			if s.pos >= s.len {
				break
			}
			if s.data[s.pos] == 'u' {
				if s.pos+5 <= s.len {
					s.pos += 5 // u + 4 hex digits
				} else {
					s.pos = s.len // will trigger "Unterminated string"
				}
			} else {
				s.pos++
			}
			continue
		}
		if c < 0x20 {
			return "", s.error("Unescaped control character in string")
		}
		s.pos++
	}
	return "", s.error("Unterminated string")
}

// parseObjectKey parses a string and interns it for key deduplication.
func (s *scanner) parseObjectKey() (string, error) {
	s.pos++ // skip opening "
	start := s.pos
	hasEscape := false
	for s.pos < s.len {
		c := s.data[s.pos]
		if c == '"' {
			if !hasEscape {
				result := s.internKey(s.data[start:s.pos])
				s.pos++ // skip closing "
				return result, nil
			}
			result, err := s.materializeString(start, s.pos)
			if err != nil {
				return "", err
			}
			s.pos++ // skip closing "
			return result, nil
		}
		if c == '\\' {
			hasEscape = true
			s.pos++
			if s.pos >= s.len {
				break
			}
			if s.data[s.pos] == 'u' {
				if s.pos+5 <= s.len {
					s.pos += 5
				} else {
					s.pos = s.len
				}
			} else {
				s.pos++
			}
			continue
		}
		if c < 0x20 {
			return "", s.error("Unescaped control character in string")
		}
		s.pos++
	}
	return "", s.error("Unterminated string")
}

func (s *scanner) materializeString(start, end int) (string, error) {
	// Reuse scratch buffer to avoid per-call allocation
	s.scratch = s.scratch[:0]
	if cap(s.scratch) < end-start {
		s.scratch = make([]byte, 0, end-start)
	}
	buf := s.scratch
	i := start
	for i < end {
		c := s.data[i]
		if c == '\\' {
			i++
			if i >= end {
				return "", &SyntaxError{msg: "Unexpected end of escape sequence", Offset: int64(i)}
			}
			esc := s.data[i]
			switch esc {
			case '"':
				buf = append(buf, '"')
				i++
			case '\\':
				buf = append(buf, '\\')
				i++
			case '/':
				buf = append(buf, '/')
				i++
			case 'b':
				buf = append(buf, '\b')
				i++
			case 'f':
				buf = append(buf, '\f')
				i++
			case 'n':
				buf = append(buf, '\n')
				i++
			case 'r':
				buf = append(buf, '\r')
				i++
			case 't':
				buf = append(buf, '\t')
				i++
			case 'u':
				if i+4 >= end {
					return "", &SyntaxError{msg: "Invalid unicode escape", Offset: int64(i)}
				}
				code, err := parseHex4(s.data[i+1 : i+5])
				if err != nil {
					return "", &SyntaxError{msg: "Invalid unicode escape", Offset: int64(i)}
				}
				i += 5
				// Handle surrogate pairs
				if utf16.IsSurrogate(rune(code)) {
					if i+1 < end && s.data[i] == '\\' && s.data[i+1] == 'u' {
						if i+5 < end {
							code2, err := parseHex4(s.data[i+2 : i+6])
							if err == nil {
								r := utf16.DecodeRune(rune(code), rune(code2))
								if r != '\uFFFD' {
									buf = appendRune(buf, r)
									i += 6
									continue
								}
							}
						}
					}
					// Lone surrogate -> replacement character
					buf = appendRune(buf, '\uFFFD')
				} else {
					buf = appendRune(buf, rune(code))
				}
			default:
				return "", &SyntaxError{msg: "Invalid escape sequence '\\" + string(rune(esc)) + "'", Offset: int64(i - 1)}
			}
		} else {
			// Find the next backslash or end for bulk copy
			j := i + 1
			for j < end && s.data[j] != '\\' {
				j++
			}
			buf = append(buf, s.data[i:j]...)
			i = j
		}
	}
	s.scratch = buf // save grown buffer for next call
	return string(buf), nil
}

func parseHex4(b []byte) (uint16, error) {
	var val uint16
	for _, c := range b {
		v := hexDecode[c]
		if v == 0xFF {
			return 0, errInvalidHex
		}
		val = val<<4 | uint16(v)
	}
	return val, nil
}

var errInvalidHex = &SyntaxError{msg: "invalid hex digit"}

func appendRune(buf []byte, r rune) []byte {
	var tmp [4]byte
	n := encodeRuneToUTF8(tmp[:], r)
	return append(buf, tmp[:n]...)
}

func encodeRuneToUTF8(p []byte, r rune) int {
	if r < 0x80 {
		p[0] = byte(r)
		return 1
	}
	if r < 0x800 {
		p[0] = byte(0xC0 | (r >> 6))
		p[1] = byte(0x80 | (r & 0x3F))
		return 2
	}
	if r < 0x10000 {
		p[0] = byte(0xE0 | (r >> 12))
		p[1] = byte(0x80 | ((r >> 6) & 0x3F))
		p[2] = byte(0x80 | (r & 0x3F))
		return 3
	}
	p[0] = byte(0xF0 | (r >> 18))
	p[1] = byte(0x80 | ((r >> 12) & 0x3F))
	p[2] = byte(0x80 | ((r >> 6) & 0x3F))
	p[3] = byte(0x80 | (r & 0x3F))
	return 4
}

// unsafeString creates a string that references the byte slice data without copying.
// The returned string is only valid as long as the byte slice is not modified.
func unsafeString(b []byte) string {
	if len(b) == 0 {
		return ""
	}
	return unsafe.String(&b[0], len(b))
}

// ── Number parsing ──────────────────────────────────────────────────────

func (s *scanner) parseNumber(negative bool) (Value, error) {
	start := s.pos
	if negative {
		start = s.pos - 1
	}

	// Accumulate integer digits
	var intValue int64
	digitCount := 0
	for s.pos < s.len {
		d := s.data[s.pos] - '0'
		if d > 9 {
			break
		}
		intValue = intValue*10 + int64(d)
		digitCount++
		s.pos++
	}
	if digitCount == 0 {
		return Value{}, s.error("Expected digit")
	}

	// Leading zero check: "01" is invalid, "0" alone is ok
	firstDigitPos := start
	if negative {
		firstDigitPos = start + 1
	}
	if digitCount > 1 && s.data[firstDigitPos] == '0' {
		return Value{}, s.error("Leading zeros not allowed")
	}

	// Check for bigint suffix 'n'
	if s.pos < s.len && s.data[s.pos] == 'n' {
		s.pos++
		return BigIntVal(string(s.data[start : s.pos-1])), nil
	}

	isFloat := false

	// Fraction
	if s.pos < s.len && s.data[s.pos] == '.' {
		isFloat = true
		s.pos++ // skip '.'
		fracDigits := 0
		for s.pos < s.len {
			d := s.data[s.pos] - '0'
			if d > 9 {
				break
			}
			fracDigits++
			s.pos++
		}
		if fracDigits == 0 {
			return Value{}, s.error("Expected digit after decimal point")
		}
	}

	// Exponent
	if s.pos < s.len {
		e := s.data[s.pos]
		if e == 'e' || e == 'E' {
			isFloat = true
			s.pos++
			if s.pos < s.len {
				sign := s.data[s.pos]
				if sign == '+' || sign == '-' {
					s.pos++
				}
			}
			expDigits := 0
			for s.pos < s.len {
				d := s.data[s.pos] - '0'
				if d > 9 {
					break
				}
				expDigits++
				s.pos++
			}
			if expDigits == 0 {
				return Value{}, s.error("Expected digit in exponent")
			}
		}
	}

	// Check for invalid bigint suffix after float
	if s.pos < s.len && s.data[s.pos] == 'n' {
		if isFloat {
			return Value{}, s.error("BigInt cannot have decimal point or exponent")
		}
	}

	// Guard against absurdly long number literals (DoS)
	if s.pos-start > maxNumberLen {
		return Value{}, s.error("Number literal too long")
	}

	// Fast path: small integers (≤15 digits, no float)
	if !isFloat && digitCount <= 15 {
		if negative {
			if intValue == 0 {
				return NumberVal(math.Copysign(0, -1)), nil
			}
			return NumberVal(float64(-intValue)), nil
		}
		return NumberVal(float64(intValue)), nil
	}

	f, err := strconv.ParseFloat(string(s.data[start:s.pos]), 64)
	if err != nil && !errors.Is(err, strconv.ErrRange) {
		return Value{}, s.error("Invalid number")
	}
	// ErrRange: overflow → ±Inf, underflow → 0 — both valid in RDN
	return NumberVal(f), nil
}

// ── Date/Time parsing ───────────────────────────────────────────────────

func (s *scanner) readDigits2() (int, error) {
	if s.pos+1 >= s.len {
		return 0, s.error("Unexpected end of input")
	}
	d1 := int(s.data[s.pos]) - '0'
	d2 := int(s.data[s.pos+1]) - '0'
	if d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 {
		return 0, s.error("Expected 2-digit number")
	}
	s.pos += 2
	return d1*10 + d2, nil
}

func (s *scanner) readDigits3() (int, error) {
	if s.pos+2 >= s.len {
		return 0, s.error("Unexpected end of input")
	}
	d1 := int(s.data[s.pos]) - '0'
	d2 := int(s.data[s.pos+1]) - '0'
	d3 := int(s.data[s.pos+2]) - '0'
	if d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 || d3 < 0 || d3 > 9 {
		return 0, s.error("Expected 3-digit number")
	}
	s.pos += 3
	return d1*100 + d2*10 + d3, nil
}

func (s *scanner) readDigits4() (int, error) {
	if s.pos+3 >= s.len {
		return 0, s.error("Unexpected end of input")
	}
	d1 := int(s.data[s.pos]) - '0'
	d2 := int(s.data[s.pos+1]) - '0'
	d3 := int(s.data[s.pos+2]) - '0'
	d4 := int(s.data[s.pos+3]) - '0'
	if d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 || d3 < 0 || d3 > 9 || d4 < 0 || d4 > 9 {
		return 0, s.error("Expected 4-digit year")
	}
	s.pos += 4
	return d1*1000 + d2*100 + d3*10 + d4, nil
}

func (s *scanner) parseAt() (Value, error) {
	s.pos++ // skip @
	if s.pos >= s.len {
		return Value{}, s.error("Unexpected end after @")
	}

	ch := s.data[s.pos]

	// Duration: @P...
	if ch == 'P' {
		return s.parseDuration()
	}

	// Distinguish time vs date vs unix timestamp
	if ch >= '0' && ch <= '9' {
		if s.pos+2 < s.len && s.data[s.pos+2] == ':' {
			return s.parseTimeOnly()
		}
		if s.pos+4 < s.len && s.data[s.pos+4] == '-' {
			return s.parseDateTime()
		}
		return s.parseUnixTimestamp()
	}

	return Value{}, s.error("Invalid @ literal")
}

func (s *scanner) parseDateTime() (Value, error) {
	year, err := s.readDigits4()
	if err != nil {
		return Value{}, err
	}
	if err := s.expect('-'); err != nil {
		return Value{}, err
	}
	month, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}
	if err := s.expect('-'); err != nil {
		return Value{}, err
	}
	day, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}

	// Date only: @YYYY-MM-DD
	if s.pos >= s.len || s.data[s.pos] != 'T' {
		t := time.Date(year, time.Month(month), day, 0, 0, 0, 0, time.UTC)
		return DateTimeVal(t), nil
	}

	s.pos++ // skip 'T'
	hours, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}
	if err := s.expect(':'); err != nil {
		return Value{}, err
	}
	minutes, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}
	if err := s.expect(':'); err != nil {
		return Value{}, err
	}
	seconds, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}

	ms := 0
	if s.pos < s.len && s.data[s.pos] == '.' {
		s.pos++ // skip '.'
		ms, err = s.readDigits3()
		if err != nil {
			return Value{}, err
		}
	}

	if err := s.expect('Z'); err != nil {
		return Value{}, err
	}

	t := time.Date(year, time.Month(month), day, hours, minutes, seconds, ms*1_000_000, time.UTC)
	return DateTimeVal(t), nil
}

func (s *scanner) parseTimeOnly() (Value, error) {
	hours, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}
	if err := s.expect(':'); err != nil {
		return Value{}, err
	}
	minutes, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}
	if err := s.expect(':'); err != nil {
		return Value{}, err
	}
	seconds, err := s.readDigits2()
	if err != nil {
		return Value{}, err
	}

	ms := 0
	if s.pos < s.len && s.data[s.pos] == '.' {
		s.pos++
		ms, err = s.readDigits3()
		if err != nil {
			return Value{}, err
		}
	}

	return TimeOnlyVal(TimeOnly{Hours: hours, Minutes: minutes, Seconds: seconds, Milliseconds: ms}), nil
}

func (s *scanner) parseDuration() (Value, error) {
	start := s.pos
	s.pos++ // skip 'P'
	for s.pos < s.len {
		c := s.data[s.pos]
		if (c >= '0' && c <= '9') || c == 'Y' || c == 'M' || c == 'D' || c == 'T' || c == 'H' || c == 'S' || c == '.' {
			s.pos++
		} else {
			break
		}
	}
	iso := string(s.data[start:s.pos])
	if len(iso) < 3 {
		return Value{}, s.error("Invalid duration")
	}
	hasDesignator := false
	for i := 1; i < len(iso); i++ {
		c := iso[i]
		if c == 'Y' || c == 'M' || c == 'D' || c == 'H' || c == 'S' {
			hasDesignator = true
			break
		}
	}
	if !hasDesignator {
		return Value{}, s.error("Invalid duration: no designator found")
	}
	return DurationVal(iso), nil
}

func (s *scanner) parseUnixTimestamp() (Value, error) {
	start := s.pos
	for s.pos < s.len {
		d := s.data[s.pos] - '0'
		if d > 9 {
			break
		}
		s.pos++
	}
	digits := string(s.data[start:s.pos])
	num, err := strconv.ParseInt(digits, 10, 64)
	if err != nil {
		return Value{}, s.error("Invalid unix timestamp")
	}
	if len(digits) <= 10 {
		return DateTimeVal(time.Unix(num, 0).UTC()), nil
	}
	return DateTimeVal(time.UnixMilli(num).UTC()), nil
}

// ── RegExp parsing ──────────────────────────────────────────────────────

func (s *scanner) parseRegExp() (Value, error) {
	s.pos++ // skip opening /
	patternStart := s.pos
	escaped := false

	for s.pos < s.len {
		c := s.data[s.pos]
		if escaped {
			escaped = false
			s.pos++
			continue
		}
		if c == '\\' {
			escaped = true
			s.pos++
			continue
		}
		if c == '/' {
			break
		}
		s.pos++
	}

	if s.pos >= s.len {
		return Value{}, s.error("Unterminated regular expression")
	}
	raw := s.data[patternStart:s.pos]
	if len(raw) == 0 {
		return Value{}, s.error("Empty regular expression body")
	}
	pattern := unescapeRegExpSlash(raw)
	s.pos++ // skip closing /

	flagStart := s.pos
	for s.pos < s.len {
		c := s.data[s.pos]
		if c == 'd' || c == 'g' || c == 'i' || c == 'm' || c == 's' || c == 'u' || c == 'v' || c == 'y' {
			s.pos++
		} else {
			break
		}
	}
	flags := string(s.data[flagStart:s.pos])
	return RegExpVal(pattern, flags), nil
}

func unescapeRegExpSlash(raw []byte) string {
	hasEscape := false
	for i := 0; i+1 < len(raw); i++ {
		if raw[i] == '\\' && raw[i+1] == '/' {
			hasEscape = true
			break
		}
	}
	if !hasEscape {
		return string(raw)
	}
	buf := make([]byte, 0, len(raw))
	for i := 0; i < len(raw); i++ {
		if raw[i] == '\\' && i+1 < len(raw) && raw[i+1] == '/' {
			buf = append(buf, '/')
			i++
		} else {
			buf = append(buf, raw[i])
		}
	}
	return string(buf)
}

// ── Binary parsing ──────────────────────────────────────────────────────

func (s *scanner) parseBinaryB64() (Value, error) {
	s.pos++ // skip 'b'
	if s.pos >= s.len || s.data[s.pos] != '"' {
		return Value{}, s.error("Expected '\"' after 'b'")
	}
	s.pos++ // skip opening "

	start := s.pos
	for s.pos < s.len && s.data[s.pos] != '"' {
		s.pos++
	}
	if s.pos >= s.len {
		return Value{}, s.error("Unterminated binary literal")
	}
	content := s.data[start:s.pos]
	s.pos++ // skip closing "

	if len(content) == 0 {
		return BinaryVal([]byte{}), nil
	}

	if len(content)%4 != 0 {
		return Value{}, s.error("Invalid base64: length must be a multiple of 4")
	}

	padding := 0
	if content[len(content)-1] == '=' {
		padding++
	}
	if len(content) > 1 && content[len(content)-2] == '=' {
		padding++
	}

	outLen := (len(content) / 4) * 3 - padding
	if outLen > maxBinarySize {
		return Value{}, s.error("Binary data too large")
	}
	out := make([]byte, outLen)

	outPos := 0
	lastGroup := len(content) - 4
	for i := 0; i < len(content); i += 4 {
		a := b64Decode[content[i]]
		b := b64Decode[content[i+1]]
		c := b64Decode[content[i+2]]
		d := b64Decode[content[i+3]]

		if a == 0xFF || a == 0xFE || b == 0xFF || b == 0xFE {
			return Value{}, s.error("Invalid base64 character")
		}
		if c == 0xFF || d == 0xFF {
			return Value{}, s.error("Invalid base64 character")
		}

		if (c == 0xFE || d == 0xFE) && i != lastGroup {
			return Value{}, s.error("Invalid base64: padding in non-final group")
		}

		if c == 0xFE {
			if d != 0xFE {
				return Value{}, s.error("Invalid base64 padding")
			}
			if b&0x0F != 0 {
				return Value{}, s.error("Invalid base64: non-zero padding bits")
			}
			out[outPos] = (a << 2) | (b >> 4)
			outPos++
		} else if d == 0xFE {
			if c&0x03 != 0 {
				return Value{}, s.error("Invalid base64: non-zero padding bits")
			}
			out[outPos] = (a << 2) | (b >> 4)
			out[outPos+1] = ((b & 0x0F) << 4) | (c >> 2)
			outPos += 2
		} else {
			out[outPos] = (a << 2) | (b >> 4)
			out[outPos+1] = ((b & 0x0F) << 4) | (c >> 2)
			out[outPos+2] = ((c & 0x03) << 6) | d
			outPos += 3
		}
	}

	return BinaryVal(out), nil
}

func (s *scanner) parseBinaryHex() (Value, error) {
	s.pos++ // skip 'x'
	if s.pos >= s.len || s.data[s.pos] != '"' {
		return Value{}, s.error("Expected '\"' after 'x'")
	}
	s.pos++ // skip opening "

	start := s.pos
	for s.pos < s.len && s.data[s.pos] != '"' {
		s.pos++
	}
	if s.pos >= s.len {
		return Value{}, s.error("Unterminated hex literal")
	}
	content := s.data[start:s.pos]
	s.pos++ // skip closing "

	if len(content) == 0 {
		return BinaryVal([]byte{}), nil
	}
	if len(content)%2 != 0 {
		return Value{}, s.error("Invalid hex: odd length")
	}
	if len(content)/2 > maxBinarySize {
		return Value{}, s.error("Binary data too large")
	}

	out := make([]byte, len(content)/2)
	for i := 0; i < len(content); i += 2 {
		hi := hexDecode[content[i]]
		lo := hexDecode[content[i+1]]
		if hi == 0xFF || lo == 0xFF {
			return Value{}, s.error("Invalid hex character")
		}
		out[i/2] = (hi << 4) | lo
	}
	return BinaryVal(out), nil
}

// ── Collection parsing ──────────────────────────────────────────────────

func (s *scanner) parseArray() (Value, error) {
	if err := s.enterContainer(); err != nil {
		return Value{}, err
	}
	s.pos++ // skip [
	s.skipWs()
	if s.pos < s.len && s.data[s.pos] == ']' {
		s.pos++
		s.leaveContainer()
		return ArrayVal([]Value{}), nil
	}

	arr := make([]Value, 0, 8)
	v, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	arr = append(arr, v)
	s.skipWs()
	for s.pos < s.len && s.data[s.pos] == ',' {
		s.pos++
		s.skipWs()
		v, err = s.parseValue()
		if err != nil {
			return Value{}, err
		}
		arr = append(arr, v)
		s.skipWs()
	}
	if err := s.expect(']'); err != nil {
		return Value{}, err
	}
	s.leaveContainer()
	return ArrayVal(arr), nil
}

func (s *scanner) parseTuple() (Value, error) {
	if err := s.enterContainer(); err != nil {
		return Value{}, err
	}
	s.pos++ // skip (
	s.skipWs()
	if s.pos < s.len && s.data[s.pos] == ')' {
		s.pos++
		s.leaveContainer()
		return TupleVal([]Value{}), nil
	}

	arr := make([]Value, 0, 4)
	v, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	arr = append(arr, v)
	s.skipWs()
	for s.pos < s.len && s.data[s.pos] == ',' {
		s.pos++
		s.skipWs()
		v, err = s.parseValue()
		if err != nil {
			return Value{}, err
		}
		arr = append(arr, v)
		s.skipWs()
	}
	if err := s.expect(')'); err != nil {
		return Value{}, err
	}
	s.leaveContainer()
	return TupleVal(arr), nil
}

func (s *scanner) parseBrace() (Value, error) {
	if err := s.enterContainer(); err != nil {
		return Value{}, err
	}
	s.pos++ // skip {
	s.skipWs()

	// Empty braces → Object
	if s.pos < s.len && s.data[s.pos] == '}' {
		s.pos++
		s.leaveContainer()
		return ObjectVal([]KeyValue{}), nil
	}

	// Parse first value
	firstValue, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	s.skipWs()

	if s.pos >= s.len {
		return Value{}, s.error("Unterminated brace expression")
	}

	sep := s.data[s.pos]

	// : → Object
	if sep == ':' {
		if firstValue.kind != KindString {
			return Value{}, s.error("Object key must be a string")
		}
		return s.finishObject(firstValue.str)
	}

	// = → check for => (Map)
	if sep == '=' {
		if s.pos+1 < s.len && s.data[s.pos+1] == '>' {
			return s.finishMap(firstValue)
		}
		return Value{}, s.error("Expected '=>'")
	}

	// , → Set
	if sep == ',' {
		return s.finishSet(firstValue)
	}

	// } → single-element Set
	if sep == '}' {
		s.pos++
		s.leaveContainer()
		return SetVal([]Value{firstValue}), nil
	}

	return Value{}, s.error("Expected ':', '=>', ',' or '}' after value in brace expression")
}

func (s *scanner) finishObject(firstKey string) (Value, error) {
	s.pos++ // skip :
	s.skipWs()
	firstVal, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	pairs := make([]KeyValue, 0, 8)
	pairs = append(pairs, KeyValue{Key: firstKey, Value: firstVal})
	s.skipWs()

	for s.pos < s.len && s.data[s.pos] == ',' {
		s.pos++
		s.skipWs()
		if s.pos >= s.len || s.data[s.pos] != '"' {
			return Value{}, s.error("Object key must be a string")
		}
		key, err := s.parseObjectKey()
		if err != nil {
			return Value{}, err
		}
		s.skipWs()
		if err := s.expect(':'); err != nil {
			return Value{}, err
		}
		s.skipWs()
		val, err := s.parseValue()
		if err != nil {
			return Value{}, err
		}
		pairs = append(pairs, KeyValue{Key: key, Value: val})
		s.skipWs()
	}

	if err := s.expect('}'); err != nil {
		return Value{}, err
	}
	s.leaveContainer()
	return ObjectVal(pairs), nil
}

func (s *scanner) finishMap(firstKey Value) (Value, error) {
	s.pos += 2 // skip =>
	s.skipWs()
	firstVal, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	entries := make([]MapEntry, 0, 4)
	entries = append(entries, MapEntry{Key: firstKey, Value: firstVal})
	s.skipWs()

	for s.pos < s.len && s.data[s.pos] == ',' {
		s.pos++
		s.skipWs()
		key, err := s.parseValue()
		if err != nil {
			return Value{}, err
		}
		s.skipWs()
		if s.pos+1 >= s.len || s.data[s.pos] != '=' || s.data[s.pos+1] != '>' {
			return Value{}, s.error("Expected '=>' in map entry")
		}
		s.pos += 2
		s.skipWs()
		val, err := s.parseValue()
		if err != nil {
			return Value{}, err
		}
		entries = append(entries, MapEntry{Key: key, Value: val})
		s.skipWs()
	}

	if err := s.expect('}'); err != nil {
		return Value{}, err
	}
	s.leaveContainer()
	return MapVal(entries), nil
}

func (s *scanner) finishSet(firstValue Value) (Value, error) {
	items := make([]Value, 0, 8)
	items = append(items, firstValue)
	s.pos++ // skip ,
	s.skipWs()
	v, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	items = append(items, v)
	s.skipWs()

	for s.pos < s.len && s.data[s.pos] == ',' {
		s.pos++
		s.skipWs()
		v, err = s.parseValue()
		if err != nil {
			return Value{}, err
		}
		items = append(items, v)
		s.skipWs()
	}

	if err := s.expect('}'); err != nil {
		return Value{}, err
	}
	s.leaveContainer()
	return SetVal(items), nil
}

func (s *scanner) parseExplicitMap() (Value, error) {
	if err := s.enterContainer(); err != nil {
		return Value{}, err
	}
	if s.pos+3 >= s.len || s.data[s.pos+1] != 'a' || s.data[s.pos+2] != 'p' || s.data[s.pos+3] != '{' {
		return Value{}, s.error("Expected 'Map{'")
	}
	s.pos += 4 // skip 'Map{'
	s.skipWs()

	if s.pos < s.len && s.data[s.pos] == '}' {
		s.pos++
		s.leaveContainer()
		return MapVal([]MapEntry{}), nil
	}

	entries := make([]MapEntry, 0, 4)
	key, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	s.skipWs()
	if s.pos+1 >= s.len || s.data[s.pos] != '=' || s.data[s.pos+1] != '>' {
		return Value{}, s.error("Expected '=>' in map entry")
	}
	s.pos += 2
	s.skipWs()
	val, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	entries = append(entries, MapEntry{Key: key, Value: val})
	s.skipWs()

	for s.pos < s.len && s.data[s.pos] == ',' {
		s.pos++
		s.skipWs()
		key, err = s.parseValue()
		if err != nil {
			return Value{}, err
		}
		s.skipWs()
		if s.pos+1 >= s.len || s.data[s.pos] != '=' || s.data[s.pos+1] != '>' {
			return Value{}, s.error("Expected '=>' in map entry")
		}
		s.pos += 2
		s.skipWs()
		val, err = s.parseValue()
		if err != nil {
			return Value{}, err
		}
		entries = append(entries, MapEntry{Key: key, Value: val})
		s.skipWs()
	}

	if err := s.expect('}'); err != nil {
		return Value{}, err
	}
	s.leaveContainer()
	return MapVal(entries), nil
}

func (s *scanner) parseExplicitSet() (Value, error) {
	if err := s.enterContainer(); err != nil {
		return Value{}, err
	}
	if s.pos+3 >= s.len || s.data[s.pos+1] != 'e' || s.data[s.pos+2] != 't' || s.data[s.pos+3] != '{' {
		return Value{}, s.error("Expected 'Set{'")
	}
	s.pos += 4 // skip 'Set{'
	s.skipWs()

	if s.pos < s.len && s.data[s.pos] == '}' {
		s.pos++
		s.leaveContainer()
		return SetVal([]Value{}), nil
	}

	items := make([]Value, 0, 8)
	v, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	items = append(items, v)
	s.skipWs()

	for s.pos < s.len && s.data[s.pos] == ',' {
		s.pos++
		s.skipWs()
		v, err = s.parseValue()
		if err != nil {
			return Value{}, err
		}
		items = append(items, v)
		s.skipWs()
	}

	if err := s.expect('}'); err != nil {
		return Value{}, err
	}
	s.leaveContainer()
	return SetVal(items), nil
}

// ── Literal parsing ─────────────────────────────────────────────────────

func (s *scanner) parseLiteral(expected string) error {
	for i := 0; i < len(expected); i++ {
		if s.pos >= s.len || s.data[s.pos] != expected[i] {
			return s.error("Expected '" + expected + "'")
		}
		s.pos++
	}
	return nil
}

// ── Main value dispatch ─────────────────────────────────────────────────

func (s *scanner) parseValue() (Value, error) {
	s.skipWs()
	if s.pos >= s.len {
		return Value{}, s.error("Unexpected end of input")
	}
	if err := s.countElement(); err != nil {
		return Value{}, err
	}

	ch := s.data[s.pos]
	tok := tokenTable[ch]

	switch tok {
	case tString:
		str, err := s.parseString()
		if err != nil {
			return Value{}, err
		}
		return StringVal(str), nil

	case tNumber:
		return s.parseNumber(false)

	case tMinus:
		s.pos++ // skip -
		if s.pos < s.len && s.data[s.pos] == 'I' {
			if err := s.parseLiteral("Infinity"); err != nil {
				return Value{}, err
			}
			return NumberVal(math.Inf(-1)), nil
		}
		return s.parseNumber(true)

	case tOpenBrace:
		return s.parseBrace()

	case tOpenBracket:
		return s.parseArray()

	case tOpenParen:
		return s.parseTuple()

	case tTrue:
		if err := s.parseLiteral("true"); err != nil {
			return Value{}, err
		}
		return Bool(true), nil

	case tFalse:
		if err := s.parseLiteral("false"); err != nil {
			return Value{}, err
		}
		return Bool(false), nil

	case tNull:
		if err := s.parseLiteral("null"); err != nil {
			return Value{}, err
		}
		return Null(), nil

	case tAt:
		return s.parseAt()

	case tSlash:
		return s.parseRegExp()

	case tB64:
		return s.parseBinaryB64()

	case tHex:
		return s.parseBinaryHex()

	case tInfinity:
		if err := s.parseLiteral("Infinity"); err != nil {
			return Value{}, err
		}
		return NumberVal(math.Inf(1)), nil

	case tNaN:
		if err := s.parseLiteral("NaN"); err != nil {
			return Value{}, err
		}
		return NumberVal(math.NaN()), nil

	case tMap:
		return s.parseExplicitMap()

	case tSet:
		return s.parseExplicitSet()

	default:
		return Value{}, s.error("Unexpected character '" + string(rune(ch)) + "'")
	}
}

// parseRoot is the entry point: parse one value, ensure no trailing content.
func parseRoot(data []byte) (Value, error) {
	s := newScanner(data)
	v, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	s.skipWs()
	if s.pos < s.len {
		return Value{}, s.error("Unexpected data after value")
	}
	return v, nil
}

// parseRootZeroCopy is like parseRoot but strings without escapes reference the input directly.
func parseRootZeroCopy(data []byte) (Value, error) {
	s := newScannerZeroCopy(data)
	v, err := s.parseValue()
	if err != nil {
		return Value{}, err
	}
	s.skipWs()
	if s.pos < s.len {
		return Value{}, s.error("Unexpected data after value")
	}
	return v, nil
}
