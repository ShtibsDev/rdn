"""Tests for RDNResponse."""

from __future__ import annotations

import rdn
from rdn_fastapi import RDNResponse


class TestRDNResponse:
    """Tests for the RDNResponse class."""

    def test_media_type(self):
        """RDNResponse has the correct media type."""
        assert RDNResponse.media_type == "application/x-rdn"

    def test_render_dict(self):
        """RDNResponse renders a dict as RDN bytes."""
        response = RDNResponse(content={"a": 1, "b": "hello"})
        assert response.body == b'{"a":1,"b":"hello"}'
        assert response.media_type == "application/x-rdn"

    def test_render_none(self):
        """RDNResponse renders None as empty bytes."""
        response = RDNResponse(content=None)
        assert response.body == b""

    def test_render_list(self):
        """RDNResponse renders a list as RDN bytes."""
        response = RDNResponse(content=[1, 2, 3])
        assert response.body == b"[1,2,3]"

    def test_render_string(self):
        """RDNResponse renders a string as RDN bytes."""
        response = RDNResponse(content="hello")
        assert response.body == b'"hello"'

    def test_render_number(self):
        """RDNResponse renders a number as RDN bytes."""
        response = RDNResponse(content=42)
        assert response.body == b"42"

    def test_render_boolean(self):
        """RDNResponse renders booleans as RDN bytes."""
        response = RDNResponse(content=True)
        assert response.body == b"true"

    def test_render_with_indent(self):
        """RDNResponse renders with indentation when indent is specified."""
        response = RDNResponse(content={"a": 1}, indent=2)
        assert response.body == b'{\n  "a": 1\n}'

    def test_status_code(self):
        """RDNResponse respects the status_code parameter."""
        response = RDNResponse(content={"ok": True}, status_code=201)
        assert response.status_code == 201

    def test_custom_headers(self):
        """RDNResponse passes through custom headers."""
        response = RDNResponse(content={"a": 1}, headers={"x-custom": "value"})
        assert response.headers["x-custom"] == "value"
        assert response.headers["content-type"] == "application/x-rdn"

    def test_render_nested(self):
        """RDNResponse renders nested structures."""
        data = {"users": [{"name": "Alice"}, {"name": "Bob"}]}
        response = RDNResponse(content=data)
        parsed = rdn.loads(response.body)
        assert parsed == data

    def test_render_empty_dict(self):
        """RDNResponse renders an empty dict."""
        response = RDNResponse(content={})
        assert response.body == b"{}"

    def test_render_empty_list(self):
        """RDNResponse renders an empty list."""
        response = RDNResponse(content=[])
        assert response.body == b"[]"
