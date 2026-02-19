import { parse } from "@rdn/cst-parser";
import type { RdnCstNode, DocumentNode, StringLiteralNode } from "@rdn/cst-types";

const PRINT_WIDTH = 80;

export interface RdnFormatOptions {
  useExplicitMapKeyword?: boolean;
  useExplicitSetKeyword?: boolean;
}

/** Escape a string value with canonical escape sequences */
function escapeString(value: string): string {
  let result = '"';
  for (let i = 0; i < value.length; i++) {
    const ch = value.charCodeAt(i);
    switch (ch) {
      case 0x22: result += '\\"'; break;
      case 0x5c: result += "\\\\"; break;
      case 0x08: result += "\\b"; break;
      case 0x09: result += "\\t"; break;
      case 0x0a: result += "\\n"; break;
      case 0x0c: result += "\\f"; break;
      case 0x0d: result += "\\r"; break;
      default:
        if (ch < 0x20) {
          result += `\\u${ch.toString(16).padStart(4, "0")}`;
        } else {
          result += value[i];
        }
    }
  }
  return result + '"';
}

/** Try to print a node on a single line (no newlines) */
function printCompact(node: RdnCstNode, rdnOpts: RdnFormatOptions): string {
  switch (node.type) {
    case "StringLiteral": return escapeString(node.value);
    case "NumberLiteral":
    case "BigIntLiteral":
    case "DateTimeLiteral":
    case "TimeOnlyLiteral":
    case "DurationLiteral":
    case "BinaryLiteral":
    case "RegExpLiteral": return node.raw;
    case "NaNLiteral": return "NaN";
    case "BooleanLiteral": return node.value ? "true" : "false";
    case "NullLiteral": return "null";
    case "InfinityLiteral": return node.negative ? "-Infinity" : "Infinity";
    case "Array": {
      if (node.elements.length === 0) return "[]";
      const inner = node.elements.map((el) => printCompact(el, rdnOpts)).join(", ");
      return `[${inner}]`;
    }
    case "Tuple": {
      if (node.elements.length === 0) return "()";
      const inner = node.elements.map((el) => printCompact(el, rdnOpts)).join(", ");
      return `(${inner})`;
    }
    case "Object": {
      if (node.properties.length === 0) return "{}";
      const inner = node.properties.map((prop) => `${escapeString(prop.key.value)}: ${printCompact(prop.value, rdnOpts)}`).join(", ");
      return `{ ${inner} }`;
    }
    case "Map": {
      if (node.entries.length === 0) return node.explicit ? "Map{}" : "{}";
      const useKeyword = node.explicit && rdnOpts.useExplicitMapKeyword;
      const prefix = useKeyword ? "Map" : "";
      const inner = node.entries.map((entry) => `${printCompact(entry.key, rdnOpts)} => ${printCompact(entry.value, rdnOpts)}`).join(", ");
      return `${prefix}{ ${inner} }`;
    }
    case "Set": {
      if (node.elements.length === 0) return node.explicit ? "Set{}" : "{}";
      const useKeyword = node.explicit && rdnOpts.useExplicitSetKeyword;
      const prefix = useKeyword ? "Set" : "";
      const inner = node.elements.map((el) => printCompact(el, rdnOpts)).join(", ");
      return `${prefix}{ ${inner} }`;
    }
  }
}

/** Print a node with indentation, expanding to multi-line when needed */
function printNode(node: RdnCstNode, indent: string, indentUnit: string, rdnOpts: RdnFormatOptions): string {
  // Try compact first
  const compact = printCompact(node, rdnOpts);
  if (!compact.includes("\n") && indent.length + compact.length <= PRINT_WIDTH) {
    return compact;
  }

  // Expand to multi-line for collection types
  const childIndent = indent + indentUnit;

  switch (node.type) {
    case "Array": {
      if (node.elements.length === 0) return "[]";
      const lines = node.elements.map((el) => `${childIndent}${printNode(el, childIndent, indentUnit, rdnOpts)}`);
      return `[\n${lines.join(",\n")}\n${indent}]`;
    }
    case "Tuple": {
      if (node.elements.length === 0) return "()";
      const lines = node.elements.map((el) => `${childIndent}${printNode(el, childIndent, indentUnit, rdnOpts)}`);
      return `(\n${lines.join(",\n")}\n${indent})`;
    }
    case "Object": {
      if (node.properties.length === 0) return "{}";
      const lines = node.properties.map((prop) => {
        const val = printNode(prop.value, childIndent, indentUnit, rdnOpts);
        return `${childIndent}${escapeString(prop.key.value)}: ${val}`;
      });
      return `{\n${lines.join(",\n")}\n${indent}}`;
    }
    case "Map": {
      if (node.entries.length === 0) return node.explicit ? "Map{}" : "{}";
      const useKeyword = node.explicit && rdnOpts.useExplicitMapKeyword;
      const prefix = useKeyword ? "Map" : "";
      const lines = node.entries.map((entry) => {
        const key = printNode(entry.key, childIndent, indentUnit, rdnOpts);
        const val = printNode(entry.value, childIndent, indentUnit, rdnOpts);
        return `${childIndent}${key} => ${val}`;
      });
      return `${prefix}{\n${lines.join(",\n")}\n${indent}}`;
    }
    case "Set": {
      if (node.elements.length === 0) return node.explicit ? "Set{}" : "{}";
      const useKeyword = node.explicit && rdnOpts.useExplicitSetKeyword;
      const prefix = useKeyword ? "Set" : "";
      const lines = node.elements.map((el) => `${childIndent}${printNode(el, childIndent, indentUnit, rdnOpts)}`);
      return `${prefix}{\n${lines.join(",\n")}\n${indent}}`;
    }
    default:
      // Atomic nodes always fit on one line
      return compact;
  }
}

/** Recursively sort object keys (ascending) in all nested nodes */
function sortKeys(node: RdnCstNode): void {
  switch (node.type) {
    case "Object":
      node.properties.sort((a, b) => a.key.value.localeCompare(b.key.value));
      for (const prop of node.properties) sortKeys(prop.value);
      break;
    case "Array":
    case "Tuple":
    case "Set":
      for (const el of node.elements) sortKeys(el);
      break;
    case "Map":
      for (const entry of node.entries) { sortKeys(entry.key); sortKeys(entry.value); }
      break;
  }
}

/**
 * Format an RDN document.
 * Returns the original text unchanged if parsing fails.
 */
export function format(text: string, tabSize: number, insertSpaces: boolean, rdnOpts: RdnFormatOptions = {}): string {
  let doc: DocumentNode;
  try {
    doc = parse(text);
  } catch {
    return text;
  }

  const indentUnit = insertSpaces ? " ".repeat(tabSize) : "\t";
  const result = printNode(doc.body, "", indentUnit, rdnOpts);
  return result + "\n";
}

/**
 * Format an RDN document with all object keys sorted alphabetically.
 * Returns null if parsing fails.
 */
export function formatSorted(text: string, tabSize: number, insertSpaces: boolean, rdnOpts: RdnFormatOptions = {}): string | null {
  let doc: DocumentNode;
  try {
    doc = parse(text);
  } catch {
    return null;
  }

  sortKeys(doc.body);
  const indentUnit = insertSpaces ? " ".repeat(tabSize) : "\t";
  const result = printNode(doc.body, "", indentUnit, rdnOpts);
  return result + "\n";
}
