/**
 * Shared utilities for the release scripts.
 */
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";
import type { PackageConfig, VersionFile } from "./packages";

export const ROOT = resolve(import.meta.dir, "../..");

// ── Conventional Commit Parser ──────────────────────────────────────────────

export interface ParsedCommit {
  hash: string;
  type: string;
  scopes: string[];
  breaking: boolean;
  description: string;
  raw: string;
}

const COMMIT_RE = /^(?<type>[a-z]+)(?:\((?<scopes>[^)]+)\))?(?<bang>!)?:\s*(?:\p{Emoji_Presentation}\s*)?(?<desc>.+)$/u;

export function parseConventionalCommit(hash: string, message: string): ParsedCommit | null {
  const firstLine = message.split("\n")[0].trim();
  const match = firstLine.match(COMMIT_RE);
  if (!match?.groups) return null;

  const { type, scopes, bang, desc } = match.groups;
  return {
    hash,
    type,
    scopes: scopes ? scopes.split(",").map((s) => s.trim()) : [],
    breaking: bang === "!",
    description: desc.trim(),
    raw: firstLine,
  };
}

// ── Git Helpers ─────────────────────────────────────────────────────────────

export function getLatestTag(tagPrefix: string): string | null {
  const result = Bun.spawnSync(["git", "tag", "-l", `${tagPrefix}*`, "--sort=-v:refname"], { cwd: ROOT });
  const tags = result.stdout.toString().trim().split("\n").filter(Boolean);
  return tags[0] ?? null;
}

export function getRootCommit(): string {
  const result = Bun.spawnSync(["git", "rev-list", "--max-parents=0", "HEAD"], { cwd: ROOT });
  return result.stdout.toString().trim().split("\n")[0];
}

export function getCommitsBetween(from: string, to: string = "HEAD"): Array<{ hash: string; message: string }> {
  const result = Bun.spawnSync(["git", "log", `${from}..${to}`, "--pretty=format:%H %s"], { cwd: ROOT });
  const lines = result.stdout.toString().trim().split("\n").filter(Boolean);
  return lines.map((line) => {
    const spaceIdx = line.indexOf(" ");
    return { hash: line.slice(0, spaceIdx), message: line.slice(spaceIdx + 1) };
  });
}

// ── Version File I/O ────────────────────────────────────────────────────────

export function readVersion(pkg: PackageConfig): string {
  if (pkg.versionFiles.length === 0) {
    // Tag-only (e.g. Go) — read from latest tag
    const tag = getLatestTag(pkg.tagPrefix);
    if (!tag) return "0.0.0";
    return tag.slice(pkg.tagPrefix.length);
  }

  const vf = pkg.versionFiles[0];
  const fullPath = resolve(ROOT, vf.path);
  const content = readFileSync(fullPath, "utf8");

  switch (vf.type) {
    case "json": {
      const json = JSON.parse(content);
      return json[vf.key];
    }
    case "xml": {
      const re = new RegExp(`<${vf.key}>([^<]*)</${vf.key}>`);
      const m = content.match(re);
      return m?.[1] ?? "0.0.0";
    }
    case "toml": {
      const re = new RegExp(`^${vf.key}\\s*=\\s*"([^"]*)"`, "m");
      const m = content.match(re);
      return m?.[1] ?? "0.0.0";
    }
    case "properties": {
      const re = new RegExp(`^${vf.key}=(.+)$`, "m");
      const m = content.match(re);
      return m?.[1]?.trim() ?? "0.0.0";
    }
  }
}

export function writeVersion(pkg: PackageConfig, version: string): void {
  for (const vf of pkg.versionFiles) {
    const fullPath = resolve(ROOT, vf.path);
    const content = readFileSync(fullPath, "utf8");
    let updated: string;

    switch (vf.type) {
      case "json": {
        const json = JSON.parse(content);
        json[vf.key] = version;
        updated = JSON.stringify(json, null, 2) + "\n";
        break;
      }
      case "xml": {
        updated = content.replace(new RegExp(`<${vf.key}>[^<]*</${vf.key}>`), `<${vf.key}>${version}</${vf.key}>`);
        break;
      }
      case "toml": {
        updated = content.replace(new RegExp(`^(${vf.key}\\s*=\\s*)"[^"]*"`, "m"), `$1"${version}"`);
        break;
      }
      case "properties": {
        updated = content.replace(new RegExp(`^(${vf.key})=.+$`, "m"), `$1=${version}`);
        break;
      }
    }

    writeFileSync(fullPath, updated);
  }
}

