# Task 17: Create rdn-pydantic package

**Status:** completed
**Dependencies:** Task 16

## Description

Set up the `rdn-pydantic` package structure. Implement all Pydantic v2 custom types and the `RDNModel` mixin with `model_dump_rdn()` and `model_validate_rdn()` methods.

### Package Structure

```
packages/rdn-pydantic/
  pyproject.toml
  README.md
  src/
    rdn_pydantic/
      __init__.py       # Re-exports all public types and functions
      types.py          # Pydantic-compatible annotated types for all RDN types
      model.py          # RDNModel mixin
  tests/
    __init__.py
    test_types.py       # Tests for each Pydantic RDN type
    test_model.py       # Tests for model_dump_rdn / model_validate_rdn
```

### Custom Pydantic Types (`types.py`)

Each RDN type is exposed as a Pydantic-compatible annotated type using `__get_pydantic_core_schema__`. Since all RDN types map to native Python types, these are thin `Annotated` wrappers that add RDN-aware validation and serialization:

| Export Name | Python Type | RDN Type |
|-------------|-------------|----------|
| `PydanticRDNBigInt` | `Annotated[int, ...]` | BigInt (`42n`) |
| `PydanticRDNDateTime` | `Annotated[datetime, ...]` | DateTime |
| `PydanticRDNTimeOnly` | `Annotated[time, ...]` | TimeOnly |
| `PydanticRDNDuration` | `Annotated[timedelta, ...]` | Duration |
| `PydanticRDNRegExp` | `Annotated[re.Pattern, ...]` | RegExp |
| `PydanticRDNBinary` | `Annotated[bytes, ...]` | Binary |
| `PydanticRDNSet` | `Annotated[set, ...]` | Set |

Each type implements:
- **Validation**: Accepts the native Python type or a raw value that can be converted
- **Serialization**: Produces a Pydantic-friendly representation
- **`__get_pydantic_core_schema__`**: Returns a `core_schema` using `no_info_plain_validator_function` with a serialization schema

### RDNModel Mixin (`model.py`)

```python
class RDNModel(BaseModel):
    def model_dump_rdn(self, *, indent=None, exclude_none=False, by_alias=False) -> str:
        data = self.model_dump(mode="python", exclude_none=exclude_none, by_alias=by_alias)
        return rdn.dumps(data, indent=indent)

    @classmethod
    def model_validate_rdn(cls, rdn_data: str | bytes, *, strict=False) -> "RDNModel":
        parsed = rdn.loads(rdn_data)
        return cls.model_validate(parsed, strict=strict)
```

Design choice: Mixin class (explicit opt-in) rather than monkey-patching `BaseModel`.

### Serialization Config

The Pydantic types integrate with Pydantic's standard configuration (`ConfigDict`, `Field(alias=...)`, `populate_by_name`, etc.). `model_dump_rdn()` respects `by_alias`, `exclude_none`, and other standard Pydantic dump parameters.

### pyproject.toml

- Name: `rdn-pydantic`, version `0.1.0`
- Dependencies: `rdn>=0.1.0`, `pydantic>=2.0`
- Python >= 3.10

## Files to Create/Modify
- `packages/rdn-pydantic/pyproject.toml` (create)
- `packages/rdn-pydantic/README.md` (create)
- `packages/rdn-pydantic/src/rdn_pydantic/__init__.py` (create)
- `packages/rdn-pydantic/src/rdn_pydantic/types.py` (create)
- `packages/rdn-pydantic/src/rdn_pydantic/model.py` (create)
- `packages/rdn-pydantic/tests/__init__.py` (create)
- `packages/rdn-pydantic/tests/test_types.py` (create)
- `packages/rdn-pydantic/tests/test_model.py` (create)

## Acceptance Criteria
- All Pydantic types validate and serialize correctly
- `PydanticRDNBigInt` validates `int`, rejects non-int
- `PydanticRDNDateTime` validates `datetime` and string input
- `PydanticRDNRegExp` validates `re.Pattern`
- `model_dump_rdn()` produces valid RDN strings
- `model_validate_rdn()` parses RDN into validated model instances
- `model_dump_rdn(indent=2)` produces pretty-printed RDN
- `model_dump_rdn(by_alias=True)` uses field aliases
- `model_dump_rdn(exclude_none=True)` omits None fields
- Nested models work with both `model_dump_rdn` and `model_validate_rdn`
- All tests pass: `pytest packages/rdn-pydantic/tests -v`
- `pip install -e packages/rdn-pydantic` succeeds

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 17
- Tech Design: Section 5.1 (Custom Types -- full Pydantic type specifications with `__get_pydantic_core_schema__` example)
- Tech Design: Section 5.2 (Model Integration -- `RDNModel` mixin with `model_dump_rdn`/`model_validate_rdn`)
- Tech Design: Section 5.3 (Serialization Config -- `ConfigDict`, `Field` integration)
- Tech Design: Section 2.4 (Package Structure for rdn-pydantic)
- Tech Design: Section 9.2 (pyproject.toml for rdn-pydantic)
- Tech Design: Section 8.3 (Integration Tests -- test_types.py, test_model.py coverage)
- Discovery: `docs/features/python-integration/discovery.md`
