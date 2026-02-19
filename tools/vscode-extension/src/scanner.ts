/**
 * Lightweight scanner that detects unquoted object keys in RDN text.
 *
 * Does NOT fully parse RDN — it only tracks enough context to identify
 * bare identifiers in object-key position (i.e. `{foo: 1}` instead of `{"foo": 1}`).
 */

export interface UnquotedKey {
  /** The bare identifier text */
  name: string;
  /** 0-based byte offset of the first character */
  offset: number;
  /** Length of the identifier */
  length: number;
}

const enum Ctx {
  /** `{` seen but not yet disambiguated */
  UnknownBrace,
  Object,
  Map,
  Set,
  ExplicitMap,
  ExplicitSet,
  Array,
  Tuple,
}

const RDN_KEYWORDS = new Set([
  "true",
  "false",
  "null",
  "NaN",
  "Infinity",
  "Map",
  "Set",
]);

function isIdentStart(ch: string): boolean {
  return (
    (ch >= "a" && ch <= "z") ||
    (ch >= "A" && ch <= "Z") ||
    ch === "_" ||
    ch === "$"
  );
}

function isIdentChar(ch: string): boolean {
  return (
    isIdentStart(ch) || (ch >= "0" && ch <= "9")
  );
}

function isWhitespace(ch: string): boolean {
  return ch === " " || ch === "\t" || ch === "\n" || ch === "\r";
}

/**
 * Scan `text` and return all unquoted keys found in object contexts.
 */
export function scanUnquotedKeys(text: string): UnquotedKey[] {
  const results: UnquotedKey[] = [];
  const stack: Ctx[] = [];
  let i = 0;
  const len = text.length;

  function peek(): string {
    return i < len ? text[i] : "";
  }

  function skipWhitespace(): void {
    while (i < len && isWhitespace(text[i])) {
      i++;
    }
  }

  function skipString(): void {
    // i is on the opening quote
    i++; // skip opening "
    while (i < len) {
      if (text[i] === "\\") {
        i += 2; // skip escape + next char
      } else if (text[i] === '"') {
        i++; // skip closing "
        return;
      } else {
        i++;
      }
    }
  }

  function skipRegex(): void {
    // i is on the opening /
    i++; // skip opening /
    while (i < len) {
      if (text[i] === "\\") {
        i += 2;
      } else if (text[i] === "/") {
        i++; // skip closing /
        // skip flags
        while (i < len && /[dgimsuvy]/.test(text[i])) {
          i++;
        }
        return;
      } else {
        i++;
      }
    }
  }

  function readIdent(): string {
    const start = i;
    while (i < len && isIdentChar(text[i])) {
      i++;
    }
    return text.slice(start, i);
  }

  function currentCtx(): Ctx | undefined {
    return stack.length > 0 ? stack[stack.length - 1] : undefined;
  }

  function resolveUnknownBrace(to: Ctx): void {
    const idx = stack.length - 1;
    if (idx >= 0 && stack[idx] === Ctx.UnknownBrace) {
      stack[idx] = to;
    }
  }

  while (i < len) {
    skipWhitespace();
    if (i >= len) break;

    const ch = text[i];
    const ctx = currentCtx();

    // Opening brackets
    if (ch === "{") {
      // Check for Map{ or Set{
      // (handled below when we read an identifier)
      stack.push(Ctx.UnknownBrace);
      i++;
      continue;
    }

    if (ch === "[") {
      stack.push(Ctx.Array);
      i++;
      continue;
    }

    if (ch === "(") {
      stack.push(Ctx.Tuple);
      i++;
      continue;
    }

    // Closing brackets
    if (ch === "}" || ch === "]" || ch === ")") {
      if (ch === "}" && ctx === Ctx.UnknownBrace) {
        // Empty {} → Object (no diagnostics needed for empty)
        resolveUnknownBrace(Ctx.Object);
      }
      if (stack.length > 0) stack.pop();
      i++;
      continue;
    }

    // String
    if (ch === '"') {
      skipString();

      // After a string in an unknown-brace context, look at what follows
      if (ctx === Ctx.UnknownBrace) {
        skipWhitespace();
        const next = peek();
        if (next === ":") {
          resolveUnknownBrace(Ctx.Object);
        } else if (next === "=" && i + 1 < len && text[i + 1] === ">") {
          resolveUnknownBrace(Ctx.Map);
        } else if (next === "," || next === "}") {
          resolveUnknownBrace(Ctx.Set);
        }
      }
      continue;
    }

    // @ prefix — skip date/time/duration literals
    if (ch === "@") {
      i++; // skip @
      // Consume until whitespace or structural char
      while (i < len && !isWhitespace(text[i]) && !/[,}\])\r\n]/.test(text[i])) {
        i++;
      }
      continue;
    }

    // Binary: b" or x"
    if ((ch === "b" || ch === "x") && i + 1 < len && text[i + 1] === '"') {
      i++; // skip prefix
      skipString();
      continue;
    }

    // Regex: / (only when not after a number or in ambiguous division context)
    // Simple heuristic: / at start of value position is regex
    if (ch === "/") {
      skipRegex();
      continue;
    }

    // Arrow =>
    if (ch === "=" && i + 1 < len && text[i + 1] === ">") {
      if (ctx === Ctx.UnknownBrace) {
        resolveUnknownBrace(Ctx.Map);
      }
      i += 2;
      continue;
    }

    // Colon : (object key-value separator)
    if (ch === ":") {
      if (ctx === Ctx.UnknownBrace) {
        resolveUnknownBrace(Ctx.Object);
      }
      i++;
      continue;
    }

    // Comma
    if (ch === ",") {
      if (ctx === Ctx.UnknownBrace) {
        resolveUnknownBrace(Ctx.Set);
      }
      i++;
      continue;
    }

    // Numbers (including negative, BigInt, special)
    if (ch === "-" || (ch >= "0" && ch <= "9")) {
      if (ch === "-") i++;
      while (i < len && ((text[i] >= "0" && text[i] <= "9") || text[i] === "." || text[i] === "e" || text[i] === "E" || text[i] === "+" || text[i] === "-" || text[i] === "n")) {
        i++;
      }
      continue;
    }

    // Identifiers (keywords, potential unquoted keys, Map/Set prefixes)
    if (isIdentStart(ch)) {
      const identStart = i;
      const ident = readIdent();

      // Map{ or Set{ — push explicit context
      if ((ident === "Map" || ident === "Set") && i < len && text[i] === "{") {
        stack.push(ident === "Map" ? Ctx.ExplicitMap : Ctx.ExplicitSet);
        i++; // skip {
        continue;
      }

      // -Infinity
      if (ident === "Infinity") {
        continue;
      }

      // RDN keywords — never unquoted keys
      if (RDN_KEYWORDS.has(ident)) {
        // In unknown-brace, check what follows to disambiguate
        if (ctx === Ctx.UnknownBrace) {
          skipWhitespace();
          const next = peek();
          if (next === "," || next === "}") {
            resolveUnknownBrace(Ctx.Set);
          }
        }
        continue;
      }

      // If we're in an object or unknown-brace context, and the next non-ws char is :,
      // this is an unquoted key
      skipWhitespace();
      const next = peek();

      if (next === ":") {
        // This identifier is used as a key
        if (ctx === Ctx.UnknownBrace) {
          resolveUnknownBrace(Ctx.Object);
        }
        const resolvedCtx = currentCtx();
        if (resolvedCtx === Ctx.Object) {
          results.push({ name: ident, offset: identStart, length: ident.length });
        }
      } else if (ctx === Ctx.UnknownBrace) {
        if (next === "=" && i + 1 < len && text[i + 1] === ">") {
          resolveUnknownBrace(Ctx.Map);
        } else if (next === "," || next === "}") {
          resolveUnknownBrace(Ctx.Set);
        }
      }
      continue;
    }

    // Skip any other character
    i++;
  }

  return results;
}

