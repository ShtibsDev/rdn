import * as vscode from "vscode";
import { getHoverConfig, type RdnHoverConfig } from "./config";
import { formatDate, expandDuration, groupDigits, formatByteSize } from "./format";

// ─── Token Types ─────────────────────────────────────────────────────────────

export type TokenKind =
  | "dateTimeFull"
  | "dateTimeNoMillis"
  | "dateOnly"
  | "unixTimestamp"
  | "timeOnly"
  | "duration"
  | "bigint"
  | "binaryBase64"
  | "binaryHex"
  | "regexp"
  | "nan"
  | "infinity"
  | "negInfinity"
  | "mapKeyword"
  | "setKeyword"
  | "mapArrow"
  | "tuple"
  | "implicitMap"
  | "implicitSet";

export interface TokenInfo {
  kind: TokenKind;
  text: string;
  range: vscode.Range;
}

// ─── String detection (quote-parity scan) ────────────────────────────────────

function isInsideString(lineText: string, charPos: number): boolean {
  let inString = false;
  for (let i = 0; i < charPos; i++) {
    const c = lineText[i]!;
    if (c === "\\" && inString) { i++; continue; }
    if (c === '"') {
      // Skip binary literals: b"..." and x"..." are not regular strings
      if (!inString && i > 0 && (lineText[i - 1] === "b" || lineText[i - 1] === "x")) {
        // Find the closing quote and skip the entire binary literal
        let j = i + 1;
        while (j < lineText.length) {
          if (lineText[j] === "\\" ) { j += 2; continue; }
          if (lineText[j] === '"') break;
          j++;
        }
        if (j < lineText.length && j >= charPos) return false; // charPos is inside a binary literal
        i = j; // skip past closing quote
        continue;
      }
      inString = !inString;
    }
  }
  return inString;
}

// ─── Token Detection ─────────────────────────────────────────────────────────

