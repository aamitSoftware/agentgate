import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { ApiError } from "@/api/client";
import { useAgents, useDecideGrant, useGrantQueue, useRequestGrant, useReviewQueue, useServer, useServers, type GrantVerb } from "@/api/queries";
import type { ReviewGrant } from "@/api/types";
import { useAuth } from "@/auth/auth";
import { PageHeader } from "@/components/shell";
import { Empty, Failure, GrantStatusBadge, Mono, ReviewStatusBadge, RiskBadge } from "@/components/signals";
import { Button } from "@/components/ui/button";
import { Input, Label, Select, Textarea } from "@/components/ui/field";
import { Panel } from "@/components/ui/panel";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { shortId, timeAgo } from "@/lib/format";

export function ApprovalsPage() {
  const reviews = useReviewQueue();
  const grants = useGrantQueue();
  const { canAdminister } = useAuth();

  return (
    <>
      <PageHeader
        title="Approvals"
        subtitle="Nothing reaches an agent until two different reviewers approve it: manifests before they are pinned, grants before they are enforced."
      />
      <div className="space-y-4 p-5">
        <Panel title="Manifest reviews">
          {reviews.error ? (
            <Failure error={reviews.error} />
          ) : reviews.data?.length === 0 ? (
            <Empty>The queue is empty.</Empty>
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>Server</Th>
                  <Th>Risk</Th>
                  <Th className="text-right">Findings</Th>
                  <Th>Manifest</Th>
                  <Th>Kind</Th>
                  <Th>State</Th>
                  <Th>Waiting</Th>
                </tr>
              </thead>
              <tbody>
                {reviews.data?.map((review) => (
                  <Tr key={review.id}>
                    <Td>
                      <Link to="/approvals/reviews/$reviewId" params={{ reviewId: review.id }} className="font-mono font-medium hover:text-info">
                        {review.serverName}
                      </Link>
                    </Td>
                    <Td>
                      <RiskBadge risk={review.riskLevel} />
                    </Td>
                    <Td className="text-right font-mono tabular">{review.findingCount}</Td>
                    <Td>
                      <Mono value={review.manifestHash} />
                    </Td>
                    <Td className="text-ink-muted">
                      {review.previousHash ? (
                        <span className="text-danger">re-review after drift</span>
                      ) : (
                        "new server"
                      )}
                    </Td>
                    <Td>
                      <ReviewStatusBadge status={review.status} />
                    </Td>
                    <Td className="text-ink-faint">{timeAgo(review.createdOn)}</Td>
                  </Tr>
                ))}
              </tbody>
            </Table>
          )}
        </Panel>

        <Panel title="Grant requests">
          {grants.error ? (
            <Failure error={grants.error} />
          ) : grants.data?.length === 0 ? (
            <Empty>No grant is waiting for a decision.</Empty>
          ) : (
            <div>
              {grants.data?.map((grant) => (
                <GrantDecision key={grant.id} grant={grant} />
              ))}
            </div>
          )}
        </Panel>

        {canAdminister && <RequestGrantForm />}
      </div>
    </>
  );
}

function GrantDecision({ grant }: { grant: ReviewGrant }) {
  const { identity, canReview } = useAuth();
  const decide = useDecideGrant();
  const [comment, setComment] = useState("");
  const gaveFirstApproval = grant.firstApproverId === identity.sub;
  const act = (verb: GrantVerb) => decide.mutate({ grantId: grant.id, verb, comment }, { onSuccess: () => setComment("") });

  return (
    <div className="grid grid-cols-[minmax(0,1fr)_22rem] gap-4 border-b border-line/70 px-3 py-3 last:border-b-0 max-lg:grid-cols-1">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-mono font-medium">{grant.agentClientId}</span>
          <span className="text-ink-faint">→</span>
          <span className="font-mono font-medium">{grant.serverName}</span>
          <GrantStatusBadge status={grant.status} />
        </div>
        <div className="mt-1.5 flex flex-wrap gap-1">
          {grant.tools.map((tool) => (
            <span key={tool} className="rounded-sm border border-line-strong px-1.5 font-mono text-[11px]">
              {tool}
            </span>
          ))}
        </div>
        <p className="mt-1.5 text-ink-muted">“{grant.justification}”</p>
        <p className="mt-1 text-[12px] text-ink-faint">
          requested {timeAgo(grant.createdOn)} · {grant.expiresAt ? `expires ${timeAgo(grant.expiresAt)}` : "no expiry"}
          {grant.firstApproverId && ` · first approval by ${gaveFirstApproval ? "you" : shortId(grant.firstApproverId)}`}
        </p>
      </div>
      {canReview && (
        <div className="space-y-2">
          <Textarea placeholder="Comment (required to reject)" value={comment} onChange={(event) => setComment(event.target.value)} />
          <div className="flex items-center gap-2">
            <Button
              variant="approve"
              size="sm"
              disabled={decide.isPending || gaveFirstApproval}
              title={gaveFirstApproval ? "Four-eyes: the second approval must come from a different reviewer." : undefined}
              onClick={() => act("approve")}
            >
              {grant.status === "PendingApproval" ? "Approve & activate" : "Approve"}
            </Button>
            <Button variant="danger" size="sm" disabled={decide.isPending || !comment.trim()} onClick={() => act("reject")}>
              Reject
            </Button>
            {gaveFirstApproval && <span className="text-[12px] text-ink-faint">waiting for a second reviewer</span>}
          </div>
          {decide.error && <p className="text-[12px] text-danger">{decide.error instanceof ApiError ? decide.error.message : "Failed."}</p>}
        </div>
      )}
    </div>
  );
}

