import { Link, useParams } from "@tanstack/react-router";
import { ChevronRight } from "lucide-react";
import { useMemo, useState } from "react";
import { useDecideReview, useReview } from "@/api/queries";
import type { AuditEntry, ReviewDetail, RiskFinding } from "@/api/types";
import { useAuth } from "@/auth/auth";
import { PageHeader } from "@/components/shell";
import { Failure, Mono, ReviewStatusBadge, RiskBadge } from "@/components/signals";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/field";
import { Panel } from "@/components/ui/panel";
import { cn } from "@/lib/cn";
import { diffManifests, type ToolDiff } from "@/lib/diff";
import { shortId, timeAgo, utc } from "@/lib/format";

export function ReviewPage() {
  const { reviewId } = useParams({ from: "/approvals/reviews/$reviewId" });
  const review = useReview(reviewId);

  if (review.error) return <Failure error={review.error} />;
  if (!review.data) return null;
  const { summary } = review.data;

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            <Link to="/approvals" className="text-ink-muted hover:text-ink">
              Approvals
            </Link>
            <ChevronRight size={14} className="text-ink-faint" />
            <span className="font-mono">{summary.serverName}</span>
            <ReviewStatusBadge status={summary.status} />
            <RiskBadge risk={summary.riskLevel} />
          </span>
        }
        subtitle={
          <span>
            Manifest <Mono value={summary.manifestHash} />
            {summary.previousHash ? (
              <>
                {" "}would replace pinned <Mono value={summary.previousHash} />
              </>
            ) : (
              " for a new server"
            )}{" "}
            · opened {timeAgo(summary.createdOn)} · {review.data.submissionReason}
          </span>
        }
      />
      <div className="grid grid-cols-[minmax(0,1fr)_21rem] gap-4 p-5 max-xl:grid-cols-1">
        <div className="min-w-0 space-y-4">
          <Findings findings={review.data.findings} />
          <ManifestDiff review={review.data} />
        </div>
        <div className="space-y-4">
          <Decision review={review.data} />
          <AuditTrail entries={review.data.auditEntries} />
        </div>
      </div>
    </>
  );
}

const severityOrder = { High: 0, Medium: 1, Low: 2 } as const;