export function detectToken(document: vscode.TextDocument, position: vscode.Position): TokenInfo | null {
  const line = position.line;
  const lineText = document.lineAt(line).text;
  const col = position.character;

  if (col >= lineText.length) return null;
  if (isInsideString(lineText, col)) return null;

  const ch = lineText[col]!;

  // ── @ prefix: DateTime, TimeOnly, Duration, Unix ──
  // Find the @ that contains this position
  if (ch === "@" || isPartOfAtLiteral(lineText, col)) {
    const atStart = findAtStart(lineText, col);
    if (atStart !== -1) {
      const atEnd = findAtEnd(lineText, atStart);
      const text = lineText.slice(atStart, atEnd);
      const body = text.slice(1); // strip @

      const range = new vscode.Range(line, atStart, line, atEnd);

      if (body.startsWith("P") || body.startsWith("-P")) {
        return { kind: "duration", text, range };
      }
      if (/^\d{4}-/.test(body)) {
        if (/T/.test(body)) {
          if (/\.\d+/.test(body)) return { kind: "dateTimeFull", text, range };
          return { kind: "dateTimeNoMillis", text, range };
        }
        return { kind: "dateOnly", text, range };
      }
      if (/^\d+:\d{2}/.test(body)) {
        return { kind: "timeOnly", text, range };
      }
      if (/^\d+$/.test(body)) {
        return { kind: "unixTimestamp", text, range };
      }
    }
  }

  // ── BigInt: digits followed by n ──
  if ((ch >= "0" && ch <= "9") || ch === "n" || ch === "-") {
    const bigintMatch = matchBigInt(lineText, col);
    if (bigintMatch) {
      const range = new vscode.Range(line, bigintMatch.start, line, bigintMatch.end);
      return { kind: "bigint", text: lineText.slice(bigintMatch.start, bigintMatch.end), range };
    }
  }

  // ── Binary: b"..." or x"..." ──
  // Also detect when hovering inside binary content (not just on prefix or quotes)
  {
    const binMatch = matchBinary(lineText, col);
    if (binMatch) {
      const range = new vscode.Range(line, binMatch.start, line, binMatch.end);
      const kind = lineText[binMatch.start] === "b" ? "binaryBase64" : "binaryHex";
      return { kind, text: lineText.slice(binMatch.start, binMatch.end), range };
    }
  }

  // ── RegExp: /pattern/flags ──
  if (ch === "/") {
    const regMatch = matchRegExp(lineText, col);
    if (regMatch) {
      const range = new vscode.Range(line, regMatch.start, line, regMatch.end);
      return { kind: "regexp", text: lineText.slice(regMatch.start, regMatch.end), range };
    }
  }
  // Also detect when hovering inside a regex body or on flags
  if (ch !== "/" && !isInsideString(lineText, col)) {
    const regInside = matchRegExpContaining(lineText, col);
    if (regInside) {
      const range = new vscode.Range(line, regInside.start, line, regInside.end);
      return { kind: "regexp", text: lineText.slice(regInside.start, regInside.end), range };
    }
  }

  // ── Identifiers: NaN, Infinity, -Infinity, Map, Set ──
  if (ch === "N" && lineText.startsWith("NaN", col)) {
    return { kind: "nan", text: "NaN", range: new vscode.Range(line, col, line, col + 3) };
  }
  if (ch === "I" && lineText.startsWith("Infinity", col)) {
    // Check for preceding -
    const start = col > 0 && lineText[col - 1] === "-" ? col - 1 : col;
    const text = lineText.slice(start, col + 8);
    if (text === "-Infinity") return { kind: "negInfinity", text, range: new vscode.Range(line, start, line, col + 8) };
    return { kind: "infinity", text: "Infinity", range: new vscode.Range(line, col, line, col + 8) };
  }
  if (ch === "-" && lineText.startsWith("-Infinity", col)) {
    return { kind: "negInfinity", text: "-Infinity", range: new vscode.Range(line, col, line, col + 9) };
  }

  // Map/Set keyword — only when followed by {
  if (ch === "M" && lineText.startsWith("Map", col)) {
    const afterKeyword = col + 3;
    if (afterKeyword < lineText.length && lineText[afterKeyword] === "{") {
      return { kind: "mapKeyword", text: "Map", range: new vscode.Range(line, col, line, afterKeyword) };
    }
  }
  if (ch === "S" && lineText.startsWith("Set", col)) {
    const afterKeyword = col + 3;
    if (afterKeyword < lineText.length && lineText[afterKeyword] === "{") {
      return { kind: "setKeyword", text: "Set", range: new vscode.Range(line, col, line, afterKeyword) };
    }
  }

  // ── => arrow ──
  if (ch === "=" && col + 1 < lineText.length && lineText[col + 1] === ">") {
    return { kind: "mapArrow", text: "=>", range: new vscode.Range(line, col, line, col + 2) };
  }
  if (ch === ">" && col > 0 && lineText[col - 1] === "=") {
    return { kind: "mapArrow", text: "=>", range: new vscode.Range(line, col - 1, line, col + 1) };
  }

  // ── Tuple ( ──
  if (ch === "(") {
    // Make sure this isn't inside a string
    const offset = document.offsetAt(position);
    const count = countCollectionElements(document, offset);
    if (count !== null) {
      return { kind: "tuple", text: "(", range: new vscode.Range(line, col, line, col + 1) };
    }
  }

  // ── Implicit { — look ahead for => (map) or , / value,value (set) ──
  if (ch === "{") {
    const implicitKind = detectImplicitCollection(document, document.offsetAt(position));
    if (implicitKind) {
      return { kind: implicitKind, text: "{", range: new vscode.Range(line, col, line, col + 1) };
    }
  }

  return null;
}

// ─── Helpers for @ literals ──────────────────────────────────────────────────

function isPartOfAtLiteral(lineText: string, col: number): boolean {
  // Scan backwards to see if there's an @ before us (with valid literal chars between)
  return findAtStart(lineText, col) !== -1;
}

function findAtStart(lineText: string, col: number): number {
  let i = col;
  while (i >= 0) {
    if (lineText[i] === "@") {
      // Make sure it's not inside a string
      if (!isInsideString(lineText, i)) return i;
      return -1;
    }
    const c = lineText[i]!;
    if (/[\w.:\-+TZP]/.test(c)) { i--; continue; }
    break;
  }
  return -1;
}

function findAtEnd(lineText: string, atStart: number): number {
  let i = atStart + 1;
  while (i < lineText.length) {
    const c = lineText[i]!;
    if (/[\w.:\-+TZ]/.test(c)) { i++; continue; }
    break;
  }
  return i;
}

// ─── BigInt matcher ──────────────────────────────────────────────────────────

