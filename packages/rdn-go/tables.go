package rdn

import "fmt"

// token represents the type of the first byte of an RDN value for dispatch.
type token byte

const (
	tInvalid      token = iota
	tString             // "
	tNumber             // 0-9
	tMinus              // -
	tOpenBrace          // {
	tCloseBrace         // }
	tOpenBracket        // [
	tCloseBracket       // ]
	tOpenParen          // (
	tCloseParen         // )
	tComma              // ,
	tColon              // :
	tTrue               // t
	tFalse              // f
	tNull               // n
	tAt                 // @
	tSlash              // /
	tB64                // b
	tHex                // x
	tInfinity           // I
	tNaN                // N
	tMap                // M
	tSet                // S
	tWhitespace         // space, tab, LF, CR
)

// tokenTable is a 256-entry dispatch table: byte -> token kind.
var tokenTable [256]token

// b64Decode maps a byte to its 6-bit base64 value. 0xFF = invalid, 0xFE = padding '='.
var b64Decode [256]byte

// b64Encode is the base64 character set.
const b64Encode = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

// hexDecode maps a byte to its 0-15 hex value. 0xFF = invalid.
var hexDecode [256]byte

// escapeTable maps a byte to its escape sequence string. Empty string means no escaping needed.
var escapeTable [256]string

// digitPairs contains pre-computed two-digit strings "00" through "99".
var digitPairs [100]string

func init() {
	// Token table
	tokenTable['"'] = tString
	for i := byte('0'); i <= '9'; i++ {
		tokenTable[i] = tNumber
	}
	tokenTable['-'] = tMinus
	tokenTable['{'] = tOpenBrace
	tokenTable['}'] = tCloseBrace
	tokenTable['['] = tOpenBracket
	tokenTable[']'] = tCloseBracket
	tokenTable['('] = tOpenParen
	tokenTable[')'] = tCloseParen
	tokenTable[','] = tComma
	tokenTable[':'] = tColon
	tokenTable['t'] = tTrue
	tokenTable['f'] = tFalse
	tokenTable['n'] = tNull
	tokenTable['@'] = tAt
	tokenTable['/'] = tSlash
	tokenTable['b'] = tB64
	tokenTable['x'] = tHex
	tokenTable['I'] = tInfinity
	tokenTable['N'] = tNaN
	tokenTable['M'] = tMap
	tokenTable['S'] = tSet
	tokenTable[' '] = tWhitespace
	tokenTable['\t'] = tWhitespace
	tokenTable['\n'] = tWhitespace
	tokenTable['\r'] = tWhitespace

	// Base64 decode table
	for i := range b64Decode {
		b64Decode[i] = 0xFF
	}
	for i, c := range b64Encode {
		b64Decode[c] = byte(i)
	}
	b64Decode['='] = 0xFE

	// Hex decode table
	for i := range hexDecode {
		hexDecode[i] = 0xFF
	}
	for i := 0; i <= 9; i++ {
		hexDecode['0'+i] = byte(i)
	}
	for i := 0; i <= 5; i++ {
		hexDecode['A'+i] = byte(10 + i)
		hexDecode['a'+i] = byte(10 + i)
	}

	// Escape table
	escapeTable['"'] = `\"`
	escapeTable['\\'] = `\\`
	escapeTable['\b'] = `\b`
	escapeTable['\t'] = `\t`
	escapeTable['\n'] = `\n`
	escapeTable['\f'] = `\f`
	escapeTable['\r'] = `\r`
	for i := 0; i < 0x20; i++ {
		if escapeTable[i] == "" {
			escapeTable[i] = fmt.Sprintf(`\u%04x`, i)
		}
	}

	// Digit pairs
	for i := 0; i < 100; i++ {
		digitPairs[i] = fmt.Sprintf("%02d", i)
	}
}
