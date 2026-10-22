import type { ActivityItem } from "@/api/types";

/** The live feed keeps this many rows; older ones are in the Hub's history. */
export const keepRows = 1_000;
/** Rows buffered while the feed is paused; beyond this the oldest buffered rows are dropped. */
export const pauseBuffer = 500;
/** The gateway's own anomaly rule (Gateway.API GatewayOptions): this many denials in this window block the agent. */
export const spikeThreshold = 5;
export const spikeWindowMs = 60_000;

/** Newest first, no duplicates (a reconnect can replay rows), at most `keepRows` rows. */
export function mergeActivity(current: ActivityItem[], incoming: ActivityItem[]): ActivityItem[] {
  const seen = new Set(current.map((item) => item.id));
  const fresh = incoming.filter((item) => !seen.has(item.id));
  if (fresh.length === 0) return current;
  return [...fresh, ...current].sort((left, right) => right.occurredOn.localeCompare(left.occurredOn)).slice(0, keepRows);
}

/** Appends to a bounded buffer: the newest `limit` rows survive. */
export function appendBounded<T>(buffer: T[], item: T, limit = pauseBuffer): T[] {
  return buffer.length < limit ? [...buffer, item] : [...buffer.slice(buffer.length - limit + 1), item];
}

/** Denied calls per agent inside the gateway's window, busiest first. */
export function deniedByAgent(items: ActivityItem[], now: number, windowMs = spikeWindowMs): [string, number][] {
  const counts = new Map<string, number>();
  for (const item of items)
    if (item.kind === "call" && item.decision === "Denied" && item.agentClientId && now - new Date(item.occurredOn).getTime() <= windowMs)
      counts.set(item.agentClientId, (counts.get(item.agentClientId) ?? 0) + 1);
  return [...counts].sort((left, right) => right[1] - left[1]);
}

export type ActivityFilter = "all" | "denied" | "governance";

export function matchesFilter(item: ActivityItem, filter: ActivityFilter): boolean {
  if (filter === "denied") return item.kind === "call" && item.decision === "Denied";
  if (filter === "governance") return item.kind === "event";
  return true;
}

/** A row of the feed: one item, or a run of identical consecutive calls shown once with a count. */
export interface ActivityGroup {
  item: ActivityItem;
  repeats: number;
}

const callKey = (item: ActivityItem) =>
  item.kind === "call" ? [item.agentClientId, item.serverName, item.method, item.toolName, item.decision, item.reason].join("|") : null;

/** Collapses consecutive identical calls ("denied ×5"): a probing agent should not flood the feed. */
export function groupRepeats(items: ActivityItem[]): ActivityGroup[] {
  const groups: ActivityGroup[] = [];
  for (const item of items) {
    const last = groups.at(-1);
    const key = callKey(item);
    if (last && key !== null && callKey(last.item) === key) last.repeats++;
    else groups.push({ item, repeats: 1 });
  }
  return groups;
}
