# Task 028: Implement Code Folding

## References
- [Tech Design](../tech-design.md) — Sections 3.10, 6.12
- [Discovery](../discovery.md)

## Description
Create `RdnFoldingBuilder.kt` implementing `FoldingBuilderEx` that creates fold regions for all collection node types: objects, arrays, tuples, maps, and sets. The placeholder text shows the node type and element count (e.g., `{...3 properties}`, `[...5 elements]`). Register in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/folding/RdnFoldingBuilder.kt` — Code folding regions
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register extension

## Implementation Details

### `RdnFoldingBuilder.kt`

```kotlin
package com.rdn.intellij.folding

import com.intellij.lang.ASTNode
import com.intellij.lang.folding.FoldingBuilderEx
import com.intellij.lang.folding.FoldingDescriptor
import com.intellij.openapi.editor.Document
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.util.PsiTreeUtil
import com.rdn.intellij.psi.*

class RdnFoldingBuilder : FoldingBuilderEx() {
    override fun buildFoldRegions(root: PsiElement, document: Document, quick: Boolean): Array<FoldingDescriptor> {
        val descriptors = mutableListOf<FoldingDescriptor>()
        collectFoldRegions(root, descriptors, document)
        return descriptors.toTypedArray()
    }

    private fun collectFoldRegions(element: PsiElement, descriptors: MutableList<FoldingDescriptor>, document: Document) {
        val node = element.node
        val elementType = node?.elementType

        when (elementType) {
            RdnElementTypes.OBJECT -> {
                val properties = PsiTreeUtil.getChildrenOfType(element, PsiElement::class.java)
                    ?.filter { it.node.elementType == RdnElementTypes.OBJECT_PROPERTY }
                val count = properties?.size ?: 0
                if (count > 0 || element.textLength > 2) {
                    val placeholder = if (count == 0) "{}" else "{...${count} ${if (count == 1) "property" else "properties"}}"
                    addFoldRegion(element, placeholder, descriptors, document)
                }
            }
            RdnElementTypes.ARRAY -> {
                val elements = element.children.size
                val placeholder = if (elements == 0) "[]" else "[...${elements} ${if (elements == 1) "element" else "elements"}]"
                addFoldRegion(element, placeholder, descriptors, document)
            }
            RdnElementTypes.TUPLE -> {
                val elements = element.children.size
                val placeholder = if (elements == 0) "()" else "(...${elements} ${if (elements == 1) "element" else "elements"})"
                addFoldRegion(element, placeholder, descriptors, document)
            }
            RdnElementTypes.MAP -> {
                val entries = PsiTreeUtil.getChildrenOfType(element, PsiElement::class.java)
                    ?.filter { it.node.elementType == RdnElementTypes.MAP_ENTRY }
                val count = entries?.size ?: 0
                val placeholder = if (count == 0) "Map{}" else "Map{...${count} ${if (count == 1) "entry" else "entries"}}"
                addFoldRegion(element, placeholder, descriptors, document)
            }
            RdnElementTypes.SET -> {
                val count = element.children.size
                val placeholder = if (count == 0) "Set{}" else "Set{...${count} ${if (count == 1) "element" else "elements"}}"
                addFoldRegion(element, placeholder, descriptors, document)
            }
            else -> {}
        }

        // Recurse into children
        element.children.forEach { collectFoldRegions(it, descriptors, document) }
    }

    private fun addFoldRegion(element: PsiElement, placeholder: String, descriptors: MutableList<FoldingDescriptor>, document: Document) {
        val range = element.textRange
        // Only create a fold region if the element spans multiple lines
        if (document.getLineNumber(range.startOffset) < document.getLineNumber(range.endOffset - 1)) {
            descriptors.add(FoldingDescriptor(element.node, range, null, placeholder))
        }
    }

    override fun getPlaceholderText(node: ASTNode): String? {
        return when (node.elementType) {
            RdnElementTypes.OBJECT -> "{...}"
            RdnElementTypes.ARRAY -> "[...]"
            RdnElementTypes.TUPLE -> "(...)"
            RdnElementTypes.MAP -> "Map{...}"
            RdnElementTypes.SET -> "Set{...}"
            else -> null
        }
    }

    override fun isCollapsedByDefault(node: ASTNode): Boolean = false
}
```

### `plugin.xml` additions

```xml
<lang.foldingBuilder
    language="RDN"
    implementationClass="com.rdn.intellij.folding.RdnFoldingBuilder"/>
```

### Design notes

- **Multi-line only:** `addFoldRegion` only creates a fold descriptor when the element spans more than one line. Single-line collections (e.g., `{"a": 1}` on a single line) are not foldable — there is nothing to collapse.
- **Placeholder text vs. descriptor text:** The `FoldingDescriptor` constructor accepts a `group` (null here) and a `placeholderText`. The placeholder passed to the constructor takes precedence over `getPlaceholderText`. Both are implemented here for robustness.
- **Implicit Map vs. explicit Map:** The fold placeholder for an implicit map (no `Map` keyword) could show `{...N entries}` instead of `Map{...N entries}`. Consider detecting implicit maps by checking for the presence of `MAP_KEYWORD` token as a child.
- **Quick mode:** When `quick=true`, IntelliJ requests a fast (possibly incomplete) folding scan for initial rendering. The implementation above is safe for quick mode as it does a simple PSI walk without heavy computation.

## Acceptance Criteria
- [ ] A multi-line `{}` object can be collapsed to `{...N properties}` using the gutter fold arrow
- [ ] A multi-line `[]` array can be collapsed to `[...N elements]`
- [ ] A multi-line `()` tuple can be collapsed to `(...N elements)`
- [ ] A multi-line `Map{...}` can be collapsed to `Map{...N entries}`
- [ ] A multi-line `Set{...}` can be collapsed to `Set{...N elements}`
- [ ] Single-line collections do NOT show fold arrows (no fold region created)
- [ ] Empty `{}` does NOT show a fold arrow
- [ ] Folding is NOT collapsed by default (`isCollapsedByDefault` returns `false`)
- [ ] The placeholder text accurately reflects the element count

## Dependencies
- Depends on: task-008, task-009
- Blocks: None
