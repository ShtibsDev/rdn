package com.rdn.intellij.lexer;

import com.intellij.lexer.FlexLexer;
import com.intellij.psi.tree.IElementType;
import static com.rdn.intellij.lexer.RdnTokenTypes.*;

%%

%class RdnFlexLexer
%implements FlexLexer
%unicode
%function advance
%type IElementType

%{
    /** Distinguishes base64 vs hex binary content */
    private enum BinaryKind { BASE64, HEX }
    private BinaryKind binaryKind = null;

    /** Tracks whether we've consumed the opening quote in BINARY state */
    private boolean binaryOpened = false;

    /** Set after REGEXP_CLOSE so we can match flags in YYINITIAL */
    private boolean expectingFlags = false;

    /** Tracks whether a date part has been seen in AT_BODY (for T disambiguation) */
    private boolean seenDatePart = false;
%}

%state STRING
%state REGEXP
%state REGEXP_CHAR_CLASS
%state BINARY
%state AT_BODY

// Character classes
DIGIT       = [0-9]
HEX_DIGIT   = [0-9A-Fa-f]
WHITE_SPACE = [ \t\n\r]+

// Number components
INT         = 0 | [1-9]{DIGIT}*
SIGNED_INT  = "-"? {INT}

// Full number patterns (used in YYINITIAL)
BIGINT      = {SIGNED_INT} "n"
// FLOAT must require either a fraction OR an exponent (never matches plain integers)
FLOAT_A     = {SIGNED_INT} "." {DIGIT}+ ([eE] [+-]? {DIGIT}+)?
FLOAT_B     = {SIGNED_INT} [eE] [+-]? {DIGIT}+
INTEGER     = {SIGNED_INT}

// Date/time components (used in AT_BODY)
DATE_BODY   = {DIGIT}{4} "-" {DIGIT}{2} "-" {DIGIT}{2}
TIME_BODY   = {DIGIT}{2} ":" {DIGIT}{2} ":" {DIGIT}{2}

// Regexp flags
REGEXP_FLAG_CHARS = [dgimsuvy]+

%%

/* =========================================================================
   YYINITIAL — top-level RDN tokens
   ========================================================================= */
<YYINITIAL> {

    /* --- Regexp flags (immediately after REGEXP_CLOSE) --- */
    {REGEXP_FLAG_CHARS} {
                        if (expectingFlags) {
                            expectingFlags = false;
                            return REGEXP_FLAGS;
                        }
                        // Not after a regexp close — this is a BAD_CHARACTER sequence.
                        // We consume one character at a time so the lexer can recover.
                        yypushback(yylength() - 1);
                        return BAD_CHARACTER;
                    }

    /* --- Whitespace --- */
    {WHITE_SPACE}   { expectingFlags = false; return WHITE_SPACE; }

    /* --- Structural tokens --- */
    "{"             { expectingFlags = false; return LBRACE; }
    "}"             { expectingFlags = false; return RBRACE; }
    "["             { expectingFlags = false; return LBRACKET; }
    "]"             { expectingFlags = false; return RBRACKET; }
    "("             { expectingFlags = false; return LPAREN; }
    ")"             { expectingFlags = false; return RPAREN; }
    ":"             { expectingFlags = false; return COLON; }
    ","             { expectingFlags = false; return COMMA; }
    "=>"            { expectingFlags = false; return ARROW; }

    /* --- Keywords --- */
    "null"          { expectingFlags = false; return NULL; }
    "true"          { expectingFlags = false; return TRUE; }
    "false"         { expectingFlags = false; return FALSE; }
    "NaN"           { expectingFlags = false; return NAN; }
    "-Infinity"     { expectingFlags = false; return NEG_INFINITY; }
    "Infinity"      { expectingFlags = false; return INFINITY; }

    /* --- Map/Set keywords (with lookahead for opening brace) --- */
    "Map" / "{"     { expectingFlags = false; return MAP_KEYWORD; }
    "Set" / "{"     { expectingFlags = false; return SET_KEYWORD; }

    /* --- Binary prefixes (with lookahead for opening quote) --- */
    "b" / "\""      { expectingFlags = false; binaryKind = BinaryKind.BASE64; binaryOpened = false; yybegin(BINARY); return BINARY_PREFIX; }
    "x" / "\""      { expectingFlags = false; binaryKind = BinaryKind.HEX;    binaryOpened = false; yybegin(BINARY); return BINARY_PREFIX; }

    /* --- @ prefix for dates, times, durations --- */
    "@"             { expectingFlags = false; seenDatePart = false; yybegin(AT_BODY); return AT_SIGN; }

    /* --- String --- */
    "\""            { expectingFlags = false; yybegin(STRING); return STRING_OPEN; }

    /* --- RegExp --- */
    "/"             { expectingFlags = false; yybegin(REGEXP); return REGEXP_OPEN; }

    /* --- Numbers: BigInt > Float > Integer (longest match + order) --- */
    {BIGINT}        { expectingFlags = false; return BIGINT; }
    {FLOAT_A}       { expectingFlags = false; return FLOAT; }
    {FLOAT_B}       { expectingFlags = false; return FLOAT; }
    {INTEGER}       { expectingFlags = false; return INTEGER; }
}


