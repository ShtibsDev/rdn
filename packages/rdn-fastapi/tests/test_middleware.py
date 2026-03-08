"""Tests for RDNMiddleware."""

from __future__ import annotations

from fastapi import FastAPI
from fastapi.testclient import TestClient

from rdn_fastapi import RDNMiddleware

import rdn


def create_app() -> FastAPI:
    """Create a test FastAPI app with RDNMiddleware."""
    app = FastAPI()
    app.add_middleware(RDNMiddleware)

    @app.get("/data")
    async def get_data():
        return {"message": "hello", "count": 42}

    @app.get("/list")
    async def get_list():
        return [1, 2, 3]

    @app.post("/echo")
    async def echo(data: dict):
        return data

    return app


class TestRDNMiddleware:
    """Tests for content negotiation middleware."""

    def test_accept_rdn_converts_json_response(self):
        """Middleware converts JSON response to RDN when Accept header is set."""
        app = create_app()
        client = TestClient(app)
        response = client.get("/data", headers={"accept": "application/x-rdn"})
        assert response.status_code == 200
        assert "application/x-rdn" in response.headers["content-type"]
        parsed = rdn.loads(response.content)
        assert parsed == {"message": "hello", "count": 42}

    def test_no_accept_rdn_passes_json(self):
        """Middleware passes through JSON when Accept header does not include RDN."""
        app = create_app()
        client = TestClient(app)
        response = client.get("/data")
        assert response.status_code == 200
        assert "application/json" in response.headers["content-type"]
        assert response.json() == {"message": "hello", "count": 42}

    def test_accept_rdn_with_list_response(self):
        """Middleware converts list JSON response to RDN."""
        app = create_app()
        client = TestClient(app)
        response = client.get("/list", headers={"accept": "application/x-rdn"})
        assert response.status_code == 200
        assert "application/x-rdn" in response.headers["content-type"]
        parsed = rdn.loads(response.content)
        assert parsed == [1, 2, 3]

    def test_preserves_status_code(self):
        """Middleware preserves the original status code."""
        app = FastAPI()
        app.add_middleware(RDNMiddleware)

        @app.get("/created", status_code=201)
        async def created():
            return {"id": 1}

        client = TestClient(app)
        response = client.get("/created", headers={"accept": "application/x-rdn"})
        assert response.status_code == 201
        parsed = rdn.loads(response.content)
        assert parsed == {"id": 1}

    def test_non_json_content_type_not_converted(self):
        """Middleware does not convert non-JSON responses even with Accept: application/x-rdn."""
        app = FastAPI()
        app.add_middleware(RDNMiddleware)

        from starlette.responses import PlainTextResponse

        @app.get("/text")
        async def text():
            return PlainTextResponse("plain text response")

        client = TestClient(app)
        response = client.get("/text", headers={"accept": "application/x-rdn"})
        assert response.status_code == 200
        assert response.text == "plain text response"

    def test_multiple_accept_types(self):
        """Middleware handles Accept header with multiple types including RDN."""
        app = create_app()
        client = TestClient(app)
        response = client.get("/data", headers={"accept": "text/html, application/x-rdn, application/json"})
        assert response.status_code == 200
        assert "application/x-rdn" in response.headers["content-type"]
        parsed = rdn.loads(response.content)
        assert parsed == {"message": "hello", "count": 42}
