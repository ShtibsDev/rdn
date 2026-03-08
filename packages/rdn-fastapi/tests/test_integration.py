"""End-to-end integration tests for rdn-fastapi."""

from __future__ import annotations

import pytest
from fastapi import Depends, FastAPI
from fastapi.testclient import TestClient

from rdn_fastapi import RDNMiddleware, RDNResponse, RDNRoute, get_rdn_body

import rdn


@pytest.fixture
def app() -> FastAPI:
    """Create a full-featured FastAPI app with RDN integration."""
    app = FastAPI()
    app.router.route_class = RDNRoute
    app.add_middleware(RDNMiddleware)

    @app.get("/items", response_class=RDNResponse)
    async def list_items():
        return [{"id": 1, "name": "Widget"}, {"id": 2, "name": "Gadget"}]

    @app.get("/items/{item_id}", response_class=RDNResponse)
    async def get_item(item_id: int):
        return {"id": item_id, "name": "Widget"}

    @app.post("/items", response_class=RDNResponse, status_code=201)
    async def create_item(data=Depends(get_rdn_body)):
        return {"id": 3, **data}

    @app.post("/echo-json")
    async def echo_json(data: dict):
        """Endpoint returning JSON (for middleware negotiation tests)."""
        return data

    return app


@pytest.fixture
def client(app: FastAPI) -> TestClient:
    return TestClient(app)


class TestEndToEnd:
    """Full integration tests with a real FastAPI application."""

    def test_get_rdn_response(self, client: TestClient):
        """GET endpoint returns RDN response."""
        response = client.get("/items")
        assert response.status_code == 200
        assert response.headers["content-type"] == "application/x-rdn"
        parsed = rdn.loads(response.content)
        assert len(parsed) == 2
        assert parsed[0]["name"] == "Widget"
        assert parsed[1]["name"] == "Gadget"

    def test_get_rdn_response_with_path_param(self, client: TestClient):
        """GET endpoint with path parameter returns RDN response."""
        response = client.get("/items/42")
        assert response.status_code == 200
        parsed = rdn.loads(response.content)
        assert parsed == {"id": 42, "name": "Widget"}

    def test_post_rdn_body_rdn_response(self, client: TestClient):
        """POST with RDN body returns RDN response."""
        rdn_body = rdn.dumps({"name": "Thingamajig", "price": 9.99})
        response = client.post("/items", content=rdn_body, headers={"content-type": "application/x-rdn"})
        assert response.status_code == 201
        assert response.headers["content-type"] == "application/x-rdn"
        parsed = rdn.loads(response.content)
        assert parsed["id"] == 3
        assert parsed["name"] == "Thingamajig"
        assert parsed["price"] == 9.99

    def test_post_invalid_rdn_returns_400(self, client: TestClient):
        """POST with invalid RDN body returns 400."""
        response = client.post("/items", content=b"not valid rdn {{{", headers={"content-type": "application/x-rdn"})
        assert response.status_code == 400

    def test_middleware_content_negotiation(self, client: TestClient):
        """Middleware converts JSON response to RDN based on Accept header."""
        data = {"key": "value"}
        response = client.post("/echo-json", json=data, headers={"accept": "application/x-rdn"})
        assert response.status_code == 200
        assert "application/x-rdn" in response.headers["content-type"]
        parsed = rdn.loads(response.content)
        assert parsed == data

    def test_middleware_no_conversion_without_accept(self, client: TestClient):
        """Without Accept: application/x-rdn, middleware does not convert response."""
        data = {"key": "value"}
        response = client.post("/echo-json", json=data)
        assert response.status_code == 200
        assert "application/json" in response.headers["content-type"]
        assert response.json() == data

    def test_rdn_response_takes_precedence_over_middleware(self, client: TestClient):
        """RDNResponse endpoints serve RDN regardless of Accept header."""
        response = client.get("/items")
        assert response.headers["content-type"] == "application/x-rdn"
        parsed = rdn.loads(response.content)
        assert isinstance(parsed, list)

    def test_full_roundtrip(self, client: TestClient):
        """Full RDN round-trip: client sends RDN, server responds with RDN."""
        original = {"name": "RoundTrip", "count": 100}
        rdn_body = rdn.dumps(original)
        response = client.post("/items", content=rdn_body, headers={"content-type": "application/x-rdn"})
        assert response.status_code == 201
        parsed = rdn.loads(response.content)
        assert parsed["name"] == "RoundTrip"
        assert parsed["count"] == 100
        assert parsed["id"] == 3

    def test_empty_rdn_body(self, client: TestClient):
        """POST with empty RDN body is handled."""
        response = client.post("/items", content=b"", headers={"content-type": "application/x-rdn"})
        # Empty body should cause a parse error -> 400
        assert response.status_code == 400
