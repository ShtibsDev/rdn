# Task 026: Implement Settings State and Configurable

## References
- [Tech Design](../tech-design.md) — Sections 3.12, 4 (decision #7), 5.4, 9
- [Discovery](../discovery.md)

## Description
Create `RdnSettingsState.kt` (a `PersistentStateComponent` storing all 19 settings in `.idea/rdn.xml`) and `RdnSettingsConfigurable.kt` (the UI panel for **Settings > Languages & Frameworks > RDN**). The UI is organized into collapsible sections matching the settings schema in Section 9. Register both in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/settings/RdnSettingsState.kt` — Settings persistence
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/settings/RdnSettingsConfigurable.kt` — Settings UI
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register both extensions

## Implementation Details

### `RdnSettingsState.kt`

Copy the full `RdnSettingsState` class from Section 5.4 of the tech design:

```kotlin
package com.rdn.intellij.settings

import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.project.Project
import com.intellij.util.xmlb.XmlSerializerUtil

@Service(Service.Level.PROJECT)
@State(name = "RdnSettings", storages = [Storage("rdn.xml")])
class RdnSettingsState : PersistentStateComponent<RdnSettingsState> {
    // Formatting
    var useExplicitMapKeyword: Boolean = false
    var useExplicitSetKeyword: Boolean = false

    // Hover: master toggle
    var hoverEnabled: Boolean = true

    // Hover: category toggles
    var hoverDateTimeEnabled: Boolean = true
    var hoverTimeOnlyEnabled: Boolean = true
    var hoverDurationEnabled: Boolean = true
    var hoverBigintEnabled: Boolean = true
    var hoverBinaryEnabled: Boolean = true
    var hoverRegexpEnabled: Boolean = true
    var hoverSpecialNumbersEnabled: Boolean = true
    var hoverCollectionsEnabled: Boolean = true
    var hoverDiagnosticsEnabled: Boolean = true

    // Hover: format strings
    var hoverDateTimeFullFormat: String = "YYYY-MM-DD HH:mm:ss.SSS [UTC]"
    var hoverDateTimeDateOnlyFormat: String = "MMMM D, YYYY"
    var hoverDateTimeNoMillisFormat: String = "YYYY-MM-DD HH:mm:ss [UTC]"
    var hoverDateTimeUnixFormat: String = "YYYY-MM-DD HH:mm:ss [UTC]"
    var hoverTimeOnlyFormat: String = "HH:mm:ss"

    // Hover: detail toggles
    var hoverBigintShowBitLength: Boolean = true
    var hoverBinaryShowPreview: Boolean = true

    override fun getState(): RdnSettingsState = this

    override fun loadState(state: RdnSettingsState) {
        XmlSerializerUtil.copyBean(state, this)
    }

    companion object {
        fun getInstance(project: Project): RdnSettingsState =
            project.getService(RdnSettingsState::class.java)
    }
}
```

### `RdnSettingsConfigurable.kt`

```kotlin
package com.rdn.intellij.settings

import com.intellij.openapi.options.Configurable
import com.intellij.openapi.project.Project
import com.intellij.ui.components.JBCheckBox
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBTextField
import com.intellij.util.ui.FormBuilder
import javax.swing.JComponent
import javax.swing.JPanel

class RdnSettingsConfigurable(private val project: Project) : Configurable {
    private val state get() = RdnSettingsState.getInstance(project)

    // Formatting
    private val useExplicitMapKeyword = JBCheckBox("Keep explicit Map keyword on non-empty maps")
    private val useExplicitSetKeyword = JBCheckBox("Keep explicit Set keyword on non-empty sets")

    // Hover master
    private val hoverEnabled = JBCheckBox("Enable hover information")

    // DateTime
    private val hoverDateTimeEnabled = JBCheckBox("Enable DateTime hover")
    private val hoverDateTimeFullFormat = JBTextField(30)
    private val hoverDateTimeDateOnlyFormat = JBTextField(30)
    private val hoverDateTimeNoMillisFormat = JBTextField(30)
    private val hoverDateTimeUnixFormat = JBTextField(30)

    // TimeOnly
    private val hoverTimeOnlyEnabled = JBCheckBox("Enable TimeOnly hover")
    private val hoverTimeOnlyFormat = JBTextField(30)

    // Category toggles
    private val hoverDurationEnabled = JBCheckBox("Enable Duration hover")
    private val hoverBigintEnabled = JBCheckBox("Enable BigInt hover")
    private val hoverBigintShowBitLength = JBCheckBox("Show bit length in BigInt hover")
    private val hoverBinaryEnabled = JBCheckBox("Enable Binary hover")
    private val hoverBinaryShowPreview = JBCheckBox("Show ASCII preview of binary data")
    private val hoverRegexpEnabled = JBCheckBox("Enable RegExp hover")
    private val hoverSpecialNumbersEnabled = JBCheckBox("Enable NaN/Infinity hover")
    private val hoverCollectionsEnabled = JBCheckBox("Enable Map/Set/Tuple hover")
    private val hoverDiagnosticsEnabled = JBCheckBox("Show diagnostic hints in hover")

    override fun getDisplayName(): String = "RDN"

    override fun createComponent(): JComponent {
        return FormBuilder.createFormBuilder()
            .addSeparator()
            .addComponent(JBLabel("<html><b>Formatting</b></html>"))
            .addComponent(useExplicitMapKeyword)
            .addComponent(useExplicitSetKeyword)
            .addSeparator()
            .addComponent(JBLabel("<html><b>Hover Information</b></html>"))
            .addComponent(hoverEnabled)
            .addSeparator()
            .addComponent(JBLabel("<html><b>DateTime</b></html>"))
            .addComponent(hoverDateTimeEnabled)
            .addLabeledComponent("Full format:", hoverDateTimeFullFormat)
            .addLabeledComponent("Date-only format:", hoverDateTimeDateOnlyFormat)
            .addLabeledComponent("No-millis format:", hoverDateTimeNoMillisFormat)
            .addLabeledComponent("Unix format:", hoverDateTimeUnixFormat)
            .addSeparator()
            .addComponent(JBLabel("<html><b>TimeOnly</b></html>"))
            .addComponent(hoverTimeOnlyEnabled)
            .addLabeledComponent("Format:", hoverTimeOnlyFormat)
            .addSeparator()
            .addComponent(JBLabel("<html><b>Other Categories</b></html>"))
            .addComponent(hoverDurationEnabled)
            .addComponent(hoverBigintEnabled)
            .addComponent(hoverBigintShowBitLength)
            .addComponent(hoverBinaryEnabled)
            .addComponent(hoverBinaryShowPreview)
            .addComponent(hoverRegexpEnabled)
            .addComponent(hoverSpecialNumbersEnabled)
            .addComponent(hoverCollectionsEnabled)
            .addComponent(hoverDiagnosticsEnabled)
            .addComponentFillVertically(JPanel(), 0)
            .panel
    }

    override fun isModified(): Boolean {
        val s = state
        return useExplicitMapKeyword.isSelected != s.useExplicitMapKeyword ||
            useExplicitSetKeyword.isSelected != s.useExplicitSetKeyword ||
            hoverEnabled.isSelected != s.hoverEnabled ||
            hoverDateTimeEnabled.isSelected != s.hoverDateTimeEnabled ||
            hoverDateTimeFullFormat.text != s.hoverDateTimeFullFormat ||
            hoverDateTimeDateOnlyFormat.text != s.hoverDateTimeDateOnlyFormat ||
            hoverDateTimeNoMillisFormat.text != s.hoverDateTimeNoMillisFormat ||
            hoverDateTimeUnixFormat.text != s.hoverDateTimeUnixFormat ||
            hoverTimeOnlyEnabled.isSelected != s.hoverTimeOnlyEnabled ||
            hoverTimeOnlyFormat.text != s.hoverTimeOnlyFormat ||
            hoverDurationEnabled.isSelected != s.hoverDurationEnabled ||
            hoverBigintEnabled.isSelected != s.hoverBigintEnabled ||
            hoverBigintShowBitLength.isSelected != s.hoverBigintShowBitLength ||
            hoverBinaryEnabled.isSelected != s.hoverBinaryEnabled ||
            hoverBinaryShowPreview.isSelected != s.hoverBinaryShowPreview ||
            hoverRegexpEnabled.isSelected != s.hoverRegexpEnabled ||
            hoverSpecialNumbersEnabled.isSelected != s.hoverSpecialNumbersEnabled ||
            hoverCollectionsEnabled.isSelected != s.hoverCollectionsEnabled ||
            hoverDiagnosticsEnabled.isSelected != s.hoverDiagnosticsEnabled
    }

    override fun apply() {
        val s = state
        s.useExplicitMapKeyword = useExplicitMapKeyword.isSelected
        s.useExplicitSetKeyword = useExplicitSetKeyword.isSelected
        s.hoverEnabled = hoverEnabled.isSelected
        s.hoverDateTimeEnabled = hoverDateTimeEnabled.isSelected
        s.hoverDateTimeFullFormat = hoverDateTimeFullFormat.text
        s.hoverDateTimeDateOnlyFormat = hoverDateTimeDateOnlyFormat.text
        s.hoverDateTimeNoMillisFormat = hoverDateTimeNoMillisFormat.text
        s.hoverDateTimeUnixFormat = hoverDateTimeUnixFormat.text
        s.hoverTimeOnlyEnabled = hoverTimeOnlyEnabled.isSelected
        s.hoverTimeOnlyFormat = hoverTimeOnlyFormat.text
        s.hoverDurationEnabled = hoverDurationEnabled.isSelected
        s.hoverBigintEnabled = hoverBigintEnabled.isSelected
        s.hoverBigintShowBitLength = hoverBigintShowBitLength.isSelected
        s.hoverBinaryEnabled = hoverBinaryEnabled.isSelected
        s.hoverBinaryShowPreview = hoverBinaryShowPreview.isSelected
        s.hoverRegexpEnabled = hoverRegexpEnabled.isSelected
        s.hoverSpecialNumbersEnabled = hoverSpecialNumbersEnabled.isSelected
        s.hoverCollectionsEnabled = hoverCollectionsEnabled.isSelected
        s.hoverDiagnosticsEnabled = hoverDiagnosticsEnabled.isSelected
    }

    override fun reset() {
        val s = state
        useExplicitMapKeyword.isSelected = s.useExplicitMapKeyword
        useExplicitSetKeyword.isSelected = s.useExplicitSetKeyword
        hoverEnabled.isSelected = s.hoverEnabled
        hoverDateTimeEnabled.isSelected = s.hoverDateTimeEnabled
        hoverDateTimeFullFormat.text = s.hoverDateTimeFullFormat
        hoverDateTimeDateOnlyFormat.text = s.hoverDateTimeDateOnlyFormat
        hoverDateTimeNoMillisFormat.text = s.hoverDateTimeNoMillisFormat
        hoverDateTimeUnixFormat.text = s.hoverDateTimeUnixFormat
        hoverTimeOnlyEnabled.isSelected = s.hoverTimeOnlyEnabled
        hoverTimeOnlyFormat.text = s.hoverTimeOnlyFormat
        hoverDurationEnabled.isSelected = s.hoverDurationEnabled
        hoverBigintEnabled.isSelected = s.hoverBigintEnabled
        hoverBigintShowBitLength.isSelected = s.hoverBigintShowBitLength
        hoverBinaryEnabled.isSelected = s.hoverBinaryEnabled
        hoverBinaryShowPreview.isSelected = s.hoverBinaryShowPreview
        hoverRegexpEnabled.isSelected = s.hoverRegexpEnabled
        hoverSpecialNumbersEnabled.isSelected = s.hoverSpecialNumbersEnabled
        hoverCollectionsEnabled.isSelected = s.hoverCollectionsEnabled
        hoverDiagnosticsEnabled.isSelected = s.hoverDiagnosticsEnabled
    }
}
```

### `plugin.xml` additions

```xml
<projectService serviceImplementation="com.rdn.intellij.settings.RdnSettingsState"/>

<projectConfigurable
    parentId="language"
    displayName="RDN"
    id="com.rdn.intellij.settings.RdnSettingsConfigurable"
    implementationClass="com.rdn.intellij.settings.RdnSettingsConfigurable"
    nonDefaultProject="true"/>
```

## Acceptance Criteria
- [ ] **Settings > Languages & Frameworks > RDN** page appears in the IDE settings
- [ ] All 19 settings are displayed with appropriate UI controls
- [ ] Default values match the spec (all hover toggles `true`, all format strings as specified)
- [ ] Changing a setting and clicking OK saves it to `.idea/rdn.xml`
- [ ] Settings survive IDE restart (persisted via `PersistentStateComponent`)
- [ ] `RdnSettingsState.getInstance(project)` returns a non-null instance
- [ ] `isModified()` returns `true` only when a UI value differs from the stored value
- [ ] `reset()` restores all UI controls to the currently stored values
- [ ] Changes take effect immediately (hover, formatting observe the new values on next invocation)

## Dependencies
- Depends on: task-002
- Blocks: task-021, task-025