function matchBigInt(lineText: string, col: number): { start: number; end: number } | null {
  // Find the extent of digits around col, ending with 'n'
  let start = col;
  let end = col;

  // Scan backwards to find start of number
  while (start > 0 && ((lineText[start - 1]! >= "0" && lineText[start - 1]! <= "9") || lineText[start - 1] === "-")) {
    start--;
  }
  // Scan forward to find end
  while (end < lineText.length && lineText[end]! >= "0" && lineText[end]! <= "9") {
    end++;
  }
  // Must end with 'n'
  if (end < lineText.length && lineText[end] === "n") {
    end++;
    // Validate: must have at least one digit
    const text = lineText.slice(start, end);
    if (/^-?\d+n$/.test(text)) return { start, end };
  }
  return null;
}

// ─── Binary matcher ──────────────────────────────────────────────────────────

function matchBinary(lineText: string, col: number): { start: number; end: number } | null {
  // Check if col is on b/x prefix or inside the quoted content
  let prefixPos = -1;

  if ((lineText[col] === "b" || lineText[col] === "x") && col + 1 < lineText.length && lineText[col + 1] === '"') {
    prefixPos = col;
  } else if (lineText[col] === '"') {
    // Check if preceding char is b or x
    if (col > 0 && (lineText[col - 1] === "b" || lineText[col - 1] === "x")) {
      prefixPos = col - 1;
    }
  } else {
    // Scan backwards to find b" or x"
    let i = col;
    while (i >= 0) {
      if ((lineText[i] === "b" || lineText[i] === "x") && i + 1 < lineText.length && lineText[i + 1] === '"') {
        prefixPos = i;
        break;
      }
      if (lineText[i] === '"') {
        // Could be the opening quote — check before it
        if (i > 0 && (lineText[i - 1] === "b" || lineText[i - 1] === "x")) {
          prefixPos = i - 1;
        }
        break;
      }
      i--;
    }
  }

  if (prefixPos === -1) return null;

  // Find closing quote
  let j = prefixPos + 2; // after b"
  while (j < lineText.length) {
    if (lineText[j] === "\\") { j += 2; continue; }
    if (lineText[j] === '"') {
      j++;
      // Verify col is within this range
      if (col >= prefixPos && col < j) return { start: prefixPos, end: j };
      return null;
    }
    j++;
  }
  return null;
}

// ─── RegExp matcher ──────────────────────────────────────────────────────────

function matchRegExp(lineText: string, col: number): { start: number; end: number } | null {
  if (lineText[col] !== "/") return null;

  // Scan forward for closing /
  let i = col + 1;
  let hasBody = false;
  while (i < lineText.length) {
    if (lineText[i] === "\\") { i += 2; hasBody = true; continue; }
    if (lineText[i] === "/") {
      if (!hasBody && i === col + 1) return null; // empty //
      i++; // skip closing /
      // Read flags
      while (i < lineText.length && /[dgimsuvy]/.test(lineText[i]!)) i++;
      return { start: col, end: i };
    }
    hasBody = true;
    i++;
  }
  return null;
}

function matchRegExpContaining(lineText: string, col: number): { start: number; end: number } | null {
  // Scan backwards from col to find a potential opening /
  let i = col;
  while (i >= 0) {
    if (lineText[i] === "/") {
      // Check if this could be the opening / of a regex
      const match = matchRegExp(lineText, i);
      if (match && col >= match.start && col < match.end) return match;
      return null;
    }
    if (lineText[i] === "\n" || lineText[i] === "\r") break;
    i--;
  }
  return null;
}

// ─── Implicit collection detection ───────────────────────────────────────────

function detectImplicitCollection(document: vscode.TextDocument, openOffset: number): "implicitMap" | "implicitSet" | null {
  const text = document.getText();
  let i = openOffset + 1;
  const len = Math.min(text.length, openOffset + 10_000);

  // Skip whitespace
  while (i < len && /\s/.test(text[i]!)) i++;
  if (i >= len || text[i] === "}") return null; // empty {} → object, no hover

  // Skip past first value
  i = skipValue(text, i, len);

  // Skip whitespace
  while (i < len && /\s/.test(text[i]!)) i++;
  if (i >= len) return null;

  if (text[i] === ":" ) return null; // object
  if (text[i] === "=" && i + 1 < len && text[i + 1] === ">") return "implicitMap";
  if (text[i] === "," || text[i] === "}") return "implicitSet";

  return null;
}

