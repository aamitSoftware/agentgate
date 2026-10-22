import { Link } from "@tanstack/react-router";
import type { ActivityItem } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/cn";
import { clock, timeAgo, utc } from "@/lib/format";
import { Dot, severityTone } from "./signals";

/** One line of the live feed: a gateway decision or a governance event, loud only when it matters. */
export function ActivityRow({ item, fresh }: { item: ActivityItem; fresh?: boolean }) {
  const tint = item.severity === "critical" ? "bg-danger-soft/60" : item.severity === "warning" && item.kind === "event" ? "bg-warn-soft/40" : "";
  return (
    <div
      className={cn(
        "grid grid-cols-[4.5rem_5.5rem_minmax(0,1fr)] items-baseline gap-3 border-b border-line/70 px-3 py-1.5",
        tint,
        fresh && "animate-arrive",
      )}
    >
      <time className="font-mono text-[12px] text-ink-faint tabular" title={utc(item.occurredOn)}>
        {clock(item.occurredOn)}
      </time>
      {item.kind === "call" ? <CallDecision item={item} /> : <Badge tone={severityTone[item.severity]}>{eventLabel(item.eventKind)}</Badge>}
      {item.kind === "call" ? <CallDetail item={item} /> : <span className="min-w-0 text-ink">{item.summary}</span>}
    </div>
  );
}

function CallDecision({ item }: { item: ActivityItem }) {
  return item.decision === "Denied" ? <Badge tone="danger">denied</Badge> : <Badge tone="ok">allowed</Badge>;
}

function CallDetail({ item }: { item: ActivityItem }) {
  return (
    <span className="min-w-0 truncate">
      <span className="font-mono text-[12px] text-ink">{item.agentClientId}</span>
      <span className="text-ink-faint"> → </span>
      <span className="font-mono text-[12px] text-ink">
        {item.serverName}
        {item.toolName ? <span className="text-ink-muted">/{item.toolName}</span> : <span className="text-ink-faint"> {item.method}</span>}
      </span>
      {item.reason && <span className="ml-2 text-ink-muted">{item.reason}</span>}
    </span>
  );
}

const eventLabels: Record<string, string> = {
  ManifestSubmitted: "submitted",
  ManifestReviewOpened: "review open",
  ManifestReviewCompleted: "reviewed",
  ServerApproved: "approved",
  ServerQuarantined: "quarantine",
  ServerRejected: "rejected",
  AgentRegistered: "agent",
  GrantActivated: "grant",
  GrantSuspended: "suspended",
  GrantRevoked: "revoked",
};

export function eventLabel(kind: string | null): string {
  return (kind && eventLabels[kind]) ?? kind ?? "event";
}

/** A subject's history, newest first, as a vertical audit trail. */
export function Timeline({ items }: { items: ActivityItem[] }) {
  if (items.length === 0) return <div className="px-3 py-6 text-center text-ink-faint">Nothing recorded yet.</div>;
  return (
    <ol className="relative px-3 py-2">
      {items.map((item) => (
        <li key={item.id} className="relative grid grid-cols-[1rem_minmax(0,1fr)] gap-2 pb-3 last:pb-0">
          <span className="relative flex justify-center pt-1.5">
            <Dot tone={severityTone[item.severity]} />
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-baseline gap-x-2">
              <span className="font-mono text-[11px] tracking-wide text-ink-faint uppercase">{eventLabel(item.eventKind)}</span>
              <time className="text-[11px] text-ink-faint" title={utc(item.occurredOn)}>
                {timeAgo(item.occurredOn)}
              </time>
            </div>
            <p className={cn("text-ink", item.severity === "critical" && "text-danger")}>{item.summary}</p>
            {item.serverId && item.agentId && (
              <p className="text-[12px] text-ink-faint">
                <Link to="/servers/$serverId" params={{ serverId: item.serverId }} className="hover:text-ink">
                  server
                </Link>
                {" · "}
                <Link to="/agents/$agentId" params={{ agentId: item.agentId }} className="hover:text-ink">
                  agent
                </Link>
              </p>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}
