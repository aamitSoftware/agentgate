import { Link, useParams } from "@tanstack/react-router";
import { ArrowRight, ChevronRight } from "lucide-react";
import { useServer } from "@/api/queries";
import { GrantTable } from "@/components/grants";
import { Hash } from "@/components/hash";
import { MetaDot, PageBody, PageHeader } from "@/components/page-header";
import { ErrorState, Message, SkeletonRows } from "@/components/query-state";
import { Section } from "@/components/section";
import { RiskBadge, ServerStatusBadge } from "@/components/status";
import { RecentCalls, TimelineSection } from "@/components/subject";
import { RelativeTime } from "@/components/time";
import { Button } from "@/components/ui/button";
import { humanize } from "@/lib/words";

export function ServerPage() {
  const { serverId } = useParams({ from: "/servers/$serverId" });
  const server = useServer(serverId);
  const crumbs = [{ label: "Servers", to: "/servers" as const }];

  if (server.isPending)
    return (
      <>
        <PageHeader crumbs={crumbs} title="Server" />
        <PageBody>
          <SkeletonRows rows={6} />
        </PageBody>
      </>
    );
  if (server.isError)
    return (
      <>
        <PageHeader crumbs={crumbs} title="Server" />
        <PageBody>
          <ErrorState error={server.error} retry={() => void server.refetch()} back={{ label: "Back to servers", to: "/servers" }} />
        </PageBody>
      </>
    );

  const { summary, pinnedTools, grants, timeline, recentCalls } = server.data;
  return (
    <>
      <PageHeader
        crumbs={crumbs}
        title={<span className="font-mono">{summary.name}</span>}
        badges={<ServerStatusBadge status={summary.status} />}
        meta={
          <>
            <span className="truncate font-mono">{summary.url ?? "URL recorded once approved"}</span>
            <MetaDot />
            <span className="inline-flex items-center gap-1.5">
              Pinned <Hash value={summary.pinnedHash} />
              {summary.pendingHash && (
                <>
                  <ArrowRight aria-label="pending" className="size-3.5" />
                  <Hash value={summary.pendingHash} copy={false} />
                </>
              )}
            </span>
            <MetaDot />
            <span>
              {humanize(summary.status)} <RelativeTime value={summary.statusAsOf} />
            </span>
          </>
        }
        actions={
          summary.openReviewId &&
          summary.openReviewRisk && (
            <Button variant="outline" asChild>
              <Link to="/approvals/reviews/$reviewId" params={{ reviewId: summary.openReviewId }}>
                Open review <RiskBadge risk={summary.openReviewRisk} />
                <ChevronRight data-icon="inline-end" />
              </Link>
            </Button>
          )
        }
      />
      <PageBody className="xl:grid xl:grid-cols-[minmax(0,1fr)_24rem] xl:items-start">
        <div className="flex min-w-0 flex-col gap-6">
          <Section title="Pinned tools" description="Exactly what agents are shown: the manifest approved by two reviewers.">
            {!pinnedTools?.length ? (
              <Message title="Nothing pinned yet">Agents see no tools from this server until a manifest is approved.</Message>
            ) : (
              <dl className="divide-y">
                {pinnedTools.map((tool) => (
                  <div key={tool.name} className="flex flex-col gap-1 px-4 py-3">
                    <dt className="font-mono text-sm font-medium" translate="no">
                      {tool.name}
                    </dt>
                    <dd className="text-sm text-muted-foreground">{tool.description ?? "No description."}</dd>
                  </div>
                ))}
              </dl>
            )}
          </Section>
          <Section title="Grants" description="Agents allowed to call this server's tools.">
            {grants.length === 0 ? (
              <Message title="No grants">No agent may call this server. Admins request grants from the Grants page.</Message>
            ) : (
              <GrantTable grants={grants} show="agent" />
            )}
          </Section>
          <RecentCalls calls={recentCalls} />
        </div>
        <div className="xl:sticky xl:top-6">
          <TimelineSection items={timeline} what="server" />
        </div>
      </PageBody>
    </>
  );
}