/** Skip past a single RDN value starting at position i. Lightweight — handles strings, @-literals, nested structures. */
function skipValue(text: string, i: number, limit: number): number {
  if (i >= limit) return i;
  const ch = text[i]!;

  // String
  if (ch === '"') return skipQuotedString(text, i, limit);
  // @ literal
  if (ch === "@") {
    i++;
    while (i < limit && /[\w.:\-+TZP]/.test(text[i]!)) i++;
    return i;
  }
  // Binary
  if ((ch === "b" || ch === "x") && i + 1 < limit && text[i + 1] === '"') {
    return skipQuotedString(text, i + 1, limit);
  }
  // RegExp
  if (ch === "/") {
    i++;
    while (i < limit) {
      if (text[i] === "\\") { i += 2; continue; }
      if (text[i] === "/") { i++; while (i < limit && /[dgimsuvy]/.test(text[i]!)) i++; return i; }
      i++;
    }
    return i;
  }
  // Nested structure
  if (ch === "{" || ch === "[" || ch === "(") {
    const close = ch === "{" ? "}" : ch === "[" ? "]" : ")";
    let depth = 1;
    i++;
    while (i < limit && depth > 0) {
      if (text[i] === ch) depth++;
      else if (text[i] === close) depth--;
      else if (text[i] === '"') { i = skipQuotedString(text, i, limit); continue; }
      else if (text[i] === "\\") { i++; }
      i++;
    }
    return i;
  }
  // Number, keyword, BigInt, identifier
  while (i < limit && /[^\s,}\])\:=>]/.test(text[i]!)) i++;
  return i;
}

function skipQuotedString(text: string, start: number, limit: number): number {
  let i = start + 1;
  while (i < limit) {
    if (text[i] === "\\") { i += 2; continue; }
    if (text[i] === '"') return i + 1;
    i++;
  }
  return i;
}

// ─── Collection element counting ─────────────────────────────────────────────

const MAX_SCAN_CHARS = 10_000;

export function countCollectionElements(document: vscode.TextDocument, openOffset: number): number | null {
  const text = document.getText();
  const closeChar = text[openOffset] === "(" ? ")" : "}";
  let depth = 1;
  let count = 0;
  let hasContent = false;
  let i = openOffset + 1;
  const limit = Math.min(text.length, openOffset + MAX_SCAN_CHARS);

  while (i < limit && depth > 0) {
    const c = text[i]!;
    if (/\s/.test(c)) { i++; continue; }

    if (c === "{" || c === "[" || c === "(") { depth++; hasContent = true; i++; continue; }
    if (c === "}" || c === "]" || c === ")") {
      depth--;
      if (depth === 0 && c === closeChar) {
        return hasContent ? count + 1 : 0;
      }
      i++; continue;
    }

    if (c === '"') { i = skipQuotedString(text, i, limit); hasContent = true; continue; }
    if ((c === "b" || c === "x") && i + 1 < limit && text[i + 1] === '"') { i = skipQuotedString(text, i + 1, limit); hasContent = true; continue; }
    if (c === "/" && depth === 1) {
      // Skip regex
      i++;
      while (i < limit) {
        if (text[i] === "\\") { i += 2; continue; }
        if (text[i] === "/") { i++; while (i < limit && /[dgimsuvy]/.test(text[i]!)) i++; break; }
        i++;
      }
      hasContent = true;
      continue;
    }
    if (c === "@") {
      i++;
      while (i < limit && /[\w.:\-+TZP]/.test(text[i]!)) i++;
      hasContent = true;
      continue;
    }

    if (c === "," && depth === 1) { count++; i++; continue; }
    if (c === "=" && i + 1 < limit && text[i + 1] === ">") { i += 2; continue; }

    hasContent = true;
    i++;
  }

  // Exceeded scan limit
  if (depth > 0) return null;
  return hasContent ? count + 1 : 0;
}

// ─── Regex flag expansion ────────────────────────────────────────────────────

const FLAG_NAMES: Record<string, string> = {
  d: "hasIndices",
  g: "global",
  i: "ignoreCase",
  m: "multiline",
  s: "dotAll",
  u: "unicode",
  v: "unicodeSets",
  y: "sticky",
};

function expandFlags(flags: string): string {
  return flags.split("").map((f) => FLAG_NAMES[f] || f).join(", ");
}

// ─── Hover Provider ──────────────────────────────────────────────────────────

