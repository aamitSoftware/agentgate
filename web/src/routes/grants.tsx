import { useNavigate, useSearch } from "@tanstack/react-router";
import { Plus } from "lucide-react";
import { useId, useState } from "react";
import { toast } from "sonner";
import { useAgents, useGrants, useRequestGrant, useServer, useServers } from "@/api/queries";
import type { GrantStatus, ReviewGrant } from "@/api/types";
import { useAuth } from "@/auth/auth";
import { DataTable, RowLink, type Column } from "@/components/data-table";
import { FilterBar, matches, SearchFilter, SegmentFilter } from "@/components/filters";
import { GrantActions } from "@/components/grants";
import { PageBody, PageHeader } from "@/components/page-header";
import { Message, QueryState } from "@/components/query-state";
import { Seals } from "@/components/seals";
import { Section } from "@/components/section";
import { GrantStatusBadge } from "@/components/status";
import { Expiry, RelativeTime } from "@/components/time";
import { ToolList, ToolPicker } from "@/components/tool-list";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import type { GrantStatusFilter } from "./search";

const statusGroups: Record<GrantStatusFilter, { label: string; statuses: GrantStatus[] | null }> = {
  active: { label: "Active", statuses: ["Active"] },
  suspended: { label: "Suspended", statuses: ["Suspended"] },
  waiting: { label: "Awaiting approval", statuses: ["Requested", "PendingApproval"] },
  ended: { label: "Revoked or rejected", statuses: ["Revoked", "Rejected"] },
  all: { label: "All", statuses: null },
};

const inGroup = (grant: ReviewGrant, filter: GrantStatusFilter) => statusGroups[filter].statuses?.includes(grant.status) ?? true;

const columns: Column<ReviewGrant>[] = [
  {
    id: "agent",
    header: "Agent",
    sortValue: (grant) => grant.agentClientId,
    cell: (grant) => (
      <RowLink to="/agents/$agentId" params={{ agentId: grant.agentId }} className="font-mono" translate="no">
        {grant.agentClientId}
      </RowLink>
    ),
  },
  { id: "server", header: "Server", sortValue: (grant) => grant.serverName, cell: (grant) => <span className="font-mono">{grant.serverName}</span> },
  { id: "tools", header: "Tools", cell: (grant) => <ToolList tools={grant.tools} /> },
  {
    id: "status",
    header: "Status",
    sortValue: (grant) => grant.status,
    cell: (grant) => (
      <span className="inline-flex items-center gap-2">
        <GrantStatusBadge status={grant.status} />
        {(grant.status === "Requested" || grant.status === "PendingApproval") && <Seals firstApproverId={grant.firstApproverId} complete={false} />}
      </span>
    ),
  },
  { id: "expires", header: "Expires", sortValue: (grant) => grant.expiresAt ?? "9999", cell: (grant) => <Expiry value={grant.expiresAt} /> },
  {
    id: "requested",
    header: "Requested",
    sortValue: (grant) => grant.createdOn,
    cell: (grant) => <RelativeTime value={grant.createdOn} className="text-muted-foreground" />,
  },
  { id: "actions", header: "", interactive: true, className: "w-0", cell: (grant) => <GrantActions grant={grant} /> },
];

export function GrantsPage() {
  const { status = "active", q = "" } = useSearch({ from: "/grants" });
  const navigate = useNavigate({ from: "/grants" });
  const grants = useGrants();
  const { canAdminister } = useAuth();

  return (
    <>
      <PageHeader
        title="Grants"
        meta="Which agent may call which tools on which server. The gateway denies everything else."
        actions={canAdminister && <RequestGrantDialog onRequested={() => void navigate({ search: { status: "waiting" } })} />}
      />
      <PageBody>
        <Section title="Grants">
          <FilterBar>
            <SegmentFilter
              label="Filter by status"
              value={status}
              options={(Object.keys(statusGroups) as GrantStatusFilter[]).map((value) => ({
                value,
                label: statusGroups[value].label,
                count: grants.data?.filter((grant) => inGroup(grant, value)).length,
              }))}
              onChange={(next) => void navigate({ search: (previous) => ({ ...previous, status: next === "active" ? undefined : next }), replace: true })}
            />
            <SearchFilter
              value={q}
              placeholder="Filter by agent, server or tool"
              onChange={(next) => void navigate({ search: (previous) => ({ ...previous, q: next || undefined }), replace: true })}
            />
          </FilterBar>
          <QueryState
            query={grants}
            empty={<Message title="No grants yet">An admin requests a grant for an agent; two reviewers approve it before the gateway enforces it.</Message>}
          >
            {(data) => {
              const rows = data.filter((grant) => inGroup(grant, status) && matches(q, grant.agentClientId, grant.serverName, ...grant.tools));
              return rows.length ? (
                <DataTable label="Grants" columns={columns} rows={rows} rowKey={(grant) => grant.id} />
              ) : (
                <Message title="No grant matches">Change the status filter or the search.</Message>
              );
            }}
          </QueryState>
        </Section>
      </PageBody>
    </>
  );
}

