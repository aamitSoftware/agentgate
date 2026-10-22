import {
  Ban,
  CircleCheck,
  CircleDashed,
  CircleSlash,
  Clock,
  Info,
  OctagonAlert,
  PauseCircle,
  ShieldAlert,
  TriangleAlert,
  type LucideIcon,
} from "lucide-react";
import type { GrantStatus, ReviewStatus, RiskLevel, ServerStatus, Severity } from "@/api/types";
import { Badge } from "@/components/ui/badge";

// One vocabulary for state across the console (console-ui skill). Colour carries meaning and nothing else:
// green = in force, amber = waiting or risky, red = blocked or dangerous, grey = finished or informational.
// Every badge also carries an icon and a label, so colour is never the only signal.

export type Tone = "success" | "warning" | "danger" | "info" | "neutral";

interface Look {
  tone: Tone;
  label: string;
  icon: LucideIcon;
}

function StateBadge({ look, className }: { look: Look; className?: string }) {
  const Icon = look.icon;
  return (
    <Badge variant={look.tone} className={className}>
      <Icon aria-hidden="true" />
      {look.label}
    </Badge>
  );
}

const serverLooks: Record<ServerStatus, Look> = {
  Approved: { tone: "success", label: "Approved", icon: CircleCheck },
  UnderReview: { tone: "warning", label: "Under review", icon: Clock },
  Quarantined: { tone: "danger", label: "Quarantined", icon: ShieldAlert },
  Rejected: { tone: "neutral", label: "Rejected", icon: Ban },
};

export const ServerStatusBadge = ({ status }: { status: ServerStatus }) => <StateBadge look={serverLooks[status]} />;

const reviewLooks: Record<ReviewStatus, Look> = {
  Open: { tone: "warning", label: "Awaiting first approval", icon: Clock },
  PartiallyApproved: { tone: "info", label: "Awaiting second approval", icon: Clock },
  Approved: { tone: "success", label: "Approved", icon: CircleCheck },
  Rejected: { tone: "neutral", label: "Rejected", icon: Ban },
  Superseded: { tone: "neutral", label: "Superseded", icon: CircleSlash },
};

export const ReviewStatusBadge = ({ status }: { status: ReviewStatus }) => <StateBadge look={reviewLooks[status]} />;

const grantLooks: Record<GrantStatus, Look> = {
  Requested: { tone: "warning", label: "Awaiting first approval", icon: Clock },
  PendingApproval: { tone: "info", label: "Awaiting second approval", icon: Clock },
  Active: { tone: "success", label: "Active", icon: CircleCheck },
  Suspended: { tone: "danger", label: "Suspended", icon: PauseCircle },
  Revoked: { tone: "neutral", label: "Revoked", icon: Ban },
  Rejected: { tone: "neutral", label: "Rejected", icon: Ban },
};

export const GrantStatusBadge = ({ status }: { status: GrantStatus }) => <StateBadge look={grantLooks[status]} />;

const riskLooks: Record<RiskLevel, Look> = {
  Low: { tone: "neutral", label: "Low risk", icon: CircleDashed },
  Medium: { tone: "warning", label: "Medium risk", icon: TriangleAlert },
  High: { tone: "danger", label: "High risk", icon: OctagonAlert },
};

export const RiskBadge = ({ risk }: { risk: RiskLevel }) => <StateBadge look={riskLooks[risk]} />;

export const DecisionBadge = ({ decision }: { decision: "Allowed" | "Denied" }) =>
  decision === "Denied" ? (
    <StateBadge look={{ tone: "danger", label: "Denied", icon: Ban }} />
  ) : (
    <StateBadge look={{ tone: "success", label: "Allowed", icon: CircleCheck }} />
  );

const severityTones: Record<Severity, Tone> = { info: "neutral", warning: "warning", critical: "danger" };

const eventLabels: Record<string, string> = {
  ManifestSubmitted: "Submitted",
  ManifestReviewOpened: "Review opened",
  ManifestReviewCompleted: "Reviewed",
  ServerApproved: "Approved",
  ServerQuarantined: "Quarantined",
  ServerRejected: "Rejected",
  AgentRegistered: "Agent registered",
  GrantActivated: "Grant active",
  GrantSuspended: "Grant suspended",
  GrantRevoked: "Grant revoked",
  AnomalyDetected: "Anomaly",
};

export function eventLabel(kind: string | null): string {
  return (kind && eventLabels[kind]) ?? kind ?? "Event";
}

const severityIcons: Record<Severity, LucideIcon> = { info: Info, warning: TriangleAlert, critical: OctagonAlert };

export const EventBadge = ({ kind, severity }: { kind: string | null; severity: Severity }) => (
  <StateBadge look={{ tone: severityTones[severity], label: eventLabel(kind), icon: severityIcons[severity] }} />
);

export const severityTone = (severity: Severity): Tone => severityTones[severity];
