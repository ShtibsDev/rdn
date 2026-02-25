"""FastAPI integration for RDN (Rich Data Notation).

Provides RDNResponse, RDNRoute, and RDNMiddleware for seamless
RDN content-type handling in FastAPI/Starlette applications.
"""

from rdn_fastapi.middleware import RDNMiddleware
from rdn_fastapi.response import RDNResponse
from rdn_fastapi.routing import RDNRoute, get_rdn_body

__all__ = ["RDNResponse", "RDNRoute", "RDNMiddleware", "get_rdn_body"]
