# Task 001: Establish baseline benchmarks

## Status: pending

## Tier: Setup

## Description
Create a comprehensive pytest-benchmark test file that covers all payload categories (small/medium/large JSON and medium/large RDN payloads) for both parse and stringify operations. Add `pytest-benchmark` to the dev dependencies. Run the benchmarks and record the baseline numbers as a JSON artifact. This provides the baseline against which all three tiers of optimization are measured.

## Files to Modify
- `packages/rdn-python/tests/test_benchmark.py` — new file with pytest-benchmark tests
- `packages/rdn-python/pyproject.toml` — add `pytest-benchmark` dev dependency

## Implementation Details
The benchmark tests should cover the same payload categories used in the existing `bench.py` file:

**Benchmarked operations** (using `pytest-benchmark`):
- Parse: small JSON, medium JSON, large JSON, medium RDN, large RDN
- Stringify: small object, medium object, large object, medium RDN object, large RDN object
- Micro: string-heavy parse, number-heavy parse, nested-object parse

**Fixture data**: Reuse the same fixtures from `bench.py`.

**CI configuration**: Add `pytest-benchmark` to test dependencies. In CI, run benchmarks with `--benchmark-only --benchmark-json=benchmark.json`. Store the JSON artifact. Set minimum threshold via `--benchmark-min-rounds=100`.

For regression detection, use `pytest-benchmark compare` against a stored baseline. Initially, no hard threshold -- just track and report. After Tier 3 is complete, establish minimum ops/sec thresholds based on the final numbers.

## Dependencies
- Depends on: none
- Blocks: 2, 3, 4, 5, 6

## Acceptance Criteria
- [ ] `pytest -k benchmark --benchmark-only` runs successfully
- [ ] Produces JSON output artifact
- [ ] Covers parse + stringify for small/medium/large JSON payloads
- [ ] Covers parse + stringify for medium/large RDN payloads
- [ ] `pytest-benchmark` is listed as a dev dependency in `pyproject.toml`

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.4, Section 12 (Task 1)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
