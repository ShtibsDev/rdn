import org.jetbrains.intellij.platform.gradle.TestFrameworkType

plugins {
    id("org.jetbrains.kotlin.jvm") version "2.0.0"
    id("org.jetbrains.intellij.platform")
    id("org.jetbrains.grammarkit") version "2022.3.2.2"
}

group = "com.rdn"
version = providers.gradleProperty("pluginVersion").get()

kotlin {
    jvmToolchain(17)
}

dependencies {
    intellijPlatform {
        create("IC", providers.gradleProperty("platformVersion").get())
        pluginVerifier()
        testFramework(TestFrameworkType.Platform)
    }
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.junit.jupiter:junit-jupiter:5.10.2")
    testRuntimeOnly("org.junit.vintage:junit-vintage-engine:5.10.2")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

sourceSets {
    main {
        java {
            srcDir("src/main/gen")
        }
    }
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
            First-class RDN (Rich Data Notation) support for all IntelliJ-based IDEs.
            Provides syntax highlighting, real-time diagnostics, quick fixes,
            completions, hover documentation, and document formatting.
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
    generateLexer {
        sourceFile.set(file("src/main/kotlin/com/rdn/intellij/lexer/Rdn.flex"))
        targetOutputDir.set(file("src/main/gen/com/rdn/intellij/lexer"))
        purgeOldFiles.set(true)
    }

    generateParser {
        sourceFile.set(file("src/main/kotlin/com/rdn/intellij/parser/Rdn.bnf"))
        targetRootOutputDir.set(file("src/main/gen"))
        pathToParser.set("com/rdn/intellij/parser/RdnParser.java")
        pathToPsiRoot.set("com/rdn/intellij/psi")
        purgeOldFiles.set(true)
    }

    // Ensure generated sources are available before compilation
    compileKotlin {
        dependsOn(generateLexer, generateParser)
    }

    compileJava {
        dependsOn(generateLexer, generateParser)
    }

    test {
        useJUnitPlatform()
        systemProperty("testSuiteDir", rootProject.projectDir.resolve("../../test-suite").absolutePath)
    }
}
