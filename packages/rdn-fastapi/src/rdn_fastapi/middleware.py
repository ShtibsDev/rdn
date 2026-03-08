"""RDNMiddleware -- ASGI middleware for transparent RDN content-type negotiation."""

from __future__ import annotations

import json
from typing import Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

import rdn


class RDNMiddleware(BaseHTTPMiddleware):
    """ASGI middleware for transparent RDN content-type negotiation.

    When the client sends Accept: application/x-rdn, the middleware
    intercepts JSON responses and re-serializes them as RDN.

    Usage:
        app = FastAPI()
        app.add_middleware(RDNMiddleware)
    """

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        response = await call_next(request)

        accept = request.headers.get("accept", "")
        if "application/x-rdn" in accept and response.headers.get("content-type", "").startswith("application/json"):
            # Consume the response body from the iterator
            body = b""
            async for chunk in response.body_iterator:
                body += chunk if isinstance(chunk, bytes) else chunk.encode("utf-8")
            data = json.loads(body)
            rdn_body = rdn.dumps(data).encode("utf-8")
            return Response(content=rdn_body, status_code=response.status_code, headers={**dict(response.headers), "content-type": "application/x-rdn"})

        return response