function RequestGrantForm() {
  const agents = useAgents();
  const servers = useServers();
  const request = useRequestGrant();
  const [agentId, setAgentId] = useState("");
  const [serverId, setServerId] = useState("");
  const [tools, setTools] = useState<string[]>([]);
  const [days, setDays] = useState("30");
  const [justification, setJustification] = useState("");
  const server = useServer(serverId);

  const approvedServers = servers.data?.filter((candidate) => candidate.status === "Approved") ?? [];
  const toggle = (tool: string) => setTools((current) => (current.includes(tool) ? current.filter((name) => name !== tool) : [...current, tool]));
  const submit = () =>
    request.mutate(
      {
        agentId,
        serverId,
        tools,
        expiresAt: days ? new Date(Date.now() + Number(days) * 86_400_000).toISOString() : null,
        justification,
      },
      { onSuccess: () => (setTools([]), setJustification("")) },
    );

  return (
    <Panel title="Request a grant">
      <div className="grid grid-cols-[1fr_1fr_8rem] gap-3 p-3 max-lg:grid-cols-1">
        <div>
          <Label htmlFor="grant-agent">Agent</Label>
          <Select id="grant-agent" value={agentId} onChange={(event) => setAgentId(event.target.value)}>
            <option value="">Choose an agent…</option>
            {agents.data?.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name} ({agent.clientId})
              </option>
            ))}
          </Select>
        </div>
        <div>
          <Label htmlFor="grant-server">Server (approved only)</Label>
          <Select id="grant-server" value={serverId} onChange={(event) => (setServerId(event.target.value), setTools([]))}>
            <option value="">Choose a server…</option>
            {approvedServers.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>
                {candidate.name}
              </option>
            ))}
          </Select>
        </div>
        <div>
          <Label htmlFor="grant-days">Expires in days</Label>
          <Input id="grant-days" inputMode="numeric" value={days} onChange={(event) => setDays(event.target.value.replace(/\D/g, ""))} />
        </div>
        {serverId && (
          <div className="col-span-full">
            <Label>Tools from the pinned manifest</Label>
            <div className="flex flex-wrap gap-1.5">
              {server.data?.pinnedTools?.map((tool) => (
                <label
                  key={tool.name}
                  className={`flex cursor-pointer items-center gap-1.5 rounded-sm border px-2 py-1 font-mono text-[12px] ${tools.includes(tool.name) ? "border-info bg-info-soft text-ink" : "border-line-strong text-ink-muted"}`}
                >
                  <input type="checkbox" className="accent-[var(--color-info)]" checked={tools.includes(tool.name)} onChange={() => toggle(tool.name)} />
                  {tool.name}
                </label>
              ))}
            </div>
          </div>
        )}
        <div className="col-span-full">
          <Label htmlFor="grant-justification">Why does the agent need this?</Label>
          <Textarea id="grant-justification" value={justification} onChange={(event) => setJustification(event.target.value)} />
        </div>
        <div className="col-span-full flex items-center gap-3">
          <Button disabled={!agentId || !serverId || tools.length === 0 || !justification.trim() || request.isPending} onClick={submit}>
            Submit for two approvals
          </Button>
          {request.isSuccess && <span className="text-[12px] text-ok">Requested. It is now in the queue above.</span>}
          {request.error && <span className="text-[12px] text-danger">{request.error.message}</span>}
        </div>
      </div>
    </Panel>
  );
}
