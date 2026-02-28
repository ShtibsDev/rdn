#!/usr/bin/env bun
/**
 * Release orchestrator — bump, changelog, commit, tag, push.
 *
 * Usage:
 *   bun scripts/release/release.ts --package <name> --bump <patch|minor|major|prerelease> [--preid <alpha|beta|rc>] [--dry-run] [--no-push]
 */
import { parseArgs } from "node:util";
import { getPackage } from "./packages";
import { ROOT, readVersion, writeVersion, bumpVersion, getLatestTag, getRootCommit, getCommitsBetween, parseConventionalCommit, formatChangelog, prependToChangelog, type BumpType } from "./utils";

const { values } = parseArgs({
  options: {
    package: { type: "string" },
    bump: { type: "string" },
    preid: { type: "string" },
    "dry-run": { type: "boolean", default: false },
    "no-push": { type: "boolean", default: false },
  },
  strict: true,
});

if (!values.package || !values.bump) {
  console.error("Usage: bun scripts/release/release.ts --package <name> --bump <patch|minor|major|prerelease> [--preid <alpha|beta|rc>] [--dry-run] [--no-push]");
  process.exit(1);
}

const validBumps = ["patch", "minor", "major", "prerelease"];
if (!validBumps.includes(values.bump)) {
  console.error(`Invalid bump type "${values.bump}". Must be one of: ${validBumps.join(", ")}`);
  process.exit(1);
}

const dryRun = values["dry-run"] ?? false;
const noPush = values["no-push"] ?? false;
const pkg = getPackage(values.package);

// ── 1. Verify clean working tree ────────────────────────────────────────────

const statusResult = Bun.spawnSync(["git", "status", "--porcelain"], { cwd: ROOT });
if (statusResult.stdout.toString().trim() && !dryRun) {
  console.error("Working tree is not clean. Commit or stash changes before releasing.");
  process.exit(1);
}

// ── 2. Bump version ─────────────────────────────────────────────────────────

const current = readVersion(pkg);
const next = bumpVersion(current, values.bump as BumpType, values.preid);
console.log(`\nVersion: ${current} → ${next}`);

if (!dryRun && pkg.versionFiles.length > 0) {
  writeVersion(pkg, next);
}

// ── 3. Generate changelog ───────────────────────────────────────────────────

const from = getLatestTag(pkg.tagPrefix) ?? getRootCommit();
const rawCommits = getCommitsBetween(from, "HEAD");
const parsed = rawCommits
  .map((c) => parseConventionalCommit(c.hash, c.message))
  .filter((c) => c !== null);
const filtered = parsed.filter((c) => c.scopes.some((s) => pkg.scopes.includes(s)));

const date = new Date().toISOString().slice(0, 10);

if (filtered.length > 0) {
  const entry = formatChangelog(filtered, next, date);
  console.log(`\nChangelog entry:\n${entry}`);
  if (!dryRun) {
    prependToChangelog(pkg.changelogPath, entry);
  }
} else {
  console.warn("\nNo matching commits found — changelog will not be updated.");
}

// ── 4. Build commit message ─────────────────────────────────────────────────

const scope = pkg.scopes[0];
const tag = `${pkg.tagPrefix}${next}`;
const commitMsg = `chore(${scope}): 🔖 release ${pkg.name}@${next}`;
const isPrerelease = next.includes("-");

console.log(`\nProposed commit: ${commitMsg}`);
console.log(`Tag: ${tag}`);

if (dryRun) {
  console.log("\n[dry-run] No changes made.");
  process.exit(0);
}

// ── 5. Wait for user confirmation ───────────────────────────────────────────

process.stdout.write("\nProceed? [y/N] ");
const buf = Buffer.alloc(10);
const fd = require("node:fs").openSync("/dev/tty", "r");
const bytesRead = require("node:fs").readSync(fd, buf, 0, 10, null);
require("node:fs").closeSync(fd);
const answer = buf.slice(0, bytesRead).toString().trim().toLowerCase();

if (answer !== "y" && answer !== "yes") {
  console.log("Aborted.");
  process.exit(1);
}

// ── 6. Stage, commit, tag, push ─────────────────────────────────────────────

const filesToStage = [...pkg.versionFiles.map((f) => f.path), pkg.changelogPath];

Bun.spawnSync(["git", "add", ...filesToStage], { cwd: ROOT, stdio: ["inherit", "inherit", "inherit"] });
Bun.spawnSync(["git", "commit", "-m", commitMsg], { cwd: ROOT, stdio: ["inherit", "inherit", "inherit"] });
Bun.spawnSync(["git", "tag", "-a", tag, "-m", `Release ${pkg.name}@${next}`], { cwd: ROOT, stdio: ["inherit", "inherit", "inherit"] });

if (!noPush) {
  Bun.spawnSync(["git", "push", "--follow-tags"], { cwd: ROOT, stdio: ["inherit", "inherit", "inherit"] });
  console.log(`\nPushed ${tag}`);
} else {
  console.log(`\nCommitted and tagged ${tag} (not pushed — use git push --follow-tags)`);
}
