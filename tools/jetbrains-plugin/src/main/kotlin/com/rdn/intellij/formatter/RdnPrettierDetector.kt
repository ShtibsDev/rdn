package com.rdn.intellij.formatter

import com.intellij.openapi.project.Project
import java.io.File

/**
 * Detects whether Prettier with the RDN plugin is available in the project,
 * and provides a method to run Prettier on RDN source text.
 */
object RdnPrettierDetector {

    private val PRETTIER_CONFIG_FILES = listOf(".prettierrc", ".prettierrc.json", ".prettierrc.js", ".prettierrc.cjs", ".prettierrc.yaml", ".prettierrc.yml", "prettier.config.js", "prettier.config.cjs")

    /**
     * Returns `true` when the project has a Prettier config and the
     * `prettier-plugin-rdn` package is installed (either hoisted or in pnpm).
     */
    fun isPrettierAvailable(project: Project): Boolean {
        val projectDir = project.basePath?.let { File(it) } ?: return false
        val configExists = PRETTIER_CONFIG_FILES.any { File(projectDir, it).exists() } || packageJsonHasPrettier(projectDir)
        if (!configExists) return false
        return File(projectDir, "node_modules/prettier-plugin-rdn").exists() || File(projectDir, "node_modules/.pnpm").let { pnpm -> pnpm.exists() && pnpm.walk().any { it.isDirectory && it.name == "prettier-plugin-rdn" } }
    }

    private fun packageJsonHasPrettier(projectDir: File): Boolean {
        val packageJson = File(projectDir, "package.json")
        if (!packageJson.exists()) return false
        return try {
            packageJson.readText().contains("\"prettier\"")
        } catch (_: Exception) {
            false
        }
    }

    /**
     * Runs `npx prettier --parser rdn` on the given [text] and returns the
     * formatted result, or `null` if the process fails.
     */
    fun runPrettier(text: String, project: Project): String? {
        val projectDir = project.basePath ?: return null
        return try {
            val process = ProcessBuilder("npx", "prettier", "--parser", "rdn", "--stdin-filepath", "input.rdn").directory(File(projectDir)).redirectErrorStream(true).start()
            process.outputStream.use { it.write(text.toByteArray()) }
            val result = process.inputStream.bufferedReader().readText()
            if (process.waitFor() == 0) result else null
        } catch (_: Exception) {
            null
        }
    }
}
