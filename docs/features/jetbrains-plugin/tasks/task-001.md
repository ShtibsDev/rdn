# Task 001: Scaffold Gradle Project

## References
- [Tech Design](../tech-design.md) — Section 6.15
- [Discovery](../discovery.md)

## Description
Create the `tools/jetbrains-plugin/` directory and all Gradle project scaffolding files. This is the foundation on which every other task depends. Configure IntelliJ Platform Gradle Plugin 2.x, Kotlin 2.0, JDK 17 target, and platform version 2024.3 (build 243). Add the bare-bones `plugin.xml` skeleton that subsequent tasks will extend.

## Files to Create/Modify
- `tools/jetbrains-plugin/build.gradle.kts` — Gradle build script with IntelliJ Platform plugin configuration
- `tools/jetbrains-plugin/settings.gradle.kts` — Gradle settings with plugin management
- `tools/jetbrains-plugin/gradle.properties` — Version properties
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Plugin descriptor skeleton
- `.gitignore` — Add Gradle build directories for the new module

## Implementation Details

### `build.gradle.kts`

```kotlin
plugins {
    id("org.jetbrains.kotlin.jvm") version "2.0.0"
    id("org.jetbrains.intellij.platform") version "2.2.0"
}

group = "com.rdn"
version = providers.gradleProperty("pluginVersion").get()

kotlin {
    jvmToolchain(17)
}

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        create("IC", providers.gradleProperty("platformVersion").get())
        instrumentationTools()
        pluginVerifier()
    }
    testImplementation("org.junit.jupiter:junit-jupiter:5.10.2")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

intellijPlatform {
    pluginConfiguration {
        id = "com.rdn.intellij"
        name = "RDN"
        version = providers.gradleProperty("pluginVersion").get()

        ideaVersion {
            sinceBuild = providers.gradleProperty("sinceBuild").get()
            untilBuild = providers.gradleProperty("untilBuild").get()
        }

        description = """
            First-class RDN (Rich Data Notation) support: syntax highlighting,
            real-time diagnostics, quick fixes, completions, hover docs, and formatting.
        """.trimIndent()
    }

    signing {
        certificateChain = providers.environmentVariable("CERTIFICATE_CHAIN")
        privateKey = providers.environmentVariable("PRIVATE_KEY")
        password = providers.environmentVariable("PRIVATE_KEY_PASSWORD")
    }

    publishing {
        token = providers.environmentVariable("PUBLISH_TOKEN")
    }
}

tasks {
    test {
        useJUnitPlatform()
        systemProperty(
            "testSuiteDir",
            rootProject.projectDir.resolve("../../test-suite").absolutePath
        )
    }
}
```

### `settings.gradle.kts`

```kotlin
pluginManagement {
    repositories {
        maven("https://packages.jetbrains.team/maven/p/ij/intellij-dependencies")
        gradlePluginPortal()
        mavenCentral()
    }
}

plugins {
    id("org.jetbrains.intellij.platform.settings") version "2.2.0"
}

dependencyResolutionManagement {
    repositoriesMode = RepositoriesMode.FAIL_ON_PROJECT_REPOS
    repositories {
        mavenCentral()
        intellijPlatform {
            defaultRepositories()
        }
    }
}

rootProject.name = "rdn-intellij"
```

### `gradle.properties`

```properties
# Plugin
pluginVersion=0.1.0

# Platform
platformVersion=2024.3
sinceBuild=243
untilBuild=253.*

# Gradle
org.gradle.caching=true
org.gradle.configuration-cache=true
```

### `src/main/resources/META-INF/plugin.xml` (skeleton)

```xml
<idea-plugin>
    <id>com.rdn.intellij</id>
    <name>RDN</name>
    <vendor email="support@rdn.dev" url="https://rdn.dev">RDN</vendor>

    <description><![CDATA[
        First-class RDN (Rich Data Notation) support for all IntelliJ-based IDEs.
        Provides syntax highlighting, real-time diagnostics, quick fixes,
        completions, hover documentation, and document formatting.
    ]]></description>

    <depends>com.intellij.modules.platform</depends>

    <extensions defaultExtensionNs="com.intellij">
        <!-- Extensions registered by subsequent tasks -->
    </extensions>
</idea-plugin>
```

### `.gitignore` additions

Add to the root `.gitignore`:
```
tools/jetbrains-plugin/.gradle/
tools/jetbrains-plugin/build/
tools/jetbrains-plugin/.idea/
```

## Acceptance Criteria
- [ ] `cd tools/jetbrains-plugin && ./gradlew dependencies` resolves without errors
- [ ] `./gradlew buildPlugin` produces a `.zip` in `build/distributions/` (even with no source yet)
- [ ] `./gradlew verifyPlugin` passes (no incompatible API usage)
- [ ] `plugin.xml` is valid XML and the plugin ID is `com.rdn.intellij`
- [ ] Gradle build cache is enabled (`org.gradle.caching=true` in `gradle.properties`)
- [ ] JDK target is 17 (verify with `./gradlew compileKotlin --info | grep jvmTarget`)

## Dependencies
- Depends on: None
- Blocks: task-002, task-003, task-004, task-008, task-009
