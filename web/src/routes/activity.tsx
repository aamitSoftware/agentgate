import { useEffect, useMemo, useRef, useState } from "react";
import { useRecentActivity } from "@/api/queries";
import { followActivity, type StreamState } from "@/api/sse";
import type { ActivityItem } from "@/api/types";
import { ActivityRow } from "@/components/activity";
import { PageHeader } from "@/components/shell";
import { Dot, Empty, Failure } from "@/components/signals";
import { Button } from "@/components/ui/button";
import { Panel } from "@/components/ui/panel";
import { cn } from "@/lib/cn";

const keep = 1_000;
const spikeWindowMs = 5 * 60_000;
/** Denials in the window that make an agent stand out (M5 turns this into an automatic suspension). */
const spikeThreshold = 5;

type Filter = "all" | "denied" | "governance";

/** Newest first, no duplicates (a reconnect can replay rows), at most `keep` rows. */
export function mergeActivity(current: ActivityItem[], incoming: ActivityItem[]): ActivityItem[] {
  const seen = new Set(current.map((item) => item.id));
  const fresh = incoming.filter((item) => !seen.has(item.id));
  if (fresh.length === 0) return current;
  return [...fresh, ...current].sort((left, right) => right.occurredOn.localeCompare(left.occurredOn)).slice(0, keep);
}

export function deniedByAgent(items: ActivityItem[], now: number): [string, number][] {
  const counts = new Map<string, number>();
  for (const item of items)
    if (item.kind === "call" && item.decision === "Denied" && item.agentClientId && now - new Date(item.occurredOn).getTime() <= spikeWindowMs)
      counts.set(item.agentClientId, (counts.get(item.agentClientId) ?? 0) + 1);
  return [...counts].sort((left, right) => right[1] - left[1]);
}

export function ActivityPage() {
  const recent = useRecentActivity();
  const [items, setItems] = useState<ActivityItem[]>([]);
  const [freshIds, setFreshIds] = useState<Set<string>>(new Set());
  const [state, setState] = useState<StreamState>("connecting");
  const [filter, setFilter] = useState<Filter>("all");
  const [paused, setPaused] = useState(false);
  const pausedRef = useRef(paused);
  const buffered = useRef<ActivityItem[]>([]);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (recent.data) setItems((current) => mergeActivity(current, recent.data));
  }, [recent.data]);

  useEffect(() => {
    pausedRef.current = paused;
    if (!paused && buffered.current.length) {
      const pending = buffered.current;
      buffered.current = [];
      setItems((current) => mergeActivity(current, pending));
    }
  }, [paused]);

  useEffect(() => {
    const abort = new AbortController();
    void followActivity(
      (item) => {
        if (pausedRef.current) {
          buffered.current.push(item);
          return;
        }
        setItems((current) => mergeActivity(current, [item]));
        setFreshIds((current) => new Set(current).add(item.id));
      },
      setState,
      abort.signal,
    );
    const tick = setInterval(() => setNow(Date.now()), 5_000);
    return () => (abort.abort(), clearInterval(tick));
  }, []);

  const visible = useMemo(
    () =>
      items.filter((item) =>
        filter === "all" ? true : filter === "denied" ? item.kind === "call" && item.decision === "Denied" : item.kind === "event",
      ),
    [items, filter],
  );
  const spikes = useMemo(() => deniedByAgent(items, now), [items, now]);
  const calls = items.filter((item) => item.kind === "call");
  const deniedShare = calls.length ? Math.round((100 * calls.filter((item) => item.decision === "Denied").length) / calls.length) : 0;

  return (
    <>
      <PageHeader title="Live activity" subtitle="Every gateway decision and governance event, as it happens.">
        <StreamIndicator state={state} paused={paused} />
      </PageHeader>
      <div className="grid grid-cols-[minmax(0,1fr)_17rem] gap-4 p-5 max-xl:grid-cols-1">
        <Panel
          title={`${visible.length} rows`}
          actions={
            <div className="flex items-center gap-1">
              {(["all", "denied", "governance"] as const).map((option) => (
                <Button key={option} variant="ghost" size="sm" className={cn(filter === option && "bg-raised text-ink")} onClick={() => setFilter(option)}>
                  {option}
                </Button>
              ))}
              <span className="mx-1 h-4 w-px bg-line" />
              <Button variant="ghost" size="sm" onClick={() => setPaused((value) => !value)}>
                {paused ? `Resume (${buffered.current.length})` : "Pause"}
              </Button>
            </div>
          }
        >
          {recent.error ? (
            <Failure error={recent.error} />
          ) : visible.length === 0 ? (
            <Empty>Waiting for activity. Run the demo agent to see calls arrive.</Empty>
          ) : (
            <div className="max-h-[calc(100vh-11rem)] overflow-y-auto">
              {visible.map((item) => (
                <ActivityRow key={item.id} item={item} fresh={freshIds.has(item.id)} />
              ))}
            </div>
          )}
        </Panel>

        <div className="space-y-4">
          <Panel title="Denials by agent · 5 min">
            {spikes.length === 0 ? (
              <Empty>No denials.</Empty>
            ) : (
              <ul className="space-y-2 p-3">
                {spikes.map(([agent, count]) => (
                  <li key={agent}>
                    <div className="flex items-baseline justify-between">
                      <span className={cn("font-mono text-[12px]", count >= spikeThreshold && "text-danger")}>{agent}</span>
                      <span className={cn("font-mono tabular", count >= spikeThreshold ? "text-danger" : "text-ink-muted")}>{count}</span>
                    </div>
                    <div className="mt-1 h-1 rounded-full bg-raised">
                      <div
                        className={cn("h-1 rounded-full", count >= spikeThreshold ? "bg-danger" : "bg-warn")}
                        style={{ width: `${Math.min(100, (100 * count) / (spikeThreshold * 2))}%` }}
                      />
                    </div>
                    {count >= spikeThreshold && <p className="mt-1 text-[11px] text-danger">Spike: possible prompt injection.</p>}
                  </li>
                ))}
              </ul>
            )}
          </Panel>
          <Panel title="In this view">
            <dl className="grid grid-cols-2 gap-y-1.5 p-3 text-[12px]">
              <dt className="text-ink-faint">Tool calls</dt>
              <dd className="text-right font-mono tabular">{calls.length}</dd>
              <dt className="text-ink-faint">Denied</dt>
              <dd className="text-right font-mono tabular">{deniedShare}%</dd>
              <dt className="text-ink-faint">Governance events</dt>
              <dd className="text-right font-mono tabular">{items.length - calls.length}</dd>
            </dl>
          </Panel>
        </div>
      </div>
    </>
  );
}

function StreamIndicator({ state, paused }: { state: StreamState; paused: boolean }) {
  const label = paused ? "paused" : state;
  const tone = paused ? "warn" : state === "live" ? "ok" : "warn";
  return (
    <span className="flex items-center gap-2 font-mono text-[12px] text-ink-muted">
      <Dot tone={tone} pulse={state === "live" && !paused} />
      {label}
    </span>
  );
}
