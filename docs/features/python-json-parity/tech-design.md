# Tech Design: `python-json-parity`

## 1. Overview

This feature closes six API gaps between the `rdn` Python package and Python's built-in `json` module, making `rdn` a complete drop-in replacement. The six items are:

1. **`py.typed`** marker file (PEP 561)
2. **`__version__`** attribute
3. **`parse`/`stringify`** public aliases
4. **`skipkeys`** parameter on `dumps()`/`dump()`/`RDNEncoder`
5. **`allow_nan`** parameter on `dumps()`/`dump()`/`RDNEncoder`
6. **CLI tool** (`python -m rdn`)

All changes are additive. Default parameter values preserve existing behavior, so this is fully backward-compatible.

---

## 2. As-Is vs To-Be

| Item | As-Is | To-Be |
|------|-------|-------|
| `py.typed` | Missing. Type checkers cannot verify `rdn` types in downstream projects. | Empty `src/rdn/py.typed` marker file included in wheel. |
| `__version__` | Not exposed. `rdn.__version__` raises `AttributeError`. | `rdn.__version__` returns the package version string (e.g. `"0.1.0"`), sourced from `importlib.metadata`. |
| `parse`/`stringify` | Not exported. Only `loads`/`dumps` available. | `rdn.parse` aliases `rdn.loads`, `rdn.stringify` aliases `rdn.dumps`. Both in `__all__`. |
| `skipkeys` | Non-string dict keys always raise `TypeError`. No `skipkeys` parameter exists. | `dumps(obj, skipkeys=True)` silently skips non-string keys (matching `json.dumps`). Default `False` preserves current error behavior. Implemented in both pure Python and Rust native extension. |
| `allow_nan` | `NaN`/`Infinity` are always serialized (native RDN types). No `allow_nan` parameter. | `dumps(obj, allow_nan=False)` raises `ValueError` on `NaN`/`Infinity` (matching `json.dumps`). Default `True` preserves current behavior. Implemented in both pure Python and Rust native extension. |
| CLI tool | `python -m rdn` fails with `No module named rdn.__main__`. | `python -m rdn` validates and pretty-prints RDN, matching `python -m json.tool` behavior. |

---

## 3. Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| `skipkeys` semantics | Silently skip non-string keys | Match `json.dumps(skipkeys=True)` exactly. |
| `allow_nan` default | `True` | RDN natively supports NaN/Infinity, so the default should allow them. This differs from `json` (default `True` there too, but `json` emits invalid JSON). |
| `allow_nan=False` error type | `ValueError` | Match `json.dumps` which raises `ValueError("Out of range float values are not JSON compliant")`. |
| `parse`/`stringify` aliases | Function-level aliases in `__init__.py` | Simple assignment (`parse = loads`), no wrapper functions. Added to `__all__`. |
| CLI scope | Validate + pretty-print only | Match `json.tool`. No transform, query, or schema features. |
| `__version__` source | `importlib.metadata.version("rdn")` | Single source of truth from `pyproject.toml`. Works for installed and editable installs. Fallback to `"0.0.0-dev"` for uninstalled development. |
| `py.typed` | Empty file at `src/rdn/py.typed` | PEP 561 standard. Maturin auto-includes it in the wheel since it is under `python-source = "src"`. |
| Native extension parity | Implement `skipkeys` and `allow_nan` in Rust too | Avoids silently falling back to pure Python when these parameters are used, maintaining consistent performance. |

---

## 4. Interfaces & Signatures

### 4.1 `dumps()` — Before

```python
def dumps(obj: Any, *, cls: type | None = None, ensure_ascii: bool = True,
          check_circular: bool = True, indent: int | str | None = None,
          separators: tuple[str, str] | None = None,
          default: Callable[[Any], Any] | None = None,
          sort_keys: bool = False) -> str:
```

### 4.2 `dumps()` — After

```python
def dumps(obj: Any, *, skipkeys: bool = False, ensure_ascii: bool = True,
          check_circular: bool = True, allow_nan: bool = True,
          cls: type | None = None, indent: int | str | None = None,
          separators: tuple[str, str] | None = None,
          default: Callable[[Any], Any] | None = None,
          sort_keys: bool = False) -> str:
```

Parameter order matches `json.dumps`: `skipkeys`, `ensure_ascii`, `check_circular`, `allow_nan`, `cls`, `indent`, `separators`, `default`, `sort_keys`.

### 4.3 `_serializer.stringify()` — Before

```python
def stringify(value: object, *, ensure_ascii: bool = True,
              check_circular: bool = True, sort_keys: bool = False,
              indent: int | str | None = None,
              separators: tuple[str, str] | None = None,
              default: Callable[[Any], Any] | None = None) -> str | None:
```

