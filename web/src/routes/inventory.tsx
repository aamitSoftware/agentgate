import { Link } from "@tanstack/react-router";
import { useAccess, useAgents, useReviewQueue, useServers } from "@/api/queries";
import { PageHeader } from "@/components/shell";
import { Empty, Failure, Mono, RiskBadge, ServerStatusBadge, Stat } from "@/components/signals";
import { Panel } from "@/components/legacy-ui/panel";
import { Table, Td, Th, Tr } from "@/components/legacy-ui/table";
import { timeAgo } from "@/lib/format";

export function InventoryPage() {
  const servers = useServers();
  const agents = useAgents();
  const access = useAccess();
  const reviews = useReviewQueue();

  const count = (status: string) => servers.data?.filter((server) => server.status === status).length ?? 0;
  const denied = agents.data?.reduce((total, agent) => total + agent.deniedLastHour, 0) ?? 0;

  return (
    <>
      <PageHeader title="Inventory" subtitle="Every MCP server and agent AgentGate knows, and who may call what right now." />
      <div className="space-y-4 p-5">
        <div className="grid grid-cols-5 rounded-md border border-line bg-surface max-lg:grid-cols-3">
          <Stat label="Approved" value={count("Approved")} tone="ok" hint="serving pinned manifests" />
          <Stat label="Quarantined" value={count("Quarantined")} tone={count("Quarantined") ? "danger" : undefined} hint="blocked after drift" />
          <Stat label="Awaiting review" value={reviews.data?.length ?? 0} tone={reviews.data?.length ? "warn" : undefined} hint="manifests in the queue" />
          <Stat label="Active grants" value={access.data?.length ?? 0} hint="agent × server × tools" />
          <Stat label="Denied, 1 h" value={denied} tone={denied ? "warn" : undefined} hint="across all agents" />
        </div>

        <Panel title="MCP servers">
          {servers.error ? (
            <Failure error={servers.error} />
          ) : servers.data?.length === 0 ? (
            <Empty>No servers registered yet.</Empty>
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>Server</Th>
                  <Th>Status</Th>
                  <Th>Pinned manifest</Th>
                  <Th className="text-right">Tools</Th>
                  <Th>Last review</Th>
                  <Th>Pending</Th>
                  <Th className="text-right">Grants</Th>
                  <Th>Since</Th>
                </tr>
              </thead>
              <tbody>
                {servers.data?.map((server) => (
                  <Tr key={server.id}>
                    <Td>
                      <Link to="/servers/$serverId" params={{ serverId: server.id }} className="font-mono font-medium hover:text-info">
                        {server.name}
                      </Link>
                      <div className="max-w-64 truncate text-[11px] text-ink-faint">{server.url ?? "url known once approved"}</div>
                    </Td>
                    <Td>
                      <ServerStatusBadge status={server.status} />
                    </Td>
                    <Td>
                      <Mono value={server.pinnedHash} />
                    </Td>
                    <Td className="text-right font-mono tabular">{server.toolCount}</Td>
                    <Td>
                      <RiskBadge risk={server.lastReviewRisk} />
                    </Td>
                    <Td>
                      {server.openReviewId ? (
                        <Link to="/approvals/reviews/$reviewId" params={{ reviewId: server.openReviewId }} className="inline-flex items-center gap-2">
                          <RiskBadge risk={server.openReviewRisk} prefix="review · " />
                          <Mono value={server.pendingHash} />
                        </Link>
                      ) : (
                        <span className="text-ink-faint">—</span>
                      )}
                    </Td>
                    <Td className="text-right font-mono tabular">{server.activeGrantCount}</Td>
                    <Td className="text-ink-faint">{timeAgo(server.statusAsOf)}</Td>
                  </Tr>
                ))}
              </tbody>
            </Table>
          )}
        </Panel>

        <div className="grid grid-cols-2 gap-4 max-xl:grid-cols-1">
          <Panel title="Agents · nonhuman identities">
            {agents.error ? (
              <Failure error={agents.error} />
            ) : agents.data?.length === 0 ? (
              <Empty>No agents registered yet.</Empty>
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>Agent</Th>
                    <Th>Owner</Th>
                    <Th className="text-right">Grants</Th>
                    <Th className="text-right">Denied 1 h</Th>
                  </tr>
                </thead>
                <tbody>
                  {agents.data?.map((agent) => (
                    <Tr key={agent.id}>
                      <Td>
                        <Link to="/agents/$agentId" params={{ agentId: agent.id }} className="font-medium hover:text-info">
                          {agent.name}
                        </Link>
                        <div className="font-mono text-[11px] text-ink-faint">{agent.clientId}</div>
                      </Td>
                      <Td className="text-ink-muted">{agent.owner}</Td>
                      <Td className="text-right font-mono tabular">{agent.activeGrantCount}</Td>
                      <Td className={`text-right font-mono tabular ${agent.deniedLastHour ? "text-warn" : "text-ink-faint"}`}>
                        {agent.deniedLastHour}
                      </Td>
                    </Tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Panel>

          <Panel title="Who can call what">
            {access.error ? (
              <Failure error={access.error} />
            ) : access.data?.length === 0 ? (
              <Empty>No grant is in force. The gateway denies every tool call.</Empty>
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>Agent</Th>
                    <Th>Server</Th>
                    <Th>Tools</Th>
                    <Th>Expires</Th>
                  </tr>
                </thead>
                <tbody>
                  {access.data?.map((grant) => (
                    <Tr key={grant.id}>
                      <Td className="font-mono">{grant.agentClientId}</Td>
                      <Td className="font-mono">{grant.serverName}</Td>
                      <Td>
                        <div className="flex flex-wrap gap-1">
                          {grant.tools.map((tool) => (
                            <span key={tool} className="rounded-sm bg-raised px-1.5 font-mono text-[11px] text-ink-muted">
                              {tool}
                            </span>
                          ))}
                        </div>
                      </Td>
                      <Td className="text-ink-faint">{grant.expiresAt ? timeAgo(grant.expiresAt) : "never"}</Td>
                    </Tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Panel>
        </div>
      </div>
    </>
  );
}
