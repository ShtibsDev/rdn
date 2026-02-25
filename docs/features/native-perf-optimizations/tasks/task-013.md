# Task 013: Run post-Tier-2 benchmarks

## Status: pending

## Tier: Tier 2: Type Dispatch & Caching

## Description
Run the pytest-benchmark suite and `bench.py` after all Tier 2 optimizations are complete. Record results and compare against the post-Tier-1 numbers and the original baseline. Document the per-optimization and cumulative improvement. This checkpoint validates that cached type pointers, key cache, and bit-packed state are delivering measurable gains before proceeding to Tier 3.

## Files to Modify
- None (benchmark run only; results should be saved as artifacts)

## Implementation Details
1. Run the pytest-benchmark suite: `pytest -k benchmark --benchmark-only --benchmark-json=benchmark-tier2.json`
2. Run `bench.py` for additional comparison data
3. Compare results against:
   - Task 1 baseline (original)
   - Task 7 post-Tier-1 numbers
4. Document improvements per category:
   - Parse: small/medium/large JSON, medium/large RDN (key cache should help here)
   - Stringify: small/medium/large objects, medium/large RDN objects (type cache should help here)
5. Note which optimizations had the most impact (likely type cache for stringify, key cache for parse)

**Expected gains**: Type cache should improve stringify by 5-15% (especially for payloads with many values). Key cache should improve parse by 5-10% (especially for payloads with repeated keys). Bit-packed state is a minor optimization.

## Dependencies
- Depends on: 9, 11, 12
- Blocks: 14, 20

## Acceptance Criteria
- [ ] Benchmark results are recorded as JSON artifact
- [ ] Results are compared against both Task 1 baseline and Task 7 post-Tier-1
- [ ] Per-category and cumulative improvement is documented
- [ ] All tests still pass after Tier 2 changes

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 12 (Task 13)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
