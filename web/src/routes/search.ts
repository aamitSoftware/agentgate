import type { ServerStatus } from "@/api/types";
import type { ActivityFilter } from "@/lib/activity";

// Typed URL search state per page (web-design-guidelines: the URL reflects filters and tabs). Defaults are
// left out of the URL, so plain links stay plain; unknown values fall back to the default, so a hand-edited
// or stale link still opens the page.

function oneOf<T extends string>(value: unknown, allowed: readonly T[], fallback: T): T | undefined {
  return typeof value === "string" && value !== fallback && (allowed as readonly string[]).includes(value) ? (value as T) : undefined;
}
const text = (value: unknown) => (typeof value === "string" && value.trim() ? value : undefined);

export const serverStatusFilters = ["all", "Approved", "UnderReview", "Quarantined", "Rejected"] as const satisfies readonly (ServerStatus | "all")[];
export type ServerStatusFilter = (typeof serverStatusFilters)[number];
export const serversSearch = (search: Record<string, unknown>): { status?: ServerStatusFilter; q?: string } => ({
  status: oneOf(search.status, serverStatusFilters, "all"),
  q: text(search.q),
});

export const grantStatusFilters = ["active", "suspended", "waiting", "ended", "all"] as const;
export type GrantStatusFilter = (typeof grantStatusFilters)[number];
export const grantsSearch = (search: Record<string, unknown>): { status?: GrantStatusFilter; q?: string } => ({
  status: oneOf(search.status, grantStatusFilters, "active"),
  q: text(search.q),
});

export const agentsSearch = (search: Record<string, unknown>): { q?: string } => ({ q: text(search.q) });

export const approvalTabs = ["reviews", "grants", "decided"] as const;
export type ApprovalTab = (typeof approvalTabs)[number];
export const approvalsSearch = (search: Record<string, unknown>): { tab?: ApprovalTab } => ({ tab: oneOf(search.tab, approvalTabs, "reviews") });

export const activityFilters = ["all", "denied", "governance"] as const satisfies readonly ActivityFilter[];
export const activitySearch = (search: Record<string, unknown>): { filter?: ActivityFilter } => ({
  filter: oneOf(search.filter, activityFilters, "all"),
});
