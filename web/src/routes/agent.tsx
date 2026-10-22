import { useParams } from "@tanstack/react-router";
import { useAgent } from "@/api/queries";
import { GrantTable } from "@/components/grants";
import { MetaDot, PageBody, PageHeader } from "@/components/page-header";
import { ErrorState, Message, SkeletonRows } from "@/components/query-state";
import { Section } from "@/components/section";
import { RecentCalls, TimelineSection } from "@/components/subject";
import { RelativeTime } from "@/components/time";
import { spikeThreshold } from "@/lib/activity";
import { plural } from "@/lib/format";
import { cn } from "@/lib/utils";

export function AgentPage() {
  const { agentId } = useParams({ from: "/agents/$agentId" });
  const agent = useAgent(agentId);
  const crumbs = [{ label: "Agents", to: "/agents" as const }];

  if (agent.isPending)
    return (
      <>
        <PageHeader crumbs={crumbs} title="Agent" />
        <PageBody>
          <SkeletonRows rows={6} />
        </PageBody>
      </>
    );
  if (agent.isError)
    return (
      <>
        <PageHeader crumbs={crumbs} title="Agent" />
        <PageBody>
          <ErrorState error={agent.error} retry={() => void agent.refetch()} back={{ label: "Back to agents", to: "/agents" }} />
        </PageBody>
      </>
    );

  const { summary, grants, timeline, recentCalls } = agent.data;
  const denied = summary.deniedLastHour;
  return (
    <>
      <PageHeader
        crumbs={crumbs}
        title={summary.name}
        badges={
          <span className="rounded-sm border px-1.5 font-mono text-sm text-muted-foreground" translate="no">
            {summary.clientId}
          </span>
        }
        meta={
          <>
            <span>{summary.purpose}</span>
            <MetaDot />
            <span>Owned by {summary.owner}</span>
            <MetaDot />
            <span>
              Registered <RelativeTime value={summary.registeredOn} />
            </span>
            <MetaDot />
            <span className={cn(denied >= spikeThreshold ? "font-medium text-danger" : denied && "text-warning")}>
              {plural(denied, "denied call")} in the last hour
            </span>
          </>
        }
      />
      <PageBody className="xl:grid xl:grid-cols-[minmax(0,1fr)_24rem] xl:items-start">
        <div className="flex min-w-0 flex-col gap-6">
          <Section title="Grants" description="What this agent may call. Everything else is denied at the gateway.">
            {grants.length === 0 ? (
              <Message title="No grants">This agent can call nothing yet. Admins request grants from the Grants page.</Message>
            ) : (
              <GrantTable grants={grants} show="server" />
            )}
          </Section>
          <RecentCalls calls={recentCalls} />
        </div>
        <div className="xl:sticky xl:top-6">
          <TimelineSection items={timeline} what="agent" />
        </div>
      </PageBody>
    </>
  );
}
