# Task 011: Integrate KeyCache into parser

## Status: pending

## Tier: Tier 2: Type Dispatch & Caching

## Description
Integrate the `KeyCache` into the parser so that dictionary keys are cached across `parse()` calls. On `parse()` entry, acquire the cache from the module-level static. Add a new `parse_object_key()` method that computes the key's byte range, checks the cache, and either returns the cached `PyString` or creates a new one and inserts it. Replace `parse_string_as_rust()` calls in object key parsing with the new cached path. On `parse()` exit, return the cache to the module-level static.

## Files to Modify
- `packages/rdn-native/src/parser.rs` — add `parse_object_key()`, integrate cache in `finish_object()` and `parse_brace()`
- `packages/rdn-native/src/lib.rs` — pass cache to parser in `parse()` entry point

## Implementation Details
**Cache acquisition flow**:
1. On `parse()` entry, take ownership of the cache: `let mut cache = KEY_CACHE.lock().unwrap().take().unwrap_or_else(KeyCache::new);`
2. Pass the cache to the `Parser` struct (add a `key_cache: KeyCache` field).
3. On `parse()` exit (success or error), put the cache back: `*KEY_CACHE.lock().unwrap() = Some(parser.key_cache);`
4. This avoids holding the mutex during parsing.

**New `parse_object_key()` method**:
1. After determining the key's byte range `start..end` in `parse_string()`:
2. Compute `xxh3(bytes[start..end])` -> `hash`
3. Compute `slot = hash % 2048`
4. Check `cache.entries[slot]`: if `hash` matches and `key_bytes` matches the source bytes, return `cache.entries[slot].value` (with `Py_INCREF`)
5. On miss: create `PyString::new(py, &source[start..end])`, store in cache slot (replacing any existing entry), return the new `PyString`
6. Use the `PyString` directly as the dict key via `dict.set_item(py_string, val)`, eliminating the intermediate Rust `String`

**Replacement points**:
- `finish_object()` (parser.rs line 870): Replace `parse_string_as_rust()` call with `parse_object_key()` that returns a `Bound<'py, PyString>` directly
- `parse_brace()` (parser.rs lines 831-833): Same replacement for the first key in a brace-disambiguated object

**Memory and cleanup**: The cache is never explicitly cleared -- entries are evicted via round-robin replacement. The `PyObject` references keep Python strings alive. On interpreter shutdown, Python's finalizer handles cleanup. Fixed-size (2048 entries) bounds memory at ~150KB.

**New Python-level tests**:
- Parse a payload with many repeated keys, verify correct values
- Parse payloads with > 2048 unique keys, verify no corruption
- Parse multiple payloads sequentially, verify cross-call cache reuse works correctly

## Dependencies
- Depends on: 10
- Blocks: 13

## Acceptance Criteria
- [ ] `parse_object_key()` method exists and uses the KeyCache
- [ ] Object keys are cached across `parse()` calls
- [ ] No intermediate Rust `String` conversion for dict keys
- [ ] Cache is acquired/returned on `parse()` entry/exit
- [ ] Tests with repeated keys pass
- [ ] Tests with > 2048 unique keys pass (no corruption)
- [ ] All existing tests pass

## References
- Tech design: `docs/features/native-perf-optimizations/tech-design.md` Section 6.2.2, Section 12 (Task 11)
- Discovery: `docs/features/native-perf-optimizations/discovery.md`
