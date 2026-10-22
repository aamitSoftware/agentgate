import { Link, useNavigate, useSearch } from "@tanstack/react-router";
import { useServers } from "@/api/queries";
import type { ServerSummary } from "@/api/types";
import { DataTable, RowLink, type Column } from "@/components/data-table";
import { FilterBar, matches, SearchFilter, SegmentFilter } from "@/components/filters";
import { Hash } from "@/components/hash";
import { PageBody, PageHeader } from "@/components/page-header";
import { Message, QueryState } from "@/components/query-state";
import { Section } from "@/components/section";
import { RiskBadge, ServerStatusBadge } from "@/components/status";
import { RelativeTime } from "@/components/time";
import type { ServerStatusFilter } from "./search";

const columns: Column<ServerSummary>[] = [
  {
    id: "name",
    header: "Server",
    sortValue: (server) => server.name,
    cell: (server) => (
      <span className="flex min-w-0 flex-col">
        <RowLink to="/servers/$serverId" params={{ serverId: server.id }} className="font-mono" translate="no">
          {server.name}
        </RowLink>
        <span className="max-w-72 truncate text-xs text-muted-foreground">{server.url ?? "URL recorded once approved"}</span>
      </span>
    ),
  },
  { id: "status", header: "Status", sortValue: (server) => server.status, cell: (server) => <ServerStatusBadge status={server.status} /> },
  { id: "pinned", header: "Pinned manifest", cell: (server) => <Hash value={server.pinnedHash} />, interactive: true },
  { id: "tools", header: "Tools", numeric: true, sortValue: (server) => server.toolCount, cell: (server) => server.toolCount },
  {
    id: "review",
    header: "Review",
    interactive: true,
    cell: (server) =>
      server.openReviewId && server.openReviewRisk ? (
        <Link
          to="/approvals/reviews/$reviewId"
          params={{ reviewId: server.openReviewId }}
          className="inline-flex items-center gap-2 rounded-sm text-sm text-primary hover:underline"
        >
          Open review <RiskBadge risk={server.openReviewRisk} />
        </Link>
      ) : server.lastReviewRisk ? (
        <span className="inline-flex items-center gap-2 text-sm text-muted-foreground">
          Last <RiskBadge risk={server.lastReviewRisk} />
        </span>
      ) : (
        <span className="text-muted-foreground">None</span>
      ),
  },
  { id: "grants", header: "Grants", numeric: true, sortValue: (server) => server.activeGrantCount, cell: (server) => server.activeGrantCount },
  {
    id: "since",
    header: "Status since",
    sortValue: (server) => server.statusAsOf,
    cell: (server) => <RelativeTime value={server.statusAsOf} className="text-muted-foreground" />,
  },
];

const statusLabels: Record<ServerStatusFilter, string> = {
  all: "All",
  Approved: "Approved",
  UnderReview: "Under review",
  Quarantined: "Quarantined",
  Rejected: "Rejected",
};

export function ServersPage() {
  const { status = "all", q = "" } = useSearch({ from: "/servers" });
  const navigate = useNavigate({ from: "/servers" });
  const servers = useServers();

  return (
    <>
      <PageHeader title="Servers" meta="MCP servers, the manifest agents are shown (pinned at approval), and any review in progress." />
      <PageBody>
        <Section title="MCP servers">
          <FilterBar>
            <SegmentFilter
              label="Filter by status"
              value={status}
              options={(Object.keys(statusLabels) as ServerStatusFilter[]).map((value) => ({
                value,
                label: statusLabels[value],
                count: servers.data && (value === "all" ? servers.data.length : servers.data.filter((server) => server.status === value).length),
              }))}
              onChange={(next) => void navigate({ search: (previous) => ({ ...previous, status: next === "all" ? undefined : next }), replace: true })}
            />
            <SearchFilter
              value={q}
              placeholder="Filter by name or URL"
              onChange={(next) => void navigate({ search: (previous) => ({ ...previous, q: next || undefined }), replace: true })}
            />
          </FilterBar>
          <QueryState
            query={servers}
            empty={<Message title="No servers registered">Registry admins register MCP servers; each one is reviewed before agents can see it.</Message>}
          >
            {(data) => {
              const rows = data.filter((server) => (status === "all" || server.status === status) && matches(q, server.name, server.url));
              return rows.length ? (
                <DataTable label="MCP servers" columns={columns} rows={rows} rowKey={(server) => server.id} initialSort={{ id: "name", direction: "ascending" }} />
              ) : (
                <Message title="No server matches">Change the status filter or the search.</Message>
              );
            }}
          </QueryState>
        </Section>
      </PageBody>
    </>
  );
}
