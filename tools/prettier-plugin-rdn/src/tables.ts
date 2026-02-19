// Token constants for 256-entry dispatch table
export const enum Token {
  INVALID = 0,
  STRING = 1,       // "
  NUMBER = 2,       // 0-9
  MINUS = 3,        // -
  OPEN_BRACE = 4,   // {
  CLOSE_BRACE = 5,  // }
  OPEN_BRACKET = 6, // [
  CLOSE_BRACKET = 7,// ]
  OPEN_PAREN = 8,   // (
  CLOSE_PAREN = 9,  // )
  COMMA = 10,       // ,
  COLON = 11,       // :
  TRUE = 12,        // t
  FALSE = 13,       // f
  NULL = 14,        // n
  AT = 15,          // @
  SLASH = 16,       // /
  B64 = 17,         // b
  HEX = 18,         // x
  INFINITY = 19,    // I
  NAN = 20,         // N
  MAP = 21,         // M
  SET = 22,         // S
  WHITESPACE = 23,
}

// 256-entry token dispatch table: charCode → Token
export const TOKEN_TABLE = /* @__PURE__ */ (() => {
  const t = new Uint8Array(256);
  t[0x22] = Token.STRING;       // "
  for (let i = 0x30; i <= 0x39; i++) t[i] = Token.NUMBER; // 0-9
  t[0x2D] = Token.MINUS;        // -
  t[0x7B] = Token.OPEN_BRACE;   // {
  t[0x7D] = Token.CLOSE_BRACE;  // }
  t[0x5B] = Token.OPEN_BRACKET; // [
  t[0x5D] = Token.CLOSE_BRACKET;// ]
  t[0x28] = Token.OPEN_PAREN;   // (
  t[0x29] = Token.CLOSE_PAREN;  // )
  t[0x2C] = Token.COMMA;        // ,
  t[0x3A] = Token.COLON;        // :
  t[0x74] = Token.TRUE;         // t
  t[0x66] = Token.FALSE;        // f
  t[0x6E] = Token.NULL;         // n
  t[0x40] = Token.AT;           // @
  t[0x2F] = Token.SLASH;        // /
  t[0x62] = Token.B64;          // b
  t[0x78] = Token.HEX;          // x
  t[0x49] = Token.INFINITY;     // I
  t[0x4E] = Token.NAN;          // N
  t[0x4D] = Token.MAP;          // M
  t[0x53] = Token.SET;          // S
  t[0x20] = Token.WHITESPACE;   // space
  t[0x09] = Token.WHITESPACE;   // tab
  t[0x0A] = Token.WHITESPACE;   // LF
  t[0x0D] = Token.WHITESPACE;   // CR
  return t;
})();

// String escape table for printer: charCode → escape string, or "" if no escaping needed
export const ESCAPE_TABLE: string[] = /* @__PURE__ */ (() => {
  const t: string[] = new Array(256).fill("");
  t[0x22] = '\\"';   // "
  t[0x5C] = "\\\\";  // \
  t[0x08] = "\\b";   // backspace
  t[0x09] = "\\t";   // tab
  t[0x0A] = "\\n";   // LF
  t[0x0C] = "\\f";   // form feed
  t[0x0D] = "\\r";   // CR
  // All control chars below 0x20 that don't have named escapes → \uXXXX
  for (let i = 0; i < 0x20; i++) {
    if (t[i] === "") {
      t[i] = `\\u${i.toString(16).padStart(4, "0")}`;
    }
  }
  return t;
})();