// ── Semver Bump ─────────────────────────────────────────────────────────────

export type BumpType = "patch" | "minor" | "major" | "prerelease";

export function bumpVersion(current: string, type: BumpType, preid?: string): string {
  // Parse: 1.2.3 or 1.2.3-alpha.0
  const preMatch = current.match(/^(\d+)\.(\d+)\.(\d+)(?:-([a-zA-Z]+)\.(\d+))?$/);
  if (!preMatch) throw new Error(`Invalid semver: ${current}`);

  let [, majorS, minorS, patchS, preTag, preNumS] = preMatch;
  let major = Number(majorS);
  let minor = Number(minorS);
  let patch = Number(patchS);

  if (type === "prerelease") {
    if (!preid) throw new Error("--preid is required for prerelease bumps");
    if (preTag === preid) {
      // Already in this pre-release series — increment pre number
      return `${major}.${minor}.${patch}-${preid}.${Number(preNumS) + 1}`;
    }
    // New pre-release series on next patch
    if (!preTag) patch++;
    return `${major}.${minor}.${patch}-${preid}.0`;
  }

  // Stable bump — strip any pre-release tag
  switch (type) {
    case "patch":
      if (preTag) return `${major}.${minor}.${patch}`; // releasing the pre-release
      return `${major}.${minor}.${patch + 1}`;
    case "minor":
      return `${major}.${minor + 1}.0`;
    case "major":
      return `${major + 1}.0.0`;
  }
}

// ── Changelog Formatting ────────────────────────────────────────────────────

const TYPE_SECTIONS: Record<string, string> = {
  feat: "Added",
  fix: "Fixed",
  refactor: "Changed",
  perf: "Changed",
  ci: "Maintenance",
  chore: "Maintenance",
  build: "Maintenance",
  style: "Maintenance",
  docs: "Maintenance",
  test: "Maintenance",
};

const SECTION_ORDER = ["Breaking Changes", "Added", "Fixed", "Changed", "Maintenance"];

export function formatChangelog(commits: ParsedCommit[], version: string, date: string): string {
  const sections: Record<string, string[]> = {};

  for (const c of commits) {
    if (c.breaking) {
      (sections["Breaking Changes"] ??= []).push(`- ${c.description} (${c.hash.slice(0, 7)})`);
    }

    const section = TYPE_SECTIONS[c.type];
    if (section) {
      (sections[section] ??= []).push(`- ${c.description} (${c.hash.slice(0, 7)})`);
    }
  }

  let md = `## [${version}] - ${date}\n`;

  for (const section of SECTION_ORDER) {
    const items = sections[section];
    if (!items?.length) continue;
    md += `\n### ${section}\n\n`;
    md += items.join("\n") + "\n";
  }

  return md;
}

export function prependToChangelog(filePath: string, entry: string): void {
  const fullPath = resolve(ROOT, filePath);

  if (!existsSync(fullPath)) {
    writeFileSync(fullPath, `# Changelog\n\n${entry}`);
    return;
  }

  const content = readFileSync(fullPath, "utf8");
  // Insert after the first H1 heading (and any blank lines following it)
  const h1Match = content.match(/^# .+\n\n?/m);
  if (h1Match) {
    const insertPos = h1Match.index! + h1Match[0].length;
    const updated = content.slice(0, insertPos) + entry + "\n" + content.slice(insertPos);
    writeFileSync(fullPath, updated);
  } else {
    // No H1 heading — prepend with one
    writeFileSync(fullPath, `# Changelog\n\n${entry}\n${content}`);
  }
}
