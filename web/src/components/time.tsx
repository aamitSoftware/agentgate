import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { timeAgo, utc } from "@/lib/format";
import { useNow } from "@/lib/now";
import { cn } from "@/lib/utils";

/** "3 minutes ago", moving on the shared clock; the absolute UTC time is in the tooltip and the markup. */
export function RelativeTime({ value, className }: { value: string; className?: string }) {
  const now = useNow();
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <time dateTime={value} className={cn("whitespace-nowrap", className)}>
          {timeAgo(value, now)}
        </time>
      </TooltipTrigger>
      <TooltipContent>{utc(value)}</TooltipContent>
    </Tooltip>
  );
}

/** A grant's expiry in words: "in 30 days", "expired 2 hours ago", "No expiry". */
export function Expiry({ value }: { value: string | null }) {
  const now = useNow();
  if (!value) return <span className="text-muted-foreground">No expiry</span>;
  const expired = new Date(value).getTime() <= now;
  return (
    <span className={cn(expired && "text-danger")}>
      {expired && "Expired "}
      <RelativeTime value={value} />
    </span>
  );
}
