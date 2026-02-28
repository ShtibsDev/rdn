#!/usr/bin/env bun

/**
 * Deterministic benchmark fixture generator for RDN.
 *
 * Generates medium (~1 MB) and large (~100 MB) fixture files in two variants:
 *   - typical:   Product-catalog API payload (~80 % JSON-native, ~20 % RDN)
 *   - rdn-heavy: IoT monitoring dashboard     (~30 % JSON-native, ~70 % RDN)
 *
 * Each variant is emitted as both .rdn and .json for apples-to-apples comparison.
 *
 * Usage:
 *   bun benchmarks/generate-fixtures.ts [--size medium|large|all] [--type typical|rdn-heavy|all] [--dry-run]
 */

import { join } from "node:path";
import { parseArgs } from "node:util";

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

const { values: flags } = parseArgs({
  args: Bun.argv.slice(2),
  options: {
    size: { type: "string", default: "all" },
    type: { type: "string", default: "all" },
    "dry-run": { type: "boolean", default: false },
  },
  strict: true,
});

const sizes =
  flags.size === "all"
    ? (["medium", "large"] as const)
    : [flags.size as "medium" | "large"];
const types =
  flags.type === "all"
    ? (["typical", "rdn-heavy"] as const)
    : [flags.type as "typical" | "rdn-heavy"];
const dryRun = flags["dry-run"] ?? false;

const FIXTURES_DIR = join(import.meta.dir, "fixtures");

// ---------------------------------------------------------------------------
// Deterministic helpers
// ---------------------------------------------------------------------------

