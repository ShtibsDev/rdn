"""RDNResponse -- FastAPI response class that serializes content as RDN."""

from __future__ import annotations

from typing import Any

from starlette.responses import Response

import rdn


class RDNResponse(Response):
    """FastAPI response class that serializes content as RDN.

    Usage:
        @app.get("/data", response_class=RDNResponse)
        async def get_data():
            return {"key": "value", "created": datetime.now(timezone.utc)}
    """

    media_type = "application/x-rdn"

    def __init__(self, content: Any = None, status_code: int = 200, headers: dict[str, str] | None = None, media_type: str | None = None, background: Any = None, *, indent: int | None = None) -> None:
        self.indent = indent
        super().__init__(content, status_code, headers, media_type, background)

    def render(self, content: Any) -> bytes:
        """Serialize content to RDN and encode as UTF-8 bytes."""
        if content is None:
            return b""
        return rdn.dumps(content, indent=self.indent).encode("utf-8")
