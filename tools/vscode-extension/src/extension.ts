import * as vscode from "vscode";
import { parse } from "@rdn/parser";
import { scanUnquotedKeys, scanBinaryErrors } from "./scanner";
import { format, formatSorted } from "./formatter";
import { RdnHoverProvider } from "./hover";
import { invalidateHoverConfig } from "./config";

const DIAGNOSTIC_SOURCE = "rdn";
const PARSE_ERROR_CODE = "parse-error";
const UNQUOTED_KEY_CODE = "unquoted-key";
const BINARY_CHAR_CODE = "invalid-binary-char";

let diagnosticCollection: vscode.DiagnosticCollection;
let debounceTimer: ReturnType<typeof setTimeout> | undefined;

/** Extract the position number from an RDN SyntaxError message like "... in RDN at position 42" */
function extractPosition(message: string): number | null {
  const match = message.match(/at position (\d+)$/);
  return match ? Number(match[1]) : null;
}

/** Get a meaningful range around an error position (highlight the problematic token or character) */
function errorRange(document: vscode.TextDocument, offset: number): vscode.Range {
  const pos = document.positionAt(offset);
  const lineText = document.lineAt(pos.line).text;
  const charAtOffset = lineText[pos.character];

  // Try to highlight the token at the error position
  if (charAtOffset && /\S/.test(charAtOffset)) {
    let end = pos.character + 1;
    while (end < lineText.length && /\S/.test(lineText[end]!) && !/[,}\]\)]/.test(lineText[end]!)) {
      end++;
    }
    return new vscode.Range(pos, new vscode.Position(pos.line, end));
  }

  // Fallback: highlight to end of line or one character
  const endChar = Math.min(pos.character + 1, lineText.length);
  return new vscode.Range(pos, new vscode.Position(pos.line, endChar));
}

