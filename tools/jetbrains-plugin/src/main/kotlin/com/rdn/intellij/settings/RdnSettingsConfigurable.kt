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

    private val useExplicitMapKeyword = JBCheckBox("Keep explicit Map keyword on non-empty maps")
    private val useExplicitSetKeyword = JBCheckBox("Keep explicit Set keyword on non-empty sets")
    private val hoverEnabled = JBCheckBox("Enable hover information")
    private val hoverDateTimeEnabled = JBCheckBox("Enable DateTime hover")
    private val hoverDateTimeFullFormat = JBTextField(30)
    private val hoverDateTimeDateOnlyFormat = JBTextField(30)
    private val hoverDateTimeNoMillisFormat = JBTextField(30)
    private val hoverDateTimeUnixFormat = JBTextField(30)
    private val hoverTimeOnlyEnabled = JBCheckBox("Enable TimeOnly hover")
    private val hoverTimeOnlyFormat = JBTextField(30)
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
