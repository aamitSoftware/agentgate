import { Link, type LinkProps } from "@tanstack/react-router";
import { ChevronRight, CircleCheck, ClipboardCheck, OctagonAlert, PauseCircle, ShieldAlert, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { useAgents, useGrantQueue, useGrants, useRecentActivity, useReviewQueue, useServers } from "@/api/queries";
import { Timeline } from "@/components/activity";
import { PageBody, PageHeader } from "@/components/page-header";
import { Message, QueryState, SkeletonRows } from "@/components/query-state";
import { Seals } from "@/components/seals";
import { Section } from "@/components/section";
import { RiskBadge } from "@/components/status";
import { RelativeTime } from "@/components/time";
import { Skeleton } from "@/components/ui/skeleton";
import { spikeThreshold } from "@/lib/activity";
import { count, plural } from "@/lib/format";
import { cn } from "@/lib/utils";

export function OverviewPage() {
  return (
    <>
      <PageHeader title="Overview" meta="What needs a person now, then the state of every server, agent and grant." />
      <PageBody>
        <Posture />
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_24rem]">
          <Attention />
          <RecentEvents />
        </div>
      </PageBody>
    </>
  );
}

interface AttentionItem {
  key: string;
  icon: LucideIcon;
  tone: "danger" | "warning" | "info";
  title: ReactNode;
  detail: ReactNode;
  link: LinkProps;
}

