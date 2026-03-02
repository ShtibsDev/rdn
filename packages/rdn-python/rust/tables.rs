/// Dispatch and lookup tables for the RDN parser and serializer.

// ---------------------------------------------------------------------------
// Token constants for 256-entry dispatch table
// ---------------------------------------------------------------------------
pub const TOKEN_INVALID: u8 = 0;
pub const TOKEN_STRING: u8 = 1; // "
pub const TOKEN_NUMBER: u8 = 2; // 0-9
pub const TOKEN_MINUS: u8 = 3; // -
pub const TOKEN_OPEN_BRACE: u8 = 4; // {
pub const TOKEN_CLOSE_BRACE: u8 = 5; // }
pub const TOKEN_OPEN_BRACKET: u8 = 6; // [
pub const TOKEN_CLOSE_BRACKET: u8 = 7; // ]
pub const TOKEN_OPEN_PAREN: u8 = 8; // (
pub const TOKEN_CLOSE_PAREN: u8 = 9; // )
pub const TOKEN_COMMA: u8 = 10; // ,
pub const TOKEN_COLON: u8 = 11; // :
pub const TOKEN_TRUE: u8 = 12; // t
pub const TOKEN_FALSE: u8 = 13; // f
pub const TOKEN_NULL: u8 = 14; // n
pub const TOKEN_AT: u8 = 15; // @
pub const TOKEN_SLASH: u8 = 16; // /
pub const TOKEN_B64: u8 = 17; // b
pub const TOKEN_HEX: u8 = 18; // x
pub const TOKEN_INFINITY: u8 = 19; // I
pub const TOKEN_NAN: u8 = 20; // N
pub const TOKEN_MAP: u8 = 21; // M
pub const TOKEN_SET: u8 = 22; // S
pub const TOKEN_WHITESPACE: u8 = 23;

// ---------------------------------------------------------------------------
// TOKEN_TABLE — 256-entry array mapping byte → token constant
// ---------------------------------------------------------------------------
pub const TOKEN_TABLE: [u8; 256] = {
    let mut t = [TOKEN_INVALID; 256];
    t[0x22] = TOKEN_STRING; // "
    t[0x30] = TOKEN_NUMBER; // 0
    t[0x31] = TOKEN_NUMBER; // 1
    t[0x32] = TOKEN_NUMBER; // 2
    t[0x33] = TOKEN_NUMBER; // 3
    t[0x34] = TOKEN_NUMBER; // 4
    t[0x35] = TOKEN_NUMBER; // 5
    t[0x36] = TOKEN_NUMBER; // 6
    t[0x37] = TOKEN_NUMBER; // 7
    t[0x38] = TOKEN_NUMBER; // 8
    t[0x39] = TOKEN_NUMBER; // 9
    t[0x2D] = TOKEN_MINUS; // -
    t[0x7B] = TOKEN_OPEN_BRACE; // {
    t[0x7D] = TOKEN_CLOSE_BRACE; // }
    t[0x5B] = TOKEN_OPEN_BRACKET; // [
    t[0x5D] = TOKEN_CLOSE_BRACKET; // ]
    t[0x28] = TOKEN_OPEN_PAREN; // (
    t[0x29] = TOKEN_CLOSE_PAREN; // )
    t[0x2C] = TOKEN_COMMA; // ,
    t[0x3A] = TOKEN_COLON; // :
    t[0x74] = TOKEN_TRUE; // t
    t[0x66] = TOKEN_FALSE; // f
    t[0x6E] = TOKEN_NULL; // n
    t[0x40] = TOKEN_AT; // @
    t[0x2F] = TOKEN_SLASH; // /
    t[0x62] = TOKEN_B64; // b
    t[0x78] = TOKEN_HEX; // x
    t[0x49] = TOKEN_INFINITY; // I
    t[0x4E] = TOKEN_NAN; // N
    t[0x4D] = TOKEN_MAP; // M
    t[0x53] = TOKEN_SET; // S
    t[0x20] = TOKEN_WHITESPACE; // space
    t[0x09] = TOKEN_WHITESPACE; // tab
    t[0x0A] = TOKEN_WHITESPACE; // LF
    t[0x0D] = TOKEN_WHITESPACE; // CR
    t
};

