# rdn-fastapi

FastAPI/Starlette integration for [RDN (Rich Data Notation)](https://github.com/AstroSnout/rdn).

Provides `RDNResponse`, `RDNRoute`, and `RDNMiddleware` for seamless RDN content-type handling in FastAPI applications.

## Installation

```bash
pip install rdn-fastapi
```

With optional Pydantic model integration:

```bash
pip install rdn-fastapi[pydantic]
```

## Quick Start

### RDNResponse

Use `RDNResponse` as a response class to serialize return values as RDN:

```python
from fastapi import FastAPI
from rdn_fastapi import RDNResponse
from datetime import datetime, timezone

app = FastAPI()

@app.get("/data", response_class=RDNResponse)
async def get_data():
    return {"key": "value", "created": datetime.now(timezone.utc)}
```

Responses are serialized with `Content-Type: application/x-rdn`.

### RDNRoute

Use `RDNRoute` to automatically parse RDN request bodies:

```python
from fastapi import FastAPI, Depends
from rdn_fastapi import RDNRoute, get_rdn_body

app = FastAPI()
app.router.route_class = RDNRoute

@app.post("/data")
async def create_data(data=Depends(get_rdn_body)):
    return data
```

When clients send `Content-Type: application/x-rdn`, the body is parsed automatically.

### RDNMiddleware

Use `RDNMiddleware` for transparent content-type negotiation:

```python
from fastapi import FastAPI
from rdn_fastapi import RDNMiddleware

app = FastAPI()
app.add_middleware(RDNMiddleware)
```

When clients send `Accept: application/x-rdn`, JSON responses are automatically re-serialized as RDN.

## Content Type

RDN uses the media type `application/x-rdn`.

## Requirements

- Python >= 3.10
- rdn >= 0.1.0
- fastapi >= 0.100.0