### 4.4 `_serializer.stringify()` — After

```python
def stringify(value: object, *, skipkeys: bool = False,
              ensure_ascii: bool = True, check_circular: bool = True,
              allow_nan: bool = True, sort_keys: bool = False,
              indent: int | str | None = None,
              separators: tuple[str, str] | None = None,
              default: Callable[[Any], Any] | None = None) -> str | None:
```

### 4.5 `RDNEncoder.__init__()` — Before

```python
def __init__(self, *, ensure_ascii: bool = True, check_circular: bool = True,
             indent: int | str | None = None,
             separators: tuple[str, str] | None = None,
             default: Callable[[Any], Any] | None = None,
             sort_keys: bool = False) -> None:
```

### 4.6 `RDNEncoder.__init__()` — After

```python
def __init__(self, *, skipkeys: bool = False, ensure_ascii: bool = True,
             check_circular: bool = True, allow_nan: bool = True,
             indent: int | str | None = None,
             separators: tuple[str, str] | None = None,
             default: Callable[[Any], Any] | None = None,
             sort_keys: bool = False) -> None:
```

### 4.7 Rust `stringify()` — Before

```rust
#[pyo3(signature = (value, *, ensure_ascii=true, check_circular=true, sort_keys=false, indent=None, separators=None))]
fn stringify(py, value, ensure_ascii, check_circular, sort_keys, indent, separators) -> PyResult<PyObject>
```

### 4.8 Rust `stringify()` — After

```rust
#[pyo3(signature = (value, *, skipkeys=false, ensure_ascii=true, check_circular=true, allow_nan=true, sort_keys=false, indent=None, separators=None))]
fn stringify(py, value, skipkeys, ensure_ascii, check_circular, allow_nan, sort_keys, indent, separators) -> PyResult<PyObject>
```

### 4.9 New Module Exports

```python
# In __init__.py
__version__: str  # e.g. "0.1.0"
parse = loads      # alias
stringify = dumps  # alias

__all__ = [
    "loads", "load", "dumps", "dump",
    "parse", "stringify",
    "RDNDecoder", "RDNEncoder", "RDNDecodeError", "MAX_SAFE_INTEGER",
    "__version__",
]
```

### 4.10 CLI Interface

```
usage: python -m rdn [-h] [--sort-keys] [--no-ensure-ascii]
                      [--indent INDENT | --tab | --no-indent | --compact]
                      [infile] [outfile]
```

---

## 5. Implementation Details

### 5.1 `py.typed` Marker

**File to create:** `packages/rdn-python/src/rdn/py.typed`

- Empty file (zero bytes).
- Maturin automatically includes all files under `python-source = "src"` in the built wheel. No `pyproject.toml` changes needed.
- Verification: after `pip install -e .`, confirm `py.typed` exists in the installed package directory.

### 5.2 `__version__` Attribute

**File to modify:** `packages/rdn-python/src/rdn/__init__.py`

Add after the existing imports (before `__all__`):

```python
from importlib.metadata import version, PackageNotFoundError

try:
    __version__ = version("rdn")
except PackageNotFoundError:
    __version__ = "0.0.0-dev"
```

Add `"__version__"` to `__all__`.

`importlib.metadata` is available in all supported Python versions (3.10+). `PackageNotFoundError` catches the case where the package is used from a source checkout without installation (rare, since maturin editable installs register metadata).

### 5.3 `parse`/`stringify` Aliases

**File to modify:** `packages/rdn-python/src/rdn/__init__.py`

After the `load()` function definition, add:

```python
parse = loads
stringify = dumps
```

Note: The internal `_serializer.stringify` import is already aliased as `_stringify` in `__init__.py` (line 14), so there is no naming conflict. The module-level `stringify` will refer to the public alias of `dumps`.

Add `"parse"` and `"stringify"` to `__all__`.

### 5.4 `skipkeys` Parameter

This requires changes in four layers: `__init__.py`, `_serializer.py`, `encoder.py`, and the Rust native extension (`rust/lib.rs` + `rust/serializer.rs`).

#### 5.4.1 `__init__.py` Changes

1. Add `skipkeys: bool = False` to `dumps()` signature (first keyword parameter, matching `json.dumps` order).
2. Pass `skipkeys` through to `_stringify()` call.
3. Pass `skipkeys` through to `_native_stringify()` call.
4. Pass `skipkeys` to `cls()` instantiation when `cls is not None`.
5. **Hot-path routing**: The native extension supports `skipkeys`, so no routing changes needed.

#### 5.4.2 `_serializer.py` Changes

