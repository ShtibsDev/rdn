package rdnhttp

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"

	rdn "github.com/ShtibsDev/rdn/packages/rdn-go"
)

type contextKey struct{}

// ReadRequest reads and parses the request body into an rdn.Value. It respects
// the MaxBodySize option and handles both application/rdn and application/json
// content types identically (since RDN is a JSON superset).
func ReadRequest(r *http.Request, v *rdn.Value, opts ...Options) error {
	o := mergeOpts(opts)
	body := limitBody(r, o)
	data, err := io.ReadAll(body)
	if err != nil {
		return fmt.Errorf("rdnhttp: read body: %w", err)
	}
	val, err := rdn.Parse(data)
	if err != nil {
		return fmt.Errorf("rdnhttp: parse body: %w", err)
	}
	*v = val
	return nil
}

// WriteResponse negotiates the response format from the request's Accept header
// and writes the value to w with the appropriate Content-Type.
func WriteResponse(w http.ResponseWriter, r *http.Request, v rdn.Value, opts ...Options) error {
	o := mergeOpts(opts)
	format := NegotiateFormat(r, o)
	return writeFormat(w, v, format, o)
}

// Negotiate returns HTTP middleware that performs content-type negotiation and
// stores the result in the request context. Downstream handlers can retrieve
// the format with FormatFromContext.
func Negotiate(next http.Handler, opts ...Options) http.Handler {
	o := mergeOpts(opts)
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		format := NegotiateFormat(r, o)
		ctx := context.WithValue(r.Context(), contextKey{}, format)
		next.ServeHTTP(w, r.WithContext(ctx))
	})
}

// NegotiateFunc returns a middleware constructor compatible with common router
// middleware chains (func(http.Handler) http.Handler).
func NegotiateFunc(opts ...Options) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return Negotiate(next, opts...)
	}
}

// FormatFromContext retrieves the negotiated Format stored by the Negotiate
// middleware. Returns FormatRDN if the context has no negotiated value.
func FormatFromContext(ctx context.Context) Format {
	if f, ok := ctx.Value(contextKey{}).(Format); ok {
		return f
	}
	return FormatRDN
}

// HandlerFunc is the signature for HandleRDN callbacks. It receives the parsed
// request value and returns the response value.
type HandlerFunc func(r *http.Request, v rdn.Value) (rdn.Value, error)

// HandleRDN returns an http.Handler that reads an RDN (or JSON) request body,
// passes it to fn, and writes the response in the negotiated format.
func HandleRDN(fn HandlerFunc, opts ...Options) http.Handler {
	o := mergeOpts(opts)
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		var reqVal rdn.Value
		if r.Body != nil && r.ContentLength != 0 {
			if err := ReadRequest(r, &reqVal, o); err != nil {
				http.Error(w, err.Error(), http.StatusBadRequest)
				return
			}
		}
		respVal, err := fn(r, reqVal)
		if err != nil {
			http.Error(w, err.Error(), http.StatusInternalServerError)
			return
		}
		format := NegotiateFormat(r, o)
		if err := writeFormat(w, respVal, format, o); err != nil {
			http.Error(w, err.Error(), http.StatusInternalServerError)
		}
	})
}

// ── internal helpers ────────────────────────────────────────────────────

func mergeOpts(opts []Options) Options {
	if len(opts) > 0 {
		return opts[0]
	}
	return DefaultOptions()
}

func limitBody(r *http.Request, o Options) io.Reader {
	max := o.maxBody()
	if max < 0 {
		return r.Body
	}
	return io.LimitReader(r.Body, max+1) // +1 to detect overflow
}

func writeFormat(w http.ResponseWriter, v rdn.Value, format Format, o Options) error {
	switch format {
	case FormatJSON:
		native, err := valueToNative(v)
		if err != nil {
			return fmt.Errorf("rdnhttp: json fallback: %w", err)
		}
		w.Header().Set("Content-Type", MediaTypeJSON)
		enc := json.NewEncoder(w)
		if o.Indent != "" {
			enc.SetIndent(o.Prefix, o.Indent)
		}
		return enc.Encode(native)
	default:
		w.Header().Set("Content-Type", MediaTypeRDN)
		enc := rdn.NewEncoder(w)
		if o.Indent != "" {
			enc.SetIndent(o.Prefix, o.Indent)
		}
		return enc.Encode(v)
	}
}

// valueToNative converts an rdn.Value to native Go types suitable for
// encoding/json. Only the JSON-compatible subset is supported; extended types
// (BigInt, DateTime, TimeOnly, Duration, RegExp, Binary, Map, Set, Tuple)
// return an error.
func valueToNative(v rdn.Value) (any, error) {
	switch v.Kind() {
	case rdn.KindNull:
		return nil, nil
	case rdn.KindBool:
		return v.BoolVal(), nil
	case rdn.KindNumber:
		if v.IsNaN() || v.IsInf() {
			return nil, fmt.Errorf("cannot represent %v in JSON", v)
		}
		return v.Float64(), nil
	case rdn.KindString:
		return v.Str(), nil
	case rdn.KindArray:
		elems := v.Array()
		arr := make([]any, len(elems))
		for i, elem := range elems {
			val, err := valueToNative(elem)
			if err != nil {
				return nil, err
			}
			arr[i] = val
		}
		return arr, nil
	case rdn.KindObject:
		pairs := v.Object()
		m := make(map[string]any, len(pairs))
		for _, kv := range pairs {
			val, err := valueToNative(kv.Value)
			if err != nil {
				return nil, err
			}
			m[kv.Key] = val
		}
		return m, nil
	default:
		return nil, fmt.Errorf("rdn type %v has no JSON representation", v.Kind())
	}
}
