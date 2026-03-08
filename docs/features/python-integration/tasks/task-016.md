# Task 16: Update __init__.py exports and README

**Status:** pending
**Dependencies:** Tasks 14, 15

## Description

Finalize the public API surface in `__init__.py` with proper `__all__` exports and all re-exports. Update the package README with installation, usage examples, API reference, and type mapping documentation.

### Public API Exports

The `__init__.py` must export:

```python
__all__ = [
    # Top-level functions
    "loads", "dumps", "load", "dump",
    # Classes
    "RDNDecoder", "RDNEncoder",
    # Exceptions
    "RDNDecodeError",
    # Constants
    "MAX_SAFE_INTEGER",
]
```

All exports should be importable directly from `rdn`:
```python
from rdn import loads, dumps, load, dump, RDNDecoder, RDNEncoder, RDNDecodeError
```

### Native Extension Fallback Stub

Include the Phase 2 native extension fallback mechanism (non-functional stub for now):

```python
try:
    from rdn._native import parse as _native_parse, stringify as _native_stringify
    _USE_NATIVE = True
except ImportError:
    _USE_NATIVE = False
```

This ensures the import structure is ready for the future Rust/maturin C extension (Phase 2, separate tech design).

### README Content

The README should include:
1. **Overview**: What RDN is, what this package does
2. **Installation**: `pip install rdn`
3. **Quick Start**: Basic `loads()`/`dumps()` examples
4. **Type Mapping Table**: Full table of RDN types to Python types (from Section 3.2)
5. **API Reference**: All function signatures with parameter descriptions
6. **Parse Hooks**: Examples for each hook (`parse_float`, `parse_bigint`, `parse_datetime`, etc.)
7. **Class-Based API**: `RDNDecoder`/`RDNEncoder` usage and subclassing
8. **File I/O**: `load()`/`dump()` examples
9. **Error Handling**: `RDNDecodeError` usage

## Files to Create/Modify
- `packages/rdn-python/src/rdn/__init__.py` (modify)
- `packages/rdn-python/README.md` (rewrite)

## Acceptance Criteria
- `from rdn import loads, dumps, load, dump, RDNDecoder, RDNEncoder, RDNDecodeError` all work
- `from rdn import MAX_SAFE_INTEGER` works
- `rdn.__all__` is defined and contains all public exports
- `dir(rdn)` includes all expected names
- README has complete examples for all major features
- README type mapping table matches the tech design
- README documents all function parameters

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 16
- Tech Design: Section 3.1 (Public API Surface -- full function signatures)
- Tech Design: Section 3.2 (Type Mapping table)
- Tech Design: Section 3.7 (C Extension Strategy -- fallback mechanism)
- Discovery: `docs/features/python-integration/discovery.md`
