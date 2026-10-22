/** Hashes are long; twelve hex characters are enough to tell manifests apart on screen. */
export function shortHash(hash: string): string {
  return hash.length > 12 ? hash.slice(0, 12) : hash;
}

/** Long ids (Keycloak subjects, grant ids) shortened the same way git shortens commits. */
export function shortId(id: string): string {
  return id.length > 8 ? id.slice(0, 8) : id;
}

const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ["day", 86_400_000],
  ["hour", 3_600_000],
  ["minute", 60_000],
  ["second", 1_000],
];
const relative = new Intl.RelativeTimeFormat(undefined, { numeric: "auto", style: "long" });

export function timeAgo(iso: string, now: number = Date.now()): string {
  const elapsed = new Date(iso).getTime() - now;
  // The shared clock ticks every few seconds and clocks drift: a moment ago is "now", never "in 10 seconds".
  if (elapsed > 0 && elapsed < 60_000) return relative.format(0, "second");
  for (const [unit, size] of units) {
    if (Math.abs(elapsed) >= size || unit === "second") return relative.format(Math.round(elapsed / size), unit);
  }
  return "";
}

/** Absolute UTC timestamp, the way the audit trail should be read. */
export function utc(iso: string): string {
  return `${new Date(iso).toISOString().replace("T", " ").slice(0, 19)} UTC`;
}

/** Wall-clock UTC time for a live feed row (labelled UTC by the feed's header). */
export function clock(iso: string): string {
  return new Date(iso).toISOString().slice(11, 19);
}

const integer = new Intl.NumberFormat();
export const count = (value: number) => integer.format(value);

/** "1 finding", "3 findings". */
export function plural(value: number, noun: string, nouns = `${noun}s`): string {
  return `${count(value)} ${value === 1 ? noun : nouns}`;
}
