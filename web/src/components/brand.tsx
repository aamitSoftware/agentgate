import { cn } from "@/lib/utils";

/** The AgentGate mark: a gate (two posts and a lintel) over a pinned line. */
export function BrandMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className={cn("size-6 shrink-0", className)}>
      <rect width="24" height="24" rx="6" className="fill-primary" />
      <path d="M7 18V8.5M17 18V8.5M5.5 8.5h13M9.5 13h5" className="stroke-primary-foreground" strokeWidth="2" strokeLinecap="round" fill="none" />
    </svg>
  );
}
