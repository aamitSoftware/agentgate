import { getAccessToken } from "@/auth/session";
import { reportUnauthorized } from "./client";
import type { ActivityItem } from "./types";

export interface SseEvent {
  event: string;
  data: string;
  id?: string;
}

/**
 * Incremental text/event-stream parser: feed it chunks as they arrive, get back complete events.
 * Only what the Hub sends is supported (event, data, id; comments ignored).
 */
export class SseParser {
  private buffer = "";

  push(chunk: string): SseEvent[] {
    this.buffer += chunk.replace(/\r\n?/g, "\n");
    const events: SseEvent[] = [];
    let boundary: number;
    while ((boundary = this.buffer.indexOf("\n\n")) >= 0) {
      const block = this.buffer.slice(0, boundary);
      this.buffer = this.buffer.slice(boundary + 2);
      const event: SseEvent = { event: "message", data: "" };
      const data: string[] = [];
      for (const line of block.split("\n")) {
        if (!line || line.startsWith(":")) continue;
        const colon = line.indexOf(":");
        const field = colon < 0 ? line : line.slice(0, colon);
        const value = colon < 0 ? "" : line.slice(colon + 1).replace(/^ /, "");
        if (field === "event") event.event = value;
        else if (field === "data") data.push(value);
        else if (field === "id") event.id = value;
      }
      event.data = data.join("\n");
      if (block.trim()) events.push(event);
    }
    return events;
  }
}

export type StreamState = "connecting" | "live" | "retrying" | "unauthorized";

/** Only rows shaped like the Hub's ActivityItem get through; anything else is dropped, not trusted. */
export function parseActivity(data: string): ActivityItem | null {
  try {
    const value = JSON.parse(data) as Partial<ActivityItem> | null;
    if (!value || typeof value.id !== "string" || typeof value.occurredOn !== "string" || (value.kind !== "call" && value.kind !== "event"))
      return null;
    return value as ActivityItem;
  } catch {
    return null;
  }
}

/** Resolves after `ms`, or as soon as `signal` aborts. */
function pause(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(resolve, ms);
    signal.addEventListener("abort", () => (clearTimeout(timer), resolve()), { once: true });
  });
}

/**
 * Follows the Hub's live activity with fetch (EventSource cannot send a bearer token) and reconnects with
 * backoff until `signal` aborts. Rows that happen while disconnected are missed (the Hub has no replay yet);
 * callers refetch the snapshot when they mount. A 401 ends the stream: the session is gone, not the network.
 */
export async function followActivity(
  onItem: (item: ActivityItem) => void,
  onState: (state: StreamState) => void,
  signal: AbortSignal,
): Promise<void> {
  let attempt = 0;
  while (!signal.aborted) {
    onState(attempt === 0 ? "connecting" : "retrying");
    try {
      const token = await getAccessToken();
      if (!token) {
        reportUnauthorized();
        return onState("unauthorized");
      }
      const response = await fetch("/api/hub/activity/stream", {
        headers: { Accept: "text/event-stream", Authorization: `Bearer ${token}` },
        signal,
      });
      if (response.status === 401) {
        reportUnauthorized();
        return onState("unauthorized");
      }
      if (!response.ok || !response.body) throw new Error(`stream ${response.status}`);

      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
      const parser = new SseParser();
      for (let first = true; ; first = false) {
        const { value, done } = await reader.read();
        if (done) break;
        if (first) {
          onState("live"); // bytes arrived: the Hub opens every stream with a heartbeat
          attempt = 0;
        }
        for (const event of parser.push(value)) {
          if (event.event !== "activity" || !event.data) continue;
          const item = parseActivity(event.data);
          if (item) onItem(item);
          else console.warn("skipped a malformed activity event", event.data);
        }
      }
    } catch (error) {
      if (signal.aborted) return;
      console.warn("activity stream dropped", error);
    }
    attempt++;
    await pause(Math.min(15_000, 500 * 2 ** attempt), signal);
  }
}
