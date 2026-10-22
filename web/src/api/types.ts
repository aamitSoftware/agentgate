// Mirrors of the services' DTOs (JSON, camelCase, enums as names).

export type ServerStatus = "UnderReview" | "Approved" | "Quarantined" | "Rejected";
export type RiskLevel = "Low" | "Medium" | "High";
export type Severity = "info" | "warning" | "critical";

// --- Hub (read model) ---

export interface ServerSummary {
  id: string;
  name: string;
  url: string | null;
  status: ServerStatus;
  statusAsOf: string;
  pinnedHash: string | null;
  toolCount: number;
  pendingHash: string | null;
  openReviewId: string | null;
  openReviewRisk: RiskLevel | null;
  lastReviewRisk: RiskLevel | null;
  activeGrantCount: number;
}

export interface PinnedTool {
  name: string;
  title?: string;
  description?: string;
  inputSchema?: unknown;
  annotations?: Record<string, unknown>;
}

export interface HubGrant {
  id: string;
  agentId: string;
  agentClientId: string;
  serverId: string;
  serverName: string;
  tools: string[];
  expiresAt: string | null;
  status: "Active" | "Suspended" | "Revoked";
  statusAsOf: string;
}

export interface ActivityItem {
  id: string;
  kind: "call" | "event";
  occurredOn: string;
  severity: Severity;
  agentClientId: string | null;
  serverName: string | null;
  method: string | null;
  toolName: string | null;
  decision: "Allowed" | "Denied" | null;
  reason: string | null;
  traceId: string | null;
  eventKind: string | null;
  summary: string | null;
  actorId: string | null;
  serverId: string | null;
  agentId: string | null;
}

export interface ServerDetail {
  summary: ServerSummary;
  pinnedTools: PinnedTool[] | null;
  grants: HubGrant[];
  timeline: ActivityItem[];
  recentCalls: ActivityItem[];
}

export interface AgentSummary {
  id: string;
  name: string;
  clientId: string;
  owner: string;
  purpose: string;
  registeredOn: string;
  activeGrantCount: number;
  deniedLastHour: number;
}

export interface AgentDetail {
  summary: AgentSummary;
  grants: HubGrant[];
  timeline: ActivityItem[];
  recentCalls: ActivityItem[];
}

// --- Review (decisions) ---

export type ReviewStatus = "Open" | "PartiallyApproved" | "Approved" | "Rejected" | "Superseded";

export interface ReviewSummary {
  id: string;
  serverId: string;
  serverName: string;
  manifestHash: string;
  previousHash: string | null;
  riskLevel: RiskLevel;
  findingCount: number;
  status: ReviewStatus;
  firstApproverId: string | null;
  createdOn: string;
}

export interface RiskFinding {
  toolName: string;
  rule: string;
  severity: RiskLevel;
  evidence: string;
}

export interface AuditEntry {
  action: string;
  actorId: string;
  occurredOn: string;
  comment: string | null;
  fromStatus: string | null;
  toStatus: string;
}

export interface ReviewDetail {
  summary: ReviewSummary;
  submissionReason: string;
  findings: RiskFinding[];
  toolsJson: string;
  previousToolsJson: string | null;
  auditEntries: AuditEntry[];
}

export type GrantStatus = "Requested" | "PendingApproval" | "Active" | "Suspended" | "Revoked" | "Rejected";

export interface ReviewGrant {
  id: string;
  agentId: string;
  agentClientId: string;
  serverId: string;
  serverName: string;
  tools: string[];
  expiresAt: string | null;
  justification: string;
  status: GrantStatus;
  firstApproverId: string | null;
  createdOn: string;
  auditEntries: AuditEntry[];
}
