import { useId, useState } from "react";
import { toast } from "sonner";
import { useDecideGrant } from "@/api/queries";
import type { GrantStatus, HubGrant } from "@/api/types";
import { useAuth } from "@/auth/auth";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { DataTable, RowLink, type Column } from "@/components/data-table";
import { GrantStatusBadge } from "@/components/status";
import { Expiry } from "@/components/time";
import { ToolList } from "@/components/tool-list";
import { Button } from "@/components/ui/button";
import { Field, FieldLabel } from "@/components/ui/field";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";

interface ActionableGrant {
  id: string;
  status: GrantStatus | HubGrant["status"];
  agentClientId: string;
  serverName: string;
}

/**
 * What a reviewer can do to a grant in force: suspend (reversible, inline), resume (one click), revoke
 * (irreversible, confirmed). Nothing renders for roles that cannot act.
 */
export function GrantActions({ grant }: { grant: ActionableGrant }) {
  const { canReview } = useAuth();
  const decide = useDecideGrant();
  if (!canReview || (grant.status !== "Active" && grant.status !== "Suspended")) return null;
  const who = `${grant.agentClientId} on ${grant.serverName}`;

  const resume = () =>
    decide.mutate(
      { grantId: grant.id, verb: "resume", comment: "" },
      {
        onSuccess: () => toast.success(`Resumed: ${who} may call its granted tools again.`),
        onError: (error) => toast.error(`Couldn't resume: ${error.message}`),
      },
    );

  return (
    <div className="flex justify-end gap-1">
      {grant.status === "Active" ? (
        <SuspendPopover
          who={who}
          onSuspend={(reason) => decide.mutateAsync({ grantId: grant.id, verb: "suspend", comment: reason })}
        />
      ) : (
        <Button size="sm" variant="ghost" disabled={decide.isPending} onClick={resume}>
          Resume
        </Button>
      )}
      <ConfirmDialog
        trigger={
          <Button size="sm" variant="ghost" className="text-danger hover:text-danger">
            Revoke…
          </Button>
        }
        title={`Revoke ${grant.agentClientId}'s grant?`}
        consequence={
          <>
            {grant.agentClientId} loses access to {grant.serverName} at the gateway immediately. This cannot be undone: access
            afterwards needs a new request and two approvals.
          </>
        }
        confirmLabel="Revoke grant"
        reasonLabel="Why are you revoking it?"
        onConfirm={async (reason) => {
          await decide.mutateAsync({ grantId: grant.id, verb: "revoke", comment: reason });
          toast.success(`Revoked: ${who}.`);
        }}
      />
    </div>
  );
}

function SuspendPopover({ who, onSuspend }: { who: string; onSuspend: (reason: string) => Promise<unknown> }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [pending, setPending] = useState(false);
  const reasonId = useId();

  const submit = async () => {
    setPending(true);
    try {
      await onSuspend(reason.trim());
      toast.success(`Suspended: ${who} is denied until a reviewer resumes it.`);
      setOpen(false);
      setReason("");
    } catch (error) {
      toast.error(`Couldn't suspend: ${error instanceof Error ? error.message : "refused"}`);
    } finally {
      setPending(false);
    }
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button size="sm" variant="ghost">
          Suspend…
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="flex w-80 flex-col gap-3">
        <form
          className="flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <Field>
            <FieldLabel htmlFor={reasonId}>Why suspend {who}?</FieldLabel>
            <Textarea id={reasonId} name="reason" autoComplete="off" value={reason} onChange={(event) => setReason(event.target.value)} />
          </Field>
          <p className="text-sm text-muted-foreground">The gateway denies its calls until a reviewer resumes the grant.</p>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" size="sm" disabled={!reason.trim() || pending}>
              {pending && <Spinner data-icon="inline-start" />}
              Suspend grant
            </Button>
          </div>
        </form>
      </PopoverContent>
    </Popover>
  );
}

/** Grants of one server (listing agents) or one agent (listing servers), with the actions that apply. */
export function GrantTable({ grants, show }: { grants: HubGrant[]; show: "agent" | "server" }) {
  const columns: Column<HubGrant>[] = [
    show === "agent"
      ? {
          id: "agent",
          header: "Agent",
          sortValue: (grant) => grant.agentClientId,
          cell: (grant) => (
            <RowLink to="/agents/$agentId" params={{ agentId: grant.agentId }} className="font-mono">
              {grant.agentClientId}
            </RowLink>
          ),
        }
      : {
          id: "server",
          header: "Server",
          sortValue: (grant) => grant.serverName,
          cell: (grant) => (
            <RowLink to="/servers/$serverId" params={{ serverId: grant.serverId }} className="font-mono">
              {grant.serverName}
            </RowLink>
          ),
        },
    { id: "tools", header: "Tools", cell: (grant) => <ToolList tools={grant.tools} /> },
    { id: "status", header: "Status", sortValue: (grant) => grant.status, cell: (grant) => <GrantStatusBadge status={grant.status} /> },
    { id: "expires", header: "Expires", sortValue: (grant) => grant.expiresAt ?? "9999", cell: (grant) => <Expiry value={grant.expiresAt} /> },
    { id: "actions", header: "", interactive: true, className: "w-0", cell: (grant) => <GrantActions grant={grant} /> },
  ];
  return <DataTable label="Grants" columns={columns} rows={grants} rowKey={(grant) => grant.id} />;
}
