import { useParams } from "@tanstack/react-router";
import { ArrowRight, ChevronDown, ChevronRight } from "lucide-react";
import { useMemo, useState } from "react";
import { useDecideReview, useReview } from "@/api/queries";
import type { AuditEntry, ReviewDetail, RiskFinding } from "@/api/types";
import { ActorName } from "@/components/actor";
import { InlineArrow } from "@/components/arrow";
import { DecisionPanel } from "@/components/decision-panel";
import { Hash } from "@/components/hash";
import { MetaDot, PageBody, PageHeader } from "@/components/page-header";
import { ErrorState, Message, SkeletonRows } from "@/components/query-state";
import { Section } from "@/components/section";
import { RiskBadge, ReviewStatusBadge } from "@/components/status";
import { RelativeTime } from "@/components/time";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { diffManifests, type ToolDiff } from "@/lib/diff";
import { plural } from "@/lib/format";
import { cn } from "@/lib/utils";
import { humanize } from "@/lib/words";

export function ReviewPage() {
  const { reviewId } = useParams({ from: "/approvals/reviews/$reviewId" });
  const review = useReview(reviewId);
  const crumbs = [{ label: "Approvals", to: "/approvals" as const }];

  if (review.isPending)
    return (
      <>
        <PageHeader crumbs={crumbs} title="Review" />
        <PageBody>
          <SkeletonRows rows={6} />
        </PageBody>
      </>
    );
  if (review.isError)
    return (
      <>
        <PageHeader crumbs={crumbs} title="Review" />
        <PageBody>
          <ErrorState error={review.error} retry={() => void review.refetch()} back={{ label: "Back to approvals", to: "/approvals" }} />
        </PageBody>
      </>
    );

  const { summary } = review.data;
  return (
    <>
      <PageHeader
        crumbs={crumbs}
        title={
          <>
            Review of <span className="font-mono">{summary.serverName}</span>
          </>
        }
        badges={
          <>
            <ReviewStatusBadge status={summary.status} />
            <RiskBadge risk={summary.riskLevel} />
          </>
        }
        meta={
          <>
            {summary.previousHash ? (
              <span className="inline-flex flex-wrap items-center gap-1.5">
                Pinned <Hash value={summary.previousHash} copy={false} />
                <ArrowRight aria-label="would be replaced by" className="size-3.5" />
                <Hash value={summary.manifestHash} />
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5">
                New server, manifest <Hash value={summary.manifestHash} />
              </span>
            )}
            <MetaDot />
            <span>
              Opened <RelativeTime value={summary.createdOn} />
            </span>
          </>
        }
      />
      <PageBody className="lg:grid lg:grid-cols-[minmax(0,1fr)_22rem] lg:items-start">
        <div className="flex min-w-0 flex-col gap-6">
          <p className="text-muted-foreground">{review.data.submissionReason}</p>
          <Findings findings={review.data.findings} />
          <ManifestDiff review={review.data} />
        </div>
        <div className="flex flex-col gap-6 lg:sticky lg:top-6">
          <Decision review={review.data} />
          <AuditTrail entries={review.data.auditEntries} />
        </div>
      </PageBody>
    </>
  );
}

const severityOrder = { High: 0, Medium: 1, Low: 2 } as const;

