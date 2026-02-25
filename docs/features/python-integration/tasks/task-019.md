# Task 19: Update monorepo documentation and CI

**Status:** completed
**Dependencies:** Tasks 16, 17, 18

## Description

Update the monorepo root documentation and CI workflow to include the Python packages. This is the final integration task that ensures the Python implementation is discoverable, documented, and tested in CI.

### CLAUDE.md Updates

Add Python build/test commands to the `CLAUDE.md` file's Build & Test Commands section:

```bash
### Python
cd packages/rdn-python && pip install -e . && pytest
cd packages/rdn-pydantic && pip install -e . && pytest
cd packages/rdn-fastapi && pip install -e . && pytest
```

Document the package relationships and the fact that `rdn` is zero-dependency while `rdn-pydantic` and `rdn-fastapi` have their own dependencies.

### README.md Updates

Add the Python implementation to the root `README.md`:
- List `rdn` (Python) in the implementations section alongside TypeScript, Rust, C#, Go
- Mention `rdn-pydantic` and `rdn-fastapi` as ecosystem packages
- Link to each package's README for detailed documentation

### CI Workflow Updates

Add a `python` job to `.github/workflows/ci.yml`:

```yaml
python:
  runs-on: ubuntu-latest
  strategy:
    matrix:
      python-version: ["3.10", "3.11", "3.12", "3.13"]
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-python@v5
      with:
        python-version: ${{ matrix.python-version }}
    - name: Install rdn
      run: pip install -e packages/rdn-python[dev]
    - name: Run rdn tests
      run: pytest packages/rdn-python/tests -v
    - name: Type check rdn
      run: mypy packages/rdn-python/src/rdn
    - name: Install rdn-pydantic
      run: pip install -e packages/rdn-pydantic[dev]
    - name: Run rdn-pydantic tests
      run: pytest packages/rdn-pydantic/tests -v
    - name: Install rdn-fastapi
      run: pip install -e packages/rdn-fastapi[dev]
    - name: Run rdn-fastapi tests
      run: pytest packages/rdn-fastapi/tests -v
```

The CI should:
- Test on Python 3.10, 3.11, 3.12, and 3.13
- Install and test each package in dependency order
- Run mypy type checking on the core `rdn` package
- Run all conformance tests

## Files to Create/Modify
- `CLAUDE.md` (modify)
- `README.md` (modify)
- `.github/workflows/ci.yml` (modify)

## Acceptance Criteria
- `CLAUDE.md` documents Python build/test commands in the Build & Test section
- `README.md` lists the Python implementation and ecosystem packages
- CI workflow has a `python` job with matrix for Python 3.10-3.13
- CI installs and tests all three Python packages
- CI runs mypy type checking on `rdn`
- CI runs conformance tests as part of `rdn` test suite
- CI workflow syntax is valid YAML

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 19
- Tech Design: Section 9.4 (CI Configuration -- full workflow YAML)
- Current CLAUDE.md: `CLAUDE.md` (existing build commands to update)
- Current CI: `.github/workflows/ci.yml` (existing workflow to extend)
- Discovery: `docs/features/python-integration/discovery.md`