export class RdnHoverProvider implements vscode.HoverProvider {
  provideHover(document: vscode.TextDocument, position: vscode.Position): vscode.Hover | null {
    const config = getHoverConfig();
    if (!config.enabled) return null;

    const token = detectToken(document, position);
    if (!token) return null;

    switch (token.kind) {
      case "dateTimeFull":
      case "dateTimeNoMillis":
      case "dateOnly":
      case "unixTimestamp":
        return this.hoverDateTime(token, config, document);
      case "timeOnly":
        return config.timeOnly.enabled ? this.hoverTimeOnly(token, config) : null;
      case "duration":
        return config.duration.enabled ? this.hoverDuration(token) : null;
      case "bigint":
        return config.bigint.enabled ? this.hoverBigInt(token, config) : null;
      case "binaryBase64":
        return config.binary.enabled ? this.hoverBinaryBase64(token, config) : null;
      case "binaryHex":
        return config.binary.enabled ? this.hoverBinaryHex(token, config) : null;
      case "regexp":
        return config.regexp.enabled ? this.hoverRegExp(token) : null;
      case "nan":
      case "infinity":
      case "negInfinity":
        return config.specialNumbers.enabled ? this.hoverSpecialNumber(token) : null;
      case "mapKeyword":
      case "setKeyword":
        return config.collections.enabled ? this.hoverExplicitCollection(token, document) : null;
      case "mapArrow":
        return config.collections.enabled ? this.hoverMapArrow(token) : null;
      case "tuple":
        return config.collections.enabled ? this.hoverTuple(token, document) : null;
      case "implicitMap":
      case "implicitSet":
        return config.collections.enabled ? this.hoverImplicitCollection(token, document) : null;
      default:
        return null;
    }
  }

  private hoverDateTime(token: TokenInfo, config: RdnHoverConfig, document: vscode.TextDocument): vscode.Hover | null {
    if (!config.dateTime.enabled) return null;
    const body = token.text.slice(1); // strip @
    const md = new vscode.MarkdownString();
    const diagnostics: string[] = [];

    if (token.kind === "unixTimestamp") {
      const digits = body;
      const isSeconds = digits.length <= 10;
      const ms = isSeconds ? Number(digits) * 1000 : Number(digits);
      const date = new Date(ms);
      if (isNaN(date.getTime())) {
        md.appendMarkdown(`**Unix Timestamp**\n\nInvalid timestamp: \`${body}\``);
        return new vscode.Hover(md, token.range);
      }
      const formatted = formatDate(date, config.dateTime.unixFormat, "YYYY-MM-DD HH:mm:ss [UTC]");
      md.appendMarkdown(`**Unix Timestamp** _(${isSeconds ? "seconds" : "milliseconds"})_\n\n${formatted}`);

      // Diagnostic: exactly 10 digits — ambiguous
      if (config.diagnostics.enabled && digits.length === 10) {
        const msDate = new Date(Number(digits));
        if (!isNaN(msDate.getTime())) {
          const msFormatted = formatDate(msDate, config.dateTime.unixFormat, "YYYY-MM-DD HH:mm:ss [UTC]");
          diagnostics.push(`\n\n---\n\n💡 If interpreted as **milliseconds**: ${msFormatted}`);
        }
      }
    } else {
      let variant: string;
      let fmtKey: string;
      let defaultFmt: string;
      switch (token.kind) {
        case "dateTimeFull": variant = "full ISO 8601"; fmtKey = config.dateTime.fullFormat; defaultFmt = "YYYY-MM-DD HH:mm:ss.SSS [UTC]"; break;
        case "dateTimeNoMillis": variant = "no milliseconds"; fmtKey = config.dateTime.noMillisFormat; defaultFmt = "YYYY-MM-DD HH:mm:ss [UTC]"; break;
        case "dateOnly": variant = "date only"; fmtKey = config.dateTime.dateOnlyFormat; defaultFmt = "MMMM D, YYYY"; break;
        default: return null;
      }
      const date = new Date(body);
      if (isNaN(date.getTime())) {
        md.appendMarkdown(`**DateTime** _(${variant})_\n\nInvalid date: \`${body}\``);
        return new vscode.Hover(md, token.range);
      }
      const formatted = formatDate(date, fmtKey, defaultFmt);
      md.appendMarkdown(`**DateTime** _(${variant})_\n\n${formatted}`);
    }

    for (const d of diagnostics) md.appendMarkdown(d);
    return new vscode.Hover(md, token.range);
  }

