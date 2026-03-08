package rdnhttp

import (
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	rdn "github.com/ShtibsDev/rdn/packages/rdn-go"
)

// ── NegotiateFormat ─────────────────────────────────────────────────────

func TestNegotiateFormat(t *testing.T) {
	tests := []struct {
		name   string
		accept string
		opts   Options
		want   Format
	}{
		{"rdn explicit", MediaTypeRDN, DefaultOptions(), FormatRDN},
		{"json explicit with fallback", MediaTypeJSON, DefaultOptions(), FormatJSON},
		{"json explicit no fallback", MediaTypeJSON, Options{JSONFallback: false}, FormatRDN},
		{"wildcard", "*/*", DefaultOptions(), FormatRDN},
		{"empty", "", DefaultOptions(), FormatRDN},
		{"both prefer rdn", MediaTypeRDN + ", " + MediaTypeJSON, DefaultOptions(), FormatRDN},
		{"json with quality", MediaTypeJSON + ";q=0.9", DefaultOptions(), FormatJSON},
		{"unknown type", "text/html", DefaultOptions(), FormatRDN},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			r := httptest.NewRequest("GET", "/", nil)
			if tt.accept != "" {
				r.Header.Set("Accept", tt.accept)
			}
			got := NegotiateFormat(r, tt.opts)
			if got != tt.want {
				t.Errorf("NegotiateFormat() = %v, want %v", got, tt.want)
			}
		})
	}
}

// ── DetectContentType ───────────────────────────────────────────────────

func TestDetectContentType(t *testing.T) {
	tests := []struct {
		name string
		ct   string
		want Format
	}{
		{"rdn", MediaTypeRDN, FormatRDN},
		{"json", MediaTypeJSON, FormatJSON},
		{"json with charset", MediaTypeJSON + "; charset=utf-8", FormatJSON},
		{"empty defaults to rdn", "", FormatRDN},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			r := httptest.NewRequest("POST", "/", nil)
			if tt.ct != "" {
				r.Header.Set("Content-Type", tt.ct)
			}
			got := DetectContentType(r)
			if got != tt.want {
				t.Errorf("DetectContentType() = %v, want %v", got, tt.want)
			}
		})
	}
}

// ── AcceptsRDN / IsRDNContentType ───────────────────────────────────────

func TestAcceptsRDN(t *testing.T) {
	r := httptest.NewRequest("GET", "/", nil)
	r.Header.Set("Accept", MediaTypeRDN)
	if !AcceptsRDN(r) {
		t.Error("expected AcceptsRDN to return true")
	}

	r2 := httptest.NewRequest("GET", "/", nil)
	r2.Header.Set("Accept", MediaTypeJSON)
	if AcceptsRDN(r2) {
		t.Error("expected AcceptsRDN to return false for JSON")
	}
}

func TestIsRDNContentType(t *testing.T) {
	r := httptest.NewRequest("POST", "/", nil)
	r.Header.Set("Content-Type", MediaTypeRDN)
	if !IsRDNContentType(r) {
		t.Error("expected IsRDNContentType to return true")
	}

	r2 := httptest.NewRequest("POST", "/", nil)
	r2.Header.Set("Content-Type", MediaTypeJSON)
	if IsRDNContentType(r2) {
		t.Error("expected IsRDNContentType to return false for JSON")
	}
}

// ── ReadRequest / WriteResponse roundtrip ───────────────────────────────

func TestRDNInRDNOut(t *testing.T) {
	body := `{"name":"Alice","age":30}`
	r := httptest.NewRequest("POST", "/", strings.NewReader(body))
	r.Header.Set("Content-Type", MediaTypeRDN)
	r.Header.Set("Accept", MediaTypeRDN)

	var v rdn.Value
	if err := ReadRequest(r, &v); err != nil {
		t.Fatalf("ReadRequest: %v", err)
	}
	if v.Kind() != rdn.KindObject {
		t.Fatalf("expected Object, got %v", v.Kind())
	}

	w := httptest.NewRecorder()
	if err := WriteResponse(w, r, v); err != nil {
		t.Fatalf("WriteResponse: %v", err)
	}

	resp := w.Result()
	if ct := resp.Header.Get("Content-Type"); ct != MediaTypeRDN {
		t.Errorf("Content-Type = %q, want %q", ct, MediaTypeRDN)
	}

	respBody, _ := io.ReadAll(resp.Body)
	// Parse the response back to verify roundtrip
	v2, err := rdn.Parse(respBody)
	if err != nil {
		t.Fatalf("failed to parse response: %v", err)
	}
	if !v.Equal(v2) {
		t.Errorf("roundtrip mismatch:\n  sent: %s\n  got:  %s", body, respBody)
	}
}