/** Simple seeded PRNG (mulberry32). */
function createRng(seed: number) {
  let s = seed | 0;
  return () => {
    s = (s + 0x6d2b79f5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

let rng = createRng(42);

function pick<T>(arr: readonly T[]): T {
  return arr[Math.floor(rng() * arr.length)];
}

function pickN<T>(arr: readonly T[], n: number): T[] {
  const shuffled = [...arr];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled.slice(0, n);
}

function deterministicFloat(min: number, max: number, decimals = 2): number {
  return Number((min + rng() * (max - min)).toFixed(decimals));
}

function deterministicInt(min: number, max: number): number {
  return Math.floor(min + rng() * (max - min + 1));
}

function deterministicDate(yearMin: number, yearMax: number): Date {
  const year = deterministicInt(yearMin, yearMax);
  const month = deterministicInt(0, 11);
  const day = deterministicInt(1, 28);
  const hour = deterministicInt(0, 23);
  const minute = deterministicInt(0, 59);
  const second = deterministicInt(0, 59);
  return new Date(Date.UTC(year, month, day, hour, minute, second));
}

function isoZ(d: Date): string {
  return d.toISOString();
}

function dateOnly(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function toBase64(str: string): string {
  return Buffer.from(str).toString("base64");
}

function toHex(str: string): string {
  return Buffer.from(str).toString("hex").toUpperCase();
}

function padTime(n: number): string {
  return String(n).padStart(2, "0");
}

// ---------------------------------------------------------------------------
// Data pools
// ---------------------------------------------------------------------------

const PRODUCT_ADJECTIVES = [
  "Premium",
  "Ultra",
  "Pro",
  "Essential",
  "Classic",
  "Advanced",
  "Elite",
  "Compact",
  "Deluxe",
  "Smart",
] as const;
const PRODUCT_NOUNS = [
  "Wireless Headphones",
  "Mechanical Keyboard",
  "Gaming Mouse",
  "USB-C Hub",
  "Portable Speaker",
  "Webcam",
  "Monitor Stand",
  "Desk Lamp",
  "Power Bank",
  "Charging Pad",
  "Laptop Stand",
  "Microphone",
  "Drawing Tablet",
  "Ergonomic Chair",
  "Standing Desk",
  "Cable Organizer",
  "Screen Protector",
  "Phone Mount",
  "External SSD",
  "Docking Station",
] as const;
const CATEGORIES = [
  "Electronics/Audio",
  "Electronics/Peripherals",
  "Electronics/Accessories",
  "Office/Furniture",
  "Office/Lighting",
  "Computing/Storage",
  "Computing/Displays",
  "Mobile/Accessories",
] as const;
const CURRENCIES = ["USD", "EUR", "GBP", "JPY", "CAD"] as const;
const TAGS_POOL = [
  "wireless",
  "bluetooth",
  "usb-c",
  "ergonomic",
  "gaming",
  "portable",
  "rechargeable",
  "premium",
  "eco-friendly",
  "compact",
  "rgb",
  "noise-cancelling",
  "waterproof",
  "foldable",
  "lightweight",
] as const;
const COUNTRIES = [
  "US",
  "JP",
  "TW",
  "DE",
  "KR",
  "CN",
  "SE",
  "UK",
  "CA",
  "AU",
] as const;
const MANUFACTURER_NAMES = [
  "SoundTech Corp",
  "KeyCraft Industries",
  "PixelForge Inc",
  "NovaTech Solutions",
  "HyperGear Labs",
  "VoltEdge Systems",
  "ArcLight Devices",
  "ZenithWare",
  "PulsePoint Tech",
  "CoreStream Electronics",
] as const;
const REVIEWER_NAMES = [
  "AudioPhile42",
  "TypeMaster",
  "TechNerd99",
  "GadgetGuru",
  "ProReviewer",
  "DigitalNomad",
  "SmartBuyer",
  "QualityFirst",
  "ValueHunter",
  "EarlyAdopter",
  "PowerUser",
  "CasualTech",
  "MinimalistDev",
  "SetupKing",
  "DeskVibes",
] as const;
const REVIEW_TITLES = [
  "Excellent quality",
  "Great value for money",
  "Exceeded expectations",
  "Solid build",
  "Good but not perfect",
  "Highly recommended",
  "Best in class",
  "Decent for the price",
  "Amazing features",
  "Worth every penny",
  "Could be better",
  "Surprisingly good",
  "Professional grade",
  "Daily driver material",
  "Game changer",
] as const;
const REVIEW_BODIES = [
  "This product has been a great addition to my setup. Build quality is excellent and it works exactly as advertised.",
  "I've been using this for several months now and it continues to impress. The attention to detail is remarkable.",
  "After comparing multiple options, I settled on this one and couldn't be happier with my choice.",
  "The performance is outstanding. I use it daily for both work and personal projects without any issues.",
  "Setup was straightforward and the product started working right out of the box. Very pleased with the purchase.",
  "While there are a few minor quirks, the overall experience has been very positive. Would recommend.",
  "This is my second purchase from this brand and they continue to deliver high-quality products.",
  "The design is sleek and functional. It fits perfectly into my workspace without taking up too much room.",
  "I was skeptical at first but this product genuinely delivers on its promises. Excellent value.",
  "Customer support was helpful when I had questions. The product itself is well-made and reliable.",
] as const;

const SENSOR_LOCATIONS = [
  "Building 7 - North Wing",
  "Rooftop Platform 3",
  "Server Room Alpha",
  "Cold Storage Unit 2",
  "Main Lobby",
  "Parking Garage B1",
  "Loading Dock 4",
  "HVAC Control Room",
  "Generator Building",
  "Water Treatment Plant",
  "Perimeter Fence East",
  "Warehouse Floor 2",
  "Lab 301-A",
  "Clean Room 5",
  "Data Center Pod 12",
  "Solar Array Field",
  "Pump Station 8",
  "Substation Delta",
  "Fire Control Panel",
  "Security Booth 1",
] as const;
const SENSOR_NAMES_PREFIX = [
  "Warehouse Sensor",
  "Rooftop Weather Station",
  "Server Monitor",
  "Cold Chain Tracker",
  "Ambient Monitor",
  "Air Quality Probe",
  "Vibration Sensor",
  "Flow Meter",
  "Pressure Gauge",
  "Power Meter",
] as const;
const SENSOR_CAPABILITIES_POOL = [
  "read",
  "write",
  "calibrate",
  "stream",
  "alert",
  "log",
  "configure",
  "reset",
] as const;
const SENSOR_TAG_POOL = [
  "temperature",
  "humidity",
  "pressure",
  "wind",
  "solar",
  "rainfall",
  "vibration",
  "flow",
  "power",
  "gas",
  "smoke",
  "motion",
  "light",
  "sound",
  "radiation",
] as const;
const TECHNICIAN_NAMES = [
  "Maria Santos",
  "James Chen",
  "Aisha Patel",
  "Erik Johansson",
  "Yuki Tanaka",
  "Carlos Rivera",
  "Fatima Al-Hassan",
  "David Kim",
  "Olga Petrova",
  "Thomas Mueller",
] as const;
const MAINT_PARTS = [
  "humidity-probe",
  "filter-cap",
  "anemometer",
  "pressure-valve",
  "circuit-board",
  "antenna",
  "battery-pack",
  "thermal-paste",
  "fan-assembly",
  "gasket-seal",
  "power-supply",
  "display-module",
  "relay-switch",
  "fuse-box",
  "coolant-line",
] as const;
const MAINT_NOTES = [
  "Routine quarterly maintenance. All sensors within specification.",
  "Emergency repair after power surge. Replaced damaged components.",
  "Annual calibration and component replacement as scheduled.",
  "Firmware update applied successfully. Performance improved by 12%.",
  "Preventive maintenance. Minor wear detected on moving parts.",
  "Post-storm inspection. Cleaned debris and verified alignment.",
  "Replaced end-of-life components. Extended warranty registered.",
  "Investigated intermittent readings. Root cause identified and fixed.",
] as const;
const ERROR_PATTERNS_POOL = [
  "/timeout_\\d+/i",
  "/conn_refused_\\d{1,5}/",
  "/^SENSOR_ERR_[A-Z]+$/",
  "/data_corrupt_0x[0-9a-f]+/",
  "/^ALERT_LEVEL_[1-5]$/",
  "/calibration_drift_\\d+\\.\\d+/",
] as const;

// ---------------------------------------------------------------------------
// Dual-output builders (return { rdn, json } string fragments)
// ---------------------------------------------------------------------------

interface Dual {
  rdn: string;
  json: string;
}

function dualBigInt(n: bigint): Dual {
  return { rdn: `${n}n`, json: `"${n}"` };
}

function dualDate(d: Date): Dual {
  const iso = isoZ(d);
  return { rdn: `@${iso}`, json: `"${iso}"` };
}

function dualDateOnly(d: Date): Dual {
  const ds = dateOnly(d);
  return { rdn: `@${ds}`, json: `"${ds}"` };
}

function dualTimeOnly(h: number, m: number, s: number, ms?: number): Dual {
  const base = `${padTime(h)}:${padTime(m)}:${padTime(s)}`;
  const full =
    ms != null && ms > 0 ? `${base}.${String(ms).padStart(3, "0")}` : base;
  return { rdn: `@${full}`, json: `"${full}"` };
}

function dualDuration(iso: string): Dual {
  return { rdn: `@${iso}`, json: `"${iso}"` };
}

function dualRegExp(pattern: string, flags: string = ""): Dual {
  const flagStr = flags ? `/${flags}` : "/";
  return {
    rdn: `/${pattern}${flagStr}`,
    json: `"${pattern.replace(/\\/g, "\\\\")}"`,
  };
}

function dualBinaryB64(data: string): Dual {
  const b64 = toBase64(data);
  return { rdn: `b"${b64}"`, json: `"${b64}"` };
}

function dualBinaryHex(data: string): Dual {
  const hex = toHex(data);
  const b64 = toBase64(data);
  return { rdn: `x"${hex}"`, json: `"${b64}"` };
}

function dualNaN(): Dual {
  return { rdn: "NaN", json: "null" };
}

function dualInfinity(): Dual {
  return { rdn: "Infinity", json: "null" };
}

function dualNegInfinity(): Dual {
  return { rdn: "-Infinity", json: "null" };
}

function dualSet(items: Dual[]): Dual {
  return {
    rdn: `Set{${items.map((i) => i.rdn).join(", ")}}`,
    json: `[${items.map((i) => i.json).join(", ")}]`,
  };
}

function dualStringSet(items: string[]): Dual {
  const quoted = items.map((s) => `"${s}"`);
  return {
    rdn: `Set{${quoted.join(", ")}}`,
    json: `[${quoted.join(", ")}]`,
  };
}

function dualImplicitSet(items: string[]): Dual {
  const quoted = items.map((s) => `"${s}"`);
  return {
    rdn: `{${quoted.join(", ")}}`,
    json: `[${quoted.join(", ")}]`,
  };
}

function dualTuple(items: Dual[]): Dual {
  return {
    rdn: `(${items.map((i) => i.rdn).join(", ")})`,
    json: `[${items.map((i) => i.json).join(", ")}]`,
  };
}

function dualLiteral(rdnVal: string, jsonVal: string): Dual {
  return { rdn: rdnVal, json: jsonVal };
}

function dualNumber(n: number): Dual {
  const s = JSON.stringify(n);
  return { rdn: s, json: s };
}

function dualString(s: string): Dual {
  const q = JSON.stringify(s);
  return { rdn: q, json: q };
}

// ---------------------------------------------------------------------------
// Typical payload builder — Product Catalog
// ---------------------------------------------------------------------------

function buildTypicalProduct(index: number): Dual {
  const id = dualBigInt(BigInt(1000000000000000 + index + 1));
  const sku = `${pick(["WH", "KB", "MS", "HB", "SP", "WC", "MN", "DL", "PB", "CP"])}-${String(1000 + index).slice(1)}-${pick(["BLK", "WHT", "SLV", "BLU", "RED"])}`;
  const adjective = pick(PRODUCT_ADJECTIVES);
  const noun = pick(PRODUCT_NOUNS);
  const name = `${adjective} ${noun}`;
  const category = pick(CATEGORIES);
  const price = deterministicFloat(9.99, 999.99);
  const currency = pick(CURRENCIES);
  const inStock = rng() > 0.15;
  const quantity = inStock ? deterministicInt(10, 5000) : 0;
  const weight = deterministicFloat(0.05, 15.0);
  const dimL = deterministicFloat(5, 80, 1);
  const dimW = deterministicFloat(3, 50, 1);
  const dimH = deterministicFloat(1, 40, 1);
  const tags = pickN(TAGS_POOL, deterministicInt(2, 5));
  const createdAt = deterministicDate(2023, 2024);
  const updatedAt = deterministicDate(2025, 2025);
  const mfgName = pick(MANUFACTURER_NAMES);
  const mfgCountry = pick(COUNTRIES);
  const avgRating = deterministicFloat(3.0, 5.0, 1);
  const ratingCount = deterministicInt(50, 10000);
  const r5 = Math.round((ratingCount * (avgRating - 3)) / 3);
  const r4 = Math.round(ratingCount * 0.3);
  const r3 = Math.round(ratingCount * 0.1);
  const r2 = Math.round(ratingCount * 0.05);
  const r1 = ratingCount - r5 - r4 - r3 - r2;
  const description = `${adjective} ${noun.toLowerCase()} featuring cutting-edge technology and premium build quality. Designed for both professionals and enthusiasts who demand the best performance.`;

  const numReviews = deterministicInt(1, 5);
  const reviews: Dual[] = [];
  for (let r = 0; r < numReviews; r++) {
    const userId = dualBigInt(BigInt(deterministicInt(1000, 99999)));
    const author = JSON.stringify(pick(REVIEWER_NAMES));
    const rating = deterministicInt(1, 5);
    const title = JSON.stringify(pick(REVIEW_TITLES));
    const body = JSON.stringify(pick(REVIEW_BODIES));
    const reviewDate = dualDate(deterministicDate(2025, 2025));
    const helpful = deterministicInt(0, 500);
    const verified = rng() > 0.2;
    reviews.push(
      dualLiteral(
        `{"userId": ${userId.rdn}, "author": ${author}, "rating": ${rating}, "title": ${title}, "body": ${body}, "createdAt": ${reviewDate.rdn}, "helpful": ${helpful}, "verified": ${verified}}`,
        `{"userId": ${userId.json}, "author": ${author}, "rating": ${rating}, "title": ${title}, "body": ${body}, "createdAt": ${reviewDate.json}, "helpful": ${helpful}, "verified": ${verified}}`,
      ),
    );
  }

  const numRelated = deterministicInt(2, 5);
  const relatedIds: Dual[] = [];
  for (let r = 0; r < numRelated; r++) {
    relatedIds.push(
      dualBigInt(BigInt(1000000000000000 + deterministicInt(1, 500))),
    );
  }

  const createdAtD = dualDate(createdAt);
  const updatedAtD = dualDate(updatedAt);
  const tagsJson = JSON.stringify(tags);

  const fields = [
    `"id": ${id.rdn}`,
    `"sku": ${JSON.stringify(sku)}`,
    `"name": ${JSON.stringify(name)}`,
    `"description": ${JSON.stringify(description)}`,
    `"category": ${JSON.stringify(category)}`,
    `"price": ${price}`,
    `"currency": ${JSON.stringify(currency)}`,
    `"inStock": ${inStock}`,
    `"quantity": ${quantity}`,
    `"weight": ${weight}`,
    `"dimensions": {"length": ${dimL}, "width": ${dimW}, "height": ${dimH}, "unit": "cm"}`,
    `"tags": ${tagsJson}`,
    `"createdAt": ${createdAtD.rdn}`,
    `"updatedAt": ${updatedAtD.rdn}`,
    `"manufacturer": {"name": ${JSON.stringify(mfgName)}, "country": ${JSON.stringify(mfgCountry)}, "website": "https://${mfgName.toLowerCase().replace(/\s+/g, "")}.example.com"}`,
    `"ratings": {"average": ${avgRating}, "count": ${ratingCount}, "distribution": {"5": ${r5}, "4": ${r4}, "3": ${r3}, "2": ${r2}, "1": ${Math.max(0, r1)}}}`,
    `"reviews": [\n        ${reviews.map((r) => r.rdn).join(",\n        ")}\n      ]`,
    `"relatedProductIds": [${relatedIds.map((r) => r.rdn).join(", ")}]`,
  ];

  const fieldsJson = [
    `"id": ${id.json}`,
    `"sku": ${JSON.stringify(sku)}`,
    `"name": ${JSON.stringify(name)}`,
    `"description": ${JSON.stringify(description)}`,
    `"category": ${JSON.stringify(category)}`,
    `"price": ${price}`,
    `"currency": ${JSON.stringify(currency)}`,
    `"inStock": ${inStock}`,
    `"quantity": ${quantity}`,
    `"weight": ${weight}`,
    `"dimensions": {"length": ${dimL}, "width": ${dimW}, "height": ${dimH}, "unit": "cm"}`,
    `"tags": ${tagsJson}`,
    `"createdAt": ${createdAtD.json}`,
    `"updatedAt": ${updatedAtD.json}`,
    `"manufacturer": {"name": ${JSON.stringify(mfgName)}, "country": ${JSON.stringify(mfgCountry)}, "website": "https://${mfgName.toLowerCase().replace(/\s+/g, "")}.example.com"}`,
    `"ratings": {"average": ${avgRating}, "count": ${ratingCount}, "distribution": {"5": ${r5}, "4": ${r4}, "3": ${r3}, "2": ${r2}, "1": ${Math.max(0, r1)}}}`,
    `"reviews": [\n        ${reviews.map((r) => r.json).join(",\n        ")}\n      ]`,
    `"relatedProductIds": [${relatedIds.map((r) => r.json).join(", ")}]`,
  ];

  return {
    rdn: `    {\n      ${fields.join(",\n      ")}\n    }`,
    json: `    {\n      ${fieldsJson.join(",\n      ")}\n    }`,
  };
}

function buildTypicalPayload(productCount: number): Dual {
  const timestamp = dualDate(new Date("2025-11-20T09:15:33.472Z"));
  const totalPages = Math.ceil(productCount / 20);

  const metaRdn = `"meta": {"apiVersion": "3.2.1", "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "timestamp": ${timestamp.rdn}, "pagination": {"page": 1, "perPage": 20, "totalItems": ${productCount}, "totalPages": ${totalPages}}}`;
  const metaJson = `"meta": {"apiVersion": "3.2.1", "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "timestamp": ${timestamp.json}, "pagination": {"page": 1, "perPage": 20, "totalItems": ${productCount}, "totalPages": ${totalPages}}}`;

  const products: Dual[] = [];
  for (let i = 0; i < productCount; i++) {
    products.push(buildTypicalProduct(i));
  }

  return {
    rdn: `{\n  ${metaRdn},\n  "products": [\n${products.map((p) => p.rdn).join(",\n")}\n  ]\n}\n`,
    json: `{\n  ${metaJson},\n  "products": [\n${products.map((p) => p.json).join(",\n")}\n  ]\n}\n`,
  };
}

// ---------------------------------------------------------------------------
// RDN-heavy payload builder — IoT Monitoring Dashboard
// ---------------------------------------------------------------------------

function buildSensor(
  index: number,
  readingCount: number,
  maintCount: number,
): Dual {
  const sensorId = dualBigInt(BigInt(8800000000000100 + index + 1));
  const namePrefix = pick(SENSOR_NAMES_PREFIX);
  const nameSuffix = `${String.fromCharCode(65 + (index % 26))}${index + 1}`;
  const name = `${namePrefix} ${nameSuffix}`;

  const firmwareStr = `firmware_v${deterministicInt(1, 9)}.${deterministicInt(0, 99)}_sensor_${index}`;
  const firmware = dualBinaryB64(firmwareStr);
  const calibStr = `cal_data_${index}_${deterministicInt(1000, 9999)}`;
  const calibration = dualBinaryHex(calibStr);

  const loc = pick(SENSOR_LOCATIONS);
  const lat = deterministicFloat(30.0, 50.0, 4);
  const lon = deterministicFloat(-130.0, -70.0, 4);
  const location = dualTuple([
    dualString(loc),
    dualNumber(lat),
    dualNumber(lon),
  ]);

  const validNamePattern = dualRegExp("^[A-Za-z0-9_-]{3,64}$", "");
  const subnet = deterministicInt(1, 254);
  const ipPattern = dualRegExp(`^192\\.168\\.${subnet}\\.\\d{1,3}$`, "i");

  const numTags = deterministicInt(2, 4);
  const tags = dualStringSet(pickN(SENSOR_TAG_POOL, numTags));
  const numCaps = deterministicInt(2, 4);
  const capabilities = dualImplicitSet(
    pickN(SENSOR_CAPABILITIES_POOL, numCaps),
  );

  const installedAt = dualDate(deterministicDate(2022, 2024));
  const lastCalibration = dualDateOnly(deterministicDate(2025, 2025));

  const uptimeH = deterministicInt(20, 24);
  const uptimeM = deterministicInt(0, 59);
  const dailyUptime = dualDuration(
    `PT${uptimeH}H${uptimeM > 0 ? `${uptimeM}M` : ""}`,
  );

  const bootH = deterministicInt(0, 8);
  const bootTime = dualTimeOnly(bootH, 0, 0);
  const shutH = deterministicInt(20, 23);
  const shutM = deterministicInt(0, 59);
  const shutS = deterministicInt(0, 59);
  const shutdownTime = dualTimeOnly(shutH, shutM, shutS);

  // Readings: Map{ Date => { readings object } }
  const readingEntries: string[] = [];
  const readingEntriesJson: string[] = [];
  const baseDate = new Date("2025-11-20T00:00:00.000Z");
  for (let r = 0; r < readingCount; r++) {
    const readingDate = new Date(baseDate.getTime() + r * 3600000);
    const rd = dualDate(readingDate);
    const temp =
      r % 7 === 0 ? dualNaN() : dualNumber(deterministicFloat(-10, 45));
    const humidity = dualNumber(deterministicFloat(10, 95));
    const pressure =
      r % 11 === 0 ? dualInfinity() : dualNumber(deterministicFloat(950, 1050));
    const signal =
      r % 13 === 0
        ? dualNegInfinity()
        : dualNumber(deterministicFloat(-80, -20));
    const errorRate = dualNumber(deterministicFloat(0, 0.05, 3));
    readingEntries.push(
      `        ${rd.rdn} => {"temperature": ${temp.rdn}, "humidity": ${humidity.rdn}, "pressure": ${pressure.rdn}, "signalStrength": ${signal.rdn}, "errorRate": ${errorRate.rdn}}`,
    );
    readingEntriesJson.push(
      `        ${rd.json}: {"temperature": ${temp.json}, "humidity": ${humidity.json}, "pressure": ${pressure.json}, "signalStrength": ${signal.json}, "errorRate": ${errorRate.json}}`,
    );
  }

  // Alert thresholds: Map{ string => Tuple(min, max) }
  const thresholdKeys = ["temperature", "humidity", "pressure"];
  const thresholdEntries: string[] = [];
  const thresholdEntriesJson: string[] = [];
  for (const key of thresholdKeys) {
    const min = deterministicFloat(-50, 0);
    const max = deterministicFloat(50, 150);
    const t = dualTuple([dualNumber(min), dualNumber(max)]);
    thresholdEntries.push(`        "${key}" => ${t.rdn}`);
    thresholdEntriesJson.push(`        "${key}": ${t.json}`);
  }

  // Maintenance log
  const maintEntries: string[] = [];
  const maintEntriesJson: string[] = [];
  for (let m = 0; m < maintCount; m++) {
    const ts = dualDate(deterministicDate(2025, 2025));
    const tech = pick(TECHNICIAN_NAMES);
    const durH = deterministicInt(0, 4);
    const durM = deterministicInt(0, 59);
    const dur = dualDuration(`PT${durH > 0 ? `${durH}H` : ""}${durM}M`);
    const numParts = deterministicInt(1, 3);
    const parts = dualStringSet(pickN(MAINT_PARTS, numParts));
    const checksumStr = `chk_${index}_${m}_${deterministicInt(1000, 9999)}`;
    const checksum = dualBinaryB64(checksumStr);
    const notes = pick(MAINT_NOTES);

    maintEntries.push(
      `        {"timestamp": ${ts.rdn}, "technician": ${JSON.stringify(tech)}, "duration": ${dur.rdn}, "partsReplaced": ${parts.rdn}, "checksum": ${checksum.rdn}, "notes": ${JSON.stringify(notes)}}`,
    );
    maintEntriesJson.push(
      `        {"timestamp": ${ts.json}, "technician": ${JSON.stringify(tech)}, "duration": ${dur.json}, "partsReplaced": ${parts.json}, "checksum": ${checksum.json}, "notes": ${JSON.stringify(notes)}}`,
    );
  }

  // Status history: Map{ Date-only => Set{statuses} }
  const statusEntries: string[] = [];
  const statusEntriesJson: string[] = [];
  for (let d = 0; d < 3; d++) {
    const sd = new Date(Date.UTC(2025, 10, 18 + d));
    const sdd = dualDateOnly(sd);
    const statuses =
      rng() > 0.3
        ? dualImplicitSet(["online", "nominal"])
        : dualImplicitSet(["online", "warning"]);
    statusEntries.push(`        ${sdd.rdn} => ${statuses.rdn}`);
    statusEntriesJson.push(`        ${sdd.json}: ${statuses.json}`);
  }

  const rdnStr = `    {
      "sensorId": ${sensorId.rdn},
      "name": ${JSON.stringify(name)},
      "firmware": ${firmware.rdn},
      "calibrationData": ${calibration.rdn},
      "location": ${location.rdn},
      "validNamePattern": ${validNamePattern.rdn},
      "ipPattern": ${ipPattern.rdn},
      "tags": ${tags.rdn},
      "capabilities": ${capabilities.rdn},
      "installedAt": ${installedAt.rdn},
      "lastCalibration": ${lastCalibration.rdn},
      "dailyUptime": ${dailyUptime.rdn},
      "bootTime": ${bootTime.rdn},
      "shutdownTime": ${shutdownTime.rdn},
      "readings": Map{
${readingEntries.join(",\n")}
      },
      "alertThresholds": Map{
${thresholdEntries.join(",\n")}
      },
      "maintenanceLog": [
${maintEntries.join(",\n")}
      ],
      "statusHistory": Map{
${statusEntries.join(",\n")}
      }
    }`;

  const jsonStr = `    {
      "sensorId": ${sensorId.json},
      "name": ${JSON.stringify(name)},
      "firmware": ${firmware.json},
      "calibrationData": ${calibration.json},
      "location": ${location.json},
      "validNamePattern": ${validNamePattern.json},
      "ipPattern": ${ipPattern.json},
      "tags": ${tags.json},
      "capabilities": ${capabilities.json},
      "installedAt": ${installedAt.json},
      "lastCalibration": ${lastCalibration.json},
      "dailyUptime": ${dailyUptime.json},
      "bootTime": ${bootTime.json},
      "shutdownTime": ${shutdownTime.json},
      "readings": {
${readingEntriesJson.join(",\n")}
      },
      "alertThresholds": {
${thresholdEntriesJson.join(",\n")}
      },
      "maintenanceLog": [
${maintEntriesJson.join(",\n")}
      ],
      "statusHistory": {
${statusEntriesJson.join(",\n")}
      }
    }`;

  return { rdn: rdnStr, json: jsonStr };
}

function buildRdnHeavyPayload(
  sensorCount: number,
  readingsPerSensor: number,
  maintPerSensor: number,
): Dual {
  const dashboardId = dualBigInt(9000000000000001n);
  const generatedAt = dualDate(new Date("2025-11-20T09:15:33.472Z"));
  const refreshInterval = dualDuration("PT30S");
  const windowStart = dualDate(new Date("2025-11-20T00:00:00.000Z"));
  const windowEnd = dualDate(new Date("2025-11-20T23:59:59.999Z"));
  const dataWindow = dualTuple([windowStart, windowEnd]);
  const version = dualBigInt(42n);

  const sensors: Dual[] = [];
  for (let i = 0; i < sensorCount; i++) {
    sensors.push(buildSensor(i, readingsPerSensor, maintPerSensor));
  }

  // Aggregations
  const tempByHourEntries: string[] = [];
  const tempByHourEntriesJson: string[] = [];
  for (let h = 0; h < Math.min(readingsPerSensor, 24); h++) {
    const t = dualTimeOnly(h, 0, 0);
    const minT = dualNumber(deterministicFloat(-5, 15));
    const avgT = dualNumber(deterministicFloat(15, 25));
    const maxT =
      h % 5 === 0 ? dualNaN() : dualNumber(deterministicFloat(25, 45));
    const tuple = dualTuple([minT, avgT, maxT]);
    tempByHourEntries.push(`      ${t.rdn} => ${tuple.rdn}`);
    tempByHourEntriesJson.push(`      ${t.json}: ${tuple.json}`);
  }

  const errorPatterns = pickN(ERROR_PATTERNS_POOL, deterministicInt(2, 4));

  // Known firmwares set (binary values)
  const numFirmwares = Math.min(sensorCount, 5);
  const firmwareSetItems: Dual[] = [];
  for (let f = 0; f < numFirmwares; f++) {
    firmwareSetItems.push(dualBinaryB64(`firmware_v${f + 1}.0_ref`));
  }
  const knownFirmwares = dualSet(firmwareSetItems);

  // Sensor pairs set (tuples of BigInts)
  const numPairs = Math.min(Math.floor(sensorCount / 2), 5);
  const pairItems: Dual[] = [];
  for (let p = 0; p < numPairs; p++) {
    pairItems.push(
      dualTuple([
        dualBigInt(BigInt(8800000000000100 + p * 2 + 1)),
        dualBigInt(BigInt(8800000000000100 + p * 2 + 2)),
      ]),
    );
  }
  const sensorPairs = dualSet(pairItems);

  // Uptime summary: Map{ Date-only => Duration }
  const uptimeEntries: string[] = [];
  const uptimeEntriesJson: string[] = [];
  for (let d = 0; d < 3; d++) {
    const ud = dualDateOnly(new Date(Date.UTC(2025, 10, 18 + d)));
    const totalH = sensorCount * deterministicInt(20, 24);
    const totalM = deterministicInt(0, 59);
    const dur = dualDuration(`PT${totalH}H${totalM > 0 ? `${totalM}M` : ""}`);
    uptimeEntries.push(`      ${ud.rdn} => ${dur.rdn}`);
    uptimeEntriesJson.push(`      ${ud.json}: ${dur.json}`);
  }

  const rdnStr = `{
  "dashboard": {
    "id": ${dashboardId.rdn},
    "generatedAt": ${generatedAt.rdn},
    "refreshInterval": ${refreshInterval.rdn},
    "dataWindow": ${dataWindow.rdn},
    "version": ${version.rdn}
  },
  "sensors": [
${sensors.map((s) => s.rdn).join(",\n")}
  ],
  "aggregations": {
    "temperatureByHour": Map{
${tempByHourEntries.join(",\n")}
    },
    "errorPatterns": [${errorPatterns.map((p) => p).join(", ")}],
    "knownFirmwares": ${knownFirmwares.rdn},
    "sensorPairs": ${sensorPairs.rdn},
    "uptimeSummary": Map{
${uptimeEntries.join(",\n")}
    }
  }
}
`;

  const jsonStr = `{
  "dashboard": {
    "id": ${dashboardId.json},
    "generatedAt": ${generatedAt.json},
    "refreshInterval": ${refreshInterval.json},
    "dataWindow": ${dataWindow.json},
    "version": ${version.json}
  },
  "sensors": [
${sensors.map((s) => s.json).join(",\n")}
  ],
  "aggregations": {
    "temperatureByHour": {
${tempByHourEntriesJson.join(",\n")}
    },
    "errorPatterns": [${errorPatterns.map((p) => JSON.stringify(p)).join(", ")}],
    "knownFirmwares": ${knownFirmwares.json},
    "sensorPairs": ${sensorPairs.json},
    "uptimeSummary": {
${uptimeEntriesJson.join(",\n")}
    }
  }
}
`;

  return { rdn: rdnStr, json: jsonStr };
}

// ---------------------------------------------------------------------------
// Streaming writer for large files
// ---------------------------------------------------------------------------

async function writeStreamingTypical(
  path: string,
  format: "rdn" | "json",
  productCount: number,
) {
  const writer = Bun.file(path).writer();
  const timestamp =
    format === "rdn"
      ? "@2025-11-20T09:15:33.472Z"
      : '"2025-11-20T09:15:33.472Z"';
  const totalPages = Math.ceil(productCount / 20);

  writer.write(
    `{\n  "meta": {"apiVersion": "3.2.1", "requestId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "timestamp": ${timestamp}, "pagination": {"page": 1, "perPage": 20, "totalItems": ${productCount}, "totalPages": ${totalPages}}},\n  "products": [\n`,
  );

  for (let i = 0; i < productCount; i++) {
    const product = buildTypicalProduct(i);
    const content = format === "rdn" ? product.rdn : product.json;
    if (i > 0) writer.write(",\n");
    writer.write(content);
    if (i % 100 === 0) writer.flush();
  }

  writer.write("\n  ]\n}\n");
  await writer.end();
}

async function writeStreamingSensor(
  writer: ReturnType<ReturnType<typeof Bun.file>["writer"]>,
  format: "rdn" | "json",
  index: number,
  readingCount: number,
  maintCount: number,
) {
  const sensor = buildSensor(index, readingCount, maintCount);
  writer.write(format === "rdn" ? sensor.rdn : sensor.json);
}

async function writeStreamingRdnHeavy(
  path: string,
  format: "rdn" | "json",
  sensorCount: number,
  readingsPerSensor: number,
  maintPerSensor: number,
) {
  const writer = Bun.file(path).writer();

  // Dashboard header
  const dashboardId =
    format === "rdn" ? "9000000000000001n" : '"9000000000000001"';
  const generatedAt =
    format === "rdn"
      ? "@2025-11-20T09:15:33.472Z"
      : '"2025-11-20T09:15:33.472Z"';
  const refreshInterval = format === "rdn" ? "@PT30S" : '"PT30S"';
  const windowStart =
    format === "rdn"
      ? "@2025-11-20T00:00:00.000Z"
      : '"2025-11-20T00:00:00.000Z"';
  const windowEnd =
    format === "rdn"
      ? "@2025-11-20T23:59:59.999Z"
      : '"2025-11-20T23:59:59.999Z"';
  const dataWindow =
    format === "rdn"
      ? `(${windowStart}, ${windowEnd})`
      : `[${windowStart}, ${windowEnd}]`;
  const version = format === "rdn" ? "42n" : '"42"';

  writer.write(
    `{\n  "dashboard": {\n    "id": ${dashboardId},\n    "generatedAt": ${generatedAt},\n    "refreshInterval": ${refreshInterval},\n    "dataWindow": ${dataWindow},\n    "version": ${version}\n  },\n  "sensors": [\n`,
  );

  for (let i = 0; i < sensorCount; i++) {
    if (i > 0) writer.write(",\n");
    await writeStreamingSensor(
      writer,
      format,
      i,
      readingsPerSensor,
      maintPerSensor,
    );
    if (i % 50 === 0) writer.flush();
  }

  // Aggregations — build in-memory (small relative to sensors)
  const aggPayload = buildRdnHeavyAggregations(
    format,
    sensorCount,
    readingsPerSensor,
  );
  writer.write(`\n  ],\n  "aggregations": ${aggPayload}\n}\n`);
  await writer.end();
}

function buildRdnHeavyAggregations(
  format: "rdn" | "json",
  sensorCount: number,
  readingsPerSensor: number,
): string {
  const tempByHourEntries: string[] = [];
  for (let h = 0; h < Math.min(readingsPerSensor, 24); h++) {
    const timeStr = `${padTime(h)}:00:00`;
    const t = format === "rdn" ? `@${timeStr}` : `"${timeStr}"`;
    const minT = deterministicFloat(-5, 15);
    const avgT = deterministicFloat(15, 25);
    const maxRaw =
      h % 5 === 0
        ? format === "rdn"
          ? "NaN"
          : "null"
        : String(deterministicFloat(25, 45));
    const tuple =
      format === "rdn"
        ? `(${minT}, ${avgT}, ${maxRaw})`
        : `[${minT}, ${avgT}, ${maxRaw}]`;
    const sep = format === "rdn" ? " => " : ": ";
    tempByHourEntries.push(`      ${t}${sep}${tuple}`);
  }

  const errorPatterns = pickN(ERROR_PATTERNS_POOL, deterministicInt(2, 4));
  const errorPatternsStr =
    format === "rdn"
      ? errorPatterns.join(", ")
      : errorPatterns.map((p) => JSON.stringify(p)).join(", ");

  const numFirmwares = Math.min(sensorCount, 5);
  const firmwareItems: string[] = [];
  for (let f = 0; f < numFirmwares; f++) {
    const b64 = toBase64(`firmware_v${f + 1}.0_ref`);
    firmwareItems.push(format === "rdn" ? `b"${b64}"` : `"${b64}"`);
  }
  const firmwaresStr =
    format === "rdn"
      ? `Set{${firmwareItems.join(", ")}}`
      : `[${firmwareItems.join(", ")}]`;

  const numPairs = Math.min(Math.floor(sensorCount / 2), 5);
  const pairItems: string[] = [];
  for (let p = 0; p < numPairs; p++) {
    const a = BigInt(8800000000000100 + p * 2 + 1);
    const b = BigInt(8800000000000100 + p * 2 + 2);
    pairItems.push(format === "rdn" ? `(${a}n, ${b}n)` : `["${a}", "${b}"]`);
  }
  const pairsStr =
    format === "rdn"
      ? `Set{${pairItems.join(", ")}}`
      : `[${pairItems.join(", ")}]`;

  const uptimeEntries: string[] = [];
  for (let d = 0; d < 3; d++) {
    const ds = new Date(Date.UTC(2025, 10, 18 + d)).toISOString().slice(0, 10);
    const dateStr = format === "rdn" ? `@${ds}` : `"${ds}"`;
    const totalH = sensorCount * deterministicInt(20, 24);
    const totalM = deterministicInt(0, 59);
    const durStr = `PT${totalH}H${totalM > 0 ? `${totalM}M` : ""}`;
    const dur = format === "rdn" ? `@${durStr}` : `"${durStr}"`;
    const sep = format === "rdn" ? " => " : ": ";
    uptimeEntries.push(`      ${dateStr}${sep}${dur}`);
  }

  const mapOpen = format === "rdn" ? "Map{" : "{";
  const mapClose = "}";

  return `{
    "temperatureByHour": ${mapOpen}
${tempByHourEntries.join(",\n")}
    ${mapClose},
    "errorPatterns": [${errorPatternsStr}],
    "knownFirmwares": ${firmwaresStr},
    "sensorPairs": ${pairsStr},
    "uptimeSummary": ${mapOpen}
${uptimeEntries.join(",\n")}
    ${mapClose}
  }`;
}

// ---------------------------------------------------------------------------
// Size configs
// ---------------------------------------------------------------------------

interface SizeConfig {
  typical: { products: number };
  rdnHeavy: { sensors: number; readings: number; maintenance: number };
}

const SIZE_CONFIGS: Record<string, SizeConfig> = {
  medium: {
    typical: { products: 560 },
    rdnHeavy: { sensors: 185, readings: 24, maintenance: 4 },
  },
  large: {
    typical: { products: 56000 },
    rdnHeavy: { sensors: 18500, readings: 24, maintenance: 4 },
  },
};

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function generateFixtures() {
  console.log("RDN Benchmark Fixture Generator");
  console.log(`  Sizes: ${sizes.join(", ")}`);
  console.log(`  Types: ${types.join(", ")}`);
  console.log(`  Dry run: ${dryRun}`);
  console.log();

  for (const size of sizes) {
    const config = SIZE_CONFIGS[size];
    if (!config) {
      console.error(`Unknown size: ${size}`);
      process.exit(1);
    }

    for (const type of types) {
      const rdnPath = join(FIXTURES_DIR, `${size}-${type}.rdn`);
      const jsonPath = join(FIXTURES_DIR, `${size}-${type}.json`);

      console.log(`Generating ${size}-${type}...`);

      if (dryRun) {
        if (type === "typical") {
          console.log(`  Would generate ${config.typical.products} products`);
        } else {
          console.log(
            `  Would generate ${config.rdnHeavy.sensors} sensors × ${config.rdnHeavy.readings} readings × ${config.rdnHeavy.maintenance} maint.`,
          );
        }
        console.log(`  RDN: ${rdnPath}`);
        console.log(`  JSON: ${jsonPath}`);
        continue;
      }

      // Reset RNG for each file pair so results are deterministic per-pair
      const rngSeed = size === "medium" ? 100 : 200;
      const typeSeed = type === "typical" ? 0 : 1000;
      rng = createRng(rngSeed + typeSeed);

      if (size === "large") {
        // Use streaming for large files
        if (type === "typical") {
          await writeStreamingTypical(rdnPath, "rdn", config.typical.products);
          // Reset RNG for JSON to match
          rng = createRng(rngSeed + typeSeed);
          await writeStreamingTypical(
            jsonPath,
            "json",
            config.typical.products,
          );
        } else {
          await writeStreamingRdnHeavy(
            rdnPath,
            "rdn",
            config.rdnHeavy.sensors,
            config.rdnHeavy.readings,
            config.rdnHeavy.maintenance,
          );
          rng = createRng(rngSeed + typeSeed);
          await writeStreamingRdnHeavy(
            jsonPath,
            "json",
            config.rdnHeavy.sensors,
            config.rdnHeavy.readings,
            config.rdnHeavy.maintenance,
          );
        }
      } else {
        // In-memory for medium
        let result: Dual;
        if (type === "typical") {
          result = buildTypicalPayload(config.typical.products);
        } else {
          result = buildRdnHeavyPayload(
            config.rdnHeavy.sensors,
            config.rdnHeavy.readings,
            config.rdnHeavy.maintenance,
          );
        }
        await Bun.write(rdnPath, result.rdn);
        await Bun.write(jsonPath, result.json);
      }

      // Validate & report
      const rdnSize = Bun.file(rdnPath).size;
      const jsonSize = Bun.file(jsonPath).size;
      console.log(
        `  RDN: ${(rdnSize / 1024 / 1024).toFixed(2)} MB (${rdnPath})`,
      );
      console.log(
        `  JSON: ${(jsonSize / 1024 / 1024).toFixed(2)} MB (${jsonPath})`,
      );

      // Validate JSON
      try {
        const jsonContent = await Bun.file(jsonPath).text();
        JSON.parse(jsonContent);
        console.log(`  JSON validation: PASS`);
      } catch (e) {
        console.error(`  JSON validation: FAIL - ${(e as Error).message}`);
        process.exit(1);
      }
    }
    console.log();
  }

  console.log("Done!");
}

generateFixtures().catch((err) => {
  console.error(err);
  process.exit(1);
});
