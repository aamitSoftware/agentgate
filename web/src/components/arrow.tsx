import { ArrowRight } from "lucide-react";
import { cn } from "@/lib/utils";

/** "from → to" inside text. An icon, not "→": IBM Plex has no arrow glyph, and fallback fonts differ per machine. */
export function InlineArrow({ label = "to", className }: { label?: string; className?: string }) {
  return <ArrowRight role="img" aria-label={label} className={cn("mx-1 inline size-3.5 shrink-0 align-[-0.15em] text-muted-foreground", className)} />;
}
