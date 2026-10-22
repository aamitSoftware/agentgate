import { cva, type VariantProps } from "class-variance-authority";
import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/utils";

const button = cva(
  "inline-flex items-center justify-center gap-1.5 rounded border font-medium whitespace-nowrap transition-colors disabled:pointer-events-none disabled:opacity-40",
  {
    variants: {
      variant: {
        default: "border-line-strong bg-raised text-ink hover:border-ink-faint hover:bg-line",
        approve: "border-ok/50 bg-ok-soft text-ok hover:border-ok hover:bg-ok/25",
        danger: "border-danger/50 bg-danger-soft text-danger hover:border-danger hover:bg-danger/25",
        ghost: "border-transparent text-ink-muted hover:bg-raised hover:text-ink",
      },
      size: {
        sm: "h-7 px-2.5 text-xs",
        md: "h-8 px-3 text-[13px]",
      },
    },
    defaultVariants: { variant: "default", size: "md" },
  },
);

export function Button({ className, variant, size, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & VariantProps<typeof button>) {
  return <button className={cn(button({ variant, size }), className)} {...props} />;
}
