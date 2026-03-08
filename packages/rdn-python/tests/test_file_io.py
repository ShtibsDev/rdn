"""Tests for rdn.load(), rdn.loads(), and rdn.dump() file I/O behaviour."""

from __future__ import annotations

import tempfile
from collections import OrderedDict
from decimal import Decimal
from io import BytesIO, StringIO
from pathlib import Path
from types import SimpleNamespace

import pytest

import rdn
from rdn.exceptions import RDNDecodeError


class TestDumpFileIO:
    """Test dump() with various file-like objects and real files."""

    def test_dump_to_stringio(self) -> None:
        fp = StringIO()
        rdn.dump({"key": "value"}, fp)
        assert fp.getvalue() == '{"key":"value"}'

    def test_dump_to_real_file(self, tmp_path: Path) -> None:
        """dump() writes correctly to a real file on disk."""
        path = tmp_path / "output.rdn"
        with open(path, "w") as f:
            rdn.dump({"name": "test", "count": 42}, f, sort_keys=True)
        content = path.read_text()
        assert content == '{"count":42,"name":"test"}'

    def test_dump_pretty_to_file(self, tmp_path: Path) -> None:
        """Pretty-printed dump() writes indented output to a file."""
        path = tmp_path / "pretty.rdn"
        with open(path, "w") as f:
            rdn.dump([1, 2, 3], f, indent=2)
        content = path.read_text()
        assert content == "[\n  1,\n  2,\n  3\n]"

    def test_dump_multiple_writes(self) -> None:
        """Multiple dump() calls append to the same file-like object."""
        fp = StringIO()
        rdn.dump(1, fp)
        fp.write("\n")
        rdn.dump(2, fp)
        assert fp.getvalue() == "1\n2"

    def test_dump_with_all_kwargs(self) -> None:
        """dump() passes all kwargs through to dumps()."""
        fp = StringIO()
        rdn.dump({"b": 2, "a": 1}, fp, indent=2, sort_keys=True, ensure_ascii=True, check_circular=True, separators=(",", ": "))
        result = fp.getvalue()
        assert '"a": 1' in result
        assert '"b": 2' in result
        # sort_keys ensures a comes before b
        assert result.index('"a"') < result.index('"b"')

    def test_dump_returns_none(self) -> None:
        """dump() returns None, not the serialized string."""
        fp = StringIO()
        result = rdn.dump(42, fp)
        assert result is None

    def test_dump_empty_dict(self) -> None:
        fp = StringIO()
        rdn.dump({}, fp)
        assert fp.getvalue() == "{}"

    def test_dump_none(self) -> None:
        fp = StringIO()
        rdn.dump(None, fp)
        assert fp.getvalue() == "null"

    def test_dump_nested_structure(self, tmp_path: Path) -> None:
        """Complex nested structure serializes correctly to a file."""
        path = tmp_path / "nested.rdn"
        value = {"users": [{"name": "Alice", "scores": (100, 200)}, {"name": "Bob", "scores": (150, 250)}]}
        with open(path, "w") as f:
            rdn.dump(value, f)
        content = path.read_text()
        assert '"users"' in content
        assert '"Alice"' in content
        assert "(100,200)" in content

    def test_dump_with_default(self) -> None:
        """dump() passes default kwarg through."""
        fp = StringIO()

        class Custom:
            pass

        rdn.dump(Custom(), fp, default=lambda o: "custom_value")
        assert fp.getvalue() == '"custom_value"'

    def test_dump_circular_reference_raises(self) -> None:
        """dump() raises ValueError for circular references."""
        fp = StringIO()
        a: list = [1]
        a.append(a)
        with pytest.raises(ValueError, match="Converting circular structure to RDN"):
            rdn.dump(a, fp)

    def test_dump_unsupported_type_raises(self) -> None:
        """dump() raises TypeError for unsupported types without default."""
        fp = StringIO()
        with pytest.raises(TypeError, match="object"):
            rdn.dump(object(), fp)


# ---------------------------------------------------------------------------
# rdn.loads() (Task 9)
# ---------------------------------------------------------------------------

