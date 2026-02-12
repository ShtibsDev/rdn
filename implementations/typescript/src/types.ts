/**
 * TimeOnly — represents a time-of-day value.
 * RDN syntax: @HH:MM:SS[.mmm]
 */
export interface RDNTimeOnly {
  readonly __type__: "TimeOnly";
  readonly hours: number;
  readonly minutes: number;
  readonly seconds: number;
  readonly milliseconds: number;
}

/**
 * Duration — represents an ISO 8601 duration.
 * RDN syntax: @P...
 */
export interface RDNDuration {
  readonly __type__: "Duration";
  readonly iso: string;
}

/**
 * All possible RDN value types.
 * Dates, BigInts, RegExps, Uint8Arrays, Maps, and Sets use their native JS types.
 * TimeOnly and Duration use tagged interfaces.
 */
export type RDNValue =
  | null
  | boolean
  | number
  | bigint
  | string
  | Date
  | RegExp
  | Uint8Array
  | RDNTimeOnly
  | RDNDuration
  | RDNValue[]
  | Map<RDNValue, RDNValue>
  | Set<RDNValue>
  | { [key: string]: RDNValue };

/** Reviver function for RDN.parse */
export type RDNReviver = (key: string | RDNValue, value: RDNValue) => RDNValue | undefined;

/** Replacer function for RDN.stringify */
export type RDNReplacer = (key: string | RDNValue, value: RDNValue) => RDNValue | undefined;

/** Helper to create a TimeOnly value */
export function timeOnly(hours: number, minutes: number, seconds: number, milliseconds: number = 0): RDNTimeOnly {
  return { __type__: "TimeOnly", hours, minutes, seconds, milliseconds };
}

/** Helper to create a Duration value */
export function duration(iso: string): RDNDuration {
  return { __type__: "Duration", iso };
}
