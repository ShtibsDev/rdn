# Task 008: Define PSI Element Types

## References
- [Tech Design](../tech-design.md) — Section 5.3, 6.3
- [Discovery](../discovery.md)

## Description
Create `RdnElementTypes.kt` defining all PSI (Program Structure Interface) element type constants, and `RdnFile.kt` as the `PsiFile` subclass for `.rdn` files. These types form the nodes of the PSI tree that the GrammarKit-generated parser will produce. They are consumed by the formatter, folding builder, documentation provider, and all other PSI-aware features.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/psi/RdnElementTypes.kt` — PSI element type constants
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/psi/RdnFile.kt` — PsiFile subclass

## Implementation Details

### `RdnElementTypes.kt`

Copy the full `RdnElementTypes` object exactly as defined in Section 5.3 of the tech design:

```kotlin
package com.rdn.intellij.psi

import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.IFileElementType
import com.rdn.intellij.RdnLanguage

class RdnElementType(debugName: String) : IElementType(debugName, RdnLanguage)

object RdnElementTypes {
    @JvmField val FILE = IFileElementType(RdnLanguage)

    // Value nodes
    @JvmField val STRING_LITERAL = RdnElementType("STRING_LITERAL")
    @JvmField val NUMBER_LITERAL = RdnElementType("NUMBER_LITERAL")
    @JvmField val BIGINT_LITERAL = RdnElementType("BIGINT_LITERAL")
    @JvmField val BOOLEAN_LITERAL = RdnElementType("BOOLEAN_LITERAL")
    @JvmField val NULL_LITERAL = RdnElementType("NULL_LITERAL")
    @JvmField val NAN_LITERAL = RdnElementType("NAN_LITERAL")
    @JvmField val INFINITY_LITERAL = RdnElementType("INFINITY_LITERAL")
    @JvmField val DATETIME_LITERAL = RdnElementType("DATETIME_LITERAL")
    @JvmField val TIME_ONLY_LITERAL = RdnElementType("TIME_ONLY_LITERAL")
    @JvmField val DURATION_LITERAL = RdnElementType("DURATION_LITERAL")
    @JvmField val BINARY_LITERAL = RdnElementType("BINARY_LITERAL")
    @JvmField val REGEXP_LITERAL = RdnElementType("REGEXP_LITERAL")

    // Collection nodes
    @JvmField val ARRAY = RdnElementType("ARRAY")
    @JvmField val TUPLE = RdnElementType("TUPLE")
    @JvmField val OBJECT = RdnElementType("OBJECT")
    @JvmField val OBJECT_PROPERTY = RdnElementType("OBJECT_PROPERTY")
    @JvmField val MAP = RdnElementType("MAP")
    @JvmField val MAP_ENTRY = RdnElementType("MAP_ENTRY")
    @JvmField val SET = RdnElementType("SET")

    // Key nodes
    @JvmField val OBJECT_KEY = RdnElementType("OBJECT_KEY")
}
```

### `RdnFile.kt`

```kotlin
package com.rdn.intellij.psi

import com.intellij.extapi.psi.PsiFileBase
import com.intellij.openapi.fileTypes.FileType
import com.intellij.psi.FileViewProvider
import com.rdn.intellij.RdnFileType
import com.rdn.intellij.RdnLanguage

class RdnFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, RdnLanguage) {
    override fun getFileType(): FileType = RdnFileType
    override fun toString(): String = "RDN File"
}
```

### Design notes

The PSI element types mirror the 17 CST node types used by the formatter (Section 5.5). This 1:1 mapping makes it straightforward to:
- Walk the PSI tree in `RdnFoldingBuilder` to find collection boundaries
- Extract text ranges in `RdnDocumentationProvider` for hover content
- Navigate properties in `RdnCstFormatter` when reformatting

The `IFileElementType` for `FILE` uses `RdnLanguage` directly (not a custom subclass) because IntelliJ's framework handles file element type creation internally.

### Planned PSI mixin interfaces (implemented in task-009)

After GrammarKit generates the base PSI classes, custom mixin interfaces provide typed access:

```kotlin
// Example mixin for RdnObject
interface RdnObjectMixin : PsiElement {
    fun getProperties(): List<RdnObjectProperty>
}

// Example mixin for RdnArray
interface RdnArrayMixin : PsiElement {
    fun getElements(): List<RdnValue>
}
```

These mixins are referenced in the BNF grammar via the `mixin` attribute and implemented in `src/main/kotlin/com/rdn/intellij/psi/impl/`.

## Acceptance Criteria
- [ ] `RdnElementTypes.kt` compiles without errors
- [ ] All 21 element type constants are defined (1 FILE + 12 literals + 7 collections + 1 key)
- [ ] `RdnFile` compiles and `getFileType()` returns `RdnFileType`
- [ ] `RdnElementTypes.FILE` is an `IFileElementType` (not `RdnElementType`)
- [ ] `@JvmField` annotation is present on every field
- [ ] Debug names are uppercase snake-case strings matching the constant names

## Dependencies
- Depends on: task-002
- Blocks: task-009, task-010