  private hoverTimeOnly(token: TokenInfo, config: RdnHoverConfig): vscode.Hover {
    const body = token.text.slice(1);
    const date = new Date(`1970-01-01T${body}Z`);
    const md = new vscode.MarkdownString();
    if (isNaN(date.getTime())) {
      md.appendMarkdown(`**TimeOnly**\n\nInvalid time: \`${body}\``);
    } else {
      const formatted = formatDate(date, config.timeOnly.format, "HH:mm:ss");
      md.appendMarkdown(`**TimeOnly**\n\n${formatted}`);
    }
    return new vscode.Hover(md, token.range);
  }

  private hoverDuration(token: TokenInfo): vscode.Hover {
    const body = token.text.slice(1);
    const expanded = expandDuration(body);
    const md = new vscode.MarkdownString();
    md.appendMarkdown(`**Duration**\n\n${expanded}`);
    return new vscode.Hover(md, token.range);
  }

  private hoverBigInt(token: TokenInfo, config: RdnHoverConfig): vscode.Hover {
    const digits = token.text.slice(0, -1); // strip trailing n
    const md = new vscode.MarkdownString();
    const grouped = groupDigits(digits);
    let content = `**BigInt**\n\n${grouped}`;

    if (config.bigint.showBitLength) {
      const abs = digits.startsWith("-") ? digits.slice(1) : digits;
      try {
        const val = BigInt(abs);
        const bits = val === 0n ? 1 : val.toString(2).length;
        content += ` _(${bits}-bit)_`;
      } catch { /* skip bit length on parse error */ }
    }

    // Diagnostic: fits in regular Number
    if (config.diagnostics.enabled) {
      const abs = digits.startsWith("-") ? digits.slice(1) : digits;
      try {
        const val = BigInt(abs);
        if (val <= 9007199254740991n) { // Number.MAX_SAFE_INTEGER
          content += `\n\n---\n\n💡 This value fits in a regular Number without precision loss`;
        }
      } catch { /* skip */ }
    }

    md.appendMarkdown(content);
    return new vscode.Hover(md, token.range);
  }

  private hoverBinaryBase64(token: TokenInfo, config: RdnHoverConfig): vscode.Hover {
    const inner = token.text.slice(2, -1); // strip b" and "
    const byteCount = Math.floor((inner.length * 3) / 4) - (inner.endsWith("==") ? 2 : inner.endsWith("=") ? 1 : 0);
    const md = new vscode.MarkdownString();

    // Check if binary data is an image
    const firstBytes = decodeBase64ToBytes(inner, 16);
    const imageInfo = detectImageFromBytes(firstBytes);

    if (imageInfo) {
      md.appendMarkdown(`**Base64 Binary** _(${imageInfo.label})_\n\n${formatByteSize(byteCount)}`);
      // Show inline image preview if not too large (< 250KB base64)
      if (inner.length <= 250_000) {
        md.supportHtml = true;
        md.appendMarkdown(`\n\n<img src="data:${imageInfo.mimeType};base64,${inner}" height="200" />`);
      }
    } else {
      let content = `**Base64 Binary**\n\n${formatByteSize(byteCount)}`;
      if (config.binary.showPreview) {
        const preview = base64ToAsciiPreview(inner);
        if (preview) content += `\n\nASCII preview: \`${preview}\``;
      }
      md.appendMarkdown(content);
    }

    return new vscode.Hover(md, token.range);
  }

  private hoverBinaryHex(token: TokenInfo, config: RdnHoverConfig): vscode.Hover {
    const inner = token.text.slice(2, -1); // strip x" and "
    const byteCount = Math.floor(inner.length / 2);
    const md = new vscode.MarkdownString();

    // Check if binary data is an image
    const firstBytes = decodeHexToBytes(inner, 16);
    const imageInfo = detectImageFromBytes(firstBytes);

    if (imageInfo) {
      md.appendMarkdown(`**Hex Binary** _(${imageInfo.label})_\n\n${formatByteSize(byteCount)}`);
      // Show inline image preview if not too large (< 500K hex chars ≈ 250KB)
      if (inner.length <= 500_000) {
        const b64Data = hexToBase64(inner);
        md.supportHtml = true;
        md.appendMarkdown(`\n\n<img src="data:${imageInfo.mimeType};base64,${b64Data}" height="200" />`);
      }
    } else {
      let content = `**Hex Binary**\n\n${formatByteSize(byteCount)}`;
      if (config.binary.showPreview) {
        const preview = hexToAsciiPreview(inner);
        if (preview) content += `\n\nASCII preview: \`${preview}\``;
      }
      // Diagnostic: odd hex digits
      if (config.diagnostics.enabled && inner.length % 2 !== 0) {
        content += `\n\n---\n\n⚠️ Odd number of hex digits (must be even)`;
      }
      md.appendMarkdown(content);
    }

    return new vscode.Hover(md, token.range);
  }

