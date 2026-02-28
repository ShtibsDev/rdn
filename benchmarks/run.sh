#!/usr/bin/env bash
set -euo pipefail

# Cross-implementation benchmark harness for RDN
# Runs parse/stringify benchmarks against fixture files for each implementation.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES_DIR="$SCRIPT_DIR/fixtures"

FIXTURE_FILES=(
  "small-typical"
  "small-rdn-heavy"
  "medium-typical"
  "medium-rdn-heavy"
  "large-typical"
  "large-rdn-heavy"
)

echo "=== RDN Cross-Implementation Benchmarks ==="
echo ""

echo "Available fixtures:"
for f in "${FIXTURE_FILES[@]}"; do
  rdn="$FIXTURES_DIR/$f.rdn"
  json="$FIXTURES_DIR/$f.json"
  if [ -f "$rdn" ] && [ -f "$json" ]; then
    rdn_size=$(wc -c < "$rdn" | tr -d ' ')
    json_size=$(wc -c < "$json" | tr -d ' ')
    echo "  $f — RDN: ${rdn_size}B, JSON: ${json_size}B"
  else
    echo "  $f — not generated (run: pnpm generate-fixtures)"
  fi
done
echo ""

# TypeScript
if command -v node &>/dev/null && [ -d "$SCRIPT_DIR/../packages/rdn-js/dist" ]; then
  echo "--- TypeScript ---"
  echo "TODO: Run TypeScript benchmarks"
  echo ""
fi

# Rust
if command -v cargo &>/dev/null; then
  echo "--- Rust ---"
  echo "TODO: Run 'cargo bench' in packages/rdn-rust/"
  echo ""
fi

echo "Add more implementations as they become available."
