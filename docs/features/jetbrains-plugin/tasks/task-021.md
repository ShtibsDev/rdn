# Task 021: Implement Formatting Model Builder

## References
- [Tech Design](../tech-design.md) — Sections 3.7, 4 (decision #6), 6.7
- [Discovery](../discovery.md)

## Description
Create `RdnFormattingModelBuilder.kt` that integrates `RdnCstFormatter` with IntelliJ's document formatting API. Create `RdnPrettierDetector.kt` for Prettier fallback detection. The builder checks if Prettier is available; if yes, delegates to Prettier via external process; otherwise uses `RdnCstFormatter`. Register both in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/formatter/RdnFormattingModelBuilder.kt` — Integrates formatter with IntelliJ API
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/formatter/RdnPrettierDetector.kt` — Prettier detection utility
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register formatter

## Implementation Details

### `RdnPrettierDetector.kt`

```kotlin
package com.rdn.intellij.formatter

import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VfsUtil
import java.io.File

object RdnPrettierDetector {
    /**
     * Returns true if both of the following are true:
     * 1. A Prettier config file exists in the project root
     *    (.prettierrc, .prettierrc.json, .prettierrc.js, prettier.config.js, or "prettier" key in package.json)
     * 2. prettier-plugin-rdn is installed in node_modules
     */
    fun isPrettierAvailable(project: Project): Boolean {
        val projectDir = project.basePath?.let { File(it) } ?: return false

        val configExists = listOf(
            ".prettierrc",
            ".prettierrc.json",
            ".prettierrc.js",
            ".prettierrc.cjs",
            ".prettierrc.yaml",
            ".prettierrc.yml",
            "prettier.config.js",
            "prettier.config.cjs"
        ).any { File(projectDir, it).exists() } || packageJsonHasPrettier(projectDir)

        if (!configExists) return false

        val pluginExists = File(projectDir, "node_modules/prettier-plugin-rdn").exists() ||
            File(projectDir, "node_modules/.pnpm").walk().any {
                it.isDirectory && it.name == "prettier-plugin-rdn"
            }

        return pluginExists
    }

    private fun packageJsonHasPrettier(projectDir: File): Boolean {
        val packageJson = File(projectDir, "package.json")
        if (!packageJson.exists()) return false
        return try {
            packageJson.readText().contains("\"prettier\"")
        } catch (e: Exception) {
            false
        }
    }

    /**
     * Runs prettier on the given text and returns the formatted result.
     * Returns null if prettier is not available or the process fails.
     */
    fun runPrettier(text: String, project: Project): String? {
        val projectDir = project.basePath ?: return null
        return try {
            val process = ProcessBuilder(
                "npx", "prettier", "--parser", "rdn", "--stdin-filepath", "input.rdn"
            )
                .directory(File(projectDir))
                .redirectErrorStream(true)
                .start()

            process.outputStream.use { it.write(text.toByteArray()) }
            val result = process.inputStream.bufferedReader().readText()
            val exitCode = process.waitFor()
            if (exitCode == 0) result else null
        } catch (e: Exception) {
            null
        }
    }
}
```

### `RdnFormattingModelBuilder.kt`

IntelliJ's `FormattingModelBuilder` API works with blocks and spacing rules, which is complex for a CST-based formatter. The recommended approach for a format-by-replacement strategy is to use `ExternalFormatProcessor` or override the `format` action via `DocumentFormattingEditProvider`. However, the cleanest integration point is:

```kotlin
package com.rdn.intellij.formatter

import com.intellij.formatting.*
import com.intellij.lang.ASTNode
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiFile
import com.intellij.psi.codeStyle.CodeStyleSettings
import com.rdn.intellij.RdnLanguage
import com.rdn.intellij.settings.RdnSettingsState

class RdnFormattingModelBuilder : FormattingModelBuilder {
    override fun createModel(formattingContext: FormattingContext): FormattingModel {
        val file = formattingContext.containingFile
        val project = file.project
        val settings = formattingContext.codeStyleSettings
        val rdnSettings = RdnSettingsState.getInstance(project)

        val tabSize = settings.getIndentSize(file.fileType)
        val insertSpaces = !settings.useTabCharacter(file.fileType)
        val opts = RdnFormatOptions(
            tabSize = tabSize,
            insertSpaces = insertSpaces,
            useExplicitMapKeyword = rdnSettings.useExplicitMapKeyword,
            useExplicitSetKeyword = rdnSettings.useExplicitSetKeyword
        )

        val text = file.text
        val formatted = if (RdnPrettierDetector.isPrettierAvailable(project)) {
            RdnPrettierDetector.runPrettier(text, project) ?: RdnCstFormatter.format(text, opts)
        } else {
            RdnCstFormatter.format(text, opts)
        }

        // Replace the entire document if formatting changed anything
        return RdnFormattingModel(file, formatted, formattingContext.codeStyleSettings)
    }
}

/**
 * Simple formatting model that replaces the entire document text.
 * Uses a dummy root block with a single child wrapping the entire file.
 */
class RdnFormattingModel(
    private val file: PsiFile,
    private val formattedText: String,
    private val settings: CodeStyleSettings
) : FormattingModel {
    override fun getRootBlock(): Block = RdnRootBlock(file)

    override fun createDocumentFormattingEditProvider(): DocumentFormattingEditProvider =
        object : DocumentFormattingEditProvider {
            override fun formatDocument(
                document: com.intellij.openapi.editor.Document,
                ranges: List<TextRange>,
                settings: CodeStyleSettings,
                keepLineBreaks: Boolean,
                keepTrailingSpaces: Boolean
            ) {
                document.setText(formattedText)
            }
        }
}
```

**Alternative approach:** Register an `ExternalFormatProcessor` which is simpler and more reliable for full-document replacement:

```kotlin
class RdnExternalFormatProcessor : ExternalFormatProcessor {
    override fun activeForFile(source: PsiFile): Boolean =
        source.language == RdnLanguage

    override fun format(source: PsiFile, range: TextRange, canChangeWhiteSpacesOnly: Boolean, keepLineBreaks: Boolean): TextRange? {
        val project = source.project
        val rdnSettings = RdnSettingsState.getInstance(project)
        val settings = CodeStyle.getSettings(source)
        val tabSize = settings.getIndentSize(source.fileType)
        val insertSpaces = !settings.useTabCharacter(source.fileType)
        val opts = RdnFormatOptions(tabSize, insertSpaces, useExplicitMapKeyword = rdnSettings.useExplicitMapKeyword, useExplicitSetKeyword = rdnSettings.useExplicitSetKeyword)

        val formatted = if (RdnPrettierDetector.isPrettierAvailable(project)) {
            RdnPrettierDetector.runPrettier(source.text, project) ?: RdnCstFormatter.format(source.text, opts)
        } else {
            RdnCstFormatter.format(source.text, opts)
        }

        val document = PsiDocumentManager.getInstance(project).getDocument(source) ?: return null
        document.setText(formatted)
        PsiDocumentManager.getInstance(project).commitDocument(document)
        return TextRange(0, formatted.length)
    }
}
```

Use `ExternalFormatProcessor` if `FormattingModelBuilder` proves difficult with the block-based API. Both are registered in `plugin.xml`:

```xml
<lang.formatter
    language="RDN"
    implementationClass="com.rdn.intellij.formatter.RdnFormattingModelBuilder"/>
<!-- OR for ExternalFormatProcessor: -->
<externalFormatProcessor
    implementationClass="com.rdn.intellij.formatter.RdnExternalFormatProcessor"/>
```

## Acceptance Criteria
- [ ] Pressing Ctrl+Alt+L (Cmd+Opt+L on macOS) on a `.rdn` file triggers formatting
- [ ] `{"a":1,"b":2}` is formatted to `{"a": 1, "b": 2}` with the editor's indent settings
- [ ] A file with long lines is expanded to multi-line format
- [ ] If a Prettier config exists and `prettier-plugin-rdn` is in `node_modules`, Prettier is used
- [ ] If Prettier is not available, `RdnCstFormatter` is used
- [ ] Formatting an invalid `.rdn` file leaves it unchanged (no destructive operation)
- [ ] Tab size and spaces/tabs preference from the IDE code style settings are respected
- [ ] `useExplicitMapKeyword` and `useExplicitSetKeyword` from RDN settings are respected

## Dependencies
- Depends on: task-020, task-026
- Blocks: task-022, task-029
