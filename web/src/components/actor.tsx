import { Bot, User } from "lucide-react";
import { useAuth } from "@/auth/auth";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { shortId } from "@/lib/format";

// Services record who acted as the Keycloak subject (sub) or a system actor (AgentGate.Abstractions SystemActors).
const systemActors: Record<string, string> = {
  "system:drift-detector": "Drift detector",
  "system:anomaly-detector": "Anomaly detector",
  "system:review-intake": "Review intake",
};

export function actorLabel(actorId: string, mySub: string): string {
  if (actorId === mySub) return "You";
  if (actorId in systemActors) return systemActors[actorId]!;
  if (actorId.startsWith("system:")) return actorId.slice("system:".length).replace(/-/g, " ");
  return `Reviewer ${shortId(actorId)}`;
}

/** Who did something: "You", a named system actor, or a short reviewer id with the full subject on hover. */
export function ActorName({ actorId }: { actorId: string }) {
  const { identity } = useAuth();
  const system = actorId.startsWith("system:");
  const Icon = system ? Bot : User;
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span className="inline-flex items-center gap-1 whitespace-nowrap">
          <Icon aria-hidden="true" className="size-3.5 text-muted-foreground" />
          {actorLabel(actorId, identity.sub)}
        </span>
      </TooltipTrigger>
      <TooltipContent className="font-mono">{actorId}</TooltipContent>
    </Tooltip>
  );
}
