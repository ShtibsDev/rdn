// Package rdnhttp provides HTTP content-type negotiation and handler utilities
// for serving and consuming RDN (Rich Data Notation) over HTTP.
//
// It lives in a sub-package to avoid pulling net/http into every consumer of
// the core rdn package.
package rdnhttp

// Media type constants for HTTP Content-Type and Accept headers.
const (
	MediaTypeRDN  = "application/rdn"
	MediaTypeJSON = "application/json"
)

// Format identifies the wire format for an HTTP exchange.
type Format int

const (
	FormatRDN  Format = iota // application/rdn
	FormatJSON               // application/json
)

// String returns the media type string for the format.
func (f Format) String() string {
	switch f {
	case FormatJSON:
		return MediaTypeJSON
	default:
		return MediaTypeRDN
	}
}

// defaultMaxBodySize is the default maximum request body size (10 MB).
const defaultMaxBodySize int64 = 10 * 1024 * 1024

// Options configures the behaviour of HTTP handlers and middleware.
type Options struct {
	// JSONFallback allows responding with JSON when the client does not accept
	// application/rdn. Only the JSON-compatible subset of RDN values can be
	// serialized to JSON; extended types (BigInt, DateTime, RegExp, etc.) will
	// produce an error. Default: true.
	JSONFallback bool

	// Indent sets the indentation string for pretty-printed responses.
	// An empty string produces compact output (the default).
	Indent string

	// Prefix is prepended to each indented line (see StringifyIndent).
	Prefix string

	// MaxBodySize limits the number of bytes read from a request body.
	// Zero means use the default (10 MB). Negative means no limit.
	MaxBodySize int64
}

// DefaultOptions returns Options with sensible defaults.
func DefaultOptions() Options {
	return Options{JSONFallback: true}
}

func (o Options) maxBody() int64 {
	if o.MaxBodySize < 0 {
		return -1
	}
	if o.MaxBodySize == 0 {
		return defaultMaxBodySize
	}
	return o.MaxBodySize
}
