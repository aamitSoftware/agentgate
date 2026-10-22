import { Link } from "@tanstack/react-router";
import { ChevronRight } from "lucide-react";
import { useId, useState } from "react";
import type { ActivityItem, Severity } from "@/api/types";
import { ActorName } from "@/components/actor";
import { DecisionBadge, EventBadge, eventLabel, severityTone, type Tone } from "@/components/status";
import { RelativeTime } from "@/components/time";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { clock, timeAgo } from "@/lib/format";
import { useNow } from "@/lib/now";
import { cn } from "@/lib/utils";

function Target({ item }: { item: ActivityItem }) {
  return (
    <span className="max-w-full shrink-0 truncate" translate="no">
      <span className="font-mono text-sm">{item.agentClientId}</span>
      <span className="text-muted-foreground"> → </span>
      <span className="font-mono text-sm">
        {item.serverName}
        {item.toolName ? <span className="text-muted-foreground">/{item.toolName}</span> : <span className="text-muted-foreground"> {item.method}</span>}
      </span>
    </span>
  );
}

/**
 * One row of a feed: a gateway decision or a governance event. The reason and links are always one click
 * away (never cut off without a way to read them), and runs of identical calls show once with a count.
 */
export function ActivityRow({ item, repeats = 1, fresh }: { item: ActivityItem; repeats?: number; fresh?: boolean }) {
  const [open, setOpen] = useState(false);
  const detailsId = useId();
  const now = useNow();
  const isCall = item.kind === "call";
  const hasDetails = isCall ? !!(item.reason || item.traceId) : !!(item.summary || item.actorId || item.serverId || item.agentId);

  return (
    <li className={cn("border-b last:border-b-0", item.severity === "critical" && !isCall && "bg-danger-subtle/60", fresh && "animate-arrive")}>
      <div className="grid grid-cols-[4.75rem_minmax(0,auto)_minmax(0,1fr)_auto] items-center gap-3 px-4 py-2">
        <Tooltip>
          <TooltipTrigger asChild>
            <time dateTime={item.occurredOn} className="font-mono text-xs text-muted-foreground tabular">
              {clock(item.occurredOn)}
            </time>
          </TooltipTrigger>
          <TooltipContent>{timeAgo(item.occurredOn, now)}</TooltipContent>
        </Tooltip>
        {isCall && item.decision ? <DecisionBadge decision={item.decision} /> : <EventBadge kind={item.eventKind} severity={item.severity} />}
        <span className="flex min-w-0 items-center gap-2">
          {isCall ? <Target item={item} /> : <span className="min-w-0 truncate">{item.summary}</span>}
          {isCall && item.reason && !open && <span className="hidden min-w-0 truncate text-sm text-muted-foreground lg:inline">{item.reason}</span>}
        </span>
        <span className="flex items-center gap-1">
          {repeats > 1 && (
            <span className="rounded-sm bg-muted px-1.5 text-xs font-medium text-muted-foreground tabular" aria-label={`${repeats} identical calls`}>
              ×{repeats}
            </span>
          )}
          {hasDetails && (
            <Button
              variant="ghost"
              size="icon-xs"
              aria-expanded={open}
              aria-controls={detailsId}
              aria-label={open ? "Hide details" : "Show details"}
              onClick={() => setOpen((value) => !value)}
            >
              <ChevronRight className={cn("transition-transform", open && "rotate-90")} />
            </Button>
          )}
        </span>
      </div>
      {open && (
        <dl id={detailsId} className="grid grid-cols-[6rem_minmax(0,1fr)] gap-x-3 gap-y-1 px-4 pb-3 pl-[calc(4.75rem+1.75rem)] text-sm">
          {isCall && item.reason && (
            <>
              <dt className="text-muted-foreground">Reason</dt>
              <dd className="break-words">{item.reason}</dd>
            </>
          )}
          {!isCall && item.summary && (
            <>
              <dt className="text-muted-foreground">Summary</dt>
              <dd className="break-words">{item.summary}</dd>
            </>
          )}
          {item.actorId && (
            <>
              <dt className="text-muted-foreground">By</dt>
              <dd>
                <ActorName actorId={item.actorId} />
              </dd>
            </>
          )}
          {item.traceId && (
            <>
              <dt className="text-muted-foreground">Trace</dt>
              <dd className="font-mono text-xs break-all">{item.traceId}</dd>
            </>
          )}
          <SubjectLinks item={item} />
        </dl>
      )}
    </li>
  );
}

function SubjectLinks({ item }: { item: ActivityItem }) {
  if (!item.serverId && !item.agentId) return null;
  return (
    <>
      <dt className="text-muted-foreground">Open</dt>
      <dd className="flex gap-3">
        {item.serverId && (
          <Link to="/servers/$serverId" params={{ serverId: item.serverId }} className="text-primary hover:underline">
            Server{item.serverName ? ` ${item.serverName}` : ""}
          </Link>
        )}
        {item.agentId && (
          <Link to="/agents/$agentId" params={{ agentId: item.agentId }} className="text-primary hover:underline">
            Agent{item.agentClientId ? ` ${item.agentClientId}` : ""}
          </Link>
        )}
      </dd>
    </>
  );
}

const dotTones: Record<Tone, string> = {
  success: "bg-success",
  warning: "bg-warning",
  danger: "bg-danger",
  info: "bg-info",
  neutral: "bg-muted-foreground/60",
};

const labelTones: Record<Severity, string> = { info: "text-muted-foreground", warning: "text-warning", critical: "text-danger" };

/** A subject's history, newest first: what happened, when, and who or what did it. */
export function Timeline({ items }: { items: ActivityItem[] }) {
  return (
    <ol className="flex flex-col px-4 py-3">
      {items.map((item, index) => (
        <li key={item.id} className="relative grid grid-cols-[0.75rem_minmax(0,1fr)] gap-3 pb-4 last:pb-0">
          {index < items.length - 1 && <span aria-hidden="true" className="absolute top-3 bottom-0 left-[5px] w-px bg-border" />}
          <span aria-hidden="true" className={cn("relative mt-1.5 size-2.5 rounded-full ring-4 ring-card", dotTones[severityTone(item.severity)])} />
          <div className="flex min-w-0 flex-col gap-0.5">
            <div className="flex flex-wrap items-baseline gap-x-2 text-xs">
              <span className={cn("font-medium", labelTones[item.severity])}>{eventLabel(item.eventKind)}</span>
              <RelativeTime value={item.occurredOn} className="text-muted-foreground" />
            </div>
            <p className="text-sm break-words">{item.summary}</p>
            {item.actorId && (
              <span className="text-xs text-muted-foreground">
                <ActorName actorId={item.actorId} />
              </span>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}
