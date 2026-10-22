import { Link, useParams } from "@tanstack/react-router";
import { ChevronRight } from "lucide-react";
import { useServer } from "@/api/queries";
import { ActivityRow, Timeline } from "@/components/activity";
import { GrantTable } from "@/components/grants";
import { PageHeader } from "@/components/shell";
import { Empty, Failure, Mono, RiskBadge, ServerStatusBadge } from "@/components/signals";
import { Panel } from "@/components/legacy-ui/panel";
import { timeAgo } from "@/lib/format";

export function ServerPage() {
  const { serverId } = useParams({ from: "/servers/$serverId" });
  const server = useServer(serverId);
  if (server.error) return <Failure error={server.error} />;
  if (!server.data) return null;
  const { summary, pinnedTools, grants, timeline, recentCalls } = server.data;

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            <Link to="/inventory" className="text-ink-muted hover:text-ink">
              Inventory
            </Link>
            <ChevronRight size={14} className="text-ink-faint" />
            <span className="font-mono">{summary.name}</span>
            <ServerStatusBadge status={summary.status} />
          </span>
        }
        subtitle={
          <span>
            {summary.url ?? "no URL yet"} · pinned <Mono value={summary.pinnedHash} /> · {summary.status.toLowerCase()} {timeAgo(summary.statusAsOf)}
          </span>
        }
      >
        {summary.openReviewId && (
          <Link
            to="/approvals/reviews/$reviewId"
            params={{ reviewId: summary.openReviewId }}
            className="flex items-center gap-2 rounded border border-warn/40 bg-warn-soft px-3 py-1.5 text-warn hover:border-warn"
          >
            Open review <RiskBadge risk={summary.openReviewRisk} /> <ChevronRight size={14} />
          </Link>
        )}
      </PageHeader>
      <div className="grid grid-cols-[minmax(0,1fr)_24rem] gap-4 p-5 max-xl:grid-cols-1">
        <div className="min-w-0 space-y-4">
          <Panel title={`Pinned tools · what agents are shown`}>
            {!pinnedTools?.length ? (
              <Empty>Nothing pinned: no manifest of this server has been approved yet.</Empty>
            ) : (
              <ul>
                {pinnedTools.map((tool) => (
                  <li key={tool.name} className="border-b border-line/70 px-3 py-2 last:border-b-0">
                    <div className="font-mono text-[12px] font-medium">{tool.name}</div>
                    <p className="text-ink-muted">{tool.description ?? <span className="text-ink-faint">no description</span>}</p>
                  </li>
                ))}
              </ul>
            )}
          </Panel>
          <Panel title="Grants">
            <GrantTable grants={grants} show="agent" />
          </Panel>
          <Panel title="Recent calls">
            {recentCalls.length === 0 ? <Empty>No calls observed.</Empty> : recentCalls.map((item) => <ActivityRow key={item.id} item={item} />)}
          </Panel>
        </div>
        <Panel title="Timeline">
          <Timeline items={timeline} />
        </Panel>
      </div>
    </>
  );
}
