import { useNavigate, useSearch } from "@tanstack/react-router";
import { useAgents } from "@/api/queries";
import type { AgentSummary } from "@/api/types";
import { DataTable, RowLink, type Column } from "@/components/data-table";
import { FilterBar, matches, SearchFilter } from "@/components/filters";
import { PageBody, PageHeader } from "@/components/page-header";
import { Message, QueryState } from "@/components/query-state";
import { Section } from "@/components/section";
import { RelativeTime } from "@/components/time";
import { spikeThreshold } from "@/lib/activity";
import { count } from "@/lib/format";
import { cn } from "@/lib/utils";

const columns: Column<AgentSummary>[] = [
  {
    id: "name",
    header: "Agent",
    sortValue: (agent) => agent.name,
    cell: (agent) => (
      <span className="flex min-w-0 flex-col">
        <RowLink to="/agents/$agentId" params={{ agentId: agent.id }}>
          {agent.name}
        </RowLink>
        <span className="font-mono text-xs text-muted-foreground" translate="no">
          {agent.clientId}
        </span>
      </span>
    ),
  },
  { id: "owner", header: "Owner", sortValue: (agent) => agent.owner, cell: (agent) => agent.owner },
  { id: "purpose", header: "Purpose", cell: (agent) => <span className="block max-w-96 truncate text-muted-foreground">{agent.purpose}</span> },
  { id: "grants", header: "Active grants", numeric: true, sortValue: (agent) => agent.activeGrantCount, cell: (agent) => count(agent.activeGrantCount) },
  {
    id: "denied",
    header: "Denied, last hour",
    numeric: true,
    sortValue: (agent) => agent.deniedLastHour,
    cell: (agent) => (
      <span className={cn(agent.deniedLastHour >= spikeThreshold ? "font-medium text-danger" : agent.deniedLastHour ? "text-warning" : "text-muted-foreground")}>
        {count(agent.deniedLastHour)}
      </span>
    ),
  },
  {
    id: "registered",
    header: "Registered",
    sortValue: (agent) => agent.registeredOn,
    cell: (agent) => <RelativeTime value={agent.registeredOn} className="text-muted-foreground" />,
  },
];

export function AgentsPage() {
  const { q = "" } = useSearch({ from: "/agents" });
  const navigate = useNavigate({ from: "/agents" });
  const agents = useAgents();

  return (
    <>
      <PageHeader title="Agents" meta="Nonhuman identities that call tools through the gateway, with what they may call and how often they are refused." />
      <PageBody>
        <Section title="Registered agents">
          <FilterBar>
            <SearchFilter
              value={q}
              placeholder="Filter by name, client or owner"
              onChange={(next) => void navigate({ search: { q: next || undefined }, replace: true })}
            />
          </FilterBar>
          <QueryState
            query={agents}
            empty={<Message title="No agents registered">Registry admins register an agent before it can request grants.</Message>}
          >
            {(data) => {
              const rows = data.filter((agent) => matches(q, agent.name, agent.clientId, agent.owner));
              return rows.length ? (
                <DataTable label="Agents" columns={columns} rows={rows} rowKey={(agent) => agent.id} initialSort={{ id: "name", direction: "ascending" }} />
              ) : (
                <Message title="No agent matches">Change the search.</Message>
              );
            }}
          </QueryState>
        </Section>
      </PageBody>
    </>
  );
}
