import type { Plugin } from "prettier";
import { parse as parseRdn } from "./parser.js";
import { print } from "./printer.js";
import type { DocumentNode, RdnCstNode } from "./cst.js";

const plugin: Plugin<DocumentNode | RdnCstNode> = {
  languages: [
    {
      name: "RDN",
      parsers: ["rdn"],
      extensions: [".rdn"],
      vscodeLanguageIds: ["rdn"],
    },
  ],
  options: {
    useExplicitMapKeyword: {
      type: "boolean",
      category: "RDN",
      default: false,
      description: "Keep the explicit Map keyword on non-empty maps (e.g. Map{ k => v }). When false, non-empty maps are formatted as implicit ({ k => v }).",
      since: "0.2.0",
    },
    useExplicitSetKeyword: {
      type: "boolean",
      category: "RDN",
      default: false,
      description: "Keep the explicit Set keyword on non-empty sets (e.g. Set{ 1, 2 }). When false, non-empty sets are formatted as implicit ({ 1, 2 }).",
      since: "0.2.0",
    },
    sortKeys: {
      type: "boolean",
      category: "RDN",
      default: false,
      description: "Sort object keys alphabetically (ascending). Applies recursively to all nested objects.",
      since: "0.2.0",
    },
  } as Record<string, any>,
  parsers: {
    rdn: {
      parse: (text: string) => parseRdn(text),
      astFormat: "rdn-ast",
      locStart: (node: DocumentNode | RdnCstNode) => node.start,
      locEnd: (node: DocumentNode | RdnCstNode) => node.end,
    },
  },
  printers: {
    "rdn-ast": { print },
  },
  defaultOptions: {
    tabWidth: 2,
    printWidth: 80,
    bracketSpacing: true,
  },
};

export const { languages, options, parsers, printers, defaultOptions } = plugin;
