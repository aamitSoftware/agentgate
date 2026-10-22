import { cva, type VariantProps } from "class-variance-authority";
import type { HTMLAttributes } from "react";
import { cn } from "@/lib/cn";

const badge = cva(
  "inline-flex items-center gap-1 rounded-sm border px-1.5 py-px font-mono text-[11px] leading-4 font-medium whitespace-nowrap",
  {
    variants: {
      tone: {
        neutral: "border-line-strong text-ink-muted",
        ok: "border-ok/40 bg-ok-soft text-ok",
        warn: "border-warn/40 bg-warn-soft text-warn",
        danger: "border-danger/45 bg-danger-soft text-danger",
        info: "border-info/40 bg-info-soft text-info",
      },
    },
    defaultVariants: { tone: "neutral" },
  },
);

export type Tone = NonNullable<VariantProps<typeof badge>["tone"]>;

export function Badge({ className, tone, ...props }: HTMLAttributes<HTMLSpanElement> & VariantProps<typeof badge>) {
  return <span className={cn(badge({ tone }), className)} {...props} />;
}
