package rdnhttp

import (
	"net/http"
	"strings"
)

// NegotiateFormat inspects the request's Accept header and returns the best
// response format. When the Accept header contains application/rdn, FormatRDN
// is returned. When it contains application/json (but not application/rdn) and
// opts.JSONFallback is true, FormatJSON is returned. For wildcard, empty, or
// missing Accept headers the server default (FormatRDN) is used.
func NegotiateFormat(r *http.Request, opts Options) Format {
	accept := r.Header.Get("Accept")
	if accept == "" {
		return FormatRDN
	}

	hasRDN := false
	hasJSON := false
	for _, part := range strings.Split(accept, ",") {
		mt := strings.TrimSpace(part)
		// Strip quality parameters (e.g. ";q=0.9")
		if idx := strings.IndexByte(mt, ';'); idx >= 0 {
			mt = strings.TrimSpace(mt[:idx])
		}
		switch mt {
		case MediaTypeRDN:
			hasRDN = true
		case MediaTypeJSON:
			hasJSON = true
		case "*/*":
			return FormatRDN
		}
	}

	if hasRDN {
		return FormatRDN
	}
	if hasJSON && opts.JSONFallback {
		return FormatJSON
	}
	return FormatRDN
}

// DetectContentType inspects the request's Content-Type header and returns
// the corresponding format. Since RDN is a JSON superset, both formats are
// parsed identically by rdn.Parse — this function exists so callers can
// distinguish the two when it matters.
func DetectContentType(r *http.Request) Format {
	ct := r.Header.Get("Content-Type")
	mt := strings.TrimSpace(ct)
	if idx := strings.IndexByte(mt, ';'); idx >= 0 {
		mt = strings.TrimSpace(mt[:idx])
	}
	if mt == MediaTypeJSON {
		return FormatJSON
	}
	return FormatRDN
}

// AcceptsRDN reports whether the request's Accept header includes application/rdn.
func AcceptsRDN(r *http.Request) bool {
	return strings.Contains(r.Header.Get("Accept"), MediaTypeRDN)
}

// IsRDNContentType reports whether the request's Content-Type is application/rdn.
func IsRDNContentType(r *http.Request) bool {
	ct := r.Header.Get("Content-Type")
	mt := strings.TrimSpace(ct)
	if idx := strings.IndexByte(mt, ';'); idx >= 0 {
		mt = strings.TrimSpace(mt[:idx])
	}
	return mt == MediaTypeRDN
}
