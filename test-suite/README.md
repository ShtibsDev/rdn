# RDN Conformance Test Suite

Language-agnostic tests for RDN parser/serializer implementations.

## Structure

- `valid/` — Valid RDN files with expected parse output
  - `*.rdn` — Input RDN document
  - `*.expected.json` — Expected parse result as JSON
- `invalid/` — Files that must cause a parse error
  - `*.rdn` — Invalid RDN documents
- `roundtrip/` — Parse → serialize → parse identity tests
  - `*.rdn` — Documents that should survive a roundtrip

## Extended Type Convention

Since JSON cannot represent RDN extended types natively, expected outputs use a tagged convention:

```json
{"$type": "TypeName", "value": ...}
```

| RDN Type | `$type` | `value` format |
|----------|---------|----------------|
| Date | `"Date"` | ISO 8601 string |
| BigInt | `"BigInt"` | String of digits |
| RegExp | `"RegExp"` | `{"source": "...", "flags": "..."}` |
| Binary | `"Binary"` | Base64 string |
| Map | `"Map"` | Array of `[key, value]` pairs |
| Set | `"Set"` | Array of values |
| NaN | `"Number"` | `"NaN"` |
| Infinity | `"Number"` | `"Infinity"` or `"-Infinity"` |
| TimeOnly | `"TimeOnly"` | `{"hours", "minutes", "seconds", "milliseconds"}` |
| Duration | `"Duration"` | ISO 8601 duration string |
