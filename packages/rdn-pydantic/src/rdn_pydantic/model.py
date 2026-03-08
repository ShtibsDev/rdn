"""RDNModel mixin for Pydantic v2 models with RDN serialization support."""

from __future__ import annotations

from typing import Any

import rdn
from pydantic import BaseModel


class RDNModel(BaseModel):
    """Base model with RDN serialization support.

    Inherit from ``RDNModel`` instead of ``BaseModel`` to gain
    ``model_dump_rdn()`` and ``model_validate_rdn()`` methods.

    Usage::

        from rdn_pydantic import RDNModel, PydanticRDNDateTime

        class User(RDNModel):
            name: str
            created: PydanticRDNDateTime
    """

    def model_dump_rdn(self, *, indent: int | None = None, exclude_none: bool = False, by_alias: bool = False) -> str:
        """Serialize the model to an RDN string.

        Uses ``model_dump()`` to get the dict representation, then
        serializes to RDN. RDN-specific types (BigInt, DateTime, etc.)
        are preserved through the serialization pipeline.

        Parameters
        ----------
        indent:
            Pretty-print indent level. ``None`` for compact output.
        exclude_none:
            When ``True``, fields with ``None`` values are omitted.
        by_alias:
            When ``True``, field aliases are used as keys.

        Returns
        -------
        str
            The RDN text representation of the model.
        """
        data = self.model_dump(mode="python", exclude_none=exclude_none, by_alias=by_alias)
        return rdn.dumps(data, indent=indent)

    @classmethod
    def model_validate_rdn(cls, rdn_data: str | bytes, *, strict: bool = False) -> Any:
        """Validate and create a model instance from an RDN string.

        Parses the RDN string, then validates the result through
        Pydantic's validation pipeline.

        Parameters
        ----------
        rdn_data:
            The RDN document to parse and validate.
        strict:
            When ``True``, strict validation is applied.

        Returns
        -------
        RDNModel
            A validated model instance.
        """
        parsed = rdn.loads(rdn_data)
        return cls.model_validate(parsed, strict=strict)
