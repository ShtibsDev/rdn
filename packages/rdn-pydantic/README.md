# rdn-pydantic

Pydantic v2 integration for [RDN (Rich Data Notation)](https://github.com/AstroSnout/rdn).

## Installation

```bash
pip install rdn-pydantic
```

## Quick Start

```python
from datetime import datetime, timezone
from rdn_pydantic import RDNModel, PydanticRDNDateTime, PydanticRDNBigInt, PydanticRDNSet

class User(RDNModel):
    name: str
    user_id: PydanticRDNBigInt
    created_at: PydanticRDNDateTime
    tags: PydanticRDNSet

user = User(
    name="Alice",
    user_id=12345678901234567890,
    created_at=datetime(2024, 1, 15, tzinfo=timezone.utc),
    tags={"admin", "active"},
)

# Serialize to RDN
rdn_string = user.model_dump_rdn(indent=2)
print(rdn_string)

# Deserialize from RDN
user2 = User.model_validate_rdn(rdn_string)
```

## Custom Types

| Type | Python Type | RDN Type |
|------|-------------|----------|
| `PydanticRDNBigInt` | `int` | BigInt (`42n`) |
| `PydanticRDNDateTime` | `datetime` | DateTime (`@2024-01-15T00:00:00.000Z`) |
| `PydanticRDNTimeOnly` | `time` | TimeOnly (`@14:30:00`) |
| `PydanticRDNDuration` | `timedelta` | Duration (`@P3DT4H`) |
| `PydanticRDNRegExp` | `re.Pattern` | RegExp (`/pattern/flags`) |
| `PydanticRDNBinary` | `bytes` | Binary (`b"..."`) |
| `PydanticRDNSet` | `set` | Set (`{1, 2, 3}`) |

## RDNModel

The `RDNModel` mixin extends `pydantic.BaseModel` with two methods:

- **`model_dump_rdn()`** -- Serialize the model to an RDN string. Supports `indent`, `exclude_none`, and `by_alias` parameters.
- **`model_validate_rdn()`** -- Parse an RDN string and validate it into a model instance. Supports `strict` parameter.

## License

MIT
