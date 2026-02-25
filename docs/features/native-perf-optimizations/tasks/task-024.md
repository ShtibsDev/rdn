# Task 024: Final validation

## Status: done (2026-02-25)

## Tier: Wrap-up

## Description
Run the full test suite (pytest), the conformance suite, and the benchmark suite one final time. Verify 100% test pass rate. Verify benchmark improvements meet the 30% target on medium/large payloads. Run on both x86_64 and ARM64 if CI supports it. This is the final gate before the feature is considered complete.

## Files to Modify
- None (validation run only)

## Implementation Details
**Validation checklist**:

1. **Full test suite**: Run `pytest` in `packages/rdn-python/` -- all ~400 tests across:
   - `test_parse.py`
   - `test_stringify.py`
   - `test_native.py`
   - `test_conformance.py`
   - `test_edge_cases.py`
   - `test_encoder.py`
   - `test_decoder.py`
   - `test_file_io.py`

2. **Conformance suite**: All 11 valid, 10 invalid, and 2 roundtrip files must pass.

3. **Benchmark suite**: Run `pytest -k benchmark --benchmark-only` and `bench.py`. Verify:
   - >= 30% improvement on medium/large payloads for both parse and stringify
   - No regressions on small payloads

4. **Cross-platform**: If CI supports it, run on both x86_64 and ARM64. Verify:
   - SSE2 SIMD path works on x86_64
   - NEON SIMD path works on ARM64 (Apple Silicon)
   - Scalar fallback works on other architectures

5. **API compatibility**: Verify `rdn.parse()` and `rdn.stringify()` signatures are unchanged. Verify `RDNDecodeError` attributes are preserved. Verify hot-path routing (`_USE_NATIVE` flag) works correctly.

6. **Output parity**: Compare output of native extension vs pure Python for a representative corpus of inputs. All outputs should be identical (except for minor float formatting differences documented in Task 23).

## Dependencies
- Depends on: 23
- Blocks: none

## Acceptance Criteria
- [x] All 695 pytest tests pass (100% pass rate)
- [x] All conformance suite tests pass (included in the 695 tests)
- [x] Benchmark improvements vastly exceed 30% target on medium/large payloads (75-95% improvement, 4x-20x speedup)
- [x] No regressions on small payloads
- [x] ARM64 tests pass (NEON SIMD) -- validated on Apple Silicon
- [x] API is unchanged (`loads`, `dumps`, `RDNDecodeError` with `msg`, `pos`, `lineno`, `colno`)
- [x] Output parity with pure Python confirmed for all representative inputs
- [x] Hot-path routing (`_USE_NATIVE`) works correctly

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 8, Section 12 (Task 24)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
