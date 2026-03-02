package rdn

import (
	"encoding/json"
	"math"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

const testSuiteDir = "../../test-suite"

// valueToJSON converts an rdn.Value to a Go any suitable for JSON comparison.
// Extended types use the $type tagged convention from the conformance suite.
func valueToJSON(v Value) any {
	switch v.kind {
	case KindNull:
		return nil
	case KindBool:
		return v.boolean
	case KindNumber:
		if math.IsNaN(v.num) {
			return map[string]any{"$type": "Number", "value": "NaN"}
		}
		if math.IsInf(v.num, 1) {
			return map[string]any{"$type": "Number", "value": "Infinity"}
		}
		if math.IsInf(v.num, -1) {
			return map[string]any{"$type": "Number", "value": "-Infinity"}
		}
		// Return as float64 (JSON number)
		return v.num
	case KindBigInt:
		return map[string]any{"$type": "BigInt", "value": v.str}
	case KindString:
		return v.str
	case KindArray:
		elems := v.Array()
		arr := make([]any, len(elems))
		for i, elem := range elems {
			arr[i] = valueToJSON(elem)
		}
		return arr
	case KindObject:
		pairs := v.Object()
		m := make(map[string]any, len(pairs))
		for _, kv := range pairs {
			m[kv.Key] = valueToJSON(kv.Value)
		}
		return m
	case KindDateTime:
		t := v.Time().UTC()
		return map[string]any{"$type": "Date", "value": t.Format("2006-01-02T15:04:05.000Z")}
	case KindTimeOnly:
		to := v.TimeOnlyValue()
		return map[string]any{
			"$type": "TimeOnly",
			"value": map[string]any{
				"hours": float64(to.Hours), "minutes": float64(to.Minutes),
				"seconds": float64(to.Seconds), "milliseconds": float64(to.Milliseconds),
			},
		}
	case KindDuration:
		return map[string]any{"$type": "Duration", "value": v.str}
	case KindRegExp:
		re := v.RegExpValue()
		return map[string]any{
			"$type": "RegExp",
			"value": map[string]any{"source": re.Source, "flags": re.Flags},
		}
	case KindBinary:
		// Encode as base64 for comparison
		enc := newEncoder("", "")
		defer putEncodeState(enc.buf)
		enc.encodeBase64(v.Bytes())
		raw := enc.buf.String()
		// Strip the b"..." wrapper
		b64str := raw[2 : len(raw)-1]
		return map[string]any{"$type": "Binary", "value": b64str}
	case KindMap:
		mapEntries := v.Map()
		entries := make([]any, len(mapEntries))
		for i, entry := range mapEntries {
			entries[i] = []any{valueToJSON(entry.Key), valueToJSON(entry.Value)}
		}
		return map[string]any{"$type": "Map", "value": entries}
	case KindSet:
		elems := v.Array()
		items := make([]any, len(elems))
		for i, elem := range elems {
			items[i] = valueToJSON(elem)
		}
		return map[string]any{"$type": "Set", "value": items}
	case KindTuple:
		elems := v.Array()
		arr := make([]any, len(elems))
		for i, elem := range elems {
			arr[i] = valueToJSON(elem)
		}
		return arr
	default:
		return nil
	}
}

func TestConformanceValid(t *testing.T) {
	validDir := filepath.Join(testSuiteDir, "valid")
	entries, err := os.ReadDir(validDir)
	if err != nil {
		t.Fatalf("Cannot read valid test directory: %v", err)
	}

	for _, entry := range entries {
		name := entry.Name()
		if !strings.HasSuffix(name, ".rdn") {
			continue
		}
		baseName := strings.TrimSuffix(name, ".rdn")
		t.Run(baseName, func(t *testing.T) {
			rdnPath := filepath.Join(validDir, name)
			expectedPath := filepath.Join(validDir, baseName+".expected.json")

			rdnData, err := os.ReadFile(rdnPath)
			if err != nil {
				t.Fatalf("Cannot read RDN file: %v", err)
			}
			expectedData, err := os.ReadFile(expectedPath)
			if err != nil {
				t.Fatalf("Cannot read expected JSON file: %v", err)
			}

			// Parse RDN
			val, err := Parse(rdnData)
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			// Convert to JSON-compatible structure
			got := valueToJSON(val)

			// Parse expected JSON
			var expected any
			if err := json.Unmarshal(expectedData, &expected); err != nil {
				t.Fatalf("Cannot parse expected JSON: %v", err)
			}

			// Compare by re-serializing both to JSON
			gotJSON, err := json.Marshal(got)
			if err != nil {
				t.Fatalf("Cannot marshal got: %v", err)
			}
			expectedJSON, err := json.Marshal(expected)
			if err != nil {
				t.Fatalf("Cannot marshal expected: %v", err)
			}

			if string(gotJSON) != string(expectedJSON) {
				t.Errorf("Mismatch:\n  got:      %s\n  expected: %s", gotJSON, expectedJSON)
			}
		})
	}
}

func TestConformanceInvalid(t *testing.T) {
	invalidDir := filepath.Join(testSuiteDir, "invalid")
	entries, err := os.ReadDir(invalidDir)
	if err != nil {
		t.Fatalf("Cannot read invalid test directory: %v", err)
	}

	for _, entry := range entries {
		name := entry.Name()
		if !strings.HasSuffix(name, ".rdn") {
			continue
		}
		baseName := strings.TrimSuffix(name, ".rdn")
		t.Run(baseName, func(t *testing.T) {
			rdnPath := filepath.Join(invalidDir, name)
			data, err := os.ReadFile(rdnPath)
			if err != nil {
				t.Fatalf("Cannot read file: %v", err)
			}

			_, err = Parse(data)
			if err == nil {
				t.Errorf("Expected parse error for %s but got success", name)
			}
		})
	}
}

func TestConformanceRoundtrip(t *testing.T) {
	roundtripDir := filepath.Join(testSuiteDir, "roundtrip")
	entries, err := os.ReadDir(roundtripDir)
	if err != nil {
		t.Fatalf("Cannot read roundtrip test directory: %v", err)
	}

	for _, entry := range entries {
		name := entry.Name()
		if !strings.HasSuffix(name, ".rdn") {
			continue
		}
		baseName := strings.TrimSuffix(name, ".rdn")
		t.Run(baseName, func(t *testing.T) {
			rdnPath := filepath.Join(roundtripDir, name)
			data, err := os.ReadFile(rdnPath)
			if err != nil {
				t.Fatalf("Cannot read file: %v", err)
			}

			// Parse
			val1, err := Parse(data)
			if err != nil {
				t.Fatalf("First parse failed: %v", err)
			}

			// Stringify
			serialized, err := Stringify(val1)
			if err != nil {
				t.Fatalf("Stringify failed: %v", err)
			}

			// Parse again
			val2, err := Parse(serialized)
			if err != nil {
				t.Fatalf("Second parse failed: %v\n  serialized: %s", err, serialized)
			}

			// Compare
			if !val1.Equal(val2) {
				t.Errorf("Roundtrip mismatch:\n  original:   %s\n  serialized: %s", data, serialized)
			}
		})
	}
}
