import { useNavigate } from "@tanstack/react-router";
import { Activity, Bot, ClipboardCheck, KeyRound, LayoutDashboard, Server, ShieldAlert } from "lucide-react";
import { useEffect } from "react";
import { useAgents, useReviewQueue, useServers } from "@/api/queries";
import { CommandDialog, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList, CommandSeparator } from "@/components/ui/command";

/** ⌘K / Ctrl+K: jump to a page, a server, an agent or an open review. */
export function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const navigate = useNavigate();
  const servers = useServers();
  const agents = useAgents();
  const reviews = useReviewQueue();

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() === "k" && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        onOpenChange(!open);
      }
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, onOpenChange]);

  const go = (action: () => void) => {
    onOpenChange(false);
    action();
  };

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange} title="Go to" description="Search pages, servers, agents and open reviews">
      <CommandInput placeholder="Search servers, agents, reviews…" />
      <CommandList>
        <CommandEmpty>Nothing matches.</CommandEmpty>
        <CommandGroup heading="Pages">
          <CommandItem onSelect={() => go(() => void navigate({ to: "/" }))}>
            <LayoutDashboard /> Overview
          </CommandItem>
          <CommandItem onSelect={() => go(() => void navigate({ to: "/approvals" }))}>
            <ClipboardCheck /> Approvals
          </CommandItem>
          <CommandItem onSelect={() => go(() => void navigate({ to: "/servers" }))}>
            <Server /> Servers
          </CommandItem>
          <CommandItem onSelect={() => go(() => void navigate({ to: "/agents" }))}>
            <Bot /> Agents
          </CommandItem>
          <CommandItem onSelect={() => go(() => void navigate({ to: "/grants" }))}>
            <KeyRound /> Grants
          </CommandItem>
          <CommandItem onSelect={() => go(() => void navigate({ to: "/activity" }))}>
            <Activity /> Live activity
          </CommandItem>
        </CommandGroup>
        {!!reviews.data?.length && (
          <>
            <CommandSeparator />
            <CommandGroup heading="Open reviews">
              {reviews.data.map((review) => (
                <CommandItem
                  key={review.id}
                  value={`review ${review.serverName} ${review.riskLevel}`}
                  onSelect={() => go(() => void navigate({ to: "/approvals/reviews/$reviewId", params: { reviewId: review.id } }))}
                >
                  <ShieldAlert /> Review of {review.serverName}
                  <span className="ml-auto text-xs text-muted-foreground">{review.riskLevel} risk</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </>
        )}
        {!!servers.data?.length && (
          <>
            <CommandSeparator />
            <CommandGroup heading="Servers">
              {servers.data.map((server) => (
                <CommandItem
                  key={server.id}
                  value={`server ${server.name}`}
                  onSelect={() => go(() => void navigate({ to: "/servers/$serverId", params: { serverId: server.id } }))}
                >
                  <Server /> <span className="font-mono">{server.name}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </>
        )}
        {!!agents.data?.length && (
          <>
            <CommandSeparator />
            <CommandGroup heading="Agents">
              {agents.data.map((agent) => (
                <CommandItem
                  key={agent.id}
                  value={`agent ${agent.name} ${agent.clientId}`}
                  onSelect={() => go(() => void navigate({ to: "/agents/$agentId", params: { agentId: agent.id } }))}
                >
                  <Bot /> {agent.name}
                  <span className="ml-auto font-mono text-xs text-muted-foreground">{agent.clientId}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </>
        )}
      </CommandList>
    </CommandDialog>
  );
}
