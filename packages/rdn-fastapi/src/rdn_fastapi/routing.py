"""RDNRoute -- Custom APIRoute that auto-parses RDN request bodies."""

from __future__ import annotations

from typing import Any, Callable

from fastapi import Request
from fastapi.routing import APIRoute
from starlette.responses import Response

import rdn


class RDNRoute(APIRoute):
    """Custom APIRoute that auto-parses RDN request bodies.

    When the incoming Content-Type is application/x-rdn, the request body
    is parsed as RDN and attached to ``request.state.rdn_body``.

    Usage:
        app = FastAPI()
        app.router.route_class = RDNRoute
    """

    def get_route_handler(self) -> Callable:
        original_handler = super().get_route_handler()

        async def rdn_handler(request: Request) -> Response:
            content_type = request.headers.get("content-type", "")
            if "application/x-rdn" in content_type:
                body = await request.body()
                try:
                    request.state.rdn_body = rdn.loads(body)
                except rdn.RDNDecodeError:
                    return Response(status_code=400, content="Invalid RDN body")
            return await original_handler(request)

        return rdn_handler


async def get_rdn_body(request: Request) -> Any:
    """Dependency for extracting parsed RDN body.

    Returns the pre-parsed RDN body if available (set by RDNRoute),
    otherwise parses the raw request body on demand.

    Usage:
        @app.post("/data")
        async def create_data(data=Depends(get_rdn_body)):
            return data
    """
    cached = getattr(request.state, "rdn_body", None)
    if cached is not None:
        return cached
    body = await request.body()
    return rdn.loads(body)
