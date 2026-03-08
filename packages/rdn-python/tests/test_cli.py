"""Integration tests for the ``python -m rdn`` CLI tool."""

from __future__ import annotations

import subprocess
import sys
import tempfile
from pathlib import Path

import pytest

RDN_PYTHON_DIR = str(Path(__file__).resolve().parent.parent)


def run_cli(*args: str, input_text: str | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, "-m", "rdn", *args],
        input=input_text, capture_output=True, text=True,
        cwd=RDN_PYTHON_DIR,
    )


# ---------------------------------------------------------------------------
# Basic validation
# ---------------------------------------------------------------------------

class TestBasicValidation:
    def test_valid_rdn_exit_code_zero(self) -> None:
        result = run_cli(input_text='{"a": 1}')
        assert result.returncode == 0

    def test_valid_rdn_pretty_printed_default_indent(self) -> None:
        result = run_cli(input_text='{"a":1,"b":2}')
        assert result.returncode == 0
        expected = '{\n    "a": 1,\n    "b": 2\n}\n'
        assert result.stdout == expected

    def test_invalid_rdn_exit_code_one(self) -> None:
        result = run_cli(input_text="invalid{")
        assert result.returncode == 1

    def test_invalid_rdn_error_on_stderr(self) -> None:
        result = run_cli(input_text="invalid{")
        assert result.returncode == 1
        assert result.stderr.strip() != ""


# ---------------------------------------------------------------------------
# Indentation options
# ---------------------------------------------------------------------------

class TestIndentation:
    def test_indent_2(self) -> None:
        result = run_cli("--indent", "2", input_text='{"a":1}')
        assert result.returncode == 0
        expected = '{\n  "a": 1\n}\n'
        assert result.stdout == expected

    def test_tab_indent(self) -> None:
        result = run_cli("--tab", input_text='{"a":1}')
        assert result.returncode == 0
        expected = '{\n\t"a": 1\n}\n'
        assert result.stdout == expected

    def test_no_indent(self) -> None:
        result = run_cli("--no-indent", input_text='{"a":1}')
        assert result.returncode == 0
        assert result.stdout == '{"a": 1}\n'
        assert "\n" not in result.stdout.rstrip("\n")

    def test_compact(self) -> None:
        result = run_cli("--compact", input_text='{"a": 1}')
        assert result.returncode == 0
        assert result.stdout == '{"a":1}\n'


# ---------------------------------------------------------------------------
# Sort keys
# ---------------------------------------------------------------------------

class TestSortKeys:
    def test_sort_keys(self) -> None:
        result = run_cli("--sort-keys", input_text='{"b":1,"a":2}')
        assert result.returncode == 0
        expected = '{\n    "a": 2,\n    "b": 1\n}\n'
        assert result.stdout == expected


# ---------------------------------------------------------------------------
# Ensure ASCII
# ---------------------------------------------------------------------------

class TestEnsureAscii:
    def test_no_ensure_ascii(self) -> None:
        result = run_cli("--no-ensure-ascii", input_text='{"key":"caf\u00e9"}')
        assert result.returncode == 0
        assert "caf\u00e9" in result.stdout

    def test_default_ensure_ascii(self) -> None:
        result = run_cli(input_text='{"key":"caf\\u00e9"}')
        assert result.returncode == 0
        # Default: non-ASCII should be escaped
        assert "\\u00e9" in result.stdout


# ---------------------------------------------------------------------------
# File input
# ---------------------------------------------------------------------------

class TestFileInput:
    def test_infile_positional(self, tmp_path: Path) -> None:
        infile = tmp_path / "input.rdn"
        infile.write_text('{"x": 42}')
        result = run_cli(str(infile))
        assert result.returncode == 0
        assert '"x": 42' in result.stdout

    def test_outfile_positional(self, tmp_path: Path) -> None:
        infile = tmp_path / "input.rdn"
        outfile = tmp_path / "output.rdn"
        infile.write_text('{"x": 42}')
        result = run_cli(str(infile), str(outfile))
        assert result.returncode == 0
        content = outfile.read_text()
        assert '"x": 42' in content


# ---------------------------------------------------------------------------
# Help
# ---------------------------------------------------------------------------

class TestHelp:
    def test_help_flag(self) -> None:
        result = run_cli("--help")
        assert result.returncode == 0
        assert "python -m rdn" in result.stdout
