import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

/** The console's one container: a bordered surface with an optional header row. */
export function Panel({
  title,
  actions,
  className,
  children,
  ...props
}: Omit<HTMLAttributes<HTMLElement>, "title"> & { title?: ReactNode; actions?: ReactNode }) {
  return (
    <section className={cn("rounded-md border border-line bg-surface", className)} {...props}>
      {(title || actions) && (
        <header className="flex min-h-9 items-center justify-between gap-3 border-b border-line px-3">
          <h2 className="text-[11px] font-semibold tracking-[0.12em] text-ink-muted uppercase">{title}</h2>
          {actions}
        </header>
      )}
      {children}
    </section>
  );
}
