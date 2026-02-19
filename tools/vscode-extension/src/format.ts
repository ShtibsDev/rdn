/**
 * Lightweight date/time/number formatting utilities for hover tooltips.
 * Zero external dependencies — uses only built-in Date methods (UTC).
 */

const MONTH_NAMES_FULL = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
const MONTH_NAMES_SHORT = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

const warnedFormats = new Set<string>();

function pad(n: number, width: number): string {
  return String(n).padStart(width, "0");
}

/** Token-based date formatter. All date methods use UTC. Supports [literal] escapes. */
function applyFormat(date: Date, fmt: string): string {
  let result = "";
  let i = 0;
  while (i < fmt.length) {
    // Escaped literal
    if (fmt[i] === "[") {
      const close = fmt.indexOf("]", i + 1);
      if (close === -1) { result += fmt.slice(i); break; }
      result += fmt.slice(i + 1, close);
      i = close + 1;
      continue;
    }
    // Greedy token match (longest first)
    let matched = false;
    for (const [token, fn] of TOKENS) {
      if (fmt.startsWith(token, i)) {
        result += fn(date);
        i += token.length;
        matched = true;
        break;
      }
    }
    if (!matched) {
      result += fmt[i];
      i++;
    }
  }
  return result;
}

type TokenFn = (d: Date) => string;
const TOKENS: [string, TokenFn][] = [
  ["YYYY", (d) => String(d.getUTCFullYear())],
  ["YY", (d) => String(d.getUTCFullYear()).slice(-2)],
  ["MMMM", (d) => MONTH_NAMES_FULL[d.getUTCMonth()]!],
  ["MMM", (d) => MONTH_NAMES_SHORT[d.getUTCMonth()]!],
  ["MM", (d) => pad(d.getUTCMonth() + 1, 2)],
  ["M", (d) => String(d.getUTCMonth() + 1)],
  ["DD", (d) => pad(d.getUTCDate(), 2)],
  ["D", (d) => String(d.getUTCDate())],
  ["HH", (d) => pad(d.getUTCHours(), 2)],
  ["H", (d) => String(d.getUTCHours())],
  ["hh", (d) => pad(d.getUTCHours() % 12 || 12, 2)],
  ["h", (d) => String(d.getUTCHours() % 12 || 12)],
  ["mm", (d) => pad(d.getUTCMinutes(), 2)],
  ["m", (d) => String(d.getUTCMinutes())],
  ["ss", (d) => pad(d.getUTCSeconds(), 2)],
  ["s", (d) => String(d.getUTCSeconds())],
  ["SSS", (d) => pad(d.getUTCMilliseconds(), 3)],
  ["SS", (d) => pad(d.getUTCMilliseconds(), 3).slice(0, 2)],
  ["S", (d) => pad(d.getUTCMilliseconds(), 3).slice(0, 1)],
  ["A", (d) => d.getUTCHours() < 12 ? "AM" : "PM"],
  ["a", (d) => d.getUTCHours() < 12 ? "am" : "pm"],
];

// Letters that should NOT appear unescaped in a format string outside tokens
const UNRECOGNIZED_LETTER_RE = /[a-zA-Z]+/g;
const TOKEN_LETTERS = new Set(["YYYY", "YY", "MMMM", "MMM", "MM", "M", "DD", "D", "HH", "H", "hh", "h", "mm", "m", "ss", "s", "SSS", "SS", "S", "A", "a"]);

/** Validate a format string by formatting a reference date and checking for stray letter sequences. */
function isValidFormat(fmt: string): boolean {
  try {
    const ref = new Date("2000-06-15T13:45:30.123Z");
    const result = applyFormat(ref, fmt);
    if (!result) return false;
    // Strip bracketed literals and known tokens, then check for leftover letter runs
    let stripped = fmt.replace(/\[[^\]]*\]/g, "");
    for (const [token] of TOKENS) {
      while (stripped.includes(token)) stripped = stripped.replace(token, "");
    }
    const leftover = stripped.match(UNRECOGNIZED_LETTER_RE);
    return !leftover || leftover.length === 0;
  } catch {
    return false;
  }
}

/**
 * Format a Date using the given format string. If the format is invalid,
 * falls back to defaultFormatStr. Logs a warning once per invalid format.
 */
export function formatDate(date: Date, formatStr: string, defaultFormatStr: string): string {
  if (formatStr && isValidFormat(formatStr)) {
    return applyFormat(date, formatStr);
  }
  if (formatStr && !warnedFormats.has(formatStr)) {
    warnedFormats.add(formatStr);
    console.warn(`[RDN Hover] Invalid date format "${formatStr}", falling back to default.`);
  }
  return applyFormat(date, defaultFormatStr);
}

/**
 * Expand an ISO 8601 duration string into plain English.
 * e.g. "P1Y2M3DT4H5M6S" → "1 year, 2 months, 3 days, 4 hours, 5 minutes, 6 seconds"
 */
export function expandDuration(iso: string): string {
  const parts: string[] = [];
  // Remove leading P
  let s = iso.startsWith("P") ? iso.slice(1) : iso;
  let pastT = false;

  const re = /(\d+(?:\.\d+)?)([YMDHMS])/g;
  let match: RegExpExecArray | null;
  while ((match = re.exec(s)) !== null) {
    const value = match[1]!;
    const unit = match[2]!;
    const num = parseFloat(value);

    // Check if we've passed the T separator
    const tIndex = s.indexOf("T");
    if (tIndex !== -1 && match.index > tIndex) pastT = true;
    // Also handle inline T detection
    if (!pastT) {
      const preceding = s.slice(0, match.index);
      if (preceding.includes("T")) pastT = true;
    }

    let label: string;
    switch (unit) {
      case "Y": label = num === 1 ? "year" : "years"; break;
      case "M": label = pastT ? (num === 1 ? "minute" : "minutes") : (num === 1 ? "month" : "months"); break;
      case "D": label = num === 1 ? "day" : "days"; break;
      case "H": label = num === 1 ? "hour" : "hours"; break;
      case "S": label = num === 1 ? "second" : "seconds"; break;
      default: label = unit; break;
    }
    parts.push(`${value} ${label}`);
  }

  return parts.length > 0 ? parts.join(", ") : iso;
}

/**
 * Insert commas every 3 digits from the right for BigInt display.
 * e.g. "1234567890" → "1,234,567,890"
 */
export function groupDigits(digits: string): string {
  // Handle negative sign
  const negative = digits.startsWith("-");
  const abs = negative ? digits.slice(1) : digits;
  const grouped = abs.replace(/\B(?=(\d{3})+(?!\d))/g, ",");
  return negative ? "-" + grouped : grouped;
}

/**
 * Format a byte count into a human-readable string.
 * e.g. 1024 → "1.0 KB", 5 → "5 bytes"
 */
export function formatByteSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} byte${bytes !== 1 ? "s" : ""}`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// For testing: reset the warned formats set
export function _resetWarnedFormats(): void {
  warnedFormats.clear();
}
