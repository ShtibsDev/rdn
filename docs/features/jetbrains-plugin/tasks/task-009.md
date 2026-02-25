# Task 009: Implement GrammarKit BNF Grammar

## References
- [Tech Design](../tech-design.md) — Sections 6.3, 8
- [Discovery](../discovery.md)

## Description
Write `Rdn.bnf` per the full BNF grammar in Section 8. Run GrammarKit to generate the parser and PSI implementation classes. Implement a custom `parseBrace()` method for brace disambiguation (object vs map vs set). Create `RdnParserDefinition.kt` to wire everything together. Add the GrammarKit Gradle generation task. Register in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/parser/Rdn.bnf` — GrammarKit BNF grammar
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/parser/RdnParserDefinition.kt` — ParserDefinition
- `tools/jetbrains-plugin/build.gradle.kts` — Add GrammarKit generation task
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register parserDefinition

## Implementation Details

### `Rdn.bnf`

Full grammar as specified in Section 8:

```bnf
{
    parserClass="com.rdn.intellij.parser.RdnParser"
    extends="com.intellij.extapi.psi.ASTWrapperPsiElement"

    psiClassPrefix="Rdn"
    psiImplClassSuffix="Impl"
    psiPackage="com.rdn.intellij.psi"
    psiImplPackage="com.rdn.intellij.psi.impl"

    elementTypeHolderClass="com.rdn.intellij.psi.RdnElementTypes"
    elementTypeClass="com.rdn.intellij.psi.RdnElementType"
    tokenTypeClass="com.rdn.intellij.lexer.RdnTokenType"

    tokens = [
        LBRACE="{"
        RBRACE="}"
        LBRACKET="["
        RBRACKET="]"
        LPAREN="("
        RPAREN=")"
        COLON=":"
        COMMA=","
        ARROW="=>"
    ]
}

rdnFile ::= value

value ::= object
        | array
        | tuple
        | map
        | set
        | string_literal
        | number_literal
        | bigint_literal
        | boolean_literal
        | null_literal
        | nan_literal
        | infinity_literal
        | datetime_literal
        | time_only_literal
        | duration_literal
        | binary_literal
        | regexp_literal

// Collections
object ::= LBRACE RBRACE
          | LBRACE object_property (COMMA object_property)* RBRACE
          {pin=1}

object_property ::= object_key COLON value
          {pin=2}

object_key ::= KEY_OPEN KEY_CONTENT? KEY_ESCAPE* KEY_CLOSE
             | STRING_OPEN STRING_CONTENT? STRING_ESCAPE* STRING_CLOSE

array ::= LBRACKET RBRACKET
         | LBRACKET value (COMMA value)* RBRACKET
         {pin=1}

tuple ::= LPAREN RPAREN
         | LPAREN value (COMMA value)* RPAREN
         {pin=1}

map ::= MAP_KEYWORD LBRACE RBRACE
      | MAP_KEYWORD LBRACE map_entry (COMMA map_entry)* RBRACE
      | LBRACE map_entry (COMMA map_entry)* RBRACE

map_entry ::= value ARROW value
         {pin=2}

set ::= SET_KEYWORD LBRACE RBRACE
      | SET_KEYWORD LBRACE value (COMMA value)* RBRACE
      | LBRACE value RBRACE
      | LBRACE value (COMMA value)+ RBRACE

// Atomic literals
string_literal ::= STRING_OPEN STRING_CONTENT? (STRING_ESCAPE | STRING_INVALID_ESCAPE)* STRING_CLOSE

number_literal ::= INTEGER | FLOAT

bigint_literal ::= BIGINT

boolean_literal ::= TRUE | FALSE

null_literal ::= NULL

nan_literal ::= NAN

infinity_literal ::= INFINITY | NEG_INFINITY

datetime_literal ::= AT_SIGN (DATE_PART TIME_SEPARATOR? TIME_PART? MILLIS_PART? TIMEZONE? | UNIX_TIMESTAMP)

time_only_literal ::= AT_SIGN TIME_PART MILLIS_PART?

duration_literal ::= AT_SIGN DURATION_P (DURATION_NUMBER DURATION_UNIT)* DURATION_T? (DURATION_NUMBER DURATION_UNIT)*

binary_literal ::= BINARY_PREFIX BINARY_OPEN (BINARY_CONTENT | BINARY_INVALID_CHAR)* BINARY_CLOSE

regexp_literal ::= REGEXP_OPEN regexp_body REGEXP_CLOSE REGEXP_FLAGS?

private regexp_body ::= (REGEXP_CONTENT | REGEXP_ESCAPE | REGEXP_CHAR_CLASS_ESCAPE
                        | REGEXP_QUANTIFIER | REGEXP_ANCHOR | REGEXP_ALTERNATION | REGEXP_DOT
                        | REGEXP_GROUP_OPEN | REGEXP_GROUP_CLOSE
                        | REGEXP_LOOKAROUND | REGEXP_NAMED_GROUP | REGEXP_NON_CAPTURING
                        | REGEXP_BACKREFERENCE
                        | REGEXP_CHAR_CLASS_OPEN regexp_char_class_body REGEXP_CHAR_CLASS_CLOSE)*

private regexp_char_class_body ::= REGEXP_NEGATION? (REGEXP_CONTENT | REGEXP_ESCAPE
                                   | REGEXP_CHAR_CLASS_ESCAPE | REGEXP_RANGE)*
```

