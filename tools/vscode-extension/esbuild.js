const esbuild = require("esbuild");
const path = require("path");

const production = process.argv.includes("--production");
const watch = process.argv.includes("--watch");

async function main() {
  const ctx = await esbuild.context({
    entryPoints: ["src/extension.ts"],
    bundle: true,
    format: "cjs",
    minify: production,
    sourcemap: !production,
    sourcesContent: false,
    platform: "node",
    outfile: "out/extension.js",
    external: ["vscode"],
    logLevel: "silent",
    alias: {
      "@rdn/parser": path.resolve(__dirname, "../../packages/rdn-js/src/parser.ts"),
      "@rdn/cst-parser": path.resolve(__dirname, "../../tools/prettier-plugin-rdn/src/parser.ts"),
      "@rdn/cst-types": path.resolve(__dirname, "../../tools/prettier-plugin-rdn/src/cst.ts"),
    },
  });

  if (watch) {
    await ctx.watch();
    console.log("watching...");
  } else {
    await ctx.rebuild();
    await ctx.dispose();
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