/* =========================================================================
   AT_BODY — after @ prefix, matches date/time/duration tokens
   ========================================================================= */
<AT_BODY> {

    /* --- Duration start --- */
    "P"                         { return DURATION_P; }

    /* --- Duration time separator: T followed by a digit (lookahead) --- */
    /* When seenDatePart is true, T separates date from time, not a duration marker. */
    "T" / {DIGIT}               { if (seenDatePart) return TIME_SEPARATOR; return DURATION_T; }

    /* --- Duration units --- */
    [YMDHS]                     { return DURATION_UNIT; }

    /* --- Decimal duration number (e.g., 1.5 before Y) — must precede plain digits --- */
    {DIGIT}+ "." {DIGIT}+      { return DURATION_NUMBER; }

    /* --- Date part: YYYY-MM-DD --- */
    {DATE_BODY}                 { seenDatePart = true; return DATE_PART; }

    /* --- Time separator T between date and time parts --- */
    "T"                         {
                                    if (seenDatePart) {
                                        return TIME_SEPARATOR;
                                    }
                                    // Unexpected T without date context — push back and exit
                                    yypushback(1);
                                    yybegin(YYINITIAL);
                                }

    /* --- Time part: HH:MM:SS --- */
    {TIME_BODY}                 { return TIME_PART; }

    /* --- Milliseconds: .NNN (1 to 3 fractional digits) --- */
    "." {DIGIT}{1,3}            { return MILLIS_PART; }

    /* --- Timezone Z — terminates the AT_BODY --- */
    "Z"                         { yybegin(YYINITIAL); return TIMEZONE; }

    /* --- Plain digits: duration number or unix timestamp --- */
    {DIGIT}+                    {
                                    // Peek at the character after the matched digits.
                                    // If it's a duration unit letter, this is a DURATION_NUMBER.
                                    // Otherwise it's a UNIX_TIMESTAMP (or the AT_BODY ends).
                                    if (zzMarkedPos < zzBuffer.length()) {
                                        char next = zzBuffer.charAt(zzMarkedPos);
                                        if (next == 'Y' || next == 'M' || next == 'D' ||
                                            next == 'H' || next == 'S') {
                                            return DURATION_NUMBER;
                                        }
                                    }
                                    yybegin(YYINITIAL);
                                    return UNIX_TIMESTAMP;
                                }

    /* --- Anything else: exit AT_BODY without consuming (let YYINITIAL handle it) --- */
    [^]                         { yypushback(1); yybegin(YYINITIAL); }
}


/* =========================================================================
   STRING — inside "..."
   ========================================================================= */
