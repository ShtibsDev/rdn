# Task 007: Run post-Tier-1 benchmarks

## Status: pending

## Tier: Tier 1: Build & Low-Hanging Fruit

## Description
Run the pytest-benchmark suite and `bench.py` after all Tier 1 optimizations are complete. Record results and compare against the Task 1 baseline. Document the per-optimization improvement. This checkpoint validates that the build profile, itoa/ryu formatting, hot/cold annotations, and empty collection fast-paths are delivering measurable gains before proceeding to Tier 2.

## Files to Modify
- None (benchmark run only; results should be saved as artifacts)

## Implementation Details
1. Run the pytest-benchmark suite: `pytest -k benchmark --benchmark-only --benchmark-json=benchmark-tier1.json`
2. Run `bench.py` for additional comparison data
3. Compare results against the Task 1 baseline JSON artifact
4. Document improvements per category:
   - Parse: small/medium/large JSON, medium/large RDN
   - Stringify: small/medium/large objects, medium/large RDN objects
5. Note which optimizations had the most impact (likely ryu for stringify, release profile for overall)

**Expected gains**: The release profile alone should provide 10-20% improvement. itoa/ryu should add another 5-15% for numeric-heavy payloads. Hot/cold and empty collection fast-paths are lower impact but contribute to overall improvement.

## Dependencies
- Depends on: 2, 3, 4, 5, 6
- Blocks: 8, 10

## Acceptance Criteria
- [ ] Benchmark results are recorded as JSON artifact
- [ ] Results are compared against Task 1 baseline
- [ ] Per-category improvement (or lack thereof) is documented
- [ ] All tests still pass after Tier 1 changes

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 12 (Task 7)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