const expiryOptions = [
  { value: "7", label: "7 days" },
  { value: "30", label: "30 days" },
  { value: "90", label: "90 days" },
  { value: "never", label: "No expiry" },
] as const;

function RequestGrantDialog({ onRequested }: { onRequested: () => void }) {
  const [open, setOpen] = useState(false);
  const agents = useAgents();
  const servers = useServers();
  const request = useRequestGrant();
  const [agentId, setAgentId] = useState("");
  const [serverId, setServerId] = useState("");
  const [tools, setTools] = useState<string[]>([]);
  const [expiry, setExpiry] = useState<string>("30");
  const [justification, setJustification] = useState("");
  const server = useServer(serverId);
  const ids = { agent: useId(), server: useId(), expiry: useId(), justification: useId() };

  const approvedServers = servers.data?.filter((candidate) => candidate.status === "Approved") ?? [];
  const reset = () => {
    setAgentId("");
    setServerId("");
    setTools([]);
    setExpiry("30");
    setJustification("");
    request.reset();
  };
  const submit = () =>
    request.mutate(
      {
        agentId,
        serverId,
        tools,
        expiresAt: expiry === "never" ? null : new Date(Date.now() + Number(expiry) * 86_400_000).toISOString(),
        justification: justification.trim(),
      },
      {
        onSuccess: () => {
          toast.success("Grant requested. Two reviewers must approve it before the gateway enforces it.");
          setOpen(false);
          reset();
          onRequested();
        },
      },
    );

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) reset();
      }}
    >
      <DialogTrigger asChild>
        <Button>
          <Plus data-icon="inline-start" />
          Request grant
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Request a grant</DialogTitle>
          <DialogDescription>Choose what one agent may call on one approved server. Two different reviewers approve it.</DialogDescription>
        </DialogHeader>
        <form
          className="flex flex-col gap-5"
          onSubmit={(event) => {
            event.preventDefault();
            submit();
          }}
        >
          <FieldGroup className="gap-4">
            <Field>
              <FieldLabel htmlFor={ids.agent}>Agent</FieldLabel>
              <Select value={agentId} onValueChange={setAgentId}>
                <SelectTrigger id={ids.agent} className="w-full">
                  <SelectValue placeholder="Choose an agent…" />
                </SelectTrigger>
                <SelectContent>
                  <SelectGroup>
                    {agents.data?.map((agent) => (
                      <SelectItem key={agent.id} value={agent.id}>
                        {agent.name} <span className="font-mono text-muted-foreground">{agent.clientId}</span>
                      </SelectItem>
                    ))}
                  </SelectGroup>
                </SelectContent>
              </Select>
            </Field>
            <Field>
              <FieldLabel htmlFor={ids.server}>Server</FieldLabel>
              <Select
                value={serverId}
                onValueChange={(next) => {
                  setServerId(next);
                  setTools([]);
                }}
              >
                <SelectTrigger id={ids.server} className="w-full">
                  <SelectValue placeholder="Choose an approved server…" />
                </SelectTrigger>
                <SelectContent>
                  <SelectGroup>
                    {approvedServers.map((candidate) => (
                      <SelectItem key={candidate.id} value={candidate.id}>
                        <span className="font-mono">{candidate.name}</span>
                      </SelectItem>
                    ))}
                  </SelectGroup>
                </SelectContent>
              </Select>
              <FieldDescription>Only approved servers: a grant names tools from the pinned manifest.</FieldDescription>
            </Field>
            {serverId && server.data?.pinnedTools && (
              <ToolPicker legend="Tools" tools={server.data.pinnedTools} selected={tools} onChange={setTools} />
            )}
            <Field>
              <FieldLabel htmlFor={ids.expiry}>Expires after</FieldLabel>
              <Select value={expiry} onValueChange={setExpiry}>
                <SelectTrigger id={ids.expiry} className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectGroup>
                    {expiryOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectGroup>
                </SelectContent>
              </Select>
              {expiry === "never" && <FieldDescription>The grant stays in force until a reviewer revokes it.</FieldDescription>}
            </Field>
            <Field>
              <FieldLabel htmlFor={ids.justification}>Why does the agent need this?</FieldLabel>
              <Textarea
                id={ids.justification}
                name="justification"
                autoComplete="off"
                placeholder="The task it performs and why these tools are the minimum…"
                value={justification}
                onChange={(event) => setJustification(event.target.value)}
              />
            </Field>
          </FieldGroup>
          {request.error && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{request.error.message}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button type="submit" disabled={!agentId || !serverId || tools.length === 0 || !justification.trim() || request.isPending}>
              {request.isPending && <Spinner data-icon="inline-start" />}
              Submit for two approvals
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
