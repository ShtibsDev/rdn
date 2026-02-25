# Task 022: Run post-Tier-3 benchmarks

## Status: done (2026-02-25)

## Tier: Tier 3: SIMD & Buffer

## Description
Run the pytest-benchmark suite and `bench.py` after all Tier 3 optimizations are complete. Record results and compare against the post-Tier-2 numbers and the original baseline. Document the per-tier and cumulative improvement. Verify the 30% improvement target is met on medium/large payloads for both parse and stringify.

## Files to Modify
- None (benchmark run only; results should be saved as artifacts)

## Implementation Details
1. Run the pytest-benchmark suite: `pytest -k benchmark --benchmark-only --benchmark-json=benchmark-tier3.json`
2. Run `bench.py` for additional comparison data
3. Compare results against:
   - Task 1 baseline (original)
   - Task 7 post-Tier-1 numbers
   - Task 13 post-Tier-2 numbers
4. Document improvements per category:
   - Parse: small/medium/large JSON, medium/large RDN
   - Stringify: small/medium/large objects, medium/large RDN objects
5. Create a summary table showing cumulative improvement at each tier
6. Verify the 30% improvement target:
   - Target: >= 30% improvement on medium/large payloads for both parse and stringify
   - If target is not met, document which optimizations had less impact than expected and propose follow-up work

**Expected gains from Tier 3**:
- SIMD string scanning should improve parse performance on string-heavy payloads by 20-40%
- WriteBuffer should improve stringify performance across all payload sizes by 15-25%
- Stack buffers for formatting are a smaller win, mainly for datetime/duration-heavy payloads
- Cumulative across all tiers should exceed 30% for medium/large payloads

## Dependencies
- Depends on: 21
- Blocks: 23

## Acceptance Criteria
- [ ] Benchmark results are recorded as JSON artifact
- [ ] Results compared against all previous checkpoints (baseline, Tier 1, Tier 2)
- [ ] Per-tier and cumulative improvement is documented in a summary table
- [ ] 30% improvement target assessed for medium/large payloads
- [ ] All tests still pass after Tier 3 changes

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 12 (Task 22)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
