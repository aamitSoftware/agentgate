import { describe, expect, it } from "vitest";
import type { ActivityItem } from "@/api/types";
import { deniedByAgent, mergeActivity } from "./activity";

const call = (id: string, occurredOn: string, decision: "Allowed" | "Denied", agent = "demo-agent"): ActivityItem => ({
  id,
  kind: "call",
  occurredOn,
  severity: decision === "Denied" ? "warning" : "info",
  agentClientId: agent,
  serverName: "crm",
  method: "tools/call",
  toolName: "export_all_customers",
  decision,
  reason: null,
  traceId: null,
  eventKind: null,
  summary: null,
  actorId: null,
  serverId: null,
  agentId: null,
});

describe("mergeActivity", () => {
  it("keeps newest first and drops replays", () => {
    const first = call("1", "2026-10-22T10:00:00Z", "Allowed");
    const second = call("2", "2026-10-22T10:00:05Z", "Denied");
    expect(mergeActivity([first], [second, first]).map((item) => item.id)).toEqual(["2", "1"]);
  });

  it("returns the same array when nothing is new, so React skips the render", () => {
    const current = [call("1", "2026-10-22T10:00:00Z", "Allowed")];
    expect(mergeActivity(current, [current[0]!])).toBe(current);
  });
});

describe("deniedByAgent", () => {
  it("counts only recent denials, per agent", () => {
    const now = Date.parse("2026-10-22T10:10:00Z");
    const items = [
      call("1", "2026-10-22T10:09:00Z", "Denied"),
      call("2", "2026-10-22T10:08:00Z", "Denied"),
      call("3", "2026-10-22T10:09:30Z", "Allowed"),
      call("4", "2026-10-22T09:00:00Z", "Denied"),
      call("5", "2026-10-22T10:09:50Z", "Denied", "other"),
    ];
    expect(deniedByAgent(items, now)).toEqual([
      ["demo-agent", 2],
      ["other", 1],
    ]);
  });
});