1. Add `skipkeys: bool = False` parameter to the `stringify()` function signature.
2. In the `_encode()` inner function, modify the dict-handling block. Currently:
   ```python
   if not _isinstance(k, _str):
       raise TypeError(f"Object key must be a string, got {type(k).__name__}")
   ```
   Change to:
   ```python
   if not _isinstance(k, _str):
       if skipkeys:
           continue
       raise TypeError(f"Object key must be a string, got {type(k).__name__}")
   ```
   The `continue` skips the current key-value pair entirely, matching `json.dumps(skipkeys=True)` behavior. This naturally handles the edge case where all keys are skipped (resulting in `{}`), because `parts` will be empty and `_format_container` returns `"{}"`.

#### 5.4.3 `encoder.py` Changes

1. Add `skipkeys: bool = False` to `RDNEncoder.__init__()` signature.
2. Store as `self.skipkeys = skipkeys`.
3. In `encode()` method, pass `skipkeys=self.skipkeys` to `_stringify()`.

#### 5.4.4 `rust/lib.rs` Changes

1. Add `skipkeys: bool` to the `stringify()` function signature and `#[pyo3(signature)]` macro.
2. Pass `skipkeys` to `Serializer::new()`.

#### 5.4.5 `rust/serializer.rs` Changes

1. Add a new state bit: `const STATE_SKIPKEYS_BIT: u32 = 0x400;` (after `STATE_SORT_BIT`).
2. Add `skipkeys: bool` parameter to `Serializer::new()`. Set the bit in state if true.
3. In the dict serialization block of `stringify_value()`, modify the key type check:
   - **Unsorted path**: When iterating `dict.iter()`, if `key.downcast::<PyString>()` fails and `skipkeys` bit is set, `continue` to the next pair instead of returning an error. Must adjust the separator logic to use a `first` boolean instead of `enumerate` index.
   - **Sorted path**: When collecting keys, skip non-string keys with `continue` instead of erroring.
4. Apply the same changes to the `stringify_fallback()` dict-subclass path.

### 5.5 `allow_nan` Parameter

This requires changes in the same four layers.

#### 5.5.1 `__init__.py` Changes

1. Add `allow_nan: bool = True` to `dumps()` signature (after `check_circular`, matching `json.dumps` order).
2. Pass `allow_nan` through to `_stringify()` call.
3. Pass `allow_nan` through to `_native_stringify()` call.
4. Pass `allow_nan` to `cls()` instantiation when `cls is not None`.

#### 5.5.2 `_serializer.py` Changes

1. Add `allow_nan: bool = True` parameter to the `stringify()` function signature.
2. In the `_encode()` inner function, modify the float-handling block. Currently:
   ```python
   if _isnan(value):
       return "NaN"
   if value == _INF:
       return "Infinity"
   if value == _NEG_INF:
       return "-Infinity"
   ```
   Change to:
   ```python
   if _isnan(value):
       if not allow_nan:
           raise ValueError("Out of range float values are not RDN compliant")
       return "NaN"
   if value == _INF:
       if not allow_nan:
           raise ValueError("Out of range float values are not RDN compliant")
       return "Infinity"
   if value == _NEG_INF:
       if not allow_nan:
           raise ValueError("Out of range float values are not RDN compliant")
       return "-Infinity"
   ```

#### 5.5.3 `encoder.py` Changes

1. Add `allow_nan: bool = True` to `RDNEncoder.__init__()` signature.
2. Store as `self.allow_nan = allow_nan`.
3. In `encode()` method, pass `allow_nan=self.allow_nan` to `_stringify()`.

#### 5.5.4 `rust/lib.rs` Changes

1. Add `allow_nan: bool` to the `stringify()` function signature and `#[pyo3(signature)]` macro.
2. Pass `allow_nan` to `Serializer::new()`.

#### 5.5.5 `rust/serializer.rs` Changes

1. Add a new state bit: `const STATE_ALLOW_NAN_BIT: u32 = 0x800;`.
2. Add `allow_nan: bool` parameter to `Serializer::new()`. Set the bit in state if true. **Default is true** (bit set), so the check is `state & STATE_ALLOW_NAN_BIT == 0` means "nan not allowed".
3. In the float serialization block of `stringify_value()`, after detecting NaN/Infinity, check the bit:
   ```rust
   if f.is_nan() {
       if self.state & STATE_ALLOW_NAN_BIT == 0 {
           return Err(pyo3::exceptions::PyValueError::new_err(
               "Out of range float values are not RDN compliant"
           ));
       }
       self.buf.write_str("NaN");
   }
   ```
   Same pattern for `f64::INFINITY` and `f64::NEG_INFINITY`.
