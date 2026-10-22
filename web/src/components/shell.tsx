import { Link, Outlet } from "@tanstack/react-router";
import { Activity, Boxes, LogOut, ShieldCheck } from "lucide-react";
import type { ReactNode } from "react";
import { useGrantQueue, useReviewQueue } from "@/api/queries";
import { useAuth } from "@/auth/auth";
import { Badge } from "@/components/legacy-ui/badge";

export function Shell() {
  const { identity, signOut } = useAuth();
  const reviews = useReviewQueue();
  const grants = useGrantQueue();
  const waiting = (reviews.data?.length ?? 0) + (grants.data?.length ?? 0);

  return (
    <div className="grid h-full grid-cols-[13.5rem_minmax(0,1fr)] max-md:grid-cols-1">
      <aside className="flex flex-col border-r border-line bg-surface max-md:border-r-0 max-md:border-b">
        <div className="flex h-12 items-center gap-2 border-b border-line px-4">
          <span className="size-2 rounded-full bg-accent" />
          <span className="font-semibold tracking-tight">AgentGate</span>
          <span className="ml-auto font-mono text-[10px] tracking-widest text-ink-faint uppercase">dev</span>
        </div>
        <nav className="flex flex-col gap-0.5 p-2 max-md:flex-row">
          <NavItem to="/inventory" icon={<Boxes size={15} />}>
            Inventory
          </NavItem>
          <NavItem to="/approvals" icon={<ShieldCheck size={15} />} count={waiting}>
            Approvals
          </NavItem>
          <NavItem to="/activity" icon={<Activity size={15} />}>
            Live activity
          </NavItem>
        </nav>
        <div className="mt-auto border-t border-line p-3 max-md:hidden">
          <div className="truncate font-medium">{identity.username}</div>
          <div className="mt-1 flex flex-wrap gap-1">
            {identity.roles
              .filter((role) => ["registry-admin", "security-reviewer", "auditor"].includes(role))
              .map((role) => (
                <Badge key={role}>{role}</Badge>
              ))}
          </div>
          <button onClick={signOut} className="mt-3 flex items-center gap-1.5 text-[12px] text-ink-faint hover:text-ink">
            <LogOut size={13} /> Sign out
          </button>
        </div>
      </aside>
      <main className="min-w-0 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}

function NavItem({ to, icon, count, children }: { to: string; icon: ReactNode; count?: number; children: ReactNode }) {
  return (
    <Link
      to={to}
      className="flex h-8 items-center gap-2 rounded px-2.5 text-ink-muted hover:bg-raised hover:text-ink"
      activeProps={{ className: "bg-raised !text-ink" }}
    >
      {icon}
      <span>{children}</span>
      {!!count && <span className="ml-auto rounded-sm bg-warn-soft px-1.5 font-mono text-[11px] text-warn tabular">{count}</span>}
    </Link>
  );
}

/** Page heading row: title, a one-line explanation, and page-level actions. */
export function PageHeader({ title, subtitle, children }: { title: ReactNode; subtitle?: ReactNode; children?: ReactNode }) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-3 border-b border-line px-5 py-4">
      <div className="min-w-0">
        <h1 className="text-[17px] font-semibold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-0.5 text-ink-muted">{subtitle}</p>}
      </div>
      {children}
    </header>
  );
}
