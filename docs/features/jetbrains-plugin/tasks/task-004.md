# Task 004: Implement JFlex Lexer

## References
- [Tech Design](../tech-design.md) — Sections 6.2, 7
- [Discovery](../discovery.md)

## Description
Write the JFlex grammar file `Rdn.flex` with all 5 states (YYINITIAL, STRING, REGEXP, REGEXP_CHAR_CLASS, BINARY) and all token rules per Section 7 of the tech design. Create `RdnLexerAdapter.kt` that wraps the generated Java lexer class in a `FlexAdapter`. Add the Gradle JFlex generation task to `build.gradle.kts`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/lexer/Rdn.flex` — JFlex grammar (produces `RdnFlexLexer.java`)
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/lexer/RdnLexerAdapter.kt` — FlexAdapter wrapper
- `tools/jetbrains-plugin/build.gradle.kts` — Add JFlex generation task

## Implementation Details

### `Rdn.flex`

```flex
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

%state STRING
%state REGEXP
%state REGEXP_CHAR_CLASS
%state BINARY

DIGIT       = [0-9]
HEXDIGIT    = [0-9A-Fa-f]
B64CHAR     = [A-Za-z0-9+/=]
WS          = [ \t\r\n]+
UNESCAPED   = [^\"\\\u0000-\u001F]
INTEGER     = -?(0|[1-9]{DIGIT}*)
FLOAT       = {INTEGER}(\.[0-9]+)?([eE][+-]?[0-9]+)?

%%

// ============ YYINITIAL ============
<YYINITIAL> {
    {WS}                            { return WHITE_SPACE; }

    "{"                             { return LBRACE; }
    "}"                             { return RBRACE; }
    "["                             { return LBRACKET; }
    "]"                             { return RBRACKET; }
    "("                             { return LPAREN; }
    ")"                             { return RPAREN; }
    ":"                             { return COLON; }
    ","                             { return COMMA; }
    "=>"                            { return ARROW; }

    "null"                          { return NULL; }
    "true"                          { return TRUE; }
    "false"                         { return FALSE; }
    "NaN"                           { return NAN; }
    "Infinity"                      { return INFINITY; }
    "-Infinity"                     { return NEG_INFINITY; }

    // Map/Set keywords — use lookahead to require following {
    "Map" / "{"                     { return MAP_KEYWORD; }
    "Set" / "{"                     { return SET_KEYWORD; }

    // BigInt must be tried before FLOAT/INTEGER
    {INTEGER} "n"                   { return BIGINT; }
    {FLOAT}                         { return FLOAT; }
    {INTEGER}                       { return INTEGER; }

    "@P"                            { yypushback(1); return AT_SIGN; }
    "@" {DIGIT}{4} "-"              { yypushback(yylength() - 1); return AT_SIGN; }
    "@" {DIGIT}{2} ":" {DIGIT}{2}  { yypushback(yylength() - 1); return AT_SIGN; }
    "@" {DIGIT}+                    { yypushback(yylength() - 1); return AT_SIGN; }
    "@"                             { return AT_SIGN; }

    // Duration body tokens (active after AT_SIGN emits and DURATION_P follows)
    "P"                             { return DURATION_P; }
    [YMDHS]                         { return DURATION_UNIT; }
    "T" / {DIGIT}                   { return DURATION_T; }

    // DateTime body tokens
    {DIGIT}{4} "-" {DIGIT}{2} "-" {DIGIT}{2}   { return DATE_PART; }
    "T"                             { return TIME_SEPARATOR; }
    {DIGIT}{2} ":" {DIGIT}{2} ":" {DIGIT}{2}    { return TIME_PART; }
    "." {DIGIT}{3}                  { return MILLIS_PART; }
    "Z"                             { return TIMEZONE; }
    {DIGIT}+                        { return UNIX_TIMESTAMP; }

    "b" / "\""                      { binaryKind = BinaryKind.BASE64; yybegin(BINARY); return BINARY_PREFIX; }
    "x" / "\""                      { binaryKind = BinaryKind.HEX;    yybegin(BINARY); return BINARY_PREFIX; }

    "\""                            { yybegin(STRING); return STRING_OPEN; }
    "/"                             { yybegin(REGEXP); return REGEXP_OPEN; }

    .                               { return BAD_CHARACTER; }
}

// ============ STRING ============
<STRING> {
    "\""                            { yybegin(YYINITIAL); return STRING_CLOSE; }
    \\[\"\\\/bfnrt]                 { return STRING_ESCAPE; }
    \\u {HEXDIGIT}{4}               { return STRING_ESCAPE; }
    \\.                             { return STRING_INVALID_ESCAPE; }
    [^\"\\]+                        { return STRING_CONTENT; }
}

// ============ REGEXP ============
<REGEXP> {
    "/"                             { yybegin(YYINITIAL); return REGEXP_CLOSE; }
    [dgimsuvy]+                     { return REGEXP_FLAGS; }
    "(?=" | "(?!" | "(?<=" | "(?<!" { return REGEXP_LOOKAROUND; }
    "(?<" [a-zA-Z_$][a-zA-Z0-9_$]* ">" { return REGEXP_NAMED_GROUP; }
    "(?:"                           { return REGEXP_NON_CAPTURING; }
    \\[1-9][0-9]*                   { return REGEXP_BACKREFERENCE; }
    \\k"<" [a-zA-Z_$][a-zA-Z0-9_$]* ">" { return REGEXP_BACKREFERENCE; }
    \\[dDsSwWbB]                    { return REGEXP_CHAR_CLASS_ESCAPE; }
    \\[tvnrf.\\*+?()\[\]{}|^$/]     { return REGEXP_ESCAPE; }
    \\c[A-Za-z]                     { return REGEXP_ESCAPE; }
    \\x {HEXDIGIT}{2}               { return REGEXP_ESCAPE; }
    \\u {HEXDIGIT}{4}               { return REGEXP_ESCAPE; }
    \\u"{" {HEXDIGIT}+ "}"          { return REGEXP_ESCAPE; }
    "^" | "$"                       { return REGEXP_ANCHOR; }
    "|"                             { return REGEXP_ALTERNATION; }
    "."                             { return REGEXP_DOT; }
    "("                             { return REGEXP_GROUP_OPEN; }
    ")"                             { return REGEXP_GROUP_CLOSE; }
    [+*?]                           { return REGEXP_QUANTIFIER; }
    "{" {DIGIT}+ ("," {DIGIT}*)? "}" [?+]? { return REGEXP_QUANTIFIER; }
    "["                             { yybegin(REGEXP_CHAR_CLASS); return REGEXP_CHAR_CLASS_OPEN; }
    [^/\\\[\]().|^$+*?{}]+         { return REGEXP_CONTENT; }
}

// ============ REGEXP_CHAR_CLASS ============
<REGEXP_CHAR_CLASS> {
    "]"                             { yybegin(REGEXP); return REGEXP_CHAR_CLASS_CLOSE; }
    "^"                             { return REGEXP_NEGATION; }
    "-"                             { return REGEXP_RANGE; }
    \\[dDsSwWbB]                    { return REGEXP_CHAR_CLASS_ESCAPE; }
    \\[tvnrf.\\*+?()\[\]{}|^$/]     { return REGEXP_ESCAPE; }
    \\x {HEXDIGIT}{2}               { return REGEXP_ESCAPE; }
    \\u {HEXDIGIT}{4}               { return REGEXP_ESCAPE; }
    [^\]\\-]+                       { return REGEXP_CONTENT; }
}

// ============ BINARY ============
<BINARY> {
    "\""                            { yybegin(YYINITIAL); binaryKind = null; return BINARY_CLOSE; }
    // Handled by separate rules conditioned on binaryKind in the generated code
    // For base64:
    {B64CHAR}+                      { return binaryKind == BinaryKind.BASE64 ? BINARY_CONTENT : BINARY_INVALID_CHAR; }
    [^\"A-Za-z0-9+/=]+             { return BINARY_INVALID_CHAR; }
}
```