export function activate(context: vscode.ExtensionContext): void {
  diagnosticCollection = vscode.languages.createDiagnosticCollection("rdn");
  context.subscriptions.push(diagnosticCollection);

  // --- Diagnostics: full parser validation + unquoted key detection ---

  function updateDiagnostics(document: vscode.TextDocument): void {
    if (document.languageId !== "rdn") return;

    const text = document.getText();
    const diagnostics: vscode.Diagnostic[] = [];

    // 1. Unquoted key detection first — these provide actionable quick fixes
    const keys = scanUnquotedKeys(text);
    const unquotedKeyLines = new Set<number>();
    for (const k of keys) {
      const startPos = document.positionAt(k.offset);
      const endPos = document.positionAt(k.offset + k.length);
      const range = new vscode.Range(startPos, endPos);
      unquotedKeyLines.add(range.start.line);

      const diag = new vscode.Diagnostic(range, `Unquoted key "${k.name}" — RDN requires all object keys to be quoted strings`, vscode.DiagnosticSeverity.Error);
      diag.source = DIAGNOSTIC_SOURCE;
      diag.code = UNQUOTED_KEY_CODE;
      diagnostics.push(diag);
    }

    // 2. Invalid binary character detection
    const binaryErrors = scanBinaryErrors(text);
    for (const err of binaryErrors) {
      const startPos = document.positionAt(err.offset);
      const endPos = document.positionAt(err.offset + err.length);
      const range = new vscode.Range(startPos, endPos);
      const diag = new vscode.Diagnostic(range, err.message, vscode.DiagnosticSeverity.Error);
      diag.source = DIAGNOSTIC_SOURCE;
      diag.code = BINARY_CHAR_CODE;
      diagnostics.push(diag);
    }

    // 3. Full parse validation — skip errors on lines already covered by unquoted key diagnostics
    try {
      parse(text);
    } catch (e) {
      if (e instanceof SyntaxError) {
        const offset = extractPosition(e.message);
        const cleanMessage = e.message.replace(/ in RDN at position \d+$/, "");
        const range = offset !== null ? errorRange(document, offset) : new vscode.Range(0, 0, 0, 0);
        if (!unquotedKeyLines.has(range.start.line)) {
          const diag = new vscode.Diagnostic(range, cleanMessage, vscode.DiagnosticSeverity.Error);
          diag.source = DIAGNOSTIC_SOURCE;
          diag.code = PARSE_ERROR_CODE;
          diagnostics.push(diag);
        }
      } else if (e instanceof RangeError) {
        const diag = new vscode.Diagnostic(new vscode.Range(0, 0, 0, 0), e.message, vscode.DiagnosticSeverity.Error);
        diag.source = DIAGNOSTIC_SOURCE;
        diag.code = PARSE_ERROR_CODE;
        diagnostics.push(diag);
      }
    }

    diagnosticCollection.set(document.uri, diagnostics);
  }

  function debouncedUpdate(document: vscode.TextDocument): void {
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => updateDiagnostics(document), 300);
  }

  // Run on open and change
  context.subscriptions.push(vscode.workspace.onDidOpenTextDocument(updateDiagnostics));
  context.subscriptions.push(vscode.workspace.onDidChangeTextDocument((e) => debouncedUpdate(e.document)));
  context.subscriptions.push(vscode.workspace.onDidCloseTextDocument((doc) => diagnosticCollection.delete(doc.uri)));

  // Run on already-open documents
  for (const doc of vscode.workspace.textDocuments) {
    updateDiagnostics(doc);
  }

  // --- Quick Fixes: wrap unquoted key in quotes ---

  const codeActionProvider = vscode.languages.registerCodeActionsProvider("rdn", {
    provideCodeActions(document, _range, context): vscode.CodeAction[] {
      const actions: vscode.CodeAction[] = [];
      const relevantDiags = context.diagnostics.filter((d) => d.source === DIAGNOSTIC_SOURCE && d.code === UNQUOTED_KEY_CODE);

      for (const diag of relevantDiags) {
        const keyText = document.getText(diag.range);
        const fix = new vscode.CodeAction(`Wrap "${keyText}" in quotes`, vscode.CodeActionKind.QuickFix);
        fix.edit = new vscode.WorkspaceEdit();
        fix.edit.replace(document.uri, diag.range, `"${keyText}"`);
        fix.diagnostics = [diag];
        fix.isPreferred = true;
        actions.push(fix);
      }

      // "Fix all" action when there are multiple unquoted keys
      const allDiags = diagnosticCollection.get(document.uri) || [];
      const allUnquoted = [...allDiags].filter((d) => d.code === UNQUOTED_KEY_CODE);
      if (allUnquoted.length > 1 && relevantDiags.length > 0) {
        const fixAll = new vscode.CodeAction("Wrap all unquoted keys in quotes", vscode.CodeActionKind.QuickFix);
        fixAll.edit = new vscode.WorkspaceEdit();
        const sorted = [...allUnquoted].sort((a, b) => b.range.start.compareTo(a.range.start));
        for (const diag of sorted) {
          const keyText = document.getText(diag.range);
          fixAll.edit.replace(document.uri, diag.range, `"${keyText}"`);
        }
        fixAll.diagnostics = relevantDiags;
        actions.push(fixAll);
      }

      return actions;
    },
  }, { providedCodeActionKinds: [vscode.CodeActionKind.QuickFix] });
  context.subscriptions.push(codeActionProvider);

  // --- Document Formatting ---

  const formattingProvider = vscode.languages.registerDocumentFormattingEditProvider("rdn", {
    provideDocumentFormattingEdits(document, options) {
      const text = document.getText();
      const config = vscode.workspace.getConfiguration("rdn");
      const rdnOpts = {
        useExplicitMapKeyword: config.get<boolean>("useExplicitMapKeyword", false),
        useExplicitSetKeyword: config.get<boolean>("useExplicitSetKeyword", false),
      };
      const formatted = format(text, options.tabSize, options.insertSpaces, rdnOpts);
      if (formatted === text) return [];
      const fullRange = new vscode.Range(document.positionAt(0), document.positionAt(text.length));
      return [vscode.TextEdit.replace(fullRange, formatted)];
    },
  });
  context.subscriptions.push(formattingProvider);

  // --- $schema completion ---

  const completionProvider = vscode.languages.registerCompletionItemProvider("rdn", {
    provideCompletionItems(document, position): vscode.CompletionItem[] | undefined {
      const textBefore = document.getText(new vscode.Range(new vscode.Position(0, 0), position));

      // Calculate brace depth (outside strings)
      let depth = 0;
      let inString = false;
      for (let j = 0; j < textBefore.length; j++) {
        const c = textBefore[j]!;
        if (c === "\\" && inString) { j++; continue; }
        if (c === '"') { inString = !inString; continue; }
        if (inString) continue;
        if (c === "{") depth++;
        if (c === "}") depth--;
      }

      // Only at top-level object (depth 1)
      if (depth !== 1) return undefined;

      // Check if $schema already exists in the document
      const fullText = document.getText();
      if (/"\$schema"\s*:/.test(fullText)) return undefined;

      // Find the range of the key being typed: scan backwards from cursor to find opening "
      const line = document.lineAt(position.line).text;
      let quoteStart = position.character - 1;
      while (quoteStart >= 0 && line[quoteStart] !== '"') {
        quoteStart--;
      }

      if (quoteStart < 0) return undefined;

      // Check that before the opening " we have a key-position context (after { or ,)
      const beforeQuote = line.substring(0, quoteStart).trimEnd();
      const lastChar = beforeQuote[beforeQuote.length - 1];
      if (beforeQuote.length > 0 && lastChar !== "{" && lastChar !== ",") return undefined;

      // Find the closing " after cursor (auto-close may have added it)
      let quoteEnd = position.character;
      while (quoteEnd < line.length && line[quoteEnd] !== '"') {
        quoteEnd++;
      }
      if (quoteEnd < line.length) quoteEnd++; // include the closing "

      const replaceRange = new vscode.Range(position.line, quoteStart, position.line, quoteEnd);

      const item = new vscode.CompletionItem("$schema", vscode.CompletionItemKind.Property);
      item.insertText = new vscode.SnippetString('"\\$schema": "$1"');
      item.filterText = line.substring(quoteStart, position.character);
      item.range = replaceRange;
      item.detail = "JSON Schema reference";
      item.documentation = new vscode.MarkdownString("Add a `$schema` property pointing to a JSON Schema URL for validation support.");
      item.sortText = "!0";
      return [item];
    },
  }, '"');
  context.subscriptions.push(completionProvider);

  // --- RDN keywords ---

  const RDN_KEYWORDS: { label: string; insert: string; detail: string; doc: string; sort: string }[] = [
    { label: "true", insert: "true", detail: "Boolean", doc: "Boolean true value", sort: "~k0" },
    { label: "false", insert: "false", detail: "Boolean", doc: "Boolean false value", sort: "~k1" },
    { label: "null", insert: "null", detail: "Null", doc: "Null value — represents absence of a value", sort: "~k2" },
    { label: "NaN", insert: "NaN", detail: "Not-a-Number", doc: "IEEE 754 Not-a-Number value", sort: "~k3" },
    { label: "Infinity", insert: "Infinity", detail: "Positive Infinity", doc: "IEEE 754 positive infinity", sort: "~k4" },
    { label: "-Infinity", insert: "-Infinity", detail: "Negative Infinity", doc: "IEEE 754 negative infinity", sort: "~k5" },
    { label: "Map", insert: "Map", detail: "Map keyword", doc: "Map container — use `Map{key => value}` for an explicit typed map", sort: "~k6" },
    { label: "Set", insert: "Set", detail: "Set keyword", doc: "Set container — use `Set{value, ...}` for an explicit typed set", sort: "~k7" },
    { label: "@", insert: "@", detail: "Date/Time prefix", doc: "Prefix for date, time, duration, and unix timestamp literals (`@2024-01-15`, `@14:30:00`, `@P1Y2M`, `@1704067200`)", sort: "~k8" },
    { label: "b", insert: "b", detail: "Base64 prefix", doc: "Prefix for base64-encoded binary data (`b\"SGVsbG8=\"`)", sort: "~k9" },
    { label: "x", insert: "x", detail: "Hex prefix", doc: "Prefix for hex-encoded binary data (`x\"48656C6C6F\"`)", sort: "~ka" },
  ];

  // --- RDN snippets ---

  const RDN_SNIPPETS: { label: string; filter: string; insert: string; detail: string; doc: string; sort: string }[] = [
    { label: "@date", filter: "@date", insert: "@${1:2024-01-15}", detail: "Date", doc: "Date literal — ISO 8601 date (`@2024-01-15`)", sort: "~s0" },
    { label: "@datetime", filter: "@datetime", insert: "@${1:2024-01-15T10:30:00.000Z}", detail: "DateTime", doc: "DateTime literal — ISO 8601 date with time and UTC timezone (`@2024-01-15T10:30:00.000Z`)", sort: "~s1" },
    { label: "@time", filter: "@time", insert: "@${1:14:30:00}", detail: "TimeOnly", doc: "Time-of-day literal — hours, minutes, seconds (`@14:30:00`)", sort: "~s2" },
    { label: "@duration", filter: "@duration", insert: "@P${1:1Y2M3DT4H5M6S}", detail: "Duration", doc: "ISO 8601 duration (`@P1Y2M3DT4H5M6S` = 1 year, 2 months, 3 days, 4h 5m 6s)", sort: "~s3" },
    { label: "@unix", filter: "@unix", insert: "@${1:1704067200}", detail: "Unix Timestamp", doc: "Unix epoch timestamp in seconds (`@1704067200`)", sort: "~s4" },
    { label: "Map{}", filter: "Map", insert: "Map{${1:key} => ${2:value}}", detail: "Map expression", doc: "Explicit Map container with entries (`Map{\"a\" => 1, \"b\" => 2}`)", sort: "~s5" },
    { label: "Set{}", filter: "Set", insert: "Set{${1:value}}", detail: "Set expression", doc: "Explicit Set container with values (`Set{1, 2, 3}`)", sort: "~s6" },
    { label: "tuple()", filter: "tuple", insert: "(${1:value})", detail: "Tuple expression", doc: "Tuple — fixed-length ordered collection (`(1, \"hello\", true)`)", sort: "~s7" },
    { label: "base64 b\"\"", filter: "base64", insert: "b\"${1:SGVsbG8=}\"", detail: "Base64 Binary", doc: "Binary data encoded as base64 (`b\"SGVsbG8=\"`)", sort: "~s8" },
    { label: "hex x\"\"", filter: "hex", insert: "x\"${1:48656C6C6F}\"", detail: "Hex Binary", doc: "Binary data encoded as hexadecimal (`x\"48656C6C6F\"`)", sort: "~s9" },
    { label: "regex //", filter: "regex", insert: "/${1:pattern}/${2:flags}", detail: "RegExp", doc: "Regular expression literal with optional flags (`/^test$/gi`)", sort: "~sa" },
    { label: "bigint 0n", filter: "bigint", insert: "${1:0}n", detail: "BigInt", doc: "Arbitrary-precision integer (`42n`, `-999n`)", sort: "~sb" },
  ];

  const symbolCompletionProvider = vscode.languages.registerCompletionItemProvider("rdn", {
    provideCompletionItems(document, position): vscode.CompletionItem[] | undefined {
      // Don't suggest inside strings — walk from line start and track quote parity
      const lineText = document.lineAt(position.line).text;
      let inString = false;
      for (let i = 0; i < position.character; i++) {
        const c = lineText[i]!;
        if (c === "\\" && inString) { i++; continue; }
        if (c === '"') inString = !inString;
      }
      if (inString) return undefined;

      const items: vscode.CompletionItem[] = [];

      for (const k of RDN_KEYWORDS) {
        const item = new vscode.CompletionItem(k.label, vscode.CompletionItemKind.Keyword);
        item.insertText = k.insert;
        item.detail = k.detail;
        item.documentation = new vscode.MarkdownString(k.doc);
        item.sortText = k.sort;
        items.push(item);
      }

      for (const s of RDN_SNIPPETS) {
        const item = new vscode.CompletionItem(s.label, vscode.CompletionItemKind.Snippet);
        item.insertText = new vscode.SnippetString(s.insert);
        item.filterText = s.filter;
        item.detail = s.detail;
        item.documentation = new vscode.MarkdownString(s.doc);
        item.sortText = s.sort;
        items.push(item);
      }

      return items;
    },
  }, "@", "M", "S", "b", "x", "(", "/", "N", "I", "-", "t", "f", "n", "h", "r");
  context.subscriptions.push(symbolCompletionProvider);

  // --- Hover Provider ---

  context.subscriptions.push(vscode.languages.registerHoverProvider("rdn", new RdnHoverProvider()));
  context.subscriptions.push(vscode.workspace.onDidChangeConfiguration((e) => {
    if (e.affectsConfiguration("rdn.hover")) invalidateHoverConfig();
  }));

  // --- Sort Document command ---

  context.subscriptions.push(vscode.commands.registerCommand("rdn.sortDocument", () => {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== "rdn") return;
    const text = editor.document.getText();
    const config = vscode.workspace.getConfiguration("rdn");
    const rdnOpts = {
      useExplicitMapKeyword: config.get<boolean>("useExplicitMapKeyword", false),
      useExplicitSetKeyword: config.get<boolean>("useExplicitSetKeyword", false),
    };
    const sorted = formatSorted(text, editor.options.tabSize as number ?? 2, editor.options.insertSpaces as boolean ?? true, rdnOpts);
    if (sorted === null || sorted === text) return;
    const fullRange = new vscode.Range(editor.document.positionAt(0), editor.document.positionAt(text.length));
    editor.edit((edit) => edit.replace(fullRange, sorted));
  }));

}

export function deactivate(): void {
  if (debounceTimer) clearTimeout(debounceTimer);
}
