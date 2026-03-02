package rdn

const maxDepth = 128
const maxElements = 10_000_000 // 10M total values across all collections

// scanner holds the mutable state for parsing a single RDN input.
// It is a struct (not globals) so parsing is safe for concurrent use.
type scanner struct {
	data     []byte
	pos      int
	len      int
	depth    int
	elements int // total parsed values (DoS protection)

	// Scratch buffer reused across materializeString calls.
	scratch []byte

	// Key interning cache for repeated short object keys.
	keyCache map[string]string

	// Zero-copy mode: when true, strings without escapes reference input data directly.
	zeroCopy bool
}

func newScanner(data []byte) scanner {
	return scanner{data: data, pos: 0, len: len(data), depth: 0}
}

func newScannerZeroCopy(data []byte) scanner {
	return scanner{data: data, pos: 0, len: len(data), depth: 0, zeroCopy: true}
}

func (s *scanner) error(msg string) error {
	return &SyntaxError{msg: msg, Offset: int64(s.pos)}
}

func (s *scanner) skipWs() {
	for s.pos < s.len {
		c := s.data[s.pos]
		if c == ' ' || c == '\t' || c == '\n' || c == '\r' {
			s.pos++
		} else {
			break
		}
	}
}

func (s *scanner) expect(ch byte) error {
	if s.pos >= s.len || s.data[s.pos] != ch {
		return s.error("Expected '" + string(rune(ch)) + "'")
	}
	s.pos++
	return nil
}

func (s *scanner) peek() byte {
	if s.pos >= s.len {
		return 0
	}
	return s.data[s.pos]
}

func (s *scanner) enterContainer() error {
	s.depth++
	if s.depth > maxDepth {
		return &SyntaxError{msg: "Maximum nesting depth exceeded (128)", Offset: int64(s.pos)}
	}
	return nil
}

func (s *scanner) leaveContainer() {
	s.depth--
}

func (s *scanner) countElement() error {
	s.elements++
	if s.elements > maxElements {
		return s.error("Maximum element count exceeded")
	}
	return nil
}

// ── String interning ────────────────────────────────────────────────────

const maxInternLen = 64

// internKey interns short strings for object key deduplication.
func (s *scanner) internKey(b []byte) string {
	if len(b) > maxInternLen {
		return string(b)
	}
	if s.keyCache == nil {
		s.keyCache = make(map[string]string, 16)
	}
	// string(b) in map lookup does not allocate (Go compiler optimization)
	if cached, ok := s.keyCache[string(b)]; ok {
		return cached
	}
	str := string(b)
	s.keyCache[str] = str
	return str
}