**Note:** The JFlex grammar above is illustrative. The actual implementation needs careful attention to:
1. Mutual exclusion between BIGINT and FLOAT/INTEGER rules (JFlex uses longest match, so `{INTEGER}n` will correctly prefer BIGINT when `n` follows).
2. The `binaryKind` field must be declared in the class body using `%{...%}` in JFlex syntax.
3. The REGEXP flags rule must only match immediately after REGEXP_CLOSE (emitted when `/` is seen in REGEXP state). In practice, flags come right after the closing `/` while still in YYINITIAL — ensure the rule only matches `[dgimsuvy]+` when the previous token was `REGEXP_CLOSE`.
4. Date/time token disambiguation is the hardest part. Use JFlex lookahead (`/`) and ordering carefully. A helper method approach (calling `scanDateBody()` from the `@` rule) is acceptable.

### `RdnLexerAdapter.kt`

```kotlin
package com.rdn.intellij.lexer

import com.intellij.lexer.FlexAdapter

class RdnLexerAdapter : FlexAdapter(RdnFlexLexer(null))
```

### Gradle JFlex task in `build.gradle.kts`

```kotlin
// Add to build.gradle.kts
sourceSets {
    main {
        kotlin {
            srcDir("src/main/gen")
        }
    }
}

tasks.register<JavaExec>("generateLexer") {
    group = "build"
    description = "Generate JFlex lexer from Rdn.flex"
    classpath = configurations.detachedConfiguration(
        dependencies.create("de.jflex:jflex:1.9.1")
    )
    mainClass = "jflex.Main"
    args = listOf(
        "--skel", "src/main/resources/idea-flex.skeleton",
        "-d", "src/main/gen/com/rdn/intellij/lexer",
        "src/main/kotlin/com/rdn/intellij/lexer/Rdn.flex"
    )
}

tasks.named("compileKotlin") {
    dependsOn("generateLexer")
}
```

