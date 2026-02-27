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
