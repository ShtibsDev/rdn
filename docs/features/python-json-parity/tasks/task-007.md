# Task 007: Update documentation

**References:** [discovery.md](../discovery.md) | [tech-design.md](../tech-design.md) §8.7

## Objective

Update package documentation to reflect the new API surface.

## Changes

### 1. Update `packages/rdn-python/README.md`

Add/update sections for:
- **New parameters:** `skipkeys` and `allow_nan` in `dumps()`/`dump()`/`RDNEncoder`
- **Aliases:** `rdn.parse` = `rdn.loads`, `rdn.stringify` = `rdn.dumps`
- **Version:** `rdn.__version__`
- **CLI usage:** `python -m rdn` with examples
- **Type checking:** Mention PEP 561 `py.typed` support

### 2. Update `CLAUDE.md` (root)

Update the Python section under "### Python" to mention:
- `skipkeys` and `allow_nan` parameters
- CLI tool (`python -m rdn`)
- `py.typed` marker

## Verification

- Read through updated docs for accuracy
- Verify all code examples work