### Brace Disambiguation

As noted in Section 8, the generated grammar tries `object`, `map`, `set` alternatives in order via PEG ordered choice. For robust disambiguation, implement a custom `parseBrace()` override in the parser:

```kotlin
// In a custom parser class extending the generated RdnParser:
package com.rdn.intellij.parser

import com.intellij.lang.PsiBuilder
import com.rdn.intellij.lexer.RdnTokenTypes

class RdnParser : GeneratedRdnParser() {
    /**
     * Peeks past the first value after { to determine whether we have:
     * - Object: first token after value is COLON
     * - Map: first token after value is ARROW
     * - Set: first token after value is COMMA or RBRACE
     */
    override fun parseLight(root: IElementType, builder: PsiBuilder) {
        // Delegate to generated code; override can be applied for brace disambiguation
        super.parseLight(root, builder)
    }
}
```

The simplest approach that avoids overriding generated code: rely on the PEG ordered choice in the BNF and IntelliJ's error recovery. Test with the parser tests (task-010) to verify correctness.

### `RdnParserDefinition.kt`

```kotlin
package com.rdn.intellij.parser

import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.lexer.Lexer
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.TokenSet
import com.rdn.intellij.lexer.RdnLexerAdapter
import com.rdn.intellij.lexer.RdnTokenTypes
import com.rdn.intellij.psi.RdnElementTypes
import com.rdn.intellij.psi.RdnFile

class RdnParserDefinition : ParserDefinition {
    override fun createLexer(project: Project): Lexer = RdnLexerAdapter()

    override fun createParser(project: Project): PsiParser = RdnParser()

    override fun getFileNodeType(): IFileElementType = RdnElementTypes.FILE

    override fun getWhitespaceTokens(): TokenSet =
        TokenSet.create(RdnTokenTypes.WHITE_SPACE)

    override fun getCommentTokens(): TokenSet = TokenSet.EMPTY

    override fun getStringLiteralElements(): TokenSet =
        TokenSet.create(RdnTokenTypes.STRING_CONTENT, RdnTokenTypes.STRING_OPEN, RdnTokenTypes.STRING_CLOSE)

    override fun createElement(node: ASTNode): PsiElement =
        RdnElementTypes.Factory.createElement(node)

    override fun createFile(viewProvider: FileViewProvider): PsiFile = RdnFile(viewProvider)
}
```

### Gradle GrammarKit task in `build.gradle.kts`

```kotlin
// Add grammarkit plugin
plugins {
    // existing plugins...
    id("org.jetbrains.grammarkit") version "2022.3.2"
}

tasks.register<GenerateParserTask>("generateParser") {
    sourceFile = file("src/main/kotlin/com/rdn/intellij/parser/Rdn.bnf")
    targetRootOutputDir = file("src/main/gen")
    pathToParser = "com/rdn/intellij/parser/RdnParser.java"
    pathToPsiRoot = "com/rdn/intellij/psi"
    purgeOldFiles = true
}

tasks.named("compileKotlin") {
    dependsOn("generateParser")
}
```

### `plugin.xml` additions

```xml
<lang.parserDefinition
    language="RDN"
    implementationClass="com.rdn.intellij.parser.RdnParserDefinition"/>
```

## Acceptance Criteria
- [ ] `./gradlew generateParser` produces files in `src/main/gen/com/rdn/intellij/parser/` and `src/main/gen/com/rdn/intellij/psi/`
- [ ] `./gradlew compileKotlin` succeeds with generated code
- [ ] PSI tree for `{"key": 42}` contains: `RdnFile > RdnObject > RdnObjectProperty > (RdnObjectKey, RdnNumberLiteral)`
- [ ] PSI tree for `{"k" => "v"}` parses as `RdnMap` (not `RdnObject`)
- [ ] PSI tree for `{"a", "b"}` parses as `RdnSet`
- [ ] PSI tree for `{"a"}` parses as `RdnSet` (single-element set)
- [ ] Empty `{}` parses as `RdnObject`
- [ ] `Map{}` parses as explicit `RdnMap`
- [ ] `Set{}` parses as explicit `RdnSet`
- [ ] `ParserDefinition` returns `RdnFile` from `createFile()`

## Dependencies
- Depends on: task-001, task-003, task-004, task-008
- Blocks: task-010, task-015, task-025, task-028, task-029
