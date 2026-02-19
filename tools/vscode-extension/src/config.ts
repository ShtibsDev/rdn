import * as vscode from "vscode";

export interface RdnHoverConfig {
  enabled: boolean;
  dateTime: { enabled: boolean; fullFormat: string; dateOnlyFormat: string; noMillisFormat: string; unixFormat: string; };
  timeOnly: { enabled: boolean; format: string; };
  duration: { enabled: boolean; };
  bigint: { enabled: boolean; showBitLength: boolean; };
  binary: { enabled: boolean; showPreview: boolean; };
  regexp: { enabled: boolean; };
  specialNumbers: { enabled: boolean; };
  collections: { enabled: boolean; };
  diagnostics: { enabled: boolean; };
}

const DEFAULTS: RdnHoverConfig = {
  enabled: true,
  dateTime: { enabled: true, fullFormat: "YYYY-MM-DD HH:mm:ss.SSS [UTC]", dateOnlyFormat: "MMMM D, YYYY", noMillisFormat: "YYYY-MM-DD HH:mm:ss [UTC]", unixFormat: "YYYY-MM-DD HH:mm:ss [UTC]" },
  timeOnly: { enabled: true, format: "HH:mm:ss" },
  duration: { enabled: true },
  bigint: { enabled: true, showBitLength: true },
  binary: { enabled: true, showPreview: true },
  regexp: { enabled: true },
  specialNumbers: { enabled: true },
  collections: { enabled: true },
  diagnostics: { enabled: true },
};

let cached: RdnHoverConfig | null = null;

export function getHoverConfig(): RdnHoverConfig {
  if (cached) return cached;
  const cfg = vscode.workspace.getConfiguration("rdn.hover");
  cached = {
    enabled: cfg.get<boolean>("enabled", DEFAULTS.enabled),
    dateTime: {
      enabled: cfg.get<boolean>("dateTime.enabled", DEFAULTS.dateTime.enabled),
      fullFormat: cfg.get<string>("dateTime.fullFormat", DEFAULTS.dateTime.fullFormat),
      dateOnlyFormat: cfg.get<string>("dateTime.dateOnlyFormat", DEFAULTS.dateTime.dateOnlyFormat),
      noMillisFormat: cfg.get<string>("dateTime.noMillisFormat", DEFAULTS.dateTime.noMillisFormat),
      unixFormat: cfg.get<string>("dateTime.unixFormat", DEFAULTS.dateTime.unixFormat),
    },
    timeOnly: {
      enabled: cfg.get<boolean>("timeOnly.enabled", DEFAULTS.timeOnly.enabled),
      format: cfg.get<string>("timeOnly.format", DEFAULTS.timeOnly.format),
    },
    duration: { enabled: cfg.get<boolean>("duration.enabled", DEFAULTS.duration.enabled) },
    bigint: { enabled: cfg.get<boolean>("bigint.enabled", DEFAULTS.bigint.enabled), showBitLength: cfg.get<boolean>("bigint.showBitLength", DEFAULTS.bigint.showBitLength) },
    binary: { enabled: cfg.get<boolean>("binary.enabled", DEFAULTS.binary.enabled), showPreview: cfg.get<boolean>("binary.showPreview", DEFAULTS.binary.showPreview) },
    regexp: { enabled: cfg.get<boolean>("regexp.enabled", DEFAULTS.regexp.enabled) },
    specialNumbers: { enabled: cfg.get<boolean>("specialNumbers.enabled", DEFAULTS.specialNumbers.enabled) },
    collections: { enabled: cfg.get<boolean>("collections.enabled", DEFAULTS.collections.enabled) },
    diagnostics: { enabled: cfg.get<boolean>("diagnostics.enabled", DEFAULTS.diagnostics.enabled) },
  };
  return cached;
}

export function invalidateHoverConfig(): void {
  cached = null;
}

// For testing: override the cached config
export function _setHoverConfig(config: RdnHoverConfig): void {
  cached = config;
}