func TestJSONInRDNOut(t *testing.T) {
	body := `{"name":"Bob","active":true}`
	r := httptest.NewRequest("POST", "/", strings.NewReader(body))
	r.Header.Set("Content-Type", MediaTypeJSON)
	r.Header.Set("Accept", MediaTypeRDN)

	var v rdn.Value
	if err := ReadRequest(r, &v); err != nil {
		t.Fatalf("ReadRequest: %v", err)
	}
	if v.Kind() != rdn.KindObject {
		t.Fatalf("expected Object, got %v", v.Kind())
	}

	w := httptest.NewRecorder()
	if err := WriteResponse(w, r, v); err != nil {
		t.Fatalf("WriteResponse: %v", err)
	}
	if ct := w.Result().Header.Get("Content-Type"); ct != MediaTypeRDN {
		t.Errorf("Content-Type = %q, want %q", ct, MediaTypeRDN)
	}
}

func TestRDNInJSONOut(t *testing.T) {
	body := `{"name":"Carol","score":99}`
	r := httptest.NewRequest("POST", "/", strings.NewReader(body))
	r.Header.Set("Content-Type", MediaTypeRDN)
	r.Header.Set("Accept", MediaTypeJSON)

	var v rdn.Value
	if err := ReadRequest(r, &v); err != nil {
		t.Fatalf("ReadRequest: %v", err)
	}

	w := httptest.NewRecorder()
	if err := WriteResponse(w, r, v); err != nil {
		t.Fatalf("WriteResponse: %v", err)
	}
	if ct := w.Result().Header.Get("Content-Type"); ct != MediaTypeJSON {
		t.Errorf("Content-Type = %q, want %q", ct, MediaTypeJSON)
	}
}

func TestMissingHeaders(t *testing.T) {
	body := `{"key":"value"}`
	r := httptest.NewRequest("POST", "/", strings.NewReader(body))
	// No Content-Type or Accept headers

	var v rdn.Value
	if err := ReadRequest(r, &v); err != nil {
		t.Fatalf("ReadRequest: %v", err)
	}

	w := httptest.NewRecorder()
	if err := WriteResponse(w, r, v); err != nil {
		t.Fatalf("WriteResponse: %v", err)
	}
	// Defaults to RDN
	if ct := w.Result().Header.Get("Content-Type"); ct != MediaTypeRDN {
		t.Errorf("Content-Type = %q, want %q", ct, MediaTypeRDN)
	}
}

// ── Non-JSON-compatible value + JSON Accept → error ─────────────────────

func TestNonJSONCompatibleError(t *testing.T) {
	v := rdn.BigIntVal("12345678901234567890")

	r := httptest.NewRequest("GET", "/", nil)
	r.Header.Set("Accept", MediaTypeJSON)

	w := httptest.NewRecorder()
	err := WriteResponse(w, r, v)
	if err == nil {
		t.Fatal("expected error for BigInt with JSON format")
	}
	if !strings.Contains(err.Error(), "no JSON representation") {
		t.Errorf("unexpected error message: %v", err)
	}
}

func TestNonJSONCompatibleNaN(t *testing.T) {
	v := rdn.ObjectVal([]rdn.KeyValue{
		{Key: "val", Value: rdn.NumberVal(nanFloat())},
	})
	r := httptest.NewRequest("GET", "/", nil)
	r.Header.Set("Accept", MediaTypeJSON)

	w := httptest.NewRecorder()
	err := WriteResponse(w, r, v)
	if err == nil {
		t.Fatal("expected error for NaN with JSON format")
	}
}

func nanFloat() float64 {
	// Avoid constant-folding by going through a variable
	x := 0.0
	return x / x
}

// ── MaxBodySize ─────────────────────────────────────────────────────────

func TestMaxBodySize(t *testing.T) {
	largeBody := strings.Repeat("x", 100)
	r := httptest.NewRequest("POST", "/", strings.NewReader(largeBody))

	var v rdn.Value
	err := ReadRequest(r, &v, Options{MaxBodySize: 10})
	// The body is larger than the limit — ReadAll succeeds but Parse will fail
	// because the truncated data is invalid RDN.
	if err == nil {
		t.Fatal("expected error for oversized body")
	}
}

// ── Middleware context propagation ──────────────────────────────────────

