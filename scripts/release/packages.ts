/**
 * Package registry — single source of truth for all releasable packages.
 */

export type VersionFileType = "json" | "xml" | "toml" | "properties";
export type Ecosystem = "npm" | "nuget" | "vscode" | "jetbrains" | "crates" | "pypi" | "go";

export interface VersionFile {
  path: string;
  type: VersionFileType;
  /** The key/field that holds the version string */
  key: string;
}

export interface PackageConfig {
  /** Package name used in tags and CLI */
  name: string;
  /** Conventional-commit scopes that map to this package */
  scopes: string[];
  /** Directory relative to repo root */
  dir: string;
  /** Tag prefix (version appended directly, e.g. "@rdn/typescript@") */
  tagPrefix: string;
  /** Files containing the version to update */
  versionFiles: VersionFile[];
  /** Path to CHANGELOG.md relative to repo root */
  changelogPath: string;
  /** Target ecosystem */
  ecosystem: Ecosystem;
}

export const packages: PackageConfig[] = [
  {
    name: "@rdn/typescript",
    scopes: ["typescript"],
    dir: "packages/rdn-js",
    tagPrefix: "@rdn/typescript@",
    versionFiles: [{ path: "packages/rdn-js/package.json", type: "json", key: "version" }],
    changelogPath: "packages/rdn-js/CHANGELOG.md",
    ecosystem: "npm",
  },
  {
    name: "rdn-dotnet",
    scopes: ["csharp"],
    dir: "packages/rdn-dotnet",
    tagPrefix: "rdn-dotnet@",
    versionFiles: [
      { path: "packages/rdn-dotnet/package.json", type: "json", key: "version" },
      { path: "packages/rdn-dotnet/Directory.Build.props", type: "xml", key: "Version" },
    ],
    changelogPath: "packages/rdn-dotnet/CHANGELOG.md",
    ecosystem: "nuget",
  },
  {
    name: "rdn-vscode",
    scopes: ["vscode", "vscode-extension"],
    dir: "tools/vscode-extension",
    tagPrefix: "rdn-vscode@",
    versionFiles: [{ path: "tools/vscode-extension/package.json", type: "json", key: "version" }],
    changelogPath: "tools/vscode-extension/CHANGELOG.md",
    ecosystem: "vscode",
  },
  {
    name: "prettier-plugin-rdn",
    scopes: ["prettier"],
    dir: "tools/prettier-plugin-rdn",
    tagPrefix: "prettier-plugin-rdn@",
    versionFiles: [{ path: "tools/prettier-plugin-rdn/package.json", type: "json", key: "version" }],
    changelogPath: "tools/prettier-plugin-rdn/CHANGELOG.md",
    ecosystem: "npm",
  },
  {
    name: "rdn-jetbrains",
    scopes: ["jetbrains"],
    dir: "tools/jetbrains-plugin",
    tagPrefix: "rdn-jetbrains@",
    versionFiles: [{ path: "tools/jetbrains-plugin/gradle.properties", type: "properties", key: "pluginVersion" }],
    changelogPath: "tools/jetbrains-plugin/CHANGELOG.md",
    ecosystem: "jetbrains",
  },
  {
    name: "rdn-rust",
    scopes: ["rust"],
    dir: "packages/rdn-rust",
    tagPrefix: "rdn-rust@",
    versionFiles: [{ path: "packages/rdn-rust/Cargo.toml", type: "toml", key: "version" }],
    changelogPath: "packages/rdn-rust/CHANGELOG.md",
    ecosystem: "crates",
  },
  {
    name: "rdn-python",
    scopes: ["python"],
    dir: "packages/rdn-python",
    tagPrefix: "rdn-python@",
    versionFiles: [{ path: "packages/rdn-python/pyproject.toml", type: "toml", key: "version" }],
    changelogPath: "packages/rdn-python/CHANGELOG.md",
    ecosystem: "pypi",
  },
  {
    name: "rdn-go",
    scopes: ["go"],
    dir: "packages/rdn-go",
    tagPrefix: "rdn-go@v",
    versionFiles: [], // Go uses tag-only versioning
    changelogPath: "packages/rdn-go/CHANGELOG.md",
    ecosystem: "go",
  },
];

export function getPackage(name: string): PackageConfig {
  const pkg = packages.find((p) => p.name === name);
  if (!pkg) {
    const valid = packages.map((p) => p.name).join(", ");
    throw new Error(`Unknown package "${name}". Valid packages: ${valid}`);
  }
  return pkg;
}
