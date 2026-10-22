import type { ReactNode } from "react";
import { Badge, type Tone } from "@/components/legacy-ui/badge";
import type { GrantStatus, ReviewStatus, RiskLevel, ServerStatus, Severity } from "@/api/types";
import { cn } from "@/lib/utils";
import { shortHash } from "@/lib/format";

// Colour carries meaning here and nowhere else: green = in force, amber = waiting or risky, red = blocked or dangerous.

const serverTone: Record<ServerStatus, Tone> = { Approved: "ok", UnderReview: "warn", Quarantined: "danger", Rejected: "neutral" };
const serverLabel: Record<ServerStatus, string> = {
  Approved: "approved",
  UnderReview: "under review",
  Quarantined: "quarantined",
  Rejected: "rejected",
};

export const ServerStatusBadge = ({ status }: { status: ServerStatus }) => (
  <Badge tone={serverTone[status]}>
    <Dot tone={serverTone[status]} />
    {serverLabel[status]}
  </Badge>
);

const riskTone: Record<RiskLevel, Tone> = { Low: "neutral", Medium: "warn", High: "danger" };

export const RiskBadge = ({ risk, prefix }: { risk: RiskLevel | null | undefined; prefix?: string }) =>
  risk ? (
    <Badge tone={riskTone[risk]}>
      {prefix}
      {risk.toLowerCase()} risk
    </Badge>
  ) : (
    <span className="text-ink-faint">—</span>
  );

const reviewTone: Record<ReviewStatus, Tone> = {
  Open: "warn",
  PartiallyApproved: "info",
  Approved: "ok",
  Rejected: "danger",
  Superseded: "neutral",
};
const reviewLabel: Record<ReviewStatus, string> = {
  Open: "awaiting 1st",
  PartiallyApproved: "awaiting 2nd",
  Approved: "approved",
  Rejected: "rejected",
  Superseded: "superseded",
};

export const ReviewStatusBadge = ({ status }: { status: ReviewStatus }) => <Badge tone={reviewTone[status]}>{reviewLabel[status]}</Badge>;

const grantTone: Record<GrantStatus, Tone> = {
  Requested: "warn",
  PendingApproval: "info",
  Active: "ok",
  Suspended: "danger",
  Revoked: "neutral",
  Rejected: "neutral",
};
const grantLabel: Record<GrantStatus, string> = {
  Requested: "awaiting 1st",
  PendingApproval: "awaiting 2nd",
  Active: "active",
  Suspended: "suspended",
  Revoked: "revoked",
  Rejected: "rejected",
};

export const GrantStatusBadge = ({ status }: { status: GrantStatus }) => <Badge tone={grantTone[status]}>{grantLabel[status]}</Badge>;

export const severityTone: Record<Severity, Tone> = { info: "neutral", warning: "warn", critical: "danger" };

export function Dot({ tone, pulse }: { tone: Tone; pulse?: boolean }) {
  const colour = { ok: "bg-ok", warn: "bg-warn", danger: "bg-danger", info: "bg-info", neutral: "bg-ink-faint" }[tone];
  return <span className={cn("inline-block size-1.5 shrink-0 rounded-full", colour, pulse && "animate-pulse")} />;
}

/** A hash or id: monospace, shortened, full value on hover and in the clipboard. */
export function Mono({ value, short = shortHash, className }: { value: string | null | undefined; short?: (value: string) => string; className?: string }) {
  if (!value) return <span className="text-ink-faint">—</span>;
  return (
    <span
      title={value}
      className={cn("cursor-copy font-mono text-[12px] text-ink-muted hover:text-ink", className)}
      onClick={() => void navigator.clipboard?.writeText(value)}
    >
      {short(value)}
    </span>
  );
}

export function Stat({ label, value, tone, hint }: { label: string; value: ReactNode; tone?: Tone; hint?: ReactNode }) {
  const colour = tone && tone !== "neutral" ? { ok: "text-ok", warn: "text-warn", danger: "text-danger", info: "text-info" }[tone] : "text-ink";
  return (
    <div className="min-w-0 border-r border-line px-4 py-3 last:border-r-0">
      <div className="text-[11px] tracking-wide text-ink-faint uppercase">{label}</div>
      <div className={cn("mt-0.5 font-mono text-xl font-medium tabular", colour)}>{value}</div>
      {hint && <div className="mt-0.5 truncate text-[11px] text-ink-faint">{hint}</div>}
    </div>
  );
}

export function Empty({ children }: { children: ReactNode }) {
  return <div className="px-3 py-6 text-center text-ink-faint">{children}</div>;
}

export function Failure({ error }: { error: unknown }) {
  return (
    <div className="m-3 rounded border border-danger/40 bg-danger-soft px-3 py-2 text-danger">
      {error instanceof Error ? error.message : "Something went wrong."}
    </div>
  );
}