func TestNegotiateMiddleware(t *testing.T) {
	var captured Format
	inner := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		captured = FormatFromContext(r.Context())
		w.WriteHeader(http.StatusOK)
	})

	handler := Negotiate(inner, Options{JSONFallback: true})

	r := httptest.NewRequest("GET", "/", nil)
	r.Header.Set("Accept", MediaTypeJSON)
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, r)

	if captured != FormatJSON {
		t.Errorf("FormatFromContext = %v, want FormatJSON", captured)
	}
}

func TestNegotiateMiddlewareDefaultRDN(t *testing.T) {
	var captured Format
	inner := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		captured = FormatFromContext(r.Context())
		w.WriteHeader(http.StatusOK)
	})

	handler := Negotiate(inner)

	r := httptest.NewRequest("GET", "/", nil)
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, r)

	if captured != FormatRDN {
		t.Errorf("FormatFromContext = %v, want FormatRDN", captured)
	}
}

func TestNegotiateFuncChain(t *testing.T) {
	var captured Format
	inner := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		captured = FormatFromContext(r.Context())
	})

	mw := NegotiateFunc(Options{JSONFallback: true})
	handler := mw(inner)

	r := httptest.NewRequest("GET", "/", nil)
	r.Header.Set("Accept", MediaTypeRDN)
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, r)

	if captured != FormatRDN {
		t.Errorf("FormatFromContext = %v, want FormatRDN", captured)
	}
}

func TestFormatFromContextDefault(t *testing.T) {
	// No middleware → should default to FormatRDN
	r := httptest.NewRequest("GET", "/", nil)
	got := FormatFromContext(r.Context())
	if got != FormatRDN {
		t.Errorf("FormatFromContext default = %v, want FormatRDN", got)
	}
}

// ── HandleRDN ───────────────────────────────────────────────────────────

func TestHandleRDNEcho(t *testing.T) {
	handler := HandleRDN(func(r *http.Request, v rdn.Value) (rdn.Value, error) {
		return v, nil // echo
	})

	body := `{"echo":true}`
	r := httptest.NewRequest("POST", "/", strings.NewReader(body))
	r.Header.Set("Content-Type", MediaTypeRDN)
	r.Header.Set("Accept", MediaTypeRDN)
	w := httptest.NewRecorder()

	handler.ServeHTTP(w, r)

	resp := w.Result()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("status = %d, want 200", resp.StatusCode)
	}
	if ct := resp.Header.Get("Content-Type"); ct != MediaTypeRDN {
		t.Errorf("Content-Type = %q, want %q", ct, MediaTypeRDN)
	}

	respBody, _ := io.ReadAll(resp.Body)
	v, err := rdn.Parse(respBody)
	if err != nil {
		t.Fatalf("parse response: %v", err)
	}
	if v.Kind() != rdn.KindObject {
		t.Errorf("expected Object, got %v", v.Kind())
	}
}

func TestHandleRDNNoBody(t *testing.T) {
	handler := HandleRDN(func(r *http.Request, v rdn.Value) (rdn.Value, error) {
		return rdn.StringVal("ok"), nil
	})

	r := httptest.NewRequest("GET", "/", nil)
	r.Header.Set("Accept", MediaTypeRDN)
	w := httptest.NewRecorder()

	handler.ServeHTTP(w, r)
	if w.Result().StatusCode != http.StatusOK {
		t.Fatalf("status = %d, want 200", w.Result().StatusCode)
	}
}

func TestHandleRDNBadBody(t *testing.T) {
	handler := HandleRDN(func(r *http.Request, v rdn.Value) (rdn.Value, error) {
		return v, nil
	})

	r := httptest.NewRequest("POST", "/", strings.NewReader("{invalid"))
	r.Header.Set("Content-Type", MediaTypeRDN)
	w := httptest.NewRecorder()

	handler.ServeHTTP(w, r)
	if w.Result().StatusCode != http.StatusBadRequest {
		t.Errorf("status = %d, want 400", w.Result().StatusCode)
	}
}

// ── Format.String ───────────────────────────────────────────────────────

func TestFormatString(t *testing.T) {
	if s := FormatRDN.String(); s != MediaTypeRDN {
		t.Errorf("FormatRDN.String() = %q, want %q", s, MediaTypeRDN)
	}
	if s := FormatJSON.String(); s != MediaTypeJSON {
		t.Errorf("FormatJSON.String() = %q, want %q", s, MediaTypeJSON)
	}
}