// ─── Binary character validation ─────────────────────────────────────────────

export interface BinaryCharError {
  /** 0-based byte offset of the invalid character */
  offset: number;
  /** Length of the invalid span (always 1) */
  length: number;
  /** Human-readable error message */
  message: string;
  /** Which binary encoding this error belongs to */
  kind: "base64" | "hex";
}

/**
 * Scan `text` and return all invalid characters found inside `b"..."` and `x"..."` literals.
 *
 * Valid base64 content: A-Z, a-z, 0-9, +, /   (padding `=` only at the end)
 * Valid hex content: 0-9, A-F, a-f
 */
export function scanBinaryErrors(text: string): BinaryCharError[] {
  const results: BinaryCharError[] = [];
  let i = 0;
  const len = text.length;

  while (i < len) {
    const ch = text[i]!;

    // Binary literal: b" or x"
    if ((ch === "b" || ch === "x") && i + 1 < len && text[i + 1] === '"') {
      const kind: "base64" | "hex" = ch === "b" ? "base64" : "hex";
      const contentStart = i + 2; // after prefix + opening "

      // Find closing quote (handle escape sequences to find correct end)
      let j = contentStart;
      while (j < len) {
        if (text[j] === "\\") { j += 2; continue; }
        if (text[j] === '"') break;
        j++;
      }
      const contentEnd = j;

      // Validate every character in the content
      if (kind === "hex") {
        for (let k = contentStart; k < contentEnd; k++) {
          const c = text[k]!;
          if (!/[0-9A-Fa-f]/.test(c)) {
            results.push({ offset: k, length: 1, message: `Invalid hex character '${c}'`, kind: "hex" });
          }
        }
      } else {
        let foundPadding = false;
        for (let k = contentStart; k < contentEnd; k++) {
          const c = text[k]!;
          if (c === "=") {
            foundPadding = true;
          } else if (foundPadding) {
            results.push({ offset: k, length: 1, message: `Invalid base64: data after padding '='`, kind: "base64" });
          } else if (!/[A-Za-z0-9+/]/.test(c)) {
            results.push({ offset: k, length: 1, message: `Invalid base64 character '${c}'`, kind: "base64" });
          }
        }
      }

      i = contentEnd < len ? contentEnd + 1 : contentEnd; // past closing "
      continue;
    }

    // Regular string — skip entirely to avoid false positives
    if (ch === '"') {
      i++;
      while (i < len) {
        if (text[i] === "\\") { i += 2; continue; }
        if (text[i] === '"') { i++; break; }
        i++;
      }
      continue;
    }

    // @ literal — skip
    if (ch === "@") {
      i++;
      while (i < len && /[\w.:\-+TZP]/.test(text[i]!)) i++;
      continue;
    }

    // Regex — skip
    if (ch === "/") {
      i++;
      while (i < len) {
        if (text[i] === "\\") { i += 2; continue; }
        if (text[i] === "/") { i++; while (i < len && /[dgimsuvy]/.test(text[i]!)) i++; break; }
        i++;
      }
      continue;
    }

    i++;
  }

  return results;
}
