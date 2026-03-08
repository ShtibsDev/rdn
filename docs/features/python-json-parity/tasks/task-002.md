# Task 002: Add `parse`/`stringify` aliases

**References:** [discovery.md](../discovery.md) | [tech-design.md](../tech-design.md) §5.3

## Objective

Export `parse` and `stringify` as public aliases of `loads` and `dumps` for cross-implementation consistency with TypeScript/Rust RDN packages.

## Changes

### 1. Modify `packages/rdn-python/src/rdn/__init__.py`

After the `load()` function definition (end of file), add:

```python
parse = loads
stringify = dumps
```

Update `__all__` to include `"parse"` and `"stringify"`.

### 2. Add tests: `packages/rdn-python/tests/test_aliases.py`

```python
import rdn

def test_parse_is_loads():
    assert rdn.parse is rdn.loads

def test_stringify_is_dumps():
    assert rdn.stringify is rdn.dumps

def test_aliases_in_all():
    assert "parse" in rdn.__all__
    assert "stringify" in rdn.__all__

def test_parse_works():
    assert rdn.parse('{"a":1}') == {"a": 1}

def test_stringify_works():
    result = rdn.stringify({"a": 1})
    assert '"a"' in result
```

## Verification

```bash
cd packages/rdn-python && pytest tests/test_aliases.py -v
```
