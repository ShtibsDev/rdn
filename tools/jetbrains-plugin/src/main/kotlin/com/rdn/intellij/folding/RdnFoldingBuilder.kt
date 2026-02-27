package com.rdn.intellij.folding

import com.intellij.lang.ASTNode
import com.intellij.lang.folding.FoldingBuilderEx
import com.intellij.lang.folding.FoldingDescriptor
import com.intellij.openapi.editor.Document
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiElement
import com.intellij.psi.tree.IElementType
import com.rdn.intellij.psi.RdnTypes

/**
 * Code folding for RDN collection types: objects, arrays, tuples, maps, and sets.
 *
 * Only multi-line collections are foldable. The placeholder text shows the
 * collection kind and a count of its direct children (properties, entries, or elements).
 */
class RdnFoldingBuilder : FoldingBuilderEx() {

    override fun buildFoldRegions(root: PsiElement, document: Document, quick: Boolean): Array<FoldingDescriptor> {
        val descriptors = mutableListOf<FoldingDescriptor>()
        collectFoldRegions(root, descriptors, document)
        return descriptors.toTypedArray()
    }

    private fun collectFoldRegions(element: PsiElement, descriptors: MutableList<FoldingDescriptor>, document: Document) {
        val node = element.node ?: return
        val type = node.elementType

        when (type) {
            RdnTypes.OBJECT -> {
                val count = countChildrenOfType(node, RdnTypes.OBJECT_PROPERTY)
                val placeholder = if (count == 0) "{}" else "{...${count} ${pluralize("property", "properties", count)}}"
                addFoldRegion(node, placeholder, descriptors, document)
            }
            RdnTypes.ARRAY -> {
                val count = countValueChildren(node)
                val placeholder = if (count == 0) "[]" else "[...${count} ${pluralize("element", "elements", count)}]"
                addFoldRegion(node, placeholder, descriptors, document)
            }
            RdnTypes.TUPLE -> {
                val count = countValueChildren(node)
                val placeholder = if (count == 0) "()" else "(...${count} ${pluralize("element", "elements", count)})"
                addFoldRegion(node, placeholder, descriptors, document)
            }
            RdnTypes.MAP -> {
                val count = countChildrenOfType(node, RdnTypes.MAP_ENTRY)
                val isExplicit = node.text.startsWith("Map")
                val prefix = if (isExplicit) "Map" else ""
                val placeholder = if (count == 0) "${prefix}{}" else "${prefix}{...${count} ${pluralize("entry", "entries", count)}}"
                addFoldRegion(node, placeholder, descriptors, document)
            }
            RdnTypes.SET -> {
                val count = countValueChildren(node)
                val isExplicit = node.text.startsWith("Set")
                val prefix = if (isExplicit) "Set" else ""
                val placeholder = if (count == 0) "${prefix}{}" else "${prefix}{...${count} ${pluralize("element", "elements", count)}}"
                addFoldRegion(node, placeholder, descriptors, document)
            }
        }

        // Recurse into children
        var child = node.firstChildNode
        while (child != null) {
            collectFoldRegions(child.psi, descriptors, document)
            child = child.treeNext
        }
    }

    private fun addFoldRegion(node: ASTNode, placeholder: String, descriptors: MutableList<FoldingDescriptor>, document: Document) {
        val range = node.textRange
        // Only fold regions that span multiple lines
        if (document.getLineNumber(range.startOffset) < document.getLineNumber(range.endOffset - 1)) {
            descriptors.add(FoldingDescriptor(node, range, null, placeholder))
        }
    }

    private fun countChildrenOfType(node: ASTNode, type: IElementType): Int {
        var count = 0
        var child = node.firstChildNode
        while (child != null) {
            if (child.elementType == type) count++
            child = child.treeNext
        }
        return count
    }

    /**
     * Count direct value children, skipping punctuation tokens (braces, brackets,
     * parentheses, commas, keywords).
     */
    private fun countValueChildren(node: ASTNode): Int {
        var count = 0
        var child = node.firstChildNode
        while (child != null) {
            val name = child.elementType.toString()
            // Skip structural tokens and keywords — everything else is a value
            if (name != "LBRACE" && name != "RBRACE" && name != "LBRACKET" && name != "RBRACKET" && name != "LPAREN" && name != "RPAREN" && name != "COMMA" && name != "MAP_KEYWORD" && name != "SET_KEYWORD" && name != "WHITE_SPACE") {
                count++
            }
            child = child.treeNext
        }
        return count
    }

    private fun pluralize(singular: String, plural: String, count: Int): String = if (count == 1) singular else plural

    override fun getPlaceholderText(node: ASTNode): String {
        return when (node.elementType) {
            RdnTypes.OBJECT -> "{...}"
            RdnTypes.ARRAY -> "[...]"
            RdnTypes.TUPLE -> "(...)"
            RdnTypes.MAP -> "Map{...}"
            RdnTypes.SET -> "Set{...}"
            else -> "{...}"
        }
    }

    override fun isCollapsedByDefault(node: ASTNode): Boolean = false
}
