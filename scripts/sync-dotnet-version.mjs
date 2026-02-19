#!/usr/bin/env node
/**
 * Syncs the version from packages/rdn-dotnet/package.json → Directory.Build.props.
 * Run automatically as part of `pnpm version-packages`.
 */
import { readFileSync, writeFileSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const pkgPath = resolve(root, "packages/rdn-dotnet/package.json");
const propsPath = resolve(root, "packages/rdn-dotnet/Directory.Build.props");

const { version } = JSON.parse(readFileSync(pkgPath, "utf8"));
const props = readFileSync(propsPath, "utf8");
const updated = props.replace(/<Version>[^<]*<\/Version>/, `<Version>${version}</Version>`);

if (updated !== props) {
  writeFileSync(propsPath, updated);
  console.log(`Synced rdn-dotnet version → ${version}`);
} else {
  console.log(`rdn-dotnet version already ${version}`);
}
