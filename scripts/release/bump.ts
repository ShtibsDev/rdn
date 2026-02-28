#!/usr/bin/env bun
/**
 * Version bumper — reads the current version, computes the new one, writes to all version files.
 *
 * Usage:
 *   bun scripts/release/bump.ts --package <name> --bump <patch|minor|major|prerelease> [--preid <alpha|beta|rc>] [--dry-run]
 */
import { parseArgs } from "node:util";
import { getPackage } from "./packages";
import { readVersion, writeVersion, bumpVersion, type BumpType } from "./utils";

const { values } = parseArgs({
  options: {
    package: { type: "string" },
    bump: { type: "string" },
    preid: { type: "string" },
    "dry-run": { type: "boolean", default: false },
  },
  strict: true,
});

if (!values.package || !values.bump) {
  console.error("Usage: bun scripts/release/bump.ts --package <name> --bump <patch|minor|major|prerelease> [--preid <alpha|beta|rc>] [--dry-run]");
  process.exit(1);
}

const validBumps = ["patch", "minor", "major", "prerelease"];
if (!validBumps.includes(values.bump)) {
  console.error(`Invalid bump type "${values.bump}". Must be one of: ${validBumps.join(", ")}`);
  process.exit(1);
}

const pkg = getPackage(values.package);
const current = readVersion(pkg);
const next = bumpVersion(current, values.bump as BumpType, values.preid);

console.log(`${pkg.name}: ${current} → ${next}`);

if (pkg.versionFiles.length === 0) {
  console.log(`(tag-only package — no version files to update)`);
} else if (values["dry-run"]) {
  console.log(`[dry-run] Would update: ${pkg.versionFiles.map((f) => f.path).join(", ")}`);
} else {
  writeVersion(pkg, next);
  console.log(`Updated: ${pkg.versionFiles.map((f) => f.path).join(", ")}`);
}
