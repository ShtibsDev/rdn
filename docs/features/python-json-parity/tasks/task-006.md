# Task 006: Create CLI tool (`python -m rdn`)

**References:** [discovery.md](../discovery.md) | [tech-design.md](../tech-design.md) §5.6

## Objective

Create `__main__.py` so `python -m rdn` works as a validate/pretty-print tool, matching `python -m json.tool` behavior.

## Changes

### 1. Create `packages/rdn-python/src/rdn/__main__.py`

Argparse-based CLI with:
- **Positional args:** `infile` (optional, default stdin), `outfile` (optional, default stdout)
- **Options:** `--sort-keys`, `--no-ensure-ascii`
- **Mutually exclusive indent group:** `--indent N` (default 4), `--tab`, `--no-indent`, `--compact`
- **Behavior:** Read input → `rdn.loads()` → `rdn.dumps()` with options → write output + newline
- **Error handling:** Parse errors → stderr + exit code 1; success → exit code 0

See tech-design.md §5.6 for full implementation.

### 2. Add tests: `packages/rdn-python/tests/test_cli.py`

Use `subprocess.run(["python", "-m", "rdn", ...])` for integration tests:

- Valid RDN from stdin → exit 0, pretty-printed output
- Invalid RDN → exit 1, error on stderr
- `--sort-keys` → keys sorted
- `--compact` → no whitespace (`{...}`)
- `--no-indent` → space-separated, single line
- `--tab` → tab indentation
- `--indent 2` → 2-space indentation
- `--no-ensure-ascii` → non-ASCII passes through
- File input/output (infile, outfile positional args)

## Verification

```bash
cd packages/rdn-python
echo '{"b":1,"a":2}' | python -m rdn --sort-keys
echo 'invalid{' | python -m rdn; echo "Exit: $?"
python -m rdn --help
```
