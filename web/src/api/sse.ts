import { getAccessToken } from "@/auth/session";
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

export type StreamState = "connecting" | "live" | "retrying";

/**
 * Follows the Hub's live activity with fetch (EventSource cannot send a bearer token) and reconnects with
 * backoff until <paramref name="signal"/> aborts. Rows may repeat across a reconnect; callers de-duplicate by id.
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
      const response = await fetch("/api/hub/activity/stream", {
        headers: { Accept: "text/event-stream", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        signal,
      });
      if (!response.ok || !response.body) throw new Error(`stream ${response.status}`);

      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
      const parser = new SseParser();
      onState("live"); // the Hub opens every stream with a heartbeat, so headers arrive at once
      attempt = 0;
      for (;;) {
        const { value, done } = await reader.read();
        if (done) break;
        for (const event of parser.push(value))
          if (event.event === "activity" && event.data) onItem(JSON.parse(event.data) as ActivityItem);
      }
    } catch (error) {
      if (signal.aborted) return;
      console.warn("activity stream dropped", error);
    }
    attempt++;
    await new Promise((resolve) => setTimeout(resolve, Math.min(15_000, 500 * 2 ** attempt)));
  }
}