<STRING> {

    /* --- Closing quote --- */
    "\""                                { yybegin(YYINITIAL); return STRING_CLOSE; }

    /* --- Valid JSON escape sequences --- */
    "\\" [\"\\\/bfnrt]                  { return STRING_ESCAPE; }
    "\\" "u" {HEX_DIGIT}{4}            { return STRING_ESCAPE; }

    /* --- Invalid escape (any other backslash + char) --- */
    "\\" [^]                            { return STRING_INVALID_ESCAPE; }

    /* --- String content: any run of non-special characters --- */
    [^\"\\]+                            { return STRING_CONTENT; }
}


/* =========================================================================
   REGEXP — inside /pattern/
   ========================================================================= */
<REGEXP> {

    /* --- Closing delimiter --- */
    "/"                                                 { expectingFlags = true; yybegin(YYINITIAL); return REGEXP_CLOSE; }

    /* --- Lookaround groups (must precede generic group open) --- */
    "(?="                                               { return REGEXP_LOOKAROUND; }
    "(?!"                                               { return REGEXP_LOOKAROUND; }
    "(?<="                                              { return REGEXP_LOOKAROUND; }
    "(?<!"                                              { return REGEXP_LOOKAROUND; }

    /* --- Named group: (?<name> --- */
    "(?<" [a-zA-Z_$] [a-zA-Z0-9_$]* ">"                { return REGEXP_NAMED_GROUP; }

    /* --- Non-capturing group: (?: --- */
    "(?:"                                               { return REGEXP_NON_CAPTURING; }

    /* --- Backreferences --- */
    "\\" [1-9] [0-9]*                                   { return REGEXP_BACKREFERENCE; }
    "\\" "k<" [a-zA-Z_$] [a-zA-Z0-9_$]* ">"            { return REGEXP_BACKREFERENCE; }

    /* --- Character class escapes (before generic escape) --- */
    "\\" [dDsSwWbB]                                     { return REGEXP_CHAR_CLASS_ESCAPE; }

    /* --- Specific escape sequences --- */
    "\\" "c" [A-Za-z]                                   { return REGEXP_ESCAPE; }
    "\\" "x" {HEX_DIGIT}{2}                             { return REGEXP_ESCAPE; }
    "\\" "u" {HEX_DIGIT}{4}                             { return REGEXP_ESCAPE; }
    "\\" "u{" {HEX_DIGIT}+ "}"                          { return REGEXP_ESCAPE; }

    /* --- Any other escape (catch-all for \t, \n, \[, \{, etc.) --- */
    "\\" [^]                                            { return REGEXP_ESCAPE; }

    /* --- Anchors --- */
    "^"                                                 { return REGEXP_ANCHOR; }
    "$"                                                 { return REGEXP_ANCHOR; }

    /* --- Alternation --- */
    "|"                                                 { return REGEXP_ALTERNATION; }

    /* --- Dot (any character) --- */
    "."                                                 { return REGEXP_DOT; }

    /* --- Group open/close --- */
    "("                                                 { return REGEXP_GROUP_OPEN; }
    ")"                                                 { return REGEXP_GROUP_CLOSE; }

    /* --- Quantifiers --- */
    [+*?] [?+]?                                         { return REGEXP_QUANTIFIER; }
    "{" {DIGIT}+ ("," {DIGIT}*)? "}" [?+]?              { return REGEXP_QUANTIFIER; }

    /* --- Character class open: [ --- */
    "["                                                 { yybegin(REGEXP_CHAR_CLASS); return REGEXP_CHAR_CLASS_OPEN; }

    /* --- Regexp content: runs of non-special chars --- */
    [a-zA-Z0-9_ \t\u0080-\uffff]+                        { return REGEXP_CONTENT; }

    /* --- Single non-special char fallback --- */
    [^]                                                 { return REGEXP_CONTENT; }
}


/* =========================================================================
   REGEXP_CHAR_CLASS — inside [...]
   ========================================================================= */
<REGEXP_CHAR_CLASS> {

    /* --- Close character class --- */
    "]"                                                 { yybegin(REGEXP); return REGEXP_CHAR_CLASS_CLOSE; }

    /* --- Negation caret --- */
    "^"                                                 { return REGEXP_NEGATION; }

    /* --- Range hyphen --- */
    "-"                                                 { return REGEXP_RANGE; }

    /* --- Character class escapes --- */
    "\\" [dDsSwWbB]                                     { return REGEXP_CHAR_CLASS_ESCAPE; }

    /* --- Escape sequences --- */
    "\\" "c" [A-Za-z]                                   { return REGEXP_ESCAPE; }
    "\\" "x" {HEX_DIGIT}{2}                             { return REGEXP_ESCAPE; }
    "\\" "u" {HEX_DIGIT}{4}                             { return REGEXP_ESCAPE; }
    "\\" "u{" {HEX_DIGIT}+ "}"                          { return REGEXP_ESCAPE; }

    /* --- Any other escape (catch-all for \t, \n, \[, \{, \-, etc.) --- */
    "\\" [^]                                            { return REGEXP_ESCAPE; }

    /* --- Content: runs of non-special chars inside character class --- */
    [a-zA-Z0-9_. \t\u0080-\uffff]+                     { return REGEXP_CONTENT; }

    /* --- Single character fallback --- */
    [^]                                                 { return REGEXP_CONTENT; }
}


/* =========================================================================
   BINARY — inside b"..." or x"..."

   After BINARY_PREFIX is emitted in YYINITIAL, we enter BINARY state.
   The first " is BINARY_OPEN. Content follows. The final " is BINARY_CLOSE.
   We use a single content rule and classify characters via Java code to
   avoid overlapping patterns that could cause infinite loops.
   ========================================================================= */
<BINARY> {

    /* --- Opening/closing quote --- */
    "\""            {
                        if (!binaryOpened) {
                            binaryOpened = true;
                            return BINARY_OPEN;
                        } else {
                            binaryOpened = false;
                            binaryKind = null;
                            yybegin(YYINITIAL);
                            return BINARY_CLOSE;
                        }
                    }

    /* --- Content: match any run of non-quote, non-newline chars and classify --- */
    [^\"\n\r]+      {
                        String text = yytext().toString();
                        boolean valid;
                        if (binaryKind == BinaryKind.BASE64) {
                            valid = true;
                            for (int i = 0; i < text.length(); i++) {
                                char c = text.charAt(i);
                                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                                      (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=')) {
                                    valid = false;
                                    break;
                                }
                            }
                        } else {
                            valid = true;
                            for (int i = 0; i < text.length(); i++) {
                                char c = text.charAt(i);
                                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') ||
                                      (c >= 'a' && c <= 'f'))) {
                                    valid = false;
                                    break;
                                }
                            }
                        }
                        if (valid) {
                            return BINARY_CONTENT;
                        }
                        // Mixed valid/invalid: emit only the valid prefix as BINARY_CONTENT,
                        // or if the first char is invalid, emit one char as BINARY_INVALID_CHAR.
                        if (binaryKind == BinaryKind.BASE64) {
                            int validLen = 0;
                            for (int i = 0; i < text.length(); i++) {
                                char c = text.charAt(i);
                                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                                    (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=') {
                                    validLen++;
                                } else {
                                    break;
                                }
                            }
                            if (validLen > 0) {
                                yypushback(text.length() - validLen);
                                return BINARY_CONTENT;
                            }
                            // First char is invalid — find the run of invalid chars
                            int invalidLen = 0;
                            for (int i = 0; i < text.length(); i++) {
                                char c = text.charAt(i);
                                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                                    (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=') {
                                    break;
                                }
                                invalidLen++;
                            }
                            yypushback(text.length() - invalidLen);
                            return BINARY_INVALID_CHAR;
                        } else {
                            int validLen = 0;
                            for (int i = 0; i < text.length(); i++) {
                                char c = text.charAt(i);
                                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') ||
                                    (c >= 'a' && c <= 'f')) {
                                    validLen++;
                                } else {
                                    break;
                                }
                            }
                            if (validLen > 0) {
                                yypushback(text.length() - validLen);
                                return BINARY_CONTENT;
                            }
                            int invalidLen = 0;
                            for (int i = 0; i < text.length(); i++) {
                                char c = text.charAt(i);
                                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') ||
                                    (c >= 'a' && c <= 'f')) {
                                    break;
                                }
                                invalidLen++;
                            }
                            yypushback(text.length() - invalidLen);
                            return BINARY_INVALID_CHAR;
                        }
                    }

    /* --- Newline: recover by exiting binary state --- */
    [\n\r]          { binaryOpened = false; binaryKind = null; yybegin(YYINITIAL); return BAD_CHARACTER; }
}


/* =========================================================================
   Catch-all — any state, any character not matched above
   ========================================================================= */
[^]                 { return BAD_CHARACTER; }
