# Task 029: Implement Sort Document Keys Action

## References
- [Tech Design](../tech-design.md) — Sections 3.8, 6.9
- [Discovery](../discovery.md)

## Description
Create `SortDocumentKeysAction.kt` as an `AnAction` available in the **Tools** menu and Command Palette when a `.rdn` file is active. The action calls `RdnCstFormatter.formatSorted()` on the active editor's content and replaces the document text. If parsing fails, the action is a no-op (no error dialog shown unless in debug mode). Register in `plugin.xml` with `editorLangId == rdn` visibility condition.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/actions/SortDocumentKeysAction.kt` — Sort keys action
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register action

## Implementation Details

### `SortDocumentKeysAction.kt`

```kotlin
package com.rdn.intellij.actions

import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.command.WriteCommandAction
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.project.Project
import com.intellij.psi.PsiDocumentManager
import com.rdn.intellij.RdnLanguage
import com.rdn.intellij.formatter.RdnCstFormatter
import com.rdn.intellij.formatter.RdnFormatOptions
import com.rdn.intellij.settings.RdnSettingsState

class SortDocumentKeysAction : AnAction("Sort Document Keys") {

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val editor = e.getData(CommonDataKeys.EDITOR) ?: return
        val document = editor.document
        val psiFile = PsiDocumentManager.getInstance(project).getPsiFile(document) ?: return

        // Verify the file is an RDN file
        if (psiFile.language != RdnLanguage) return

        val settings = RdnSettingsState.getInstance(project)
        val opts = RdnFormatOptions(
            tabSize = editor.settings.getTabSize(project),
            insertSpaces = !editor.settings.isUseTabCharacter(project),
            useExplicitMapKeyword = settings.useExplicitMapKeyword,
            useExplicitSetKeyword = settings.useExplicitSetKeyword
        )

        val currentText = document.text
        val sorted = RdnCstFormatter.formatSorted(currentText, opts) ?: return  // No-op on parse failure

        if (sorted == currentText) return  // No changes needed

        WriteCommandAction.runWriteCommandAction(project, "Sort RDN Document Keys", null, {
            document.setText(sorted)
            PsiDocumentManager.getInstance(project).commitDocument(document)
        })
    }

    override fun update(e: AnActionEvent) {
        val project = e.project
        val editor = e.getData(CommonDataKeys.EDITOR)
        val psiFile = if (project != null && editor != null) {
            PsiDocumentManager.getInstance(project).getPsiFile(editor.document)
        } else null

        // Only visible/enabled when the active file is .rdn
        val isRdn = psiFile?.language == RdnLanguage
        e.presentation.isEnabledAndVisible = isRdn
    }
}
```

### `plugin.xml` additions

Register the action in the Tools menu and the editor context menu:

```xml
<actions>
    <action
        id="RDN.SortDocumentKeys"
        class="com.rdn.intellij.actions.SortDocumentKeysAction"
        text="Sort RDN Document Keys"
        description="Recursively sort all object keys alphabetically and reformat the document">
        <add-to-group group-id="ToolsMenu" anchor="last"/>
        <add-to-group group-id="EditorPopupMenu" anchor="last"/>
        <keyboard-shortcut first-keystroke="ctrl alt shift S" keymap="$default"/>
    </action>
</actions>
```

### Design notes

- **No-op on failure:** `RdnCstFormatter.formatSorted()` returns `null` on parse failure, so the action silently does nothing for invalid documents. This prevents accidental data loss.
- **WriteCommandAction:** All document modifications must be wrapped in a `WriteCommandAction` to integrate with IntelliJ's undo/redo stack. After sorting, the user can undo with Ctrl+Z to restore the original order.
- **Undo group name:** The undo action in the Edit menu will show "Sort RDN Document Keys" as the description.
- **`update()` method:** The `update` method is called by IntelliJ before showing the action in menus. If `isEnabledAndVisible = false`, the action does not appear. This ensures the action only shows for `.rdn` files.
- **Keyboard shortcut:** `Ctrl+Alt+Shift+S` is a suggestion; verify it does not conflict with existing IntelliJ shortcuts before shipping.

## Acceptance Criteria
- [ ] **Tools > Sort RDN Document Keys** action appears in the menu only when a `.rdn` file is active
- [ ] Invoking the action on `{"z": 3, "a": 1}` produces `{"a": 1, "z": 3}`
- [ ] Invoking the action on a nested object sorts all levels recursively
- [ ] After sorting, Ctrl+Z undoes the sort and restores the original text
- [ ] Invoking the action on an invalid `.rdn` file silently does nothing (no error dialog)
- [ ] Invoking the action on an already-sorted document makes no change (no spurious undo entry)
- [ ] The action is NOT visible when a non-RDN file is active
- [ ] Action respects the IDE's tab/spaces setting for indentation

## Dependencies
- Depends on: task-009, task-020, task-021
- Blocks: None
