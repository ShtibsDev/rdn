# V8 Integration

This directory contains documentation, patches, and benchmark scripts for the RDN implementation inside V8.

> **Note:** The V8 fork lives separately at `~/v8` and is NOT a submodule of this repository. This directory provides a bridge between the RDN spec/test-suite and the V8 implementation.

## V8 Fork Location

The V8 fork with native RDN support is located at:

```
~/v8/v8/
```

## Building V8 with RDN Support

### Prerequisites

- [depot_tools](https://commondatastorage.googleapis.com/chrome-infra-docs/flat/depot_tools/docs/html/depot_tools_tutorial.html)
- Python 3
- A C++ compiler (clang recommended)

### Build Steps

```bash
cd ~/v8/v8

# Ensure dependencies are synced
gclient sync

# Generate build files (debug)
tools/dev/gm.py x64.debug

# Generate build files (release, for benchmarks)
tools/dev/gm.py x64.release
```

### Running d8 with RDN

```bash
# Debug build
~/v8/v8/out/x64.debug/d8 --allow-natives-syntax your-script.js

# Release build
~/v8/v8/out/x64.release/d8 your-script.js
```

## Benchmark Scripts

The `benchmarks/` directory contains d8-compatible scripts for measuring RDN parse/stringify performance.

```bash
# Run a benchmark
~/v8/v8/out/x64.release/d8 benchmarks/parse-bench.js
~/v8/v8/out/x64.release/d8 benchmarks/stringify-bench.js
```

## Patches

The `patches/` directory is reserved for patch files that can be applied to a stock V8 checkout to add RDN support. These are maintained separately from the fork for easier upstream contribution tracking.

## Key V8 Source Files

The RDN implementation in V8 primarily touches:

- `src/json/rdn-parser.h` / `src/json/rdn-parser.cc` — Recursive-descent parser
- `src/json/rdn-stringifier.cc` — Serializer with SWAR escape detection
- `src/runtime/runtime-rdn.cc` — Runtime builtins (`RDN.parse`, `RDN.stringify`)
- `src/init/bootstrapper.cc` — Installs the `RDN` global object
