import { Check } from "lucide-react";
import { useAuth } from "@/auth/auth";
import { actorLabel } from "@/components/actor";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";

/**
 * Four-eyes approval as two seals (web/DESIGN.md): empty, sealed by the first reviewer, sealed by the second.
 * The same element in queues, decisions and detail pages, so "awaiting second approval" always reads the same.
 */
export function Seals({ firstApproverId, complete, size = "sm" }: { firstApproverId: string | null; complete: boolean; size?: "sm" | "lg" }) {
  const { identity } = useAuth();
  const first = firstApproverId ? actorLabel(firstApproverId, identity.sub) : null;
  const sealed = [!!firstApproverId || complete, complete];
  const description = complete ? "Approved by two reviewers" : first ? `First approval by ${first}; awaiting a second reviewer` : "Awaiting two approvals";
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span className="inline-flex items-center gap-1" role="img" aria-label={description}>
          {sealed.map((isSealed, index) => (
            <span
              key={index}
              className={cn(
                "grid place-items-center rounded-full border",
                size === "sm" ? "size-4" : "size-6",
                isSealed ? "border-success bg-success-subtle text-success" : "border-dashed border-input text-transparent",
              )}
            >
              <Check aria-hidden="true" className={size === "sm" ? "size-2.5" : "size-3.5"} strokeWidth={3} />
            </span>
          ))}
        </span>
      </TooltipTrigger>
      <TooltipContent>{description}</TooltipContent>
    </Tooltip>
  );
}
