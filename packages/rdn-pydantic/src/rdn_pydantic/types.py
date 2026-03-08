"""Pydantic v2 custom types for RDN (Rich Data Notation).

Each type is an ``Annotated`` wrapper that uses ``__get_pydantic_core_schema__``
to integrate with Pydantic's validation and serialization pipeline.
"""

from __future__ import annotations

import base64
import re
from datetime import datetime, time, timedelta
from typing import Annotated, Any

from pydantic import GetCoreSchemaHandler
from pydantic_core import core_schema


# ---------------------------------------------------------------------------
# BigInt
# ---------------------------------------------------------------------------

class _BigIntValidator:
    """Validates that a value is an ``int`` (Python's arbitrary-precision integer)."""

    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: Any, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @staticmethod
    def _validate(value: Any) -> int:
        if isinstance(value, bool):
            raise ValueError(f"Expected int, got {type(value).__name__}")
        if isinstance(value, int):
            return value
        if isinstance(value, str):
            try:
                return int(value)
            except (ValueError, TypeError):
                pass
        raise ValueError(f"Expected int, got {type(value).__name__}")

    @staticmethod
    def _serialize(value: int) -> int:
        return value


PydanticRDNBigInt = Annotated[int, _BigIntValidator]
"""Pydantic type for RDN BigInt values. Validates as ``int``."""


# ---------------------------------------------------------------------------
# DateTime
# ---------------------------------------------------------------------------

class _DateTimeValidator:
    """Validates ``datetime`` objects.  Also accepts ISO-format strings."""

    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: Any, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @staticmethod
    def _validate(value: Any) -> datetime:
        if isinstance(value, datetime):
            return value
        if isinstance(value, str):
            try:
                return datetime.fromisoformat(value)
            except (ValueError, TypeError):
                pass
        raise ValueError(f"Expected datetime or ISO string, got {type(value).__name__}")

    @staticmethod
    def _serialize(value: datetime) -> datetime:
        return value


PydanticRDNDateTime = Annotated[datetime, _DateTimeValidator]
"""Pydantic type for RDN DateTime values. Validates ``datetime`` or ISO string."""


# ---------------------------------------------------------------------------
# TimeOnly
# ---------------------------------------------------------------------------

class _TimeOnlyValidator:
    """Validates ``time`` objects.  Also accepts ``HH:MM:SS`` strings."""

    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: Any, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @staticmethod
    def _validate(value: Any) -> time:
        if isinstance(value, time) and not isinstance(value, datetime):
            return value
        if isinstance(value, str):
            try:
                return time.fromisoformat(value)
            except (ValueError, TypeError):
                pass
        raise ValueError(f"Expected time or time string, got {type(value).__name__}")

    @staticmethod
    def _serialize(value: time) -> time:
        return value


PydanticRDNTimeOnly = Annotated[time, _TimeOnlyValidator]
"""Pydantic type for RDN TimeOnly values. Validates ``time`` or time string."""


# ---------------------------------------------------------------------------
# Duration
# ---------------------------------------------------------------------------

class _DurationValidator:
    """Validates ``timedelta`` objects.  Also accepts numeric seconds."""

    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: Any, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @staticmethod
    def _validate(value: Any) -> timedelta:
        if isinstance(value, timedelta):
            return value
        if isinstance(value, (int, float)):
            return timedelta(seconds=value)
        raise ValueError(f"Expected timedelta or numeric seconds, got {type(value).__name__}")

    @staticmethod
    def _serialize(value: timedelta) -> timedelta:
        return value


PydanticRDNDuration = Annotated[timedelta, _DurationValidator]
"""Pydantic type for RDN Duration values. Validates ``timedelta`` or numeric seconds."""


# ---------------------------------------------------------------------------
# RegExp
# ---------------------------------------------------------------------------

class _RegExpValidator:
    """Validates compiled ``re.Pattern`` objects.  Also accepts pattern strings."""

    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: Any, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @staticmethod
    def _validate(value: Any) -> re.Pattern[str]:
        if isinstance(value, re.Pattern):
            return value
        if isinstance(value, str):
            try:
                return re.compile(value)
            except re.error as exc:
                raise ValueError(f"Invalid regex pattern: {exc}") from exc
        raise ValueError(f"Expected re.Pattern or string, got {type(value).__name__}")

    @staticmethod
    def _serialize(value: re.Pattern[str]) -> str:
        return value.pattern


PydanticRDNRegExp = Annotated[re.Pattern, _RegExpValidator]
"""Pydantic type for RDN RegExp values. Validates ``re.Pattern`` or pattern string."""


# ---------------------------------------------------------------------------
# Binary
# ---------------------------------------------------------------------------

class _BinaryValidator:
    """Validates ``bytes`` objects.  Also accepts base64-encoded strings."""

    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: Any, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @staticmethod
    def _validate(value: Any) -> bytes:
        if isinstance(value, bytes):
            return value
        if isinstance(value, str):
            try:
                return base64.b64decode(value)
            except Exception as exc:
                raise ValueError(f"Invalid base64 string: {exc}") from exc
        raise ValueError(f"Expected bytes or base64 string, got {type(value).__name__}")

    @staticmethod
    def _serialize(value: bytes) -> bytes:
        return value


PydanticRDNBinary = Annotated[bytes, _BinaryValidator]
"""Pydantic type for RDN Binary values. Validates ``bytes`` or base64 string."""


# ---------------------------------------------------------------------------
# Set
# ---------------------------------------------------------------------------

class _SetValidator:
    """Validates ``set`` objects.  Also accepts lists/tuples (converted to set)."""

    @classmethod
    def __get_pydantic_core_schema__(cls, source_type: Any, handler: GetCoreSchemaHandler) -> core_schema.CoreSchema:
        return core_schema.no_info_plain_validator_function(
            cls._validate,
            serialization=core_schema.plain_serializer_function_ser_schema(cls._serialize, info_arg=False),
        )

    @staticmethod
    def _validate(value: Any) -> set:
        if isinstance(value, set):
            return value
        if isinstance(value, frozenset):
            return set(value)
        if isinstance(value, (list, tuple)):
            return set(value)
        raise ValueError(f"Expected set, list, or tuple, got {type(value).__name__}")

    @staticmethod
    def _serialize(value: set) -> list:
        return sorted(value, key=lambda x: (type(x).__name__, x))


PydanticRDNSet = Annotated[set, _SetValidator]
"""Pydantic type for RDN Set values. Validates ``set``, ``list``, or ``tuple``."""


__all__ = [
    "PydanticRDNBigInt",
    "PydanticRDNDateTime",
    "PydanticRDNTimeOnly",
    "PydanticRDNDuration",
    "PydanticRDNRegExp",
    "PydanticRDNBinary",
    "PydanticRDNSet",
]