function Findings({ findings }: { findings: RiskFinding[] }) {
  const sorted = [...findings].sort((left, right) => severityOrder[left.severity] - severityOrder[right.severity]);
  return (
    <Panel title={`Risk scan · ${findings.length} finding${findings.length === 1 ? "" : "s"}`}>
      {sorted.length === 0 ? (
        <div className="px-3 py-4 text-ink-muted">The heuristic scan found nothing. Read the manifest anyway.</div>
      ) : (
        <ul>
          {sorted.map((finding, index) => (
            <li key={index} className="grid grid-cols-[6.5rem_11rem_minmax(0,1fr)] items-start gap-3 border-b border-line/70 px-3 py-2 last:border-b-0 max-lg:grid-cols-1">
              <span className="pt-1">
                <RiskBadge risk={finding.severity} />
              </span>
              <div className="min-w-0">
                <div className="truncate font-mono text-[12px] text-ink">{finding.toolName}</div>
                <div className="font-mono text-[11px] text-ink-faint">{finding.rule}</div>
              </div>
              <code className="min-w-0 rounded-sm bg-canvas px-2 py-1 font-mono text-[12px] break-words whitespace-pre-wrap text-ink-muted">
                {finding.evidence}
              </code>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

const changeTone = { added: "ok", removed: "danger", changed: "warn", unchanged: "neutral" } as const;

/** What agents would be told after approval, against what they are told now — read like a code review. */
function ManifestDiff({ review }: { review: ReviewDetail }) {
  const tools = useMemo(() => diffManifests(review.previousToolsJson, review.toolsJson), [review.previousToolsJson, review.toolsJson]);
  const [showUnchanged, setShowUnchanged] = useState(false);
  const changed = tools.filter((tool) => tool.change !== "unchanged");
  const visible = showUnchanged ? tools : changed;

  return (
    <Panel
      title={`Manifest diff · ${changed.length} of ${tools.length} tools ${review.previousToolsJson ? "changed" : "new"}`}
      actions={
        tools.length > changed.length && (
          <Button variant="ghost" size="sm" onClick={() => setShowUnchanged((value) => !value)}>
            {showUnchanged ? "Hide" : "Show"} unchanged
          </Button>
        )
      }
    >
      <div className="space-y-3 p-3">
        {visible.map((tool) => (
          <ToolDiffView key={tool.name} tool={tool} />
        ))}
      </div>
    </Panel>
  );
}

function ToolDiffView({ tool }: { tool: ToolDiff }) {
  return (
    <div className="overflow-hidden rounded border border-line">
      <div className="flex items-center gap-2 border-b border-line bg-raised px-3 py-1.5">
        <span className="font-mono text-[12px] font-medium">{tool.name}</span>
        <Badge tone={changeTone[tool.change]}>{tool.change}</Badge>
      </div>
      <pre className="overflow-x-auto bg-canvas py-1 font-mono text-[12px] leading-5">
        {tool.lines.map((line, index) => (
          <div
            key={index}
            className={cn(
              "px-3 whitespace-pre-wrap",
              line.kind === "added" && "bg-diff-add text-diff-add-ink",
              line.kind === "removed" && "bg-diff-del text-diff-del-ink",
              line.kind === "same" && "text-ink-faint",
            )}
          >
            <span className="mr-3 inline-block w-2 select-none">{line.kind === "added" ? "+" : line.kind === "removed" ? "−" : " "}</span>
            {line.text}
          </div>
        ))}
      </pre>
    </div>
  );
}

function Decision({ review }: { review: ReviewDetail }) {
  const { identity, canReview } = useAuth();
  const decide = useDecideReview(review.summary.id);
  const [comment, setComment] = useState("");
  const { status, riskLevel, firstApproverId } = review.summary;
  const undecided = status === "Open" || status === "PartiallyApproved";
  const gaveFirstApproval = firstApproverId === identity.sub;
  const needsReason = riskLevel === "High";

  return (
    <Panel title="Decision">
      <div className="space-y-3 p-3">
        <ol className="space-y-1.5 text-[12px]">
          <Step done={!!firstApproverId} label="First approval" who={firstApproverId && (gaveFirstApproval ? "you" : shortId(firstApproverId))} />
          <Step done={status === "Approved"} label="Second approval, by a different reviewer" />
        </ol>
        {!undecided ? (
          <p className="text-ink-muted">This review is {status.toLowerCase()} and can no longer be decided.</p>
        ) : !canReview ? (
          <p className="text-ink-muted">Only security reviewers can decide.</p>
        ) : (
          <>
            {needsReason && (
              <p className="rounded border border-danger/40 bg-danger-soft px-2.5 py-1.5 text-[12px] text-danger">
                Scanned as high risk: each approval must explain why it is acceptable.
              </p>
            )}
            <Textarea
              placeholder={needsReason ? "Why is this acceptable? (required)" : "Comment (required to reject)"}
              value={comment}
              onChange={(event) => setComment(event.target.value)}
            />
            <div className="flex gap-2">
              <Button
                variant="approve"
                disabled={decide.isPending || gaveFirstApproval || (needsReason && !comment.trim())}
                title={gaveFirstApproval ? "Four-eyes: the second approval must come from a different reviewer." : undefined}
                onClick={() => decide.mutate({ verb: "approve", comment })}
              >
                {status === "PartiallyApproved" ? "Approve & pin" : "Approve"}
              </Button>
              <Button variant="danger" disabled={decide.isPending || !comment.trim()} onClick={() => decide.mutate({ verb: "reject", comment })}>
                Reject
              </Button>
            </div>
            {gaveFirstApproval && <p className="text-[12px] text-ink-faint">You gave the first approval; another reviewer must give the second.</p>}
            {status === "PartiallyApproved" && !gaveFirstApproval && (
              <p className="text-[12px] text-ink-faint">The final approval is checked live against Registry: it fails if the server advertises something newer.</p>
            )}
          </>
        )}
        {decide.error && <p className="rounded border border-danger/40 bg-danger-soft px-2.5 py-1.5 text-[12px] text-danger">{decide.error.message}</p>}
      </div>
    </Panel>
  );
}

function Step({ done, label, who }: { done: boolean; label: string; who?: string | null }) {
  return (
    <li className="flex items-center gap-2">
      <span className={cn("grid size-4 place-items-center rounded-full border text-[10px]", done ? "border-ok bg-ok-soft text-ok" : "border-line-strong text-ink-faint")}>
        {done ? "✓" : ""}
      </span>
      <span className={done ? "text-ink" : "text-ink-muted"}>{label}</span>
      {who && <span className="ml-auto font-mono text-ink-faint">{who}</span>}
    </li>
  );
}

function AuditTrail({ entries }: { entries: AuditEntry[] }) {
  return (
    <Panel title="Audit trail">
      <ol className="divide-y divide-line/70">
        {entries.map((entry, index) => (
          <li key={index} className="px-3 py-2">
            <div className="flex items-baseline justify-between gap-2">
              <span className="font-mono text-[12px]">
                {entry.action}{" "}
                <span className="text-ink-faint">
                  {entry.fromStatus ?? "∅"} → {entry.toStatus}
                </span>
              </span>
              <time className="text-[11px] text-ink-faint" title={utc(entry.occurredOn)}>
                {timeAgo(entry.occurredOn)}
              </time>
            </div>
            <div className="font-mono text-[11px] text-ink-faint" title={entry.actorId}>
              {entry.actorId.startsWith("system:") ? entry.actorId : shortId(entry.actorId)}
            </div>
            {entry.comment && <p className="mt-0.5 text-ink-muted">{entry.comment}</p>}
          </li>
        ))}
      </ol>
    </Panel>
  );
}