The IntelliJ Platform Gradle Plugin 2.x provides the `idea-flex.skeleton` automatically. Alternatively, use `org.jetbrains.grammarkit` plugin which bundles JFlex support.

## Acceptance Criteria
- [ ] `./gradlew generateLexer` produces `src/main/gen/com/rdn/intellij/lexer/RdnFlexLexer.java`
- [ ] The generated lexer compiles as part of `./gradlew compileKotlin`
- [ ] Lexing `{"key": 42}` produces: `LBRACE, STRING_OPEN, STRING_CONTENT, STRING_CLOSE, COLON, WHITE_SPACE, INTEGER, RBRACE`
- [ ] Lexing `42n` produces a single `BIGINT` token
- [ ] Lexing `/[a-z]+/gi` produces: `REGEXP_OPEN, REGEXP_CHAR_CLASS_OPEN, REGEXP_CONTENT, REGEXP_RANGE, REGEXP_CONTENT, REGEXP_CHAR_CLASS_CLOSE, REGEXP_QUANTIFIER, REGEXP_CLOSE, REGEXP_FLAGS`
- [ ] Lexing `b"SGVsbG8="` produces: `BINARY_PREFIX, BINARY_OPEN, BINARY_CONTENT, BINARY_CLOSE`
- [ ] Lexing `x"48656C6C6F"` produces: `BINARY_PREFIX, BINARY_OPEN, BINARY_CONTENT, BINARY_CLOSE`
- [ ] Lexing `@2024-01-15` produces: `AT_SIGN, DATE_PART`
- [ ] Lexing `Map{` produces: `MAP_KEYWORD, LBRACE`
- [ ] Lexing `Set{` produces: `SET_KEYWORD, LBRACE`
- [ ] Invalid escape `\q` in a string produces `STRING_INVALID_ESCAPE`

## Dependencies
- Depends on: task-001, task-002, task-003
- Blocks: task-005, task-006, task-009