4. Apply the same check in `stringify_fallback()` for float subclasses.

### 5.6 CLI Tool (`__main__.py`)

**File to create:** `packages/rdn-python/src/rdn/__main__.py`

This module is invoked via `python -m rdn`. It mirrors `python -m json.tool` behavior.

```python
"""Command-line tool for validating and pretty-printing RDN.

Usage: python -m rdn [infile] [outfile]
"""
from __future__ import annotations

import argparse
import sys

import rdn


def main() -> int:
    parser = argparse.ArgumentParser(
        prog="python -m rdn",
        description="A simple command line interface for rdn module "
                    "to validate and pretty-print RDN documents.",
    )
    parser.add_argument("infile", nargs="?", type=argparse.FileType("r"),
                        default=sys.stdin,
                        help="an RDN file to be validated or pretty-printed")
    parser.add_argument("outfile", nargs="?", type=argparse.FileType("w"),
                        default=sys.stdout,
                        help="write the output of infile to outfile")
    parser.add_argument("--sort-keys", action="store_true", default=False,
                        help="sort the output of dictionaries alphabetically by key")
    parser.add_argument("--no-ensure-ascii", action="store_true", default=False,
                        help="disable escaping of non-ASCII characters")

    group = parser.add_mutually_exclusive_group()
    group.add_argument("--indent", type=int, default=4,
                       help="separate items with newlines and use this number "
                            "of spaces for indentation")
    group.add_argument("--tab", action="store_true", default=False,
                       help="separate items with newlines and use tabs for indentation")
    group.add_argument("--no-indent", action="store_true", default=False,
                       help="separate items with spaces rather than newlines")
    group.add_argument("--compact", action="store_true", default=False,
                       help="suppress all whitespace separation (most compact)")

    args = parser.parse_args()

    # Determine indent and separators
    indent: int | str | None = args.indent
    separators: tuple[str, str] | None = None
    if args.tab:
        indent = "\t"
    elif args.no_indent:
        indent = None
        separators = (", ", ": ")
    elif args.compact:
        indent = None
        separators = (",", ":")

    try:
        text = args.infile.read()
        obj = rdn.loads(text)
        output = rdn.dumps(obj, sort_keys=args.sort_keys, indent=indent,
                           ensure_ascii=not args.no_ensure_ascii,
                           separators=separators)
        args.outfile.write(output)
        args.outfile.write("\n")
        return 0
    except rdn.RDNDecodeError as e:
        print(str(e), file=sys.stderr)
        return 1
    except (KeyboardInterrupt, BrokenPipeError):
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
```

Key behaviors:
- Default indent is 4 spaces (same as `json.tool`).
- `--compact` and `--no-indent` set `indent=None` with different separators.
- Parse errors print to stderr and exit with code 1.
- Trailing newline after output (same as `json.tool`).

---

## 6. Test Plan

### 6.1 `py.typed` and `__version__`

**File:** `packages/rdn-python/tests/test_package_meta.py`

- `pathlib.Path(rdn.__file__).parent / "py.typed"` exists.
- `rdn.__version__` is a non-empty string.
- `"__version__"` is in `rdn.__all__`.

### 6.2 `parse`/`stringify` Aliases

**File:** `packages/rdn-python/tests/test_aliases.py`

- `rdn.parse is rdn.loads` (identity check).
- `rdn.stringify is rdn.dumps` (identity check).
- `"parse"` and `"stringify"` are in `rdn.__all__`.
- `rdn.parse('{"a":1}')` returns `{"a": 1}`.
- `rdn.stringify({"a": 1})` returns a valid RDN string.

### 6.3 `skipkeys` Parameter

**Add to:** `packages/rdn-python/tests/test_stringify.py` (new `TestSkipKeys` class)

- `dumps({1: "a", "b": 2}, skipkeys=True)` returns `{"b":2}`.
- `dumps({1: "a", "b": 2}, skipkeys=False)` raises `TypeError`.
- `dumps({1: "a", "b": 2})` raises `TypeError` (default is `False`).
- `dumps({1: "a"}, skipkeys=True)` returns `{}` (all keys skipped).
- Nested dicts: `dumps({"a": {1: "x", "b": 2}}, skipkeys=True)` skips at all levels.
- `skipkeys=True` with `sort_keys=True` works.
- `skipkeys=True` via `RDNEncoder(skipkeys=True).encode(...)`.

**Add to:** `packages/rdn-python/tests/test_native.py` (new section)

- Same test cases via native extension directly.

### 6.4 `allow_nan` Parameter