  private hoverRegExp(token: TokenInfo): vscode.Hover {
    // Extract flags from the end
    const lastSlash = token.text.lastIndexOf("/");
    const flags = token.text.slice(lastSlash + 1);
    const md = new vscode.MarkdownString();
    let content = `**RegExp**`;
    if (flags) content += `\n\nFlags: ${expandFlags(flags)}`;
    md.appendMarkdown(content);
    return new vscode.Hover(md, token.range);
  }

  private hoverSpecialNumber(token: TokenInfo): vscode.Hover {
    const md = new vscode.MarkdownString();
    switch (token.kind) {
      case "nan": md.appendMarkdown(`**NaN**\n\nIEEE 754 Not-a-Number — represents an undefined or unrepresentable numeric result`); break;
      case "infinity": md.appendMarkdown(`**Infinity**\n\nIEEE 754 positive infinity — greater than any finite number`); break;
      case "negInfinity": md.appendMarkdown(`**-Infinity**\n\nIEEE 754 negative infinity — less than any finite number`); break;
    }
    return new vscode.Hover(md, token.range);
  }

  private hoverExplicitCollection(token: TokenInfo, document: vscode.TextDocument): vscode.Hover {
    const md = new vscode.MarkdownString();
    const isMap = token.kind === "mapKeyword";
    const label = isMap ? "Map" : "Set";

    // Find the { after the keyword
    const offset = document.offsetAt(token.range.end);
    const count = countCollectionElements(document, offset);

    if (count !== null) {
      const unit = isMap ? (count === 1 ? "entry" : "entries") : (count === 1 ? "element" : "elements");
      md.appendMarkdown(`**${label}**\n\n${count} ${unit}`);
    } else {
      md.appendMarkdown(`**${label}**`);
    }

    return new vscode.Hover(md, token.range);
  }

  private hoverMapArrow(token: TokenInfo): vscode.Hover {
    const md = new vscode.MarkdownString();
    md.appendMarkdown(`**=>**\n\nMap entry separator — maps a key to its value`);
    return new vscode.Hover(md, token.range);
  }

  private hoverTuple(token: TokenInfo, document: vscode.TextDocument): vscode.Hover {
    const md = new vscode.MarkdownString();
    const offset = document.offsetAt(token.range.start);
    const count = countCollectionElements(document, offset);

    if (count !== null) {
      const unit = count === 1 ? "element" : "elements";
      md.appendMarkdown(`**Tuple**\n\n${count} ${unit}`);
    } else {
      md.appendMarkdown(`**Tuple**`);
    }

    return new vscode.Hover(md, token.range);
  }

  private hoverImplicitCollection(token: TokenInfo, document: vscode.TextDocument): vscode.Hover {
    const md = new vscode.MarkdownString();
    const isMap = token.kind === "implicitMap";
    const label = isMap ? "Map" : "Set";
    const offset = document.offsetAt(token.range.start);
    const count = countCollectionElements(document, offset);

    if (count !== null) {
      const unit = isMap ? (count === 1 ? "entry" : "entries") : (count === 1 ? "element" : "elements");
      md.appendMarkdown(`**${label}** _(implicit)_\n\n${count} ${unit}`);
    } else {
      md.appendMarkdown(`**${label}** _(implicit)_`);
    }

    return new vscode.Hover(md, token.range);
  }
}

// ─── Binary byte decoding helpers ────────────────────────────────────────────

const B64_LOOKUP = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

/** Decode up to `maxBytes` bytes from a base64 string. */
export function decodeBase64ToBytes(b64: string, maxBytes: number = Infinity): number[] {
  const bytes: number[] = [];
  const clean = b64.replace(/=/g, "");
  for (let i = 0; i < clean.length && bytes.length < maxBytes; i += 4) {
    const a = B64_LOOKUP.indexOf(clean[i]!);
    const b = i + 1 < clean.length ? B64_LOOKUP.indexOf(clean[i + 1]!) : 0;
    const c = i + 2 < clean.length ? B64_LOOKUP.indexOf(clean[i + 2]!) : 0;
    const d = i + 3 < clean.length ? B64_LOOKUP.indexOf(clean[i + 3]!) : 0;
    if (a === -1 || b === -1 || c === -1 || d === -1) break;
    bytes.push((a << 2) | (b >> 4));
    if (i + 2 < clean.length && bytes.length < maxBytes) bytes.push(((b & 15) << 4) | (c >> 2));
    if (i + 3 < clean.length && bytes.length < maxBytes) bytes.push(((c & 3) << 6) | d);
  }
  return bytes;
}

