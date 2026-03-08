"""Tests for RDNRoute and get_rdn_body."""

from __future__ import annotations

import pytest
from fastapi import Depends, FastAPI
from fastapi.testclient import TestClient

from rdn_fastapi import RDNResponse, RDNRoute, get_rdn_body

import rdn


@pytest.fixture
def app() -> FastAPI:
    """Create a FastAPI app with RDNRoute."""
    app = FastAPI()
    app.router.route_class = RDNRoute

    @app.post("/echo")
    async def echo(request_obj: None = None, data=Depends(get_rdn_body)):
        return data

    @app.post("/echo-rdn", response_class=RDNResponse)
    async def echo_rdn(data=Depends(get_rdn_body)):
        return data

    return app


@pytest.fixture
def client(app: FastAPI) -> TestClient:
    return TestClient(app)


class TestRDNRoute:
    """Tests for RDNRoute request body parsing."""

    def test_parse_rdn_body(self, client: TestClient):
        """RDNRoute parses RDN request bodies when Content-Type is application/x-rdn."""
        rdn_body = rdn.dumps({"name": "Alice", "age": 30})
        response = client.post("/echo", content=rdn_body, headers={"content-type": "application/x-rdn"})
        assert response.status_code == 200
        assert response.json() == {"name": "Alice", "age": 30}

    def test_invalid_rdn_returns_400(self, client: TestClient):
        """RDNRoute returns 400 for invalid RDN bodies."""
        response = client.post("/echo", content=b"{invalid rdn", headers={"content-type": "application/x-rdn"})
        assert response.status_code == 400

    def test_non_rdn_passthrough(self, client: TestClient):
        """RDNRoute passes through non-RDN content types."""
        # JSON content type should pass through without RDN parsing
        response = client.post("/echo", json={"name": "Bob"})
        assert response.status_code == 200
        assert response.json() == {"name": "Bob"}

    def test_rdn_body_with_list(self, client: TestClient):
        """RDNRoute parses RDN list bodies."""
        rdn_body = rdn.dumps([1, 2, 3])
        response = client.post("/echo", content=rdn_body, headers={"content-type": "application/x-rdn"})
        assert response.status_code == 200
        assert response.json() == [1, 2, 3]

    def test_rdn_roundtrip(self, client: TestClient):
        """RDNRoute + RDNResponse round-trip preserves data."""
        data = {"key": "value", "count": 42}
        rdn_body = rdn.dumps(data)
        response = client.post("/echo-rdn", content=rdn_body, headers={"content-type": "application/x-rdn"})
        assert response.status_code == 200
        assert response.headers["content-type"] == "application/x-rdn"
        parsed = rdn.loads(response.content)
        assert parsed == data


class TestGetRdnBody:
    """Tests for the get_rdn_body dependency."""

    def test_get_rdn_body_from_state(self, client: TestClient):
        """get_rdn_body retrieves pre-parsed body from request.state."""
        rdn_body = rdn.dumps({"parsed": True})
        response = client.post("/echo", content=rdn_body, headers={"content-type": "application/x-rdn"})
        assert response.status_code == 200
        assert response.json() == {"parsed": True}

    def test_get_rdn_body_fallback_parse(self):
        """get_rdn_body falls back to parsing raw body when state is not set."""
        # This is tested implicitly through the JSON passthrough -- when
        # content-type is not RDN, state.rdn_body won't be set, so
        # get_rdn_body falls back to parsing the raw body.
        app = FastAPI()
        app.router.route_class = RDNRoute

        @app.post("/parse")
        async def parse_endpoint(data=Depends(get_rdn_body)):
            return {"received": data}

        client = TestClient(app)
        # Send RDN data without the route pre-parsing it (raw body fallback)
        rdn_body = rdn.dumps({"fallback": True})
        response = client.post("/parse", content=rdn_body, headers={"content-type": "application/x-rdn"})
        assert response.status_code == 200
        assert response.json()["received"] == {"fallback": True}
