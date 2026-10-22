import { Link, type LinkProps } from "@tanstack/react-router";
import { Fragment, type ReactNode } from "react";
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbSeparator } from "@/components/ui/breadcrumb";
import { cn } from "@/lib/utils";

export interface Crumb {
  label: string;
  to: LinkProps["to"];
}

/** The top of every page: where you are, what this is, its state in one line, and the page's actions. */
export function PageHeader({
  crumbs,
  title,
  badges,
  meta,
  actions,
}: {
  crumbs?: Crumb[];
  title: ReactNode;
  badges?: ReactNode;
  meta?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3 border-b px-4 py-5 sm:px-6">
      <div className="flex min-w-0 flex-col gap-1.5">
        {crumbs && (
          <Breadcrumb>
            <BreadcrumbList>
              {crumbs.map((crumb) => (
                <Fragment key={crumb.label}>
                  <BreadcrumbItem>
                    <BreadcrumbLink asChild>
                      <Link to={crumb.to}>{crumb.label}</Link>
                    </BreadcrumbLink>
                  </BreadcrumbItem>
                  <BreadcrumbSeparator />
                </Fragment>
              ))}
            </BreadcrumbList>
          </Breadcrumb>
        )}
        <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1.5">
          <h1 className="min-w-0 truncate text-xl font-semibold tracking-tight">{title}</h1>
          {badges}
        </div>
        {meta && <div className="flex flex-wrap items-center gap-x-1.5 gap-y-1 text-sm text-muted-foreground">{meta}</div>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </header>
  );
}

/** Page content below the header: consistent gutters and vertical rhythm. */
export function PageBody({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("flex flex-col gap-6 px-4 py-6 sm:px-6", className)}>{children}</div>;
}

/** The middle dot between items of a header's meta line. */
export const MetaDot = () => <span aria-hidden="true">·</span>;