// ---------------------------------------------------------------------------
// B64_DECODE — 256-entry array mapping byte → 6-bit value, 0xFF = invalid
// ---------------------------------------------------------------------------
pub const B64_DECODE: [u8; 256] = {
    let mut t = [0xFFu8; 256];
    let chars = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut i = 0;
    while i < 64 {
        t[chars[i] as usize] = i as u8;
        i += 1;
    }
    t
};

// ---------------------------------------------------------------------------
// B64_ENCODE — base64 encoding charset
// ---------------------------------------------------------------------------
pub const B64_ENCODE: &[u8; 64] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

// ---------------------------------------------------------------------------
// HEX_DECODE — 256-entry array mapping byte → 0-15, 0xFF = invalid
// ---------------------------------------------------------------------------
pub const HEX_DECODE: [u8; 256] = {
    let mut t = [0xFFu8; 256];
    t[b'0' as usize] = 0;
    t[b'1' as usize] = 1;
    t[b'2' as usize] = 2;
    t[b'3' as usize] = 3;
    t[b'4' as usize] = 4;
    t[b'5' as usize] = 5;
    t[b'6' as usize] = 6;
    t[b'7' as usize] = 7;
    t[b'8' as usize] = 8;
    t[b'9' as usize] = 9;
    t[b'A' as usize] = 10;
    t[b'B' as usize] = 11;
    t[b'C' as usize] = 12;
    t[b'D' as usize] = 13;
    t[b'E' as usize] = 14;
    t[b'F' as usize] = 15;
    t[b'a' as usize] = 10;
    t[b'b' as usize] = 11;
    t[b'c' as usize] = 12;
    t[b'd' as usize] = 13;
    t[b'e' as usize] = 14;
    t[b'f' as usize] = 15;
    t
};

// ---------------------------------------------------------------------------
// ESCAPE_TABLE — 256-entry array mapping byte → escape sequence for the
//                serializer, empty slice if no escaping is needed.
//                Each entry is a &[u8] (up to 6 bytes for \uXXXX).
// ---------------------------------------------------------------------------

/// Returns the escape sequence for a given byte, or empty if no escaping needed.
/// Control chars < 0x20 that don't have named escapes use \uXXXX.
#[inline(always)]
pub fn escape_byte(b: u8) -> &'static [u8] {
    match b {
        0x22 => b"\\\"", // "
        0x5C => b"\\\\", // \
        0x08 => b"\\b",  // backspace
        0x09 => b"\\t",  // tab
        0x0A => b"\\n",  // LF
        0x0C => b"\\f",  // form feed
        0x0D => b"\\r",  // CR
        0x00 => b"\\u0000",
        0x01 => b"\\u0001",
        0x02 => b"\\u0002",
        0x03 => b"\\u0003",
        0x04 => b"\\u0004",
        0x05 => b"\\u0005",
        0x06 => b"\\u0006",
        0x07 => b"\\u0007",
        // 0x08 handled above (backspace)
        // 0x09 handled above (tab)
        // 0x0A handled above (LF)
        0x0B => b"\\u000b",
        // 0x0C handled above (form feed)
        // 0x0D handled above (CR)
        0x0E => b"\\u000e",
        0x0F => b"\\u000f",
        0x10 => b"\\u0010",
        0x11 => b"\\u0011",
        0x12 => b"\\u0012",
        0x13 => b"\\u0013",
        0x14 => b"\\u0014",
        0x15 => b"\\u0015",
        0x16 => b"\\u0016",
        0x17 => b"\\u0017",
        0x18 => b"\\u0018",
        0x19 => b"\\u0019",
        0x1A => b"\\u001a",
        0x1B => b"\\u001b",
        0x1C => b"\\u001c",
        0x1D => b"\\u001d",
        0x1E => b"\\u001e",
        0x1F => b"\\u001f",
        _ => b"",
    }
}
