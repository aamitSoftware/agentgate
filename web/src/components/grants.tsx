import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { useDecideGrant, type GrantVerb } from "@/api/queries";
import type { HubGrant } from "@/api/types";
import { useAuth } from "@/auth/auth";
import { Empty, GrantStatusBadge } from "@/components/signals";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { timeAgo } from "@/lib/format";

/** Grants of one server or one agent, with the reviewer actions that apply to their state. */
export function GrantTable({ grants, show }: { grants: HubGrant[]; show: "agent" | "server" }) {
  if (grants.length === 0) return <Empty>No grants.</Empty>;
  return (
    <Table>
      <thead>
        <tr>
          <Th>{show === "agent" ? "Agent" : "Server"}</Th>
          <Th>Tools</Th>
          <Th>Status</Th>
          <Th>Expires</Th>
          <Th />
        </tr>
      </thead>
      <tbody>
        {grants.map((grant) => (
          <Tr key={grant.id}>
            <Td className="font-mono">
              {show === "agent" ? (
                <Link to="/agents/$agentId" params={{ agentId: grant.agentId }} className="hover:text-info">
                  {grant.agentClientId}
                </Link>
              ) : (
                <Link to="/servers/$serverId" params={{ serverId: grant.serverId }} className="hover:text-info">
                  {grant.serverName}
                </Link>
              )}
            </Td>
            <Td className="font-mono text-[12px] text-ink-muted">{grant.tools.join(", ")}</Td>
            <Td>
              <GrantStatusBadge status={grant.status} />
            </Td>
            <Td className="text-ink-faint">{grant.expiresAt ? timeAgo(grant.expiresAt) : "never"}</Td>
            <Td className="w-0">
              <GrantActions grant={grant} />
            </Td>
          </Tr>
        ))}
      </tbody>
    </Table>
  );
}

function GrantActions({ grant }: { grant: HubGrant }) {
  const { canReview } = useAuth();
  const decide = useDecideGrant();
  const [verb, setVerb] = useState<GrantVerb | null>(null);
  const [reason, setReason] = useState("");
  if (!canReview || grant.status === "Revoked") return null;

  if (verb)
    return (
      <form
        className="flex items-center gap-1.5"
        onSubmit={(event) => {
          event.preventDefault();
          decide.mutate({ grantId: grant.id, verb, comment: reason }, { onSuccess: () => (setVerb(null), setReason("")) });
        }}
      >
        <Input autoFocus className="h-7 w-48" placeholder={`Why ${verb}?`} value={reason} onChange={(event) => setReason(event.target.value)} />
        <Button size="sm" variant="danger" disabled={!reason.trim() || decide.isPending}>
          {verb}
        </Button>
        <Button size="sm" variant="ghost" type="button" onClick={() => setVerb(null)}>
          cancel
        </Button>
        {decide.error && <span className="text-[12px] text-danger">{decide.error.message}</span>}
      </form>
    );

  return (
    <div className="flex justify-end gap-1">
      {grant.status === "Active" && (
        <Button size="sm" variant="ghost" onClick={() => setVerb("suspend")}>
          Suspend
        </Button>
      )}
      {grant.status === "Suspended" && (
        <Button size="sm" variant="ghost" disabled={decide.isPending} onClick={() => decide.mutate({ grantId: grant.id, verb: "resume", comment: "" })}>
          Resume
        </Button>
      )}
      <Button size="sm" variant="ghost" className="hover:text-danger" onClick={() => setVerb("revoke")}>
        Revoke
      </Button>
    </div>
  );
}
