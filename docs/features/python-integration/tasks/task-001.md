# Task 1: Create rdn-python package scaffolding

**Status:** pending
**Dependencies:** None

## Description

Set up the package directory structure, `pyproject.toml`, and empty module files for the core `rdn` Python package. This task establishes the foundational project layout that all subsequent tasks build upon.

The package follows the monorepo convention with the source under `packages/rdn-python/`. It uses a `src/rdn/` layout so the package is published as `rdn` on PyPI. The `pyproject.toml` should configure:
- Package metadata (name `rdn`, version `0.1.0`, Python >= 3.10)
- Zero runtime dependencies (pure Python, stdlib only)
- Build system: setuptools
- Tool configs for pytest, mypy (strict), and ruff

All module files should be created as empty placeholders (or with minimal docstrings) so that the package is installable and `import rdn` works.

The package structure must match the layout defined in the tech design Section 2.3:

```
packages/rdn-python/
  pyproject.toml
  README.md
  src/
    rdn/
      __init__.py          # Public API (empty for now)
      _parser.py           # Parser (empty)
      _serializer.py       # Serializer (empty)
      _tables.py           # Lookup tables (empty)
      decoder.py           # RDNDecoder class (empty)
      encoder.py           # RDNEncoder class (empty)
      exceptions.py        # RDNDecodeError (empty)
  tests/
    __init__.py
```

## Files to Create/Modify
- `packages/rdn-python/pyproject.toml` (rewrite)
- `packages/rdn-python/src/rdn/__init__.py` (create)
- `packages/rdn-python/src/rdn/exceptions.py` (create)
- `packages/rdn-python/src/rdn/_tables.py` (create)
- `packages/rdn-python/src/rdn/_parser.py` (create)
- `packages/rdn-python/src/rdn/_serializer.py` (create)
- `packages/rdn-python/src/rdn/decoder.py` (create)
- `packages/rdn-python/src/rdn/encoder.py` (create)
- `packages/rdn-python/tests/__init__.py` (create)

## Acceptance Criteria
- `pip install -e packages/rdn-python` succeeds
- `import rdn` works (empty module)
- `pyproject.toml` has correct metadata, build-system, and tool configs

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 1
- Tech Design: Section 2.3 (Package Structure for rdn-python)
- Tech Design: Section 9.1 (pyproject.toml configuration)
- Discovery: `docs/features/python-integration/discovery.md`
