# Task 001: Add `py.typed` marker and `__version__` attribute

**References:** [discovery.md](../discovery.md) | [tech-design.md](../tech-design.md) §5.1, §5.2

## Objective

Add PEP 561 type marker and expose package version via `importlib.metadata`.

## Changes

### 1. Create `packages/rdn-python/src/rdn/py.typed`
- Empty file (zero bytes)
- Maturin auto-includes it in the wheel (under `python-source = "src"`)

### 2. Modify `packages/rdn-python/src/rdn/__init__.py`

Add after existing imports, before `__all__`:

```python
from importlib.metadata import version as _meta_version, PackageNotFoundError

try:
    __version__ = _meta_version("rdn")
except PackageNotFoundError:
    __version__ = "0.0.0-dev"
```

Add `"__version__"` to `__all__`.

### 3. Add tests: `packages/rdn-python/tests/test_package_meta.py`

```python
import pathlib
import rdn

def test_py_typed_exists():
    marker = pathlib.Path(rdn.__file__).parent / "py.typed"
    assert marker.exists()

def test_version_is_string():
    assert isinstance(rdn.__version__, str)
    assert len(rdn.__version__) > 0

def test_version_in_all():
    assert "__version__" in rdn.__all__
```

## Verification

```bash
cd packages/rdn-python && source ../../.venv/bin/activate
pytest tests/test_package_meta.py -v
python -c "import rdn; print(rdn.__version__)"
```
