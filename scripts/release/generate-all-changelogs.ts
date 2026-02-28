#!/usr/bin/env bun
/**
 * Batch changelog regeneration — regenerates all changelogs from scratch.
 *
 * Usage:
 *   bun scripts/release/generate-all-changelogs.ts [--dry-run]
 */
import { parseArgs } from "node:util";
import { writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { packages } from "./packages";
import { ROOT, getLatestTag, getRootCommit, getCommitsBetween, parseConventionalCommit, readVersion, formatChangelog } from "./utils";

const { values } = parseArgs({
  options: { "dry-run": { type: "boolean", default: false } },
  strict: true,
});

const dryRun = values["dry-run"] ?? false;

for (const pkg of packages) {
  console.log(`\n── ${pkg.name} ──`);

  // Collect all tags for this package (oldest first)
  const result = Bun.spawnSync(["git", "tag", "-l", `${pkg.tagPrefix}*`, "--sort=v:refname"], { cwd: ROOT });
  const tags = result.stdout.toString().trim().split("\n").filter(Boolean);
  const rootCommit = getRootCommit();

  // Build version windows: [rootCommit, tag1], [tag1, tag2], ...
  const windows: Array<{ from: string; to: string; version: string }> = [];

  if (tags.length === 0) {
    // No tags — single window from root to HEAD
    const version = readVersion(pkg);
    windows.push({ from: rootCommit, to: "HEAD", version });
  } else {
    // First window: root commit → first tag
    const firstTagVersion = tags[0].slice(pkg.tagPrefix.length);
    windows.push({ from: rootCommit, to: tags[0], version: firstTagVersion });

    // Middle windows
    for (let i = 1; i < tags.length; i++) {
      const v = tags[i].slice(pkg.tagPrefix.length);
      windows.push({ from: tags[i - 1], to: tags[i], version: v });
    }

    // Final window: latest tag → HEAD (unreleased)
    const headCommits = getCommitsBetween(tags[tags.length - 1], "HEAD");
    const headParsed = headCommits
      .map((c) => parseConventionalCommit(c.hash, c.message))
      .filter((c) => c !== null)
      .filter((c) => c.scopes.some((s) => pkg.scopes.includes(s)));

    if (headParsed.length > 0) {
      const currentVersion = readVersion(pkg);
      windows.push({ from: tags[tags.length - 1], to: "HEAD", version: currentVersion });
    }
  }

  // Generate entries in reverse chronological order (newest first)
  const entries: string[] = [];

  for (const w of windows.reverse()) {
    const raw = getCommitsBetween(w.from, w.to);
    const parsed = raw
      .map((c) => parseConventionalCommit(c.hash, c.message))
      .filter((c) => c !== null)
      .filter((c) => c.scopes.some((s) => pkg.scopes.includes(s)));

    if (parsed.length === 0) continue;

    // Use the tag date if it's a tag, otherwise today
    let date: string;
    if (w.to !== "HEAD") {
      const dateResult = Bun.spawnSync(["git", "log", "-1", "--format=%aI", w.to], { cwd: ROOT });
      date = dateResult.stdout.toString().trim().slice(0, 10);
    } else {
      date = new Date().toISOString().slice(0, 10);
    }

    entries.push(formatChangelog(parsed, w.version, date));
  }

  if (entries.length === 0) {
    console.log("  No matching commits found — skipping.");
    continue;
  }

  const changelog = `# Changelog\n\n${entries.join("\n")}`;

  if (dryRun) {
    console.log(changelog);
    console.log(`\n  [dry-run] Would write to ${pkg.changelogPath}`);
  } else {
    writeFileSync(resolve(ROOT, pkg.changelogPath), changelog);
    console.log(`  Wrote ${entries.length} version(s) to ${pkg.changelogPath}`);
  }
}

console.log("\nDone.");
