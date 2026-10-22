import { useNavigate, useSearch } from "@tanstack/react-router";
import { Pause, Play } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useRecentActivity } from "@/api/queries";
import { followActivity, type StreamState } from "@/api/sse";
import type { ActivityItem } from "@/api/types";
import { ActivityRow } from "@/components/activity";
import { FilterBar, SegmentFilter } from "@/components/filters";
import { PageBody, PageHeader } from "@/components/page-header";
import { ErrorState, Message, SkeletonRows } from "@/components/query-state";
import { Section } from "@/components/section";
import { Button } from "@/components/ui/button";
import {
  appendBounded,
  deniedByAgent,
  groupRepeats,
  matchesFilter,
  mergeActivity,
  spikeThreshold,
  spikeWindowMs,
  type ActivityFilter,
} from "@/lib/activity";
import { count, plural } from "@/lib/format";
import { useNow } from "@/lib/now";
import { cn } from "@/lib/utils";

const filterLabels: Record<ActivityFilter, string> = { all: "All", denied: "Denied calls", governance: "Governance events" };

export function ActivityPage() {
  const { filter = "all" } = useSearch({ from: "/activity" });
  const navigate = useNavigate({ from: "/activity" });
  const snapshot = useRecentActivity();
  const [live, setLive] = useState<ActivityItem[]>([]);
  const [buffered, setBuffered] = useState<ActivityItem[]>([]);
  const [paused, setPaused] = useState(false);
  const [state, setState] = useState<StreamState>("connecting");
  const [announcement, setAnnouncement] = useState("");
  const pausedRef = useRef(paused);
  const now = useNow();

  useEffect(() => {
    const abort = new AbortController();
    void followActivity(
      (item) => {
        if (pausedRef.current) {
          setBuffered((current) => appendBounded(current, item));
          return;
        }
        setLive((current) => mergeActivity(current, [item]));
        if (item.kind === "event" && item.summary) setAnnouncement(item.summary);
      },
      setState,
      abort.signal,
    );
    return () => abort.abort();
  }, []);

  const togglePause = () => {
    const next = !paused;
    pausedRef.current = next;
    setPaused(next);
    if (!next && buffered.length) {
      setLive((current) => mergeActivity(current, buffered));
      setBuffered([]);
    }
  };

  // The snapshot is refetched on every visit; stream rows on top of it, de-duplicated by id.
  const items = useMemo(() => mergeActivity(live, snapshot.data ?? []), [live, snapshot.data]);
  const liveIds = useMemo(() => new Set(live.map((item) => item.id)), [live]);
  const visible = useMemo(() => groupRepeats(items.filter((item) => matchesFilter(item, filter))), [items, filter]);
  const spikes = useMemo(() => deniedByAgent(items, now), [items, now]);
  const counts = useMemo(
    () => ({
      calls: items.filter((item) => item.kind === "call").length,
      denied: items.filter((item) => item.kind === "call" && item.decision === "Denied").length,
      events: items.filter((item) => item.kind === "event").length,
    }),
    [items],
  );

  return (
    <>
      <PageHeader
        title="Live activity"
        meta="Every gateway decision and governance event, as it happens. Times are UTC."
        actions={<StreamIndicator state={state} paused={paused} />}
      />
      <PageBody className="xl:grid xl:grid-cols-[minmax(0,1fr)_20rem] xl:items-start">
        <Section
          title="Feed"
          actions={
            <Button variant="outline" size="sm" onClick={togglePause} aria-pressed={paused}>
              {paused ? <Play data-icon="inline-start" /> : <Pause data-icon="inline-start" />}
              {paused ? `Resume${buffered.length ? ` (${count(buffered.length)} new)` : ""}` : "Pause"}
            </Button>
          }
        >
          <FilterBar>
            <SegmentFilter
              label="Show"
              value={filter}
              options={(Object.keys(filterLabels) as ActivityFilter[]).map((value) => ({ value, label: filterLabels[value] }))}
              onChange={(next) => void navigate({ search: { filter: next === "all" ? undefined : next }, replace: true })}
            />
            <span className="text-sm text-muted-foreground tabular">{plural(visible.length, "row")}</span>
          </FilterBar>
          {snapshot.isPending && live.length === 0 ? (
            <SkeletonRows rows={8} />
          ) : snapshot.isError && live.length === 0 ? (
            <ErrorState error={snapshot.error} retry={() => void snapshot.refetch()} />
          ) : visible.length === 0 ? (
            <Message title="Waiting for activity">Tool calls and governance events appear here as they happen. Start the demo agent to see some.</Message>
          ) : (
            <ul aria-label="Activity" className="[contain:content]">
              {visible.map((group) => (
                <ActivityRow key={group.item.id} item={group.item} repeats={group.repeats} fresh={liveIds.has(group.item.id)} />
              ))}
            </ul>
          )}
        </Section>

        <div className="flex flex-col gap-6 xl:sticky xl:top-6">
          <Section title="Denials by agent" description={`Last ${spikeWindowMs / 1000} s. The gateway blocks an agent at ${spikeThreshold}.`}>
            {spikes.length === 0 ? (
              <p className="px-4 py-6 text-center text-sm text-muted-foreground">No denials in the last minute.</p>
            ) : (
              <ul className="flex flex-col gap-3 p-4">
                {spikes.map(([agent, denied]) => {
                  const spike = denied >= spikeThreshold;
                  return (
                    <li key={agent} className="flex flex-col gap-1.5">
                      <div className="flex items-baseline justify-between gap-2">
                        <span className={cn("truncate font-mono text-sm", spike && "text-danger")}>{agent}</span>
                        <span className={cn("text-sm tabular", spike ? "font-medium text-danger" : "text-muted-foreground")}>{denied}</span>
                      </div>
                      <div className="h-1.5 rounded-full bg-muted" role="meter" aria-label={`${agent} denials`} aria-valuemin={0} aria-valuemax={spikeThreshold} aria-valuenow={Math.min(denied, spikeThreshold)}>
                        <div className={cn("h-full rounded-full", spike ? "bg-danger" : "bg-warning")} style={{ width: `${Math.min(100, (100 * denied) / spikeThreshold)}%` }} />
                      </div>
                      {spike && <p className="text-xs text-danger">At the gateway's limit: likely blocked as a possible prompt injection.</p>}
                    </li>
                  );
                })}
              </ul>
            )}
          </Section>
          <Section title="In the feed">
            <dl className="grid grid-cols-[1fr_auto] gap-y-2 p-4 text-sm">
              <dt className="text-muted-foreground">Tool calls</dt>
              <dd className="text-right tabular">{count(counts.calls)}</dd>
              <dt className="text-muted-foreground">Denied</dt>
              <dd className="text-right tabular">
                {count(counts.denied)}
                {counts.calls > 0 && <span className="text-muted-foreground"> ({Math.round((100 * counts.denied) / counts.calls)}%)</span>}
              </dd>
              <dt className="text-muted-foreground">Governance events</dt>
              <dd className="text-right tabular">{count(counts.events)}</dd>
            </dl>
          </Section>
        </div>
      </PageBody>
      <p aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </>
  );
}

const streamLooks: Record<StreamState | "paused", { label: string; dot: string }> = {
  connecting: { label: "Connecting…", dot: "bg-warning" },
  live: { label: "Live", dot: "bg-success animate-pulse" },
  retrying: { label: "Reconnecting…", dot: "bg-warning" },
  unauthorized: { label: "Session expired", dot: "bg-danger" },
  paused: { label: "Paused", dot: "bg-muted-foreground" },
};

function StreamIndicator({ state, paused }: { state: StreamState; paused: boolean }) {
  const look = streamLooks[paused ? "paused" : state];
  return (
    <span className="inline-flex items-center gap-2 rounded-md border px-2.5 py-1 text-sm" role="status">
      <span aria-hidden="true" className={cn("size-2 rounded-full", look.dot)} />
      {look.label}
    </span>
  );
}
