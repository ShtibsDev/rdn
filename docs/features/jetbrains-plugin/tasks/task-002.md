# Task 002: Create Core Plugin Classes

## References
- [Tech Design](../tech-design.md) — Section 6.1
- [Discovery](../discovery.md)

## Description
Implement the three foundational Kotlin classes: `RdnLanguage` (the language singleton), `RdnFileType` (file type registration for `.rdn`), and `RdnIcons` (icon loader). Generate 16x16 and 13x13 SVG file icons from `assets/rdn-icon.svg`. Register all three in `plugin.xml`. These classes are required by virtually every other plugin component.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/RdnLanguage.kt` — Language singleton
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/RdnFileType.kt` — LanguageFileType for `.rdn`
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/RdnIcons.kt` — Icon loader constants
- `tools/jetbrains-plugin/src/main/resources/icons/rdn-file.svg` — 16x16 file icon
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Add fileType extension

## Implementation Details

### `RdnLanguage.kt`

```kotlin
package com.rdn.intellij

import com.intellij.lang.Language

object RdnLanguage : Language("RDN") {
    private fun readResolve(): Any = RdnLanguage
}
```

### `RdnFileType.kt`

```kotlin
package com.rdn.intellij

import com.intellij.openapi.fileTypes.LanguageFileType
import javax.swing.Icon

object RdnFileType : LanguageFileType(RdnLanguage) {
    override fun getName(): String = "RDN File"
    override fun getDescription(): String = "Rich Data Notation file"
    override fun getDefaultExtension(): String = "rdn"
    override fun getIcon(): Icon = RdnIcons.FILE
}
```

### `RdnIcons.kt`

```kotlin
package com.rdn.intellij

import com.intellij.openapi.util.IconLoader

object RdnIcons {
    @JvmField val FILE = IconLoader.getIcon("/icons/rdn-file.svg", RdnIcons::class.java)
    @JvmField val PLUGIN = IconLoader.getIcon("/icons/rdn-plugin.svg", RdnIcons::class.java)
}
```

### `plugin.xml` additions

```xml
<extensions defaultExtensionNs="com.intellij">
    <fileType
        name="RDN File"
        implementationClass="com.rdn.intellij.RdnFileType"
        fieldName="INSTANCE"
        language="RDN"
        extensions="rdn"/>
</extensions>
```

### Icon generation

Generate `rdn-file.svg` (16x16) and `rdn-plugin.svg` (40x40) from `assets/rdn-icon.svg` in the root repo. The icons should be placed at `src/main/resources/icons/`. IntelliJ loads SVG icons natively; no PNG fallbacks are required for IntelliJ 2024.3+.

For dark theme variants, add `rdn-file_dark.svg` alongside `rdn-file.svg` — IntelliJ automatically selects the `_dark` variant in dark themes.

## Acceptance Criteria
- [ ] `.rdn` files display the RDN icon in the project tree
- [ ] Opening a `.rdn` file shows "RDN" in the IDE status bar language indicator
- [ ] `RdnFileType.getInstance()` returns the singleton (verifiable via `FileTypeManager.getInstance().getFileTypeByExtension("rdn")`)
- [ ] `RdnLanguage.INSTANCE.getID()` returns `"RDN"`
- [ ] Icons load without `NullPointerException` (icon file exists at the classpath path)
- [ ] Dark theme variant icon exists at `/icons/rdn-file_dark.svg`

## Dependencies
- Depends on: task-001
- Blocks: task-003, task-004, task-006, task-007, task-008, task-009
