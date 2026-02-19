import type { Doc, ParserOptions } from "prettier";
import { doc } from "prettier";
import type { DocumentNode, RdnCstNode, ObjectPropertyNode, MapEntryNode } from "./cst.js";
import { ESCAPE_TABLE } from "./tables.js";

const { group, indent, line, softline, hardline, join } = doc.builders;

// Prettier's AstPath traversal doesn't preserve strict types through path.call/map,
// so we use `any` here as all real-world Prettier plugins do.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyPath = any;
type PrintFn = (path: AnyPath) => Doc;

type AnyNode = DocumentNode | RdnCstNode | ObjectPropertyNode | MapEntryNode;

function escapeString(value: string): string {
  let result = '"';
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);
    if (code < 256) {
      const esc = ESCAPE_TABLE[code]!;
      result += esc || value[i];
    } else {
      result += value[i];
    }
  }
  result += '"';
  return result;
}

function printNode(path: AnyPath, options: ParserOptions<DocumentNode>, printFn: PrintFn): Doc {
  const node: AnyNode = path.node;

  switch (node.type) {
    case "Document":
      return [path.call(printFn, "body"), hardline];

    case "StringLiteral":
      return escapeString(node.value);

    case "NumberLiteral":
    case "BigIntLiteral":
    case "DateTimeLiteral":
    case "TimeOnlyLiteral":
    case "DurationLiteral":
    case "BinaryLiteral":
    case "RegExpLiteral":
      return node.raw;

    case "BooleanLiteral":
      return node.value ? "true" : "false";

    case "NullLiteral":
      return "null";

    case "NaNLiteral":
      return "NaN";

    case "InfinityLiteral":
      return node.negative ? "-Infinity" : "Infinity";

    case "Array": {
      if (node.elements.length === 0) return "[]";
      const printed = path.map(printFn, "elements");
      return group(["[", indent([softline, join([",", line], printed)]), softline, "]"]);
    }

    case "Tuple": {
      if (node.elements.length === 0) return "()";
      const printed = path.map(printFn, "elements");
      return group(["(", indent([softline, join([",", line], printed)]), softline, ")"]);
    }

    case "Object": {
      if (node.properties.length === 0) return "{}";
      if ((options as any).sortKeys) {
        node.properties.sort((a: ObjectPropertyNode, b: ObjectPropertyNode) => a.key.value.localeCompare(b.key.value));
      }
      const printed = path.map(printFn, "properties");
      const bracketLine = options.bracketSpacing ? line : softline;
      return group(["{", indent([bracketLine, join([",", line], printed)]), bracketLine, "}"]);
    }

    case "ObjectProperty":
      return [path.call(printFn, "key"), ": ", path.call(printFn, "value")];

    case "Map": {
      if (node.entries.length === 0) return node.explicit ? "Map{}" : "{}";
      const explicitMap = node.explicit && (options as any).useExplicitMapKeyword;
      const prefix = explicitMap ? "Map{" : "{";
      const printed = path.map(printFn, "entries");
      const bracketLine = options.bracketSpacing ? line : softline;
      return group([prefix, indent([bracketLine, join([",", line], printed)]), bracketLine, "}"]);
    }

    case "MapEntry":
      return [path.call(printFn, "key"), " => ", path.call(printFn, "value")];

    case "Set": {
      if (node.elements.length === 0) return node.explicit ? "Set{}" : "{}";
      const explicitSet = node.explicit && (options as any).useExplicitSetKeyword;
      const prefix = explicitSet ? "Set{" : "{";
      const printed = path.map(printFn, "elements");
      const bracketLine = options.bracketSpacing ? line : softline;
      return group([prefix, indent([bracketLine, join([",", line], printed)]), bracketLine, "}"]);
    }

    default: {
      const _exhaustive: never = node;
      throw new Error(`Unknown node type: ${(_exhaustive as AnyNode).type}`);
    }
  }
}

export function print(path: AnyPath, options: ParserOptions<DocumentNode>, printFn: PrintFn): Doc {
  return printNode(path, options, printFn);
}
