"""Dispatch and lookup tables for the RDN parser and serializer."""

# ---------------------------------------------------------------------------
# Token constants for 256-entry dispatch table
# ---------------------------------------------------------------------------
TOKEN_INVALID: int = 0
TOKEN_STRING: int = 1        # "
TOKEN_NUMBER: int = 2        # 0-9
TOKEN_MINUS: int = 3         # -
TOKEN_OPEN_BRACE: int = 4    # {
TOKEN_CLOSE_BRACE: int = 5   # }
TOKEN_OPEN_BRACKET: int = 6  # [
TOKEN_CLOSE_BRACKET: int = 7 # ]
TOKEN_OPEN_PAREN: int = 8    # (
TOKEN_CLOSE_PAREN: int = 9   # )
TOKEN_COMMA: int = 10        # ,
TOKEN_COLON: int = 11        # :
TOKEN_TRUE: int = 12         # t
TOKEN_FALSE: int = 13        # f
TOKEN_NULL: int = 14         # n
TOKEN_AT: int = 15           # @
TOKEN_SLASH: int = 16        # /
TOKEN_B64: int = 17          # b
TOKEN_HEX: int = 18          # x
TOKEN_INFINITY: int = 19     # I
TOKEN_NAN: int = 20          # N
TOKEN_MAP: int = 21          # M
TOKEN_SET: int = 22          # S
TOKEN_WHITESPACE: int = 23

# ---------------------------------------------------------------------------
# TOKEN_TABLE — 256-entry list mapping ord(char) → token constant
# ---------------------------------------------------------------------------
TOKEN_TABLE: list[int] = [TOKEN_INVALID] * 256

TOKEN_TABLE[0x22] = TOKEN_STRING        # "
for _i in range(0x30, 0x3A):            # 0-9
    TOKEN_TABLE[_i] = TOKEN_NUMBER
TOKEN_TABLE[0x2D] = TOKEN_MINUS         # -
TOKEN_TABLE[0x7B] = TOKEN_OPEN_BRACE    # {
TOKEN_TABLE[0x7D] = TOKEN_CLOSE_BRACE   # }
TOKEN_TABLE[0x5B] = TOKEN_OPEN_BRACKET  # [
TOKEN_TABLE[0x5D] = TOKEN_CLOSE_BRACKET # ]
TOKEN_TABLE[0x28] = TOKEN_OPEN_PAREN    # (
TOKEN_TABLE[0x29] = TOKEN_CLOSE_PAREN   # )
TOKEN_TABLE[0x2C] = TOKEN_COMMA         # ,
TOKEN_TABLE[0x3A] = TOKEN_COLON         # :
TOKEN_TABLE[0x74] = TOKEN_TRUE          # t
TOKEN_TABLE[0x66] = TOKEN_FALSE         # f
TOKEN_TABLE[0x6E] = TOKEN_NULL          # n
TOKEN_TABLE[0x40] = TOKEN_AT            # @
TOKEN_TABLE[0x2F] = TOKEN_SLASH         # /
TOKEN_TABLE[0x62] = TOKEN_B64           # b
TOKEN_TABLE[0x78] = TOKEN_HEX           # x
TOKEN_TABLE[0x49] = TOKEN_INFINITY      # I
TOKEN_TABLE[0x4E] = TOKEN_NAN           # N
TOKEN_TABLE[0x4D] = TOKEN_MAP           # M
TOKEN_TABLE[0x53] = TOKEN_SET           # S
TOKEN_TABLE[0x20] = TOKEN_WHITESPACE    # space
TOKEN_TABLE[0x09] = TOKEN_WHITESPACE    # tab
TOKEN_TABLE[0x0A] = TOKEN_WHITESPACE    # LF
TOKEN_TABLE[0x0D] = TOKEN_WHITESPACE    # CR

# ---------------------------------------------------------------------------
# B64_DECODE — 256-entry list mapping ord(char) → 6-bit value, -1 = invalid
# ---------------------------------------------------------------------------
B64_DECODE: list[int] = [-1] * 256

_B64_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
for _i, _c in enumerate(_B64_CHARS):
    B64_DECODE[ord(_c)] = _i

# ---------------------------------------------------------------------------
# B64_ENCODE — base64 encoding charset
# ---------------------------------------------------------------------------
B64_ENCODE: str = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

# ---------------------------------------------------------------------------
# HEX_DECODE — 256-entry list mapping ord(char) → 0-15, -1 = invalid
# ---------------------------------------------------------------------------
HEX_DECODE: list[int] = [-1] * 256

for _i in range(10):                     # 0-9
    HEX_DECODE[0x30 + _i] = _i
for _i in range(6):                      # A-F
    HEX_DECODE[0x41 + _i] = 10 + _i
for _i in range(6):                      # a-f
    HEX_DECODE[0x61 + _i] = 10 + _i

# ---------------------------------------------------------------------------
# ESCAPE_TABLE — 256-entry list mapping ord(char) → escape string for the
#                serializer, or "" if no escaping is needed
# ---------------------------------------------------------------------------
ESCAPE_TABLE: list[str] = [""] * 256

ESCAPE_TABLE[0x22] = '\\"'    # "
ESCAPE_TABLE[0x5C] = "\\\\"   # \
ESCAPE_TABLE[0x08] = "\\b"    # backspace
ESCAPE_TABLE[0x09] = "\\t"    # tab
ESCAPE_TABLE[0x0A] = "\\n"    # LF
ESCAPE_TABLE[0x0C] = "\\f"    # form feed
ESCAPE_TABLE[0x0D] = "\\r"    # CR
# All control chars below 0x20 that don't have named escapes → \uXXXX
for _i in range(0x20):
    if ESCAPE_TABLE[_i] == "":
        ESCAPE_TABLE[_i] = f"\\u{_i:04x}"

# ---------------------------------------------------------------------------
# DIGIT_PAIRS — 100-entry list for fast 2-digit formatting (stringifier)
# ---------------------------------------------------------------------------
DIGIT_PAIRS: list[str] = [f"{i:02d}" for i in range(100)]

# Cleanup module-level loop variables
del _i, _c, _B64_CHARS
