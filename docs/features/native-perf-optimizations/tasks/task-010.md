# Task 010: Create cache.rs KeyCache struct

## Status: pending

## Tier: Tier 2: Type Dispatch & Caching

## Description
Implement the `KeyCache` struct in `cache.rs` with 2048 direct-mapped entries, xxh3 hashing, and round-robin eviction. Add the `xxhash-rust` and `smallvec` dependencies to `Cargo.toml`. The cache stores `PyString` objects keyed by their raw bytes, enabling reuse of the same `PyString` across `parse()` calls for repeated dictionary keys. Store the cache in a module-level `static Mutex<Option<KeyCache>>`.

## Files to Modify
- `packages/rdn-native/src/cache.rs` — add `KeyCache` and `KeyCacheEntry` structs
- `packages/rdn-native/Cargo.toml` — add `xxhash-rust = { version = "0.8", features = ["xxh3"] }` and `smallvec = "1"`

## Implementation Details
**KeyCache struct** (from tech design Section 5.2):

```rust
/// Direct-mapped hash cache for dictionary keys during parsing.
/// Stores PyString objects keyed by their raw bytes, enabling
/// reuse of the same PyString across parse() calls for repeated keys.
struct KeyCache {
    entries: Box<[KeyCacheEntry; 2048]>,
    /// Number of entries currently occupied (for diagnostics only)
    count: usize,
}

struct KeyCacheEntry {
    /// Hash of the key bytes (xxh3 64-bit)
    hash: u64,
    /// The cached PyString object (Py_INCREF'd)
    value: Option<PyObject>,
    /// Raw key bytes for collision detection
    key_bytes: SmallVec<[u8; 32]>,
}
```

**Methods to implement**:
- `KeyCache::new() -> Self` — allocates 2048 empty entries
- `KeyCache::lookup(&self, bytes: &[u8]) -> Option<PyObject>` — compute `xxh3(bytes)`, slot = `hash % 2048`, check if hash and key_bytes match, return cloned `PyObject` if hit
- `KeyCache::insert(&mut self, bytes: &[u8], value: PyObject)` — compute hash, overwrite slot (round-robin eviction), `Py_DECREF` any existing value

**Storage**: Module-level static with GIL-protected access:
```rust
static KEY_CACHE: Mutex<Option<KeyCache>> = Mutex::new(None);
```

**Eviction**: Round-robin replacement -- on collision, the existing entry is overwritten (its `PyObject` is `Py_DECREF`'d). This is simple and matches orjson's approach.

**Memory budget**: 2048 entries * ~(8 hash + 8 ptr + 32 inline bytes + overhead) = ~100-150KB. Negligible.

**New dependencies**:
- `xxhash-rust = { version = "0.8", features = ["xxh3"] }` — ~10KB, no transitive deps
- `smallvec = "1"` — ~20KB, no transitive deps

**Rust unit tests**: Add `#[test]` functions verifying:
- Lookup on empty cache returns `None`
- Insert then lookup returns the value
- Eviction works (insert a different key into the same slot)
- Hash collision handling (different keys that map to the same slot)

## Dependencies
- Depends on: 7
- Blocks: 11

## Acceptance Criteria
- [ ] `KeyCache` and `KeyCacheEntry` structs are implemented in `cache.rs`
- [ ] `xxhash-rust` and `smallvec` dependencies are added to `Cargo.toml`
- [ ] `lookup()` and `insert()` methods work correctly
- [ ] Module-level `static Mutex<Option<KeyCache>>` is declared
- [ ] Rust `#[test]` functions verify lookup/insert/eviction behavior
- [ ] All existing tests pass

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 5.2, Section 6.2.2, Section 12 (Task 10)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
