import { Check, Copy } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { fingerprint } from "@/lib/fingerprint";
import { shortHash } from "@/lib/format";
import { cn } from "@/lib/utils";

/** The manifest fingerprint (web/DESIGN.md): colour cells derived from the hash; identity, not status. */
export function Fingerprint({ hash, className }: { hash: string; className?: string }) {
  return (
    <span aria-hidden="true" className={cn("inline-flex h-3 shrink-0 overflow-hidden rounded-[3px] ring-1 ring-border", className)}>
      {fingerprint(hash).map((colour, index) => (
        <span key={index} className="w-1.5" style={{ backgroundColor: colour }} />
      ))}
    </span>
  );
}

/** A manifest hash: fingerprint, short form, full value in the tooltip, and a copy button. */
export function Hash({ value, copy = true, className }: { value: string | null | undefined; copy?: boolean; className?: string }) {
  if (!value) return <span className="text-muted-foreground">None</span>;
  return (
    <span className={cn("inline-flex items-center gap-1.5 align-middle", className)}>
      <Fingerprint hash={value} />
      <Tooltip>
        <TooltipTrigger asChild>
          <span className="font-mono text-sm text-foreground tabular" translate="no">
            {shortHash(value)}
          </span>
        </TooltipTrigger>
        <TooltipContent className="font-mono">{value}</TooltipContent>
      </Tooltip>
      {copy && <CopyButton value={value} label="Copy manifest hash" />}
    </span>
  );
}

export function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false);
  const copyValue = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1_500);
    } catch {
      toast.error("Copying needs clipboard access; select the value in the tooltip instead.");
    }
  };
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-xs"
      className="relative z-10 text-muted-foreground"
      aria-label={copied ? "Copied" : label}
      onClick={() => void copyValue()}
    >
      {copied ? <Check /> : <Copy />}
    </Button>
  );
}
