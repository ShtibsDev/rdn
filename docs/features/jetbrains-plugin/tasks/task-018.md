# Task 018: Write Completion Tests

## References
- [Tech Design](../tech-design.md) — Section 10.5
- [Discovery](../discovery.md)

## Description
Create `RdnCompletionTest.kt` using IntelliJ's `BasePlatformTestCase` framework. Tests verify `$schema` completion at correct depth, suppression at depth > 1 and when already present, keyword completions, snippet completions, and suppression inside strings.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/test/kotlin/com/rdn/intellij/completion/RdnCompletionTest.kt` — All completion tests

## Implementation Details

### `RdnCompletionTest.kt`

```kotlin
package com.rdn.intellij.completion

import com.intellij.testFramework.fixtures.BasePlatformTestCase

class RdnCompletionTest : BasePlatformTestCase() {
    override fun getTestDataPath(): String = "src/test/resources/completion"

    private fun doCompletion(fileContent: String): List<String> {
        // <caret> marks the cursor position in the file content
        myFixture.configureByText("test.rdn", fileContent)
        myFixture.completeBasic()
        return myFixture.lookupElementStrings ?: emptyList()
    }

    private fun doCompletionAndApply(fileContent: String, lookup: String): String {
        myFixture.configureByText("test.rdn", fileContent)
        myFixture.completeBasic()
        val elements = myFixture.lookupElements ?: return myFixture.editor.document.text
        val element = elements.find { it.lookupString.contains(lookup) }
            ?: error("Lookup element '$lookup' not found in ${elements.map { it.lookupString }}")
        myFixture.lookup?.currentItem = element
        myFixture.finishLookup('\n')
        return myFixture.editor.document.text
    }

    // ===== $schema Tests =====

    fun testSchemaCompletion() {
        val completions = doCompletion("{\n  \"<caret>\"\n}")
        assertTrue("\$schema not offered", completions.any { it.contains("\$schema") })
    }

    fun testSchemaCompletionInserted() {
        val result = doCompletionAndApply("{\n  \"<caret>\"\n}", "\$schema")
        assertTrue("Inserted text should contain \$schema", result.contains("\"\$schema\""))
        assertTrue("Inserted text should have empty URL quotes", result.contains("\"\$schema\": \"\""))
    }

    fun testSchemaNotOfferedAtDepth2() {
        val completions = doCompletion("{\n  \"nested\": {\n    \"<caret>\"\n  }\n}")
        assertFalse("\$schema should not be offered at depth 2", completions.any { it.contains("\$schema") })
    }

    fun testSchemaNotOfferedWhenExists() {
        val completions = doCompletion("{\n  \"\$schema\": \"http://example.com\",\n  \"<caret>\"\n}")
        assertFalse("\$schema should not be offered when it already exists", completions.any { it.contains("\$schema") })
    }

    fun testSchemaNotOfferedInArray() {
        val completions = doCompletion("{\n  \"arr\": [\"<caret>\"]\n}")
        assertFalse("\$schema should not be offered inside array", completions.any { it.contains("\$schema") })
    }

    // ===== Keyword Completion Tests =====

    fun testKeywordCompletions() {
        val completions = doCompletion("{\n  \"x\": <caret>\n}")
        val expectedKeywords = listOf("true", "false", "null", "NaN", "Infinity", "-Infinity")
        for (kw in expectedKeywords) {
            assertTrue("Keyword '$kw' not found in completions", completions.any { it == kw })
        }
    }

    fun testMapSetKeywordCompletions() {
        val completions = doCompletion("{\n  \"x\": <caret>\n}")
        assertTrue("Map keyword not offered", completions.any { it == "Map" })
        assertTrue("Set keyword not offered", completions.any { it == "Set" })
    }

    fun testAtSignCompletion() {
        val completions = doCompletion("{\n  \"x\": <caret>\n}")
        assertTrue("@ keyword not offered", completions.any { it == "@" })
    }

    fun testBinaryPrefixCompletions() {
        val completions = doCompletion("{\n  \"x\": <caret>\n}")
        assertTrue("b prefix not offered", completions.any { it == "b" })
        assertTrue("x prefix not offered", completions.any { it == "x" })
    }

    // ===== Snippet Completion Tests =====

    fun testSnippetCompletions() {
        val completions = doCompletion("{\n  \"x\": <caret>\n}")
        val expectedSnippets = listOf("Map{}", "Set{}", "()", "b\"\"", "x\"\"", "//", "0n")
        for (snippet in expectedSnippets) {
            assertTrue("Snippet '$snippet' not found", completions.any { it.contains(snippet.trim('"', '/')) })
        }
    }

    fun testMapSnippetPositionsCursor() {
        myFixture.configureByText("test.rdn", "{\n  \"m\": <caret>\n}")
        myFixture.completeBasic()
        val elements = myFixture.lookupElements ?: return
        val mapElement = elements.find { it.lookupString.contains("Map{}") }
        if (mapElement != null) {
            myFixture.lookup?.currentItem = mapElement
            myFixture.finishLookup('\n')
            val caretOffset = myFixture.editor.caretModel.offset
            val docText = myFixture.editor.document.text
            // Cursor should be inside Map{|}
            val mapIdx = docText.indexOf("Map{")
            assertTrue("Cursor should be positioned after Map{", caretOffset > mapIdx + 3)
        }
    }

    // ===== String Context Guard Tests =====

    fun testNoCompletionsInsideString() {
        val completions = doCompletion("{\n  \"key\": \"<caret>\"\n}")
        // Should not offer RDN keywords inside a string value
        assertFalse("true should not be offered inside string", completions.contains("true"))
        assertFalse("null should not be offered inside string", completions.contains("null"))
        assertFalse("NaN should not be offered inside string", completions.contains("NaN"))
    }

    fun testNoCompletionsInsideStringKey() {
        val completions = doCompletion("{\n  \"<caret>\": 1\n}")
        // Inside a key string, no RDN value completions should appear
        assertFalse("null should not be offered inside key string", completions.contains("null"))
    }

    fun testCompletionsOfferedInValuePosition() {
        // After : in an object, completions ARE offered
        val completions = doCompletion("{\n  \"key\": <caret>\n}")
        assertTrue("Completions should be offered in value position", completions.isNotEmpty())
    }

    fun testCompletionsOfferedAtTopLevel() {
        // At top-level (document root), completions are offered
        val completions = doCompletion("<caret>")
        assertTrue("Completions should be offered at top level", completions.isNotEmpty())
    }
}
```

## Acceptance Criteria
- [ ] All tests pass with `./gradlew test --tests "*RdnCompletionTest*"`
- [ ] `testSchemaCompletion` — `$schema` appears in completion list at brace depth 1
- [ ] `testSchemaNotOfferedAtDepth2` — `$schema` absent when inside nested object
- [ ] `testSchemaNotOfferedWhenExists` — `$schema` absent when already in document
- [ ] `testKeywordCompletions` — all 6 value keywords appear
- [ ] `testNoCompletionsInsideString` — `true`, `null`, `NaN` absent inside string
- [ ] `testCompletionsOfferedInValuePosition` — completion list is non-empty after `:`
- [ ] Test class extends `BasePlatformTestCase` and overrides `getTestDataPath()`

## Dependencies
- Depends on: task-017
- Blocks: None (tests are standalone)