/** Decode up to `maxBytes` bytes from a hex string. */
export function decodeHexToBytes(hex: string, maxBytes: number = Infinity): number[] {
  const bytes: number[] = [];
  for (let i = 0; i + 1 < hex.length && bytes.length < maxBytes; i += 2) {
    const byte = parseInt(hex.slice(i, i + 2), 16);
    if (isNaN(byte)) break;
    bytes.push(byte);
  }
  return bytes;
}

/** Encode a byte array to base64 (pure implementation, no Buffer dependency). */
export function bytesToBase64(bytes: number[]): string {
  let result = "";
  for (let i = 0; i < bytes.length; i += 3) {
    const a = bytes[i]!;
    const b = i + 1 < bytes.length ? bytes[i + 1]! : 0;
    const c = i + 2 < bytes.length ? bytes[i + 2]! : 0;
    result += B64_LOOKUP[a >> 2]!;
    result += B64_LOOKUP[((a & 3) << 4) | (b >> 4)]!;
    result += i + 1 < bytes.length ? B64_LOOKUP[((b & 15) << 2) | (c >> 6)]! : "=";
    result += i + 2 < bytes.length ? B64_LOOKUP[c & 63]! : "=";
  }
  return result;
}

/** Convert a hex string to base64. */
function hexToBase64(hex: string): string {
  return bytesToBase64(decodeHexToBytes(hex));
}

// ─── ASCII preview helpers ───────────────────────────────────────────────────

function base64ToAsciiPreview(b64: string): string | null {
  try {
    const bytes = decodeBase64ToBytes(b64);
    return bytesToPreview(bytes);
  } catch {
    return null;
  }
}

function hexToAsciiPreview(hex: string): string | null {
  try {
    const bytes = decodeHexToBytes(hex);
    return bytesToPreview(bytes);
  } catch {
    return null;
  }
}

function bytesToPreview(bytes: number[]): string | null {
  if (bytes.length === 0) return null;
  const allPrintable = bytes.every((b) => b >= 0x20 && b <= 0x7e);
  if (!allPrintable) return null;
  const str = bytes.map((b) => String.fromCharCode(b)).join("");
  return str.length > 50 ? str.slice(0, 50) + "…" : str;
}

// ─── Image detection ─────────────────────────────────────────────────────────

interface ImageInfo {
  mimeType: string;
  label: string;
}

const IMAGE_SIGNATURES: { bytes: number[]; mimeType: string; label: string }[] = [
  { bytes: [0x89, 0x50, 0x4E, 0x47], mimeType: "image/png", label: "PNG" },
  { bytes: [0xFF, 0xD8, 0xFF], mimeType: "image/jpeg", label: "JPEG" },
  { bytes: [0x47, 0x49, 0x46, 0x38], mimeType: "image/gif", label: "GIF" },
  { bytes: [0x42, 0x4D], mimeType: "image/bmp", label: "BMP" },
  { bytes: [0x00, 0x00, 0x01, 0x00], mimeType: "image/x-icon", label: "ICO" },
];

/** Detect image format from the first bytes of binary data. Returns null if not a recognized image. */
export function detectImageFromBytes(bytes: number[]): ImageInfo | null {
  // WebP: RIFF....WEBP (bytes 0-3 and 8-11)
  if (bytes.length >= 12 && bytes[0] === 0x52 && bytes[1] === 0x49 && bytes[2] === 0x46 && bytes[3] === 0x46 && bytes[8] === 0x57 && bytes[9] === 0x45 && bytes[10] === 0x42 && bytes[11] === 0x50) {
    return { mimeType: "image/webp", label: "WebP" };
  }

  for (const sig of IMAGE_SIGNATURES) {
    if (bytes.length >= sig.bytes.length) {
      let match = true;
      for (let i = 0; i < sig.bytes.length; i++) {
        if (bytes[i] !== sig.bytes[i]) { match = false; break; }
      }
      if (match) return { mimeType: sig.mimeType, label: sig.label };
    }
  }

  return null;
}
