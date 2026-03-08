"""Tests for PEP 561 py.typed marker and __version__ attribute."""

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
