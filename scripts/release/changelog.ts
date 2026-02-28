#!/usr/bin/env bun
/**
 * Changelog generator — produces Keep-a-Changelog entries from conventional commits.
 *
 * Usage:
 *   bun scripts/release/changelog.ts --package <name> [--version <ver>] [--from <ref>] [--to <ref>] [--stdout] [--dry-run]
 */
import { parseArgs } from "node:util";
import { getPackage } from "./packages";
import { getLatestTag, getRootCommit, getCommitsBetween, parseConventionalCommit, readVersion, formatChangelog, prependToChangelog } from "./utils";

const { values } = parseArgs({
  options: {
    package: { type: "string" },
    version: { type: "string" },
    from: { type: "string" },
    to: { type: "string", default: "HEAD" },
    stdout: { type: "boolean", default: false },
    "dry-run": { type: "boolean", default: false },
  },
  strict: true,
});

if (!values.package) {
  console.error("Usage: bun scripts/release/changelog.ts --package <name> [--version <ver>] [--from <ref>] [--to <ref>] [--stdout] [--dry-run]");
  process.exit(1);
}

const pkg = getPackage(values.package);
const version = values.version ?? readVersion(pkg);
const from = values.from ?? getLatestTag(pkg.tagPrefix) ?? getRootCommit();
const to = values.to!;

const rawCommits = getCommitsBetween(from, to);
const parsed = rawCommits.map((c) => parseConventionalCommit(c.hash, c.message)).filter((c) => c !== null);

// Filter by scope — commits matching any of the package's scopes
const filtered = parsed.filter((c) => c.scopes.some((s) => pkg.scopes.includes(s)));

if (filtered.length === 0) {
  console.warn(`No matching commits for ${pkg.name} since ${from.slice(0, 7)}`);
  process.exit(0);
}

const date = new Date().toISOString().slice(0, 10);
const entry = formatChangelog(filtered, version, date);

if (values.stdout || values["dry-run"]) {
  console.log(entry);
  if (values["dry-run"]) {
    console.log(`\n[dry-run] Would update ${pkg.changelogPath}`);
  }
} else {
  prependToChangelog(pkg.changelogPath, entry);
  console.log(`Updated ${pkg.changelogPath} with ${filtered.length} commit(s) for v${version}`);
}
