/// Cached Python type pointers and string-interning key cache for hot-path dispatch.
///
/// `TypeCache` stores raw `*mut ffi::PyTypeObject` pointers for 16 Python types used in
/// the serializer's type-dispatch loop, avoiding repeated `isinstance()` calls.
///
/// `KeyCache` is an xxhash-based, fixed-size (2048-slot) string-interning cache that
/// lets the parser reuse `PyObject` string keys instead of allocating new ones for
/// every object key encountered during parsing.
use std::sync::Mutex;

use pyo3::ffi;
use pyo3::prelude::*;
use pyo3::types::*;

use smallvec::SmallVec;

// ---------------------------------------------------------------------------
// TypeCache
// ---------------------------------------------------------------------------

/// Cached raw type-object pointers for the 16 Python types used in serializer dispatch.
///
/// All pointers are valid for the lifetime of the interpreter — built-in type objects
/// are immortal, and the `_*_ref` fields hold strong references to module-level types
/// (datetime, time, timedelta, re.Pattern, bytearray) to keep them alive.
pub(crate) struct TypeCache {
    // Built-in types (immortal, no ref needed)
    pub str_type: *mut ffi::PyTypeObject,
    pub int_type: *mut ffi::PyTypeObject,
    pub bool_type: *mut ffi::PyTypeObject,
    pub float_type: *mut ffi::PyTypeObject,
    pub list_type: *mut ffi::PyTypeObject,
    pub dict_type: *mut ffi::PyTypeObject,
    pub tuple_type: *mut ffi::PyTypeObject,
    pub set_type: *mut ffi::PyTypeObject,
    pub frozenset_type: *mut ffi::PyTypeObject,
    pub bytes_type: *mut ffi::PyTypeObject,
    pub none_type: *mut ffi::PyTypeObject,

    // Module-level types (need refs to stay alive)
    pub bytearray_type: *mut ffi::PyTypeObject,
    pub datetime_type: *mut ffi::PyTypeObject,
    pub time_type: *mut ffi::PyTypeObject,
    pub timedelta_type: *mut ffi::PyTypeObject,
    pub pattern_type: *mut ffi::PyTypeObject,

    // Strong references that keep the module-level types alive
    _datetime_ref: PyObject,
    _time_ref: PyObject,
    _timedelta_ref: PyObject,
    _pattern_ref: PyObject,
    _bytearray_ref: PyObject,
}

// SAFETY: TypeCache is only ever accessed while holding the GIL.
unsafe impl Send for TypeCache {}
unsafe impl Sync for TypeCache {}

impl TypeCache {
    pub fn new(py: Python) -> PyResult<Self> {
        // Built-in types — use PyO3 type info
        let str_type = <PyString as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let int_type = <PyInt as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let bool_type = <PyBool as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let float_type = <PyFloat as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let list_type = <PyList as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let dict_type = <PyDict as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let tuple_type = <PyTuple as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let set_type = <PySet as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let frozenset_type = <PyFrozenSet as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let bytes_type = <PyBytes as pyo3::type_object::PyTypeInfo>::type_object(py).as_ptr() as *mut ffi::PyTypeObject;
        let none_type = py.None().bind(py).get_type().as_ptr() as *mut ffi::PyTypeObject;

        // Module-level types — import and cache
        let datetime_mod = py.import("datetime")?;
        let datetime_obj = datetime_mod.getattr("datetime")?.into_any().unbind();
        let time_obj = datetime_mod.getattr("time")?.into_any().unbind();
        let timedelta_obj = datetime_mod.getattr("timedelta")?.into_any().unbind();

        let re_mod = py.import("re")?;
        let empty_pattern = re_mod.call_method1("compile", ("",))?;
        let pattern_obj = empty_pattern.get_type().into_any().unbind();

        let builtins = py.import("builtins")?;
        let bytearray_obj = builtins.getattr("bytearray")?.into_any().unbind();

        let datetime_type = datetime_obj.bind(py).as_ptr() as *mut ffi::PyTypeObject;
        let time_type = time_obj.bind(py).as_ptr() as *mut ffi::PyTypeObject;
        let timedelta_type = timedelta_obj.bind(py).as_ptr() as *mut ffi::PyTypeObject;
        let pattern_type = pattern_obj.bind(py).as_ptr() as *mut ffi::PyTypeObject;
        let bytearray_type = bytearray_obj.bind(py).as_ptr() as *mut ffi::PyTypeObject;

        Ok(TypeCache {
            str_type,
            int_type,
            bool_type,
            float_type,
            list_type,
            dict_type,
            tuple_type,
            set_type,
            frozenset_type,
            bytes_type,
            none_type,
            bytearray_type,
            datetime_type,
            time_type,
            timedelta_type,
            pattern_type,
            _datetime_ref: datetime_obj,
            _time_ref: time_obj,
            _timedelta_ref: timedelta_obj,
            _pattern_ref: pattern_obj,
            _bytearray_ref: bytearray_obj,
        })
    }
}

