# Task 030: Implement Markdown Injection

## References
- [Tech Design](../tech-design.md) — Sections 3.11, 4 (decision #8), 6.13
- [Discovery](../discovery.md)

## Description
Create `RdnMarkdownInjector.kt` implementing `MultiHostInjector` that detects `` ```rdn `` fenced code blocks in Markdown files and injects the RDN language for syntax highlighting and diagnostics within the block. Register in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/injection/RdnMarkdownInjector.kt` — Markdown language injection
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register extension

## Implementation Details

### `RdnMarkdownInjector.kt`

```kotlin
package com.rdn.intellij.injection

import com.intellij.lang.injection.MultiHostInjector
import com.intellij.lang.injection.MultiHostRegistrar
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiLanguageInjectionHost
import com.intellij.psi.util.PsiTreeUtil
import com.rdn.intellij.RdnLanguage

class RdnMarkdownInjector : MultiHostInjector {
    /**
     * The classes of PSI elements that may host injected fragments.
     * For Markdown, fenced code blocks are represented by different PSI types
     * depending on the Markdown plugin version. We declare Object here and
     * filter in getLanguagesToInject.
     */
    override fun elementsToInjectIn(): List<Class<out PsiElement>> =
        listOf(PsiElement::class.java)

    override fun getLanguagesToInject(registrar: MultiHostRegistrar, context: PsiElement) {
        // Only operate in Markdown files
        val file = context.containingFile ?: return
        val fileLanguage = file.language
        if (!fileLanguage.id.contains("Markdown", ignoreCase = true)) return

        // Detect fenced code blocks with "rdn" as the language identifier.
        // The Markdown PSI structure varies by Markdown plugin version.
        // Strategy: check if this element is a code fence block with info string "rdn".
        val elementType = context.node?.elementType?.toString() ?: return

        // For IntelliJ's bundled Markdown plugin (org.intellij.plugins.markdown):
        // Code fence elements have type "MARKDOWN_CODE_FENCE"
        // The info string ("rdn") is a child element.
        if (!elementType.contains("CODE_FENCE", ignoreCase = true)) return

        // Find the fence content range (between the opening ``` and closing ```)
        val fullText = context.text
        val lines = fullText.split("\n")
        if (lines.size < 2) return

        // Parse the first line to get the language identifier
        val infoLine = lines[0].trimStart('`').trim()
        if (!infoLine.equals("rdn", ignoreCase = true)) return

        // Find the content range: after the first line, before the last line (closing ```)
        val openFenceLength = lines[0].length + 1  // +1 for the \n
        val closeFenceLength = if (lines.last().startsWith("```")) lines.last().length else 0
        val contentStart = openFenceLength
        val contentEnd = fullText.length - closeFenceLength - (if (closeFenceLength > 0) 1 else 0)  // -1 for \n before ```

        if (contentEnd <= contentStart) return

        val host = context as? PsiLanguageInjectionHost ?: return
        val contentRange = com.intellij.openapi.util.TextRange(contentStart, contentEnd)

        registrar
            .startInjecting(RdnLanguage)
            .addPlace(null, null, host, contentRange)
            .doneInjecting()
    }
}
```

**Note on Markdown PSI compatibility:** The exact PSI element type names for Markdown code fences depend on which Markdown plugin is installed (the bundled `org.intellij.plugins.markdown` or a third-party plugin). Test with the actual plugin to verify the element type string. An alternative approach is to use text-based detection:

```kotlin
// Alternative: text-based detection on any element containing ```rdn
override fun elementsToInjectIn(): List<Class<out PsiElement>> =
    listOf(PsiElement::class.java)

override fun getLanguagesToInject(registrar: MultiHostRegistrar, context: PsiElement) {
    val file = context.containingFile ?: return
    if (!file.name.endsWith(".md")) return

    val text = context.text
    if (!text.contains("```rdn")) return

    // ... find and inject ranges
}
```

For the initial implementation, target the `org.intellij.plugins.markdown` bundled plugin PSI structure. Add `<depends>org.intellij.plugins.markdown</depends>` to `plugin.xml` as an optional dependency.

### `plugin.xml` additions

```xml
<depends optional="true" config-file="rdn-markdown.xml">org.intellij.plugins.markdown</depends>
```

Create `src/main/resources/META-INF/rdn-markdown.xml`:
```xml
<idea-plugin>
    <extensions defaultExtensionNs="com.intellij">
        <multiHostInjector
            implementationClass="com.rdn.intellij.injection.RdnMarkdownInjector"/>
    </extensions>
</idea-plugin>
```

Making the Markdown injection optional ensures the plugin works in IDEs where the Markdown plugin is not installed (e.g., CLion without Markdown support).

## Acceptance Criteria
- [ ] Opening a Markdown file with the following content highlights `"key"` as an RDN string key:
  ````
  ```rdn
  {"key": 42}
  ```
  ````
- [ ] RDN syntax highlighting is active inside the fenced block
- [ ] RDN diagnostics (e.g., unquoted keys) are reported inside the fenced block
- [ ] Content outside the `` ```rdn ``` `` block is not affected by RDN language injection
- [ ] Code blocks tagged with other languages (`` ```json ``) are NOT injected with RDN
- [ ] The plugin still loads correctly in IDEs without the Markdown plugin (optional dependency)
- [ ] Nested `` ``` `` inside the RDN block does not break injection

## Dependencies
- Depends on: task-002, task-009
- Blocks: None