class TestLoads:
    """Test loads() public API."""

    def test_loads_string(self) -> None:
        assert rdn.loads('"hello"') == "hello"

    def test_loads_integer(self) -> None:
        assert rdn.loads("42") == 42

    def test_loads_object(self) -> None:
        assert rdn.loads('{"key": "value"}') == {"key": "value"}

    def test_loads_bytes(self) -> None:
        assert rdn.loads(b'"hello"') == "hello"

    def test_loads_bytearray(self) -> None:
        assert rdn.loads(bytearray(b'"hello"')) == "hello"

    def test_loads_bytes_utf8(self) -> None:
        """bytes input is decoded as UTF-8."""
        result = rdn.loads(b'"\xc3\xa9"')
        assert result == "\u00e9"

    def test_loads_invalid_type(self) -> None:
        with pytest.raises(TypeError):
            rdn.loads(123)  # type: ignore[arg-type]

    def test_loads_parse_float(self) -> None:
        result = rdn.loads("3.14", parse_float=Decimal)
        assert result == Decimal("3.14")
        assert isinstance(result, Decimal)

    def test_loads_parse_int(self) -> None:
        result = rdn.loads("42", parse_int=str)
        assert result == "42"

    def test_loads_parse_bigint(self) -> None:
        result = rdn.loads("42n", parse_bigint=Decimal)
        assert result == Decimal("42")

    def test_loads_object_hook(self) -> None:
        result = rdn.loads('{"a": 1}', object_hook=lambda d: SimpleNamespace(**d))
        assert isinstance(result, SimpleNamespace)
        assert result.a == 1

    def test_loads_object_pairs_hook(self) -> None:
        result = rdn.loads('{"a": 1}', object_pairs_hook=OrderedDict)
        assert isinstance(result, OrderedDict)

    def test_loads_object_pairs_hook_priority(self) -> None:
        """object_pairs_hook takes priority over object_hook."""
        result = rdn.loads(
            '{"a": 1}',
            object_hook=lambda d: "OBJECT",
            object_pairs_hook=lambda pairs: "PAIRS",
        )
        assert result == "PAIRS"

    def test_loads_trailing_content(self) -> None:
        with pytest.raises(RDNDecodeError, match="Unexpected data after value"):
            rdn.loads("true false")


# ---------------------------------------------------------------------------
# rdn.load() (Task 9)
# ---------------------------------------------------------------------------

class TestLoadFileIO:
    """Test load() with file-like objects."""

    def test_load_from_stringio(self) -> None:
        fp = StringIO("42")
        assert rdn.load(fp) == 42

    def test_load_from_stringio_object(self) -> None:
        fp = StringIO('{"key": "value"}')
        assert rdn.load(fp) == {"key": "value"}

    def test_load_from_bytesio(self) -> None:
        fp = BytesIO(b'"hello"')
        assert rdn.load(fp) == "hello"

    def test_load_from_real_file(self, tmp_path: Path) -> None:
        path = tmp_path / "input.rdn"
        path.write_text('{"name": "test", "count": 42}')
        with open(path) as f:
            result = rdn.load(f)
        assert result == {"name": "test", "count": 42}

    def test_load_from_binary_file(self, tmp_path: Path) -> None:
        path = tmp_path / "input.rdn"
        path.write_bytes(b'"hello"')
        with open(path, "rb") as f:
            result = rdn.load(f)
        assert result == "hello"

    def test_load_with_hooks(self) -> None:
        fp = StringIO('{"a": 1}')
        result = rdn.load(fp, object_hook=lambda d: SimpleNamespace(**d))
        assert isinstance(result, SimpleNamespace)
        assert result.a == 1

    def test_load_with_parse_float(self) -> None:
        fp = StringIO("3.14")
        result = rdn.load(fp, parse_float=Decimal)
        assert result == Decimal("3.14")

    def test_load_empty_input(self) -> None:
        fp = StringIO("")
        with pytest.raises(RDNDecodeError):
            rdn.load(fp)