/// Global singleton — initialized once during module init, read under the GIL.
static mut TYPE_CACHE: Option<TypeCache> = None;

/// Initialize the global TypeCache.  Must be called exactly once, during module init.
pub(crate) fn init_type_cache(py: Python) -> PyResult<()> {
    let cache = TypeCache::new(py)?;
    // SAFETY: called once during module init while holding the GIL.
    unsafe { TYPE_CACHE = Some(cache); }
    Ok(())
}

/// Get a reference to the global TypeCache.  Panics if not yet initialized.
#[allow(static_mut_refs)]
pub(crate) fn get_type_cache() -> &'static TypeCache {
    // SAFETY: only called after init_type_cache, always under the GIL.
    unsafe { TYPE_CACHE.as_ref().expect("TypeCache not initialized — was _native module loaded?") }
}

// ---------------------------------------------------------------------------
// KeyCache
// ---------------------------------------------------------------------------

const KEY_CACHE_SLOTS: usize = 2048;

struct KeyCacheEntry {
    hash: u64,
    value: Option<PyObject>,
    key_bytes: SmallVec<[u8; 32]>,
}

impl Default for KeyCacheEntry {
    fn default() -> Self {
        KeyCacheEntry { hash: 0, value: None, key_bytes: SmallVec::new() }
    }
}

pub(crate) struct KeyCache {
    entries: Vec<KeyCacheEntry>,
}

impl KeyCache {
    pub fn new() -> Self {
        let mut entries = Vec::with_capacity(KEY_CACHE_SLOTS);
        for _ in 0..KEY_CACHE_SLOTS {
            entries.push(KeyCacheEntry::default());
        }
        KeyCache { entries }
    }

    /// Look up a cached `PyObject` for the given byte-slice key.
    /// Returns a new owned reference (Py_INCREF) on cache hit, `None` on miss.
    pub fn lookup(&self, py: Python, bytes: &[u8]) -> Option<PyObject> {
        let hash = xxhash_rust::xxh3::xxh3_64(bytes);
        let slot = hash as usize % KEY_CACHE_SLOTS;
        let entry = &self.entries[slot];
        if entry.hash == hash && entry.key_bytes.as_slice() == bytes {
            entry.value.as_ref().map(|v| v.clone_ref(py))
        } else {
            None
        }
    }

    /// Insert (or overwrite) a key/value into the cache.  Round-robin eviction.
    pub fn insert(&mut self, _py: Python, bytes: &[u8], value: PyObject) {
        let hash = xxhash_rust::xxh3::xxh3_64(bytes);
        let slot = hash as usize % KEY_CACHE_SLOTS;
        let entry = &mut self.entries[slot];
        entry.hash = hash;
        entry.key_bytes = SmallVec::from_slice(bytes);
        entry.value = Some(value);
    }
}

/// Global key cache, protected by a Mutex so it can be taken/returned across calls.
static KEY_CACHE: Mutex<Option<KeyCache>> = Mutex::new(None);

/// Take the key cache out of the global slot for use during a parse call.
/// Returns `None` on the first call (caller should create a fresh one).
pub(crate) fn take_key_cache() -> Option<KeyCache> {
    KEY_CACHE.lock().unwrap().take()
}

/// Return the key cache to the global slot after a parse call.
pub(crate) fn return_key_cache(cache: KeyCache) {
    *KEY_CACHE.lock().unwrap() = Some(cache);
}
