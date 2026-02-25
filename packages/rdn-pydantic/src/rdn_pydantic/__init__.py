"""Pydantic v2 integration for RDN (Rich Data Notation).

Provides custom annotated types for all RDN-specific value types and an
``RDNModel`` mixin with ``model_dump_rdn()`` / ``model_validate_rdn()``
methods.
"""

from __future__ import annotations

from rdn_pydantic.model import RDNModel
from rdn_pydantic.types import (
    PydanticRDNBigInt,
    PydanticRDNBinary,
    PydanticRDNDateTime,
    PydanticRDNDuration,
    PydanticRDNRegExp,
    PydanticRDNSet,
    PydanticRDNTimeOnly,
)

__all__ = [
    "RDNModel",
    "PydanticRDNBigInt",
    "PydanticRDNBinary",
    "PydanticRDNDateTime",
    "PydanticRDNDuration",
    "PydanticRDNRegExp",
    "PydanticRDNSet",
    "PydanticRDNTimeOnly",
]