function Attention() {
  const reviews = useReviewQueue();
  const grantQueue = useGrantQueue();
  const grants = useGrants();
  const servers = useServers();
  const agents = useAgents();
  const sources = [reviews, grantQueue, grants, servers, agents];

  if (sources.some((source) => source.isPending))
    return (
      <Section title="Needs attention">
        <SkeletonRows rows={3} />
      </Section>
    );

  const items: AttentionItem[] = [];
  const riskOrder = { High: 0, Medium: 1, Low: 2 } as const;
  for (const review of [...(reviews.data ?? [])].sort((left, right) => riskOrder[left.riskLevel] - riskOrder[right.riskLevel]))
    items.push({
      key: `review-${review.id}`,
      icon: review.riskLevel === "High" ? OctagonAlert : ClipboardCheck,
      tone: review.riskLevel === "High" ? "danger" : "warning",
      title: (
        <>
          {review.previousHash ? "Re-review" : "Review"} <span className="font-mono">{review.serverName}</span>'s manifest
        </>
      ),
      detail: (
        <span className="flex items-center gap-2">
          <RiskBadge risk={review.riskLevel} />
          <Seals firstApproverId={review.firstApproverId} complete={false} />
          <span>
            {review.previousHash ? "after drift" : "new server"} · opened <RelativeTime value={review.createdOn} />
          </span>
        </span>
      ),
      link: { to: "/approvals/reviews/$reviewId", params: { reviewId: review.id } },
    });
  for (const grant of grantQueue.data ?? [])
    items.push({
      key: `request-${grant.id}`,
      icon: ClipboardCheck,
      tone: "info",
      title: (
        <>
          Grant request: <span className="font-mono">{grant.agentClientId}</span> → <span className="font-mono">{grant.serverName}</span>
        </>
      ),
      detail: (
        <span className="flex items-center gap-2">
          <Seals firstApproverId={grant.firstApproverId} complete={false} />
          {plural(grant.tools.length, "tool")} · requested <RelativeTime value={grant.createdOn} />
        </span>
      ),
      link: { to: "/approvals", search: { tab: "grants" } },
    });
  for (const server of servers.data ?? [])
    if (server.status === "Quarantined" && !server.openReviewId)
      items.push({
        key: `quarantine-${server.id}`,
        icon: ShieldAlert,
        tone: "danger",
        title: (
          <>
            <span className="font-mono">{server.name}</span> is quarantined
          </>
        ),
        detail: "Its calls are refused until a re-review approves a manifest.",
        link: { to: "/servers/$serverId", params: { serverId: server.id } },
      });
  for (const grant of grants.data ?? [])
    if (grant.status === "Suspended")
      items.push({
        key: `suspended-${grant.id}`,
        icon: PauseCircle,
        tone: "danger",
        title: (
          <>
            <span className="font-mono">{grant.agentClientId}</span>'s grant on <span className="font-mono">{grant.serverName}</span> is suspended
          </>
        ),
        detail: "Investigate, then resume or revoke it.",
        link: { to: "/agents/$agentId", params: { agentId: grant.agentId } },
      });
  for (const agent of agents.data ?? [])
    if (agent.deniedLastHour >= spikeThreshold)
      items.push({
        key: `denials-${agent.id}`,
        icon: OctagonAlert,
        tone: "warning",
        title: (
          <>
            <span className="font-mono">{agent.clientId}</span> was denied {plural(agent.deniedLastHour, "call")} in the last hour
          </>
        ),
        detail: "Repeated denials can mean a prompt-injected agent probing for tools.",
        link: { to: "/agents/$agentId", params: { agentId: agent.id } },
      });

  const failed = sources.filter((source) => source.isError).length;
  return (
    <Section
      title="Needs attention"
      description={failed ? `${plural(failed, "source")} could not be loaded; this list may be incomplete.` : undefined}
    >
      {items.length === 0 && !failed ? (
        <Message icon={<CircleCheck />} title="All clear">
          No reviews or grant requests waiting, nothing quarantined or suspended, and no agent is being denied repeatedly.
        </Message>
      ) : (
        <ul className="divide-y">
          {items.map((item) => (
            <li key={item.key}>
              <Link {...item.link} className="group flex items-center gap-3 px-4 py-3 transition-colors hover:bg-muted/60">
                <span
                  className={cn(
                    "grid size-8 shrink-0 place-items-center rounded-md",
                    item.tone === "danger" && "bg-danger-subtle text-danger",
                    item.tone === "warning" && "bg-warning-subtle text-warning",
                    item.tone === "info" && "bg-info-subtle text-info",
                  )}
                >
                  <item.icon aria-hidden="true" className="size-4" />
                </span>
                <span className="flex min-w-0 flex-col gap-1">
                  <span className="truncate font-medium">{item.title}</span>
                  <span className="text-sm text-muted-foreground">{item.detail}</span>
                </span>
                <ChevronRight aria-hidden="true" className="ml-auto size-4 shrink-0 text-muted-foreground group-hover:text-foreground" />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Section>
  );
}

function Stat({ label, value, detail, to }: { label: string; value: number | undefined; detail?: ReactNode; to: LinkProps["to"] }) {
  return (
    <Link to={to} className="flex min-w-0 flex-col gap-1 rounded-lg border bg-card px-4 py-3 transition-colors hover:border-input hover:bg-muted/40">
      <span className="text-sm text-muted-foreground">{label}</span>
      {value === undefined ? (
        <Skeleton className="h-7 w-12" />
      ) : (
        <span className="text-2xl font-semibold tracking-tight tabular">{count(value)}</span>
      )}
      <span className="truncate text-xs text-muted-foreground">{detail ?? <>&nbsp;</>}</span>
    </Link>
  );
}

function Posture() {
  const servers = useServers();
  const agents = useAgents();
  const grants = useGrants();
  const byStatus = (status: string) => servers.data?.filter((server) => server.status === status).length ?? 0;
  const active = grants.data?.filter((grant) => grant.status === "Active").length;
  const denied = agents.data?.reduce((total, agent) => total + agent.deniedLastHour, 0);

  return (
    <section aria-label="Posture" className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      <Stat
        label="Servers"
        value={servers.data?.length}
        to="/servers"
        detail={
          servers.data &&
          [
            `${byStatus("Approved")} approved`,
            `${byStatus("UnderReview")} in review`,
            `${byStatus("Quarantined")} quarantined`,
            byStatus("Rejected") ? `${byStatus("Rejected")} rejected` : null,
          ]
            .filter(Boolean)
            .join(" · ")
        }
      />
      <Stat label="Agents" value={agents.data?.length} to="/agents" detail="Registered nonhuman identities" />
      <Stat label="Active grants" value={active} to="/grants" detail="Agent × server × tools in force" />
      <Stat label="Denied calls, last hour" value={denied} to="/activity" detail="Across all agents" />
    </section>
  );
}

function RecentEvents() {
  const activity = useRecentActivity();
  return (
    <Section
      title="Recent governance events"
      actions={
        <Link to="/activity" className="text-sm text-primary hover:underline">
          Live activity
        </Link>
      }
    >
      <QueryState
        query={activity}
        isEmpty={(items) => !items.some((item) => item.kind === "event")}
        empty={<Message title="Nothing yet">Registrations, reviews, quarantines and grant changes appear here.</Message>}
      >
        {(items) => <Timeline items={items.filter((item) => item.kind === "event").slice(0, 8)} />}
      </QueryState>
    </Section>
  );
}
