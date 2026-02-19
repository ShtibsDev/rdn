import { defineConfig } from "vitest/config";
import path from "path";

export default defineConfig({
  resolve: {
    alias: {
      "@rdn/parser": path.resolve(__dirname, "../../packages/rdn-js/src/parser.ts"),
      "@rdn/cst-parser": path.resolve(__dirname, "../../tools/prettier-plugin-rdn/src/parser.ts"),
      "@rdn/cst-types": path.resolve(__dirname, "../../tools/prettier-plugin-rdn/src/cst.ts"),
    },
  },
});