function Findings({ findings }: { findings: RiskFinding[] }) {
  const byTool = useMemo(() => {
    const groups = new Map<string, RiskFinding[]>();
    for (const finding of findings.toSorted((left, right) => severityOrder[left.severity] - severityOrder[right.severity]))
      groups.set(finding.toolName, [...(groups.get(finding.toolName) ?? []), finding]);
    return [...groups];
  }, [findings]);
  const tally = (["High", "Medium", "Low"] as const)
    .map((level) => [level, findings.filter((finding) => finding.severity === level).length] as const)
    .filter(([, total]) => total > 0)
    .map(([level, total]) => `${total} ${level.toLowerCase()}`)
    .join(", ");

  return (
    <Section title="Risk scan" description={findings.length ? `${plural(findings.length, "finding")}: ${tally}` : undefined}>
      {byTool.length === 0 ? (
        <Message title="No findings">The scan found nothing. Read the manifest diff anyway: a scan can miss what a person sees.</Message>
      ) : (
        <div className="divide-y">
          {byTool.map(([tool, toolFindings]) => (
            <div key={tool} className="flex flex-col gap-3 px-4 py-4">
              <h3 className="font-mono text-sm font-medium" translate="no">
                {tool}
              </h3>
              <ul className="flex flex-col gap-3">
                {toolFindings.map((finding) => (
                  <li key={`${finding.rule}:${finding.evidence}`} className="grid gap-2 sm:grid-cols-[11rem_minmax(0,1fr)]">
                    <div className="flex flex-col items-start gap-1">
                      <RiskBadge risk={finding.severity} />
                      <span className="font-mono text-xs text-muted-foreground">{finding.rule}</span>
                    </div>
                    <blockquote className="rounded-md border bg-muted px-3 py-2 font-mono text-sm break-words whitespace-pre-wrap">{finding.evidence}</blockquote>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </Section>
  );
}

const changeVariants = { added: "success", removed: "danger", changed: "warning", unchanged: "neutral" } as const;

/** What agents would be told after approval, against what they are told now, read like a code review. */
function ManifestDiff({ review }: { review: ReviewDetail }) {
  const diff = useMemo(() => {
    try {
      return { tools: diffManifests(review.previousToolsJson, review.toolsJson) };
    } catch (error) {
      return { error };
    }
  }, [review.previousToolsJson, review.toolsJson]);
  const [showUnchanged, setShowUnchanged] = useState(false);

  if ("error" in diff) return <Section title="Manifest diff"><ErrorState error={diff.error} /></Section>;
  const changed = diff.tools.filter((tool) => tool.change !== "unchanged");
  const hidden = diff.tools.length - changed.length;
  const visible = showUnchanged ? diff.tools : changed;

  return (
    <Section
      title="Manifest diff"
      description={
        review.previousToolsJson ? `${changed.length} of ${plural(diff.tools.length, "tool")} changed against the pinned manifest` : `${plural(diff.tools.length, "tool")}, all new`
      }
      actions={
        hidden > 0 && (
          <Button variant="ghost" size="sm" aria-expanded={showUnchanged} onClick={() => setShowUnchanged((value) => !value)}>
            {showUnchanged ? <ChevronDown data-icon="inline-start" /> : <ChevronRight data-icon="inline-start" />}
            {showUnchanged ? "Hide" : "Show"} {plural(hidden, "unchanged tool")}
          </Button>
        )
      }
    >
      <div className="flex flex-col gap-4 p-4">
        {visible.map((tool) => (
          <ToolDiffView key={tool.name} tool={tool} />
        ))}
      </div>
    </Section>
  );
}

function ToolDiffView({ tool }: { tool: ToolDiff }) {
  return (
    <div className="overflow-hidden rounded-md border">
      <div className="sticky top-14 z-10 flex items-center gap-2 border-b bg-muted px-3 py-2 md:top-0">
        <h3 className="font-mono text-sm font-medium" translate="no">
          {tool.name}
        </h3>
        <Badge variant={changeVariants[tool.change]}>{humanize(tool.change)}</Badge>
      </div>
      <pre className="overflow-x-auto py-1 font-mono text-sm leading-6" aria-label={`Changes to ${tool.name}`}>
        {tool.lines.map((line, index) => (
          <div
            key={index}
            className={cn(
              "grid grid-cols-[1.5rem_minmax(0,1fr)] pr-3 whitespace-pre-wrap",
              line.kind === "added" && "bg-diff-add",
              line.kind === "removed" && "bg-diff-del",
              line.kind === "same" && "text-muted-foreground",
            )}
          >
            <span className="text-center select-none" aria-hidden="true">
              {line.kind === "added" ? "+" : line.kind === "removed" ? "−" : ""}
            </span>
            <span>
              <span className="sr-only">{line.kind === "added" ? "Added: " : line.kind === "removed" ? "Removed: " : ""}</span>
              {line.text}
            </span>
          </div>
        ))}
      </pre>
    </div>
  );
}

function Decision({ review }: { review: ReviewDetail }) {
  const decide = useDecideReview(review.summary.id);
  const { status, riskLevel, firstApproverId, serverName } = review.summary;
  return (
    <Section title="Decision" padded>
      <DecisionPanel
        subject="the manifest"
        firstApproverId={firstApproverId}
        complete={status === "Approved"}
        undecided={status === "Open" || status === "PartiallyApproved"}
        closedText={`This review is ${humanize(status).toLowerCase()} and can no longer be decided.`}
        highRisk={riskLevel === "High"}
        finalApproveLabel="Approve and pin"
        finalNote="The final approval is checked live against Registry: it fails if the server now advertises a different manifest."
        rejectConsequence={
          review.summary.previousHash
            ? `${serverName} stays quarantined: agents keep getting refused until a new manifest is approved.`
            : `${serverName} is not approved: agents cannot use it.`
        }
        onDecide={(verb, comment) => decide.mutateAsync({ verb, comment })}
      />
    </Section>
  );
}

function AuditTrail({ entries }: { entries: AuditEntry[] }) {
  return (
    <Section title="Audit trail">
      <ol className="divide-y">
        {entries.map((entry, index) => (
          <li key={`${entry.occurredOn}-${index}`} className="flex flex-col gap-1 px-4 py-3">
            <div className="flex items-baseline justify-between gap-2">
              <span className="text-sm font-medium">{humanize(entry.action)}</span>
              <RelativeTime value={entry.occurredOn} className="text-xs text-muted-foreground" />
            </div>
            <span className="text-xs text-muted-foreground">
              {entry.fromStatus && (
                <>
                  {humanize(entry.fromStatus)}
                  <InlineArrow />
                </>
              )}
              {humanize(entry.toStatus)} · <ActorName actorId={entry.actorId} />
            </span>
            {entry.comment && <p className="text-sm">{entry.comment}</p>}
          </li>
        ))}
      </ol>
    </Section>
  );
}
