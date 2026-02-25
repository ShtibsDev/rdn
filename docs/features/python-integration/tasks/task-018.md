# Task 18: Create rdn-fastapi package

**Status:** completed
**Dependencies:** Task 16 (Task 17 optional for Pydantic integration tests)

## Description

Set up the `rdn-fastapi` package structure. Implement `RDNResponse`, `RDNRoute`, and `RDNMiddleware` for FastAPI/Starlette integration. The package works with plain dicts and `rdn.loads()`/`rdn.dumps()` without requiring Pydantic, but optionally integrates with `rdn-pydantic` when installed.

### Package Structure

```
packages/rdn-fastapi/
  pyproject.toml
  README.md
  src/
    rdn_fastapi/
      __init__.py       # Re-exports RDNResponse, RDNRoute, RDNMiddleware
      response.py       # RDNResponse class
      routing.py        # RDNRoute class
      middleware.py      # RDNMiddleware ASGI middleware
  tests/
    __init__.py
    test_response.py    # Tests for RDNResponse
    test_routing.py     # Tests for RDNRoute
    test_middleware.py   # Tests for RDNMiddleware
    test_integration.py # End-to-end FastAPI integration tests
```

### RDNResponse (`response.py`)

Custom FastAPI `Response` subclass that serializes content as RDN:

- `media_type = "application/x-rdn"`
- Constructor accepts optional `indent` parameter
- `render()` method serializes content to RDN via `rdn.dumps()` and encodes as UTF-8 bytes
- Returns empty bytes for `None` content

Usage: `@app.get("/data", response_class=RDNResponse)`

### RDNRoute (`routing.py`)

Custom `APIRoute` subclass that auto-parses RDN request bodies:

- Overrides `get_route_handler()` to wrap the original handler
- When `Content-Type: application/x-rdn`, parses body as RDN and attaches to `request._rdn_body`
- Includes a `get_rdn_body` dependency helper for extracting parsed body in route handlers

Usage: `app.router.route_class = RDNRoute`

### RDNMiddleware (`middleware.py`)

ASGI middleware for transparent RDN content-type negotiation:

- When client sends `Accept: application/x-rdn`, intercepts JSON responses and re-serializes as RDN
- When client sends `Content-Type: application/x-rdn`, the body is available for parsing
- Wraps Starlette's `BaseHTTPMiddleware`

Usage: `app.add_middleware(RDNMiddleware)`

### Integration with rdn-pydantic (optional)

When `rdn-pydantic` is installed, `RDNRoute` + `RDNResponse` work seamlessly with `RDNModel` subclasses for automatic validation and serialization. This integration is optional -- tested only when both packages are installed.

### pyproject.toml

- Name: `rdn-fastapi`, version `0.1.0`
- Dependencies: `rdn>=0.1.0`, `fastapi>=0.100.0`
- Optional dependencies: `[pydantic]` -> `rdn-pydantic>=0.1.0`
- Python >= 3.10

## Files to Create/Modify
- `packages/rdn-fastapi/pyproject.toml` (create)
- `packages/rdn-fastapi/README.md` (create)
- `packages/rdn-fastapi/src/rdn_fastapi/__init__.py` (create)
- `packages/rdn-fastapi/src/rdn_fastapi/response.py` (create)
- `packages/rdn-fastapi/src/rdn_fastapi/routing.py` (create)
- `packages/rdn-fastapi/src/rdn_fastapi/middleware.py` (create)
- `packages/rdn-fastapi/tests/__init__.py` (create)
- `packages/rdn-fastapi/tests/test_response.py` (create)
- `packages/rdn-fastapi/tests/test_routing.py` (create)
- `packages/rdn-fastapi/tests/test_middleware.py` (create)
- `packages/rdn-fastapi/tests/test_integration.py` (create)

## Acceptance Criteria
- `RDNResponse` serializes content as RDN with `Content-Type: application/x-rdn`
- `RDNResponse(content={"a": 1})` renders valid RDN bytes
- `RDNResponse(content=None)` renders empty bytes
- `RDNResponse(content=data, indent=2)` produces pretty-printed RDN
- `RDNRoute` parses RDN request bodies when `Content-Type: application/x-rdn`
- `RDNRoute` passes through non-RDN request bodies unchanged
- `RDNMiddleware` handles `Accept: application/x-rdn` content negotiation
- `RDNMiddleware` re-serializes JSON responses as RDN when requested
- Integration tests with FastAPI `TestClient` pass:
  - POST RDN body -> parsed correctly
  - GET with `Accept: application/x-rdn` -> RDN response
  - Error handling (invalid RDN body returns 400)
- `pip install -e packages/rdn-fastapi` succeeds
- All tests pass: `pytest packages/rdn-fastapi/tests -v`

## Reference
- Tech Design: `docs/features/python-integration/tech-design.md` -- Section 12, Task 18
- Tech Design: Section 6.1 (RDNResponse -- full implementation with `render()` method)
- Tech Design: Section 6.2 (RDNRoute -- `get_route_handler()` override, `get_rdn_body` dependency)
- Tech Design: Section 6.3 (RDNMiddleware -- `dispatch()` method with content negotiation)
- Tech Design: Section 6.4 (Integration with rdn-pydantic -- optional model integration)
- Tech Design: Section 2.5 (Package Structure for rdn-fastapi)
- Tech Design: Section 9.3 (pyproject.toml for rdn-fastapi)
- Tech Design: Section 8.3 (Integration Tests -- test_response, test_routing, test_middleware, test_integration coverage)
- Discovery: `docs/features/python-integration/discovery.md`
