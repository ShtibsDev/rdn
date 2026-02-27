# Task 027: Implement Bracket Matching

## References
- [Tech Design](../tech-design.md) — Sections 3.9, 6.11
- [Discovery](../discovery.md)

## Description
Create `RdnBraceMatcher.kt` implementing `PairedBraceMatcher` to define the three bracket pairs for RDN: `{}`, `[]`, and `()`. Register in `plugin.xml`. This enables IntelliJ to highlight matching brackets when the cursor is on a bracket character and to provide structural navigation between matching pairs.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/braceMatcher/RdnBraceMatcher.kt` — Bracket pair definitions
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register extension

## Implementation Details

### `RdnBraceMatcher.kt`

```kotlin
package com.rdn.intellij.braceMatcher

import com.intellij.lang.BracePair
import com.intellij.lang.PairedBraceMatcher
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.lexer.RdnTokenTypes

class RdnBraceMatcher : PairedBraceMatcher {
    companion object {
        private val PAIRS = arrayOf(
            BracePair(RdnTokenTypes.LBRACE, RdnTokenTypes.RBRACE, true),
            BracePair(RdnTokenTypes.LBRACKET, RdnTokenTypes.RBRACKET, true),
            BracePair(RdnTokenTypes.LPAREN, RdnTokenTypes.RPAREN, true)
        )
    }

    override fun getPairs(): Array<BracePair> = PAIRS

    override fun isPairedBracesAllowedBeforeType(lbraceType: IElementType, tokenType: IElementType?): Boolean = true

    override fun getCodeConstructStart(file: PsiFile, openingBraceOffset: Int): Int = openingBraceOffset
}
```

### `plugin.xml` additions

```xml
<lang.braceMatcher
    language="RDN"
    implementationClass="com.rdn.intellij.braceMatcher.RdnBraceMatcher"/>
```

### Design notes

- The third argument to `BracePair` is `structural`: when `true`, the IDE highlights the matching brace in the editor gutter and uses this pair for structure-level navigation (e.g., Ctrl+Shift+M to move between matching brackets).
- All three pairs are `structural = true` because `{}`, `[]`, and `()` all define structural scopes in RDN.
- The `Map{` and `Set{` prefixes do not require special handling at the brace matcher level: the `LBRACE` token after `Map`/`Set` is still a `LBRACE` token, so `getCodeConstructStart` will find the opening `{` correctly. The matcher pairs the `{` of `Map{...}` with its closing `}` automatically.
- `isPairedBracesAllowedBeforeType` returns `true` unconditionally because RDN has no typing context where auto-closing a bracket is unwanted.

## Acceptance Criteria
- [ ] Placing cursor on `{` in `{"key": "value"}` highlights the matching `}`
- [ ] Placing cursor on `[` in `[1, 2, 3]` highlights the matching `]`
- [ ] Placing cursor on `(` in `(1, 2, 3)` highlights the matching `)`
- [ ] The `{` in `Map{"k" => "v"}` matches the closing `}`
- [ ] The `{` in `Set{"a", "b"}` matches the closing `}`
- [ ] Ctrl+Shift+M navigates between matching brackets
- [ ] `getPairs()` returns exactly 3 pairs

## Dependencies
- Depends on: task-003
- Blocks: None
