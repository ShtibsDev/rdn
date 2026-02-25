# Task 032: Add CI Workflows

## References
- [Tech Design](../tech-design.md) — Section 6.15
- [Discovery](../discovery.md)

## Description
Create two GitHub Actions workflows: (1) `jetbrains-ci.yml` for PR checks that runs `./gradlew check` on any change touching `tools/jetbrains-plugin/**`, and (2) `jetbrains-release.yml` for tag-triggered releases that builds the plugin, signs it, and publishes to JetBrains Marketplace. Also update the existing CI to ensure conformance tests always run.

## Files to Create/Modify
- `.github/workflows/jetbrains-ci.yml` — PR check workflow
- `.github/workflows/jetbrains-release.yml` — Release workflow

## Implementation Details

### `.github/workflows/jetbrains-ci.yml`

```yaml
name: JetBrains Plugin CI

on:
  push:
    branches: [main, feature/jetbrains-pluggin]
    paths:
      - 'tools/jetbrains-plugin/**'
      - 'test-suite/**'
      - '.github/workflows/jetbrains-ci.yml'
  pull_request:
    paths:
      - 'tools/jetbrains-plugin/**'
      - 'test-suite/**'
      - '.github/workflows/jetbrains-ci.yml'

jobs:
  build-and-test:
    name: Build and Test
    runs-on: ubuntu-latest

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up JDK 17
        uses: actions/setup-java@v4
        with:
          java-version: '17'
          distribution: 'temurin'
          cache: 'gradle'

      - name: Setup Gradle
        uses: gradle/actions/setup-gradle@v3
        with:
          gradle-version: wrapper
          build-root-directory: tools/jetbrains-plugin

      - name: Run checks
        working-directory: tools/jetbrains-plugin
        run: ./gradlew check --continue
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: tools/jetbrains-plugin/build/reports/tests/
          retention-days: 7

      - name: Upload build output
        uses: actions/upload-artifact@v4
        if: success()
        with:
          name: plugin-build
          path: tools/jetbrains-plugin/build/distributions/*.zip
          retention-days: 3
```

### `.github/workflows/jetbrains-release.yml`

```yaml
name: JetBrains Plugin Release

on:
  push:
    tags:
      - 'jetbrains-v*'

permissions:
  contents: write

jobs:
  release:
    name: Build and Release
    runs-on: ubuntu-latest

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up JDK 17
        uses: actions/setup-java@v4
        with:
          java-version: '17'
          distribution: 'temurin'
          cache: 'gradle'

      - name: Setup Gradle
        uses: gradle/actions/setup-gradle@v3
        with:
          gradle-version: wrapper
          build-root-directory: tools/jetbrains-plugin

      - name: Extract version from tag
        id: version
        run: echo "version=${GITHUB_REF_NAME#jetbrains-v}" >> $GITHUB_OUTPUT

      - name: Build plugin
        working-directory: tools/jetbrains-plugin
        run: ./gradlew buildPlugin
        env:
          PLUGIN_VERSION: ${{ steps.version.outputs.version }}

      - name: Run tests
        working-directory: tools/jetbrains-plugin
        run: ./gradlew test
        env:
          PLUGIN_VERSION: ${{ steps.version.outputs.version }}

      - name: Sign plugin
        working-directory: tools/jetbrains-plugin
        run: ./gradlew signPlugin
        env:
          PLUGIN_VERSION: ${{ steps.version.outputs.version }}
          CERTIFICATE_CHAIN: ${{ secrets.CERTIFICATE_CHAIN }}
          PRIVATE_KEY: ${{ secrets.PRIVATE_KEY }}
          PRIVATE_KEY_PASSWORD: ${{ secrets.PRIVATE_KEY_PASSWORD }}

      - name: Publish to JetBrains Marketplace
        working-directory: tools/jetbrains-plugin
        run: ./gradlew publishPlugin
        env:
          PLUGIN_VERSION: ${{ steps.version.outputs.version }}
          PUBLISH_TOKEN: ${{ secrets.JETBRAINS_MARKETPLACE_TOKEN }}

      - name: Upload plugin ZIP to GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: tools/jetbrains-plugin/build/distributions/*.zip
          tag_name: ${{ github.ref_name }}
          generate_release_notes: true
```

### Required GitHub repository secrets

The release workflow requires these secrets set in the GitHub repository settings:

| Secret | Description |
|---|---|
| `CERTIFICATE_CHAIN` | PEM-encoded certificate chain for plugin signing |
| `PRIVATE_KEY` | PEM-encoded private key for plugin signing |
| `PRIVATE_KEY_PASSWORD` | Password for the private key |
| `JETBRAINS_MARKETPLACE_TOKEN` | JetBrains Marketplace publish token |

For plugin signing, generate certificates using:
```bash
openssl req -x509 -newkey rsa:4096 -keyout private.pem -out chain.pem -days 365 -nodes -subj "/CN=RDN Plugin"
```

### Release trigger

To release a new version:
```bash
git tag jetbrains-v0.1.0
git push origin jetbrains-v0.1.0
```

The tag format `jetbrains-v*` ensures JetBrains releases are separate from other releases (e.g., VSCode releases tagged `vscode-v*`, npm packages tagged by `pnpm release`).

## Acceptance Criteria
- [ ] `jetbrains-ci.yml` triggers on PRs that touch `tools/jetbrains-plugin/**`
- [ ] `jetbrains-ci.yml` does NOT trigger on PRs that only touch other parts of the repo
- [ ] `jetbrains-ci.yml` runs `./gradlew check` which includes all tests
- [ ] Test results are uploaded as artifacts even when tests fail (`if: always()`)
- [ ] `jetbrains-release.yml` triggers on tags matching `jetbrains-v*`
- [ ] `jetbrains-release.yml` runs tests before signing/publishing (catches regressions)
- [ ] Plugin ZIP is attached to the GitHub release
- [ ] Both workflows use JDK 17 (`actions/setup-java@v4` with `java-version: '17'`)
- [ ] Gradle build cache is enabled via `actions/setup-gradle`

## Dependencies
- Depends on: task-001
- Blocks: None