**Add to:** `packages/rdn-python/tests/test_stringify.py` (new `TestAllowNan` class)

- `dumps(float('nan'))` returns `"NaN"` (default `allow_nan=True`).
- `dumps(float('inf'))` returns `"Infinity"`.
- `dumps(float('-inf'))` returns `"-Infinity"`.
- `dumps(float('nan'), allow_nan=False)` raises `ValueError`.
- `dumps(float('inf'), allow_nan=False)` raises `ValueError`.
- `dumps(float('-inf'), allow_nan=False)` raises `ValueError`.
- Nested: `dumps([1, float('nan')], allow_nan=False)` raises `ValueError`.
- Nested in dict value: `dumps({"a": float('inf')}, allow_nan=False)` raises `ValueError`.
- Via `RDNEncoder(allow_nan=False).encode(float('nan'))` raises `ValueError`.

**Add to:** `packages/rdn-python/tests/test_native.py` (new section)

- Same test cases via native extension.

### 6.5 CLI Tool

**File:** `packages/rdn-python/tests/test_cli.py`

Use `subprocess.run(["python", "-m", "rdn", ...])` or import and call `main()` with mocked `sys.argv`.

- Valid RDN from stdin: exit code 0, pretty-printed output.
- Invalid RDN: exit code 1, error message on stderr.
- `--sort-keys`: keys sorted in output.
- `--compact`: no whitespace.
- `--no-indent`: space-separated, no newlines.
- `--tab`: tab indentation.
- `--indent 2`: 2-space indentation.
- `--no-ensure-ascii`: non-ASCII characters pass through.

---

## 7. Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Rust state bit collision | Low | High | `STATE_SKIPKEYS_BIT = 0x400` and `STATE_ALLOW_NAN_BIT = 0x800` are safely above existing bits. Depth uses bits 0-6 (max 127). |
| `skipkeys` behavior divergence between Python and Rust | Medium | Medium | Shared test cases run against both paths. Native tests explicitly import `rdn._native.stringify`. |
| `allow_nan=True` default surprises users coming from `json` | Low | Low | `json.dumps` also defaults to `allow_nan=True`. Behavior is identical. |
| `importlib.metadata` failure in exotic install scenarios | Low | Low | Fallback to `"0.0.0-dev"` prevents import errors. |
| `skipkeys` changes separator logic in Rust | Medium | Medium | Sorted path collects keys into Vec first; skipping during collection is straightforward. Unsorted path needs `first` boolean to replace enumerate-based separator logic. |

---

## 8. Ordered Task List

1. **Add `py.typed` marker and `__version__` attribute**
   - Create empty `packages/rdn-python/src/rdn/py.typed`.
   - Add `__version__` via `importlib.metadata` to `__init__.py`.
   - Add `"__version__"` to `__all__`.
   - Add tests in `tests/test_package_meta.py`.

2. **Add `parse`/`stringify` aliases**
   - Add `parse = loads` and `stringify = dumps` to `__init__.py`.
   - Add `"parse"` and `"stringify"` to `__all__`.
   - Add tests in `tests/test_aliases.py`.

3. **Implement `skipkeys` in pure Python serializer**
   - Add `skipkeys` parameter to `_serializer.stringify()`.
   - Modify dict key iteration to skip non-string keys when `skipkeys=True`.
   - Add `skipkeys` to `RDNEncoder.__init__()`, store and pass through.
   - Add `skipkeys` to `dumps()` in `__init__.py`, pass to all paths.
   - Add `TestSkipKeys` tests to `tests/test_stringify.py`.

4. **Implement `allow_nan` in pure Python serializer**
   - Add `allow_nan` parameter to `_serializer.stringify()`.
   - Add `ValueError` raise in float handling when `allow_nan=False`.
   - Add `allow_nan` to `RDNEncoder.__init__()`, store and pass through.
   - Add `allow_nan` to `dumps()` in `__init__.py`, pass to all paths.
   - Add `TestAllowNan` tests to `tests/test_stringify.py`.

5. **Implement `skipkeys` and `allow_nan` in Rust native extension**
   - Add state bits to `rust/serializer.rs`.
   - Add parameters to `Serializer::new()`.
   - Modify dict serialization for `skipkeys`.
   - Modify float serialization for `allow_nan`.
   - Update `rust/lib.rs` function signature.
   - Add native tests to `tests/test_native.py`.

6. **Create CLI tool**
   - Create `src/rdn/__main__.py` with argparse-based CLI.
   - Add tests in `tests/test_cli.py`.

7. **Update documentation**
   - Update `packages/rdn-python/README.md` with new API surface.
   - Update `CLAUDE.md` Python section if needed.
