import { Link, useParams } from "@tanstack/react-router";
import { ChevronRight } from "lucide-react";
import { useAgent } from "@/api/queries";
import { ActivityRow, Timeline } from "@/components/activity";
import { GrantTable } from "@/components/grants";
import { PageHeader } from "@/components/shell";
import { Empty, Failure, Stat } from "@/components/signals";
import { Panel } from "@/components/ui/panel";
import { timeAgo } from "@/lib/format";

export function AgentPage() {
  const { agentId } = useParams({ from: "/agents/$agentId" });
  const agent = useAgent(agentId);
  if (agent.error) return <Failure error={agent.error} />;
  if (!agent.data) return null;
  const { summary, grants, timeline, recentCalls } = agent.data;
  const denied = recentCalls.filter((call) => call.decision === "Denied").length;

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            <Link to="/inventory" className="text-ink-muted hover:text-ink">
              Inventory
            </Link>
            <ChevronRight size={14} className="text-ink-faint" />
            {summary.name}
            <span className="font-mono text-[13px] font-normal text-ink-muted">{summary.clientId}</span>
          </span>
        }
        subtitle={`${summary.purpose} · owned by ${summary.owner} · registered ${timeAgo(summary.registeredOn)}`}
      />
      <div className="space-y-4 p-5">
        <div className="grid grid-cols-3 rounded-md border border-line bg-surface">
          <Stat label="Active grants" value={summary.activeGrantCount} />
          <Stat label="Denied, 1 h" value={summary.deniedLastHour} tone={summary.deniedLastHour ? "warn" : undefined} />
          <Stat label="Recent calls" value={recentCalls.length} hint={`${denied} denied`} />
        </div>
        <div className="grid grid-cols-[minmax(0,1fr)_24rem] gap-4 max-xl:grid-cols-1">
          <div className="min-w-0 space-y-4">
            <Panel title="Grants">
              <GrantTable grants={grants} show="server" />
            </Panel>
            <Panel title="Recent calls">
              {recentCalls.length === 0 ? <Empty>No calls observed.</Empty> : recentCalls.map((item) => <ActivityRow key={item.id} item={item} />)}
            </Panel>
          </div>
          <Panel title="Timeline">
            <Timeline items={timeline} />
          </Panel>
        </div>
      </div>
    </>
  );
}
